using UnityEngine;
using System.Collections;

/// <summary>
/// Controla o player no estilo Lineage2 / Perfect World:
/// - Movimento por clique (click-to-move)
/// - Sistema de target (clique / TAB)
/// - Ataque básico e skills em F1/F2 com cooldown e cast
/// - Auto-approach até alcance da skill
/// - Combat Mode
/// </summary>
public class Player : MonoBehaviour
{
    //==========================================================
    //  COMPONENTES
    //==========================================================

    [Header("Componentes")]
    [SerializeField] private Rigidbody2D rb;          // Movimentação física 2D.
    [SerializeField] private PlayerAnim playerAnim;   // Controlador de animação.
    [SerializeField] private EnemyHUDController enemyHud; // Para acender as esferas.

    private Camera mainCam;

    //==========================================================
    //  MOVIMENTO (CLICK-TO-MOVE)
    //==========================================================

    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 3f;        // Velocidade de caminhada.
    [SerializeField] private float stoppingDistance = 0.1f; // Distância mínima para considerar que chegou no ponto.

    /// <summary>
    /// Ponto alvo de movimento atual (no chão).
    /// </summary>
    private Vector3? moveDestination = null;

    /// <summary>
    /// Se está em movimento automático (indo a um destino ou aproximando-se do alvo).
    /// </summary>
    private bool isAutoMoving = false;

    //==========================================================
    //  TARGET & INPUT DE MOUSE
    //==========================================================

    [Header("Target / Input")]
    [SerializeField] private LayerMask groundMask;   // Layer do chão (para Raycast).
    [SerializeField] private LayerMask enemyMask;    // Layer de inimigos (para Raycast).
    [SerializeField] private float tabSearchRadius = 20f;  // Raio para achar inimigo mais próximo no TAB.
    [SerializeField] private float maxTargetDistanceToKeep = 25f; // Distância máxima para manter o target (extra segurança).

    /// <summary>
    /// Tempo máximo entre cliques para considerar duplo clique.
    /// </summary>
    [SerializeField] private float doubleClickThreshold = 0.25f;

    private float lastClickTime = -999f;
    private ITargetable lastClickedTarget = null;

    //==========================================================
    //  SKILLS E ATAQUE
    //==========================================================

    [System.Serializable]
    public class SkillSlot
    {
        [Header("Identificação")]
        public string skillName = "Skill";

        [Header("Tecla")]
        public KeyCode key = KeyCode.F1;

        [Header("Combate")]
        public float range = 1.7f;     // Alcance da skill.
        public float cooldown = 2f;    // Tempo entre usos.
        public float castTime = 0.3f;  // Tempo de conjuração.
        public int damage = 10;        // Dano da skill.

        [HideInInspector] public float lastUseTime = -999f;
    }

    [Header("Skills")]
    [SerializeField] private SkillSlot basicAttack; // Usada no duplo clique.
    [SerializeField] private SkillSlot[] skills;    // Skills de F1, F2, F3...

    /// <summary>
    /// Se está conjurando alguma skill no momento.
    /// </summary>
    private bool isCasting = false;

    /// <summary>
    /// Skill pendente de ser executada assim que chegar no alcance.
    /// </summary>
    private SkillSlot pendingSkill = null;

    /// <summary>
    /// Alvo no momento em que a skill foi iniciada.
    /// </summary>
    private ITargetable pendingSkillTarget = null;

    //==========================================================
    //  VIDA / COMBATE
    //==========================================================

    [Header("Vida & Dano")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    [Header("Combat Mode")]
    [SerializeField] private float combatFadeTime = 5f; // Tempo sem atacar/levar dano para sair de combate.

    /// <summary>
    /// Momento do último evento de combate (ataque ou dano recebido).
    /// </summary>
    private float lastCombatTime = -999f;

    /// <summary>
    /// Indica se o player está em modo de combate.
    /// </summary>
    private bool isInCombat = false;

    //==========================================================
    //  CICLO DE VIDA
    //==========================================================

    private void Awake()
    {
        // Se não foi setado no Inspector, tenta pegar automaticamente.
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (playerAnim == null) playerAnim = GetComponentInChildren<PlayerAnim>();

        mainCam = Camera.main;

        // Inicializa vida.
        currentHealth = maxHealth;
    }

    private void Update()
    {
        HandleMouseInput();
        HandleKeyboardInput();
        UpdateCombatMode();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    //==========================================================
    //  INPUT DE MOUSE (CLICK-TO-MOVE + TARGET)
    //==========================================================

    /// <summary>
    /// Trata cliques do mouse: chão, inimigos, etc.
    /// </summary>
    private void HandleMouseInput()
    {
        // Botão esquerdo do mouse.
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;

            // Primeiro tenta acertar um inimigo.
            RaycastHit2D hitEnemy = Physics2D.Raycast(mouseWorldPos, Vector2.zero, 0f, enemyMask);

            if (hitEnemy.collider != null)
            {
                // Achou inimigo.
                ITargetable target = hitEnemy.collider.GetComponent<ITargetable>();

                if (target != null)
                {
                    HandleClickOnEnemy(target);
                    return;
                }
            }

            // Se não foi inimigo, tenta chão (movimento).
            RaycastHit2D hitGround = Physics2D.Raycast(mouseWorldPos, Vector2.zero, 0f, groundMask);

            if (hitGround.collider != null)
            {
                // Define novo destino para andar.
                moveDestination = hitGround.point;
                isAutoMoving = true;

                // Clique no chão NÃO limpa o target (igual Lineage2).
                pendingSkill = null;
                pendingSkillTarget = null;
            }
        }
    }

    /// <summary>
    /// Lógica ao clicar em um inimigo com o botão esquerdo.
    /// 1 clique  → seleciona (pega target)
    /// 2 cliques → seleciona e inicia ataque básico.
    /// </summary>
    private void HandleClickOnEnemy(ITargetable target)
    {
        float timeNow = Time.time;

        // Se é um novo alvo ou passou do tempo de double click,
        // tratamos como 1º clique: apenas selecionar alvo.
        bool isDoubleClick = (target == lastClickedTarget) &&
                             (timeNow - lastClickTime <= doubleClickThreshold);

        lastClickTime = timeNow;
        lastClickedTarget = target;

        // Sempre define o target ao clicar.
        TargetManager.Instance.SetTarget(target);

        // Se foi duplo clique → inicia ataque básico automático.
        if (isDoubleClick)
        {
            TryUseSkillOnTarget(basicAttack, target);
        }
    }

    //==========================================================
    //  INPUT DE TECLADO (SKILLS / TAB)
    //==========================================================

    /// <summary>
    /// Tratamento do teclado: TAB, skills F1/F2/F3...
    /// </summary>
    private void HandleKeyboardInput()
    {
        // TAB para ciclar targets.
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            TargetManager.Instance.CycleTargetWithTab(transform.position, tabSearchRadius);
        }

        // Pressionar skills configuradas.
        foreach (var slot in skills)
        {
            if (Input.GetKeyDown(slot.key))
            {
                // Só funciona se houver target.
                ITargetable currentTarget = TargetManager.Instance.CurrentTarget;
                if (currentTarget != null && currentTarget.IsAlive)
                {
                    TryUseSkillOnTarget(slot, currentTarget);
                }
            }
        }
    }

    //==========================================================
    //  SKILLS / AUTO-APPROACH
    //==========================================================

    /// <summary>
    /// Tenta usar uma skill em um alvo.
    /// Se estiver fora do range, move-se automaticamente até entrar no alcance.
    /// </summary>
    private void TryUseSkillOnTarget(SkillSlot slot, ITargetable target)
    {
        if (slot == null || target == null) return;

        // Verifica cooldown.
        if (Time.time < slot.lastUseTime + slot.cooldown)
        {
            // Ainda em cooldown, poderia mostrar mensagem na UI.
            return;
        }

        // Já guarda como skill pendente – se estiver fora do alcance, o movimento usará isso.
        pendingSkill = slot;
        pendingSkillTarget = target;

        float dist = Vector3.Distance(transform.position, target.TargetTransform.position);

        if (dist > slot.range)
        {
            // Fora do alcance → andar até aproximar.
            moveDestination = target.TargetTransform.position;
            isAutoMoving = true;
        }
        else
        {
            // Já está em alcance → executa imediatamente.
            StartCoroutine(CastAndExecuteSkill(slot, target));
        }
    }

    /// <summary>
    /// Coroutine que gerencia cast time, animação e aplicação de dano.
    /// </summary>
    private IEnumerator CastAndExecuteSkill(SkillSlot slot, ITargetable target)
    {
        if (isCasting) yield break; // Evita sobreposição de casts.

        isCasting = true;
        slot.lastUseTime = Time.time;

        // Entra em modo de combate.
        RegisterCombatEvent();

        // Para de se mover para conjurar.
        isAutoMoving = false;
        moveDestination = null;
        rb.linearVelocity = Vector2.zero;

        // Faz o player olhar para o alvo (flip no eixo X).
        FaceTarget(target.TargetTransform.position);

        // Liga flag de casting no Animator.
        playerAnim.SetCasting(true);

        // Se for ataque básico, dispara trigger de ataque.
        if (slot == basicAttack)
        {
            playerAnim.PlayBasicAttack();
        }

        // Espera o tempo de cast.
        yield return new WaitForSeconds(slot.castTime);

        // Confere se o alvo ainda é válido.
        if (target != null && target.IsAlive)
        {
            // Aplica dano.
            target.TakeDamage(slot.damage);

            // Esferas da HUD ficam vermelhas quando atacamos de fato.
            if (enemyHud != null && TargetManager.Instance.CurrentTarget == target)
            {
                enemyHud.SetOrbsInCombat(true);
            }
        }

        // Desliga flag de casting.
        playerAnim.SetCasting(false);

        isCasting = false;

        // Limpa skill pendente (foi executada).
        if (pendingSkill == slot)
        {
            pendingSkill = null;
            pendingSkillTarget = null;
        }
    }

    //==========================================================
    //  MOVIMENTO
    //==========================================================

    /// <summary>
    /// Movimenta o player em direção ao destino atual (se houver).
    /// Também cuida de aproximar até o alcance da skill pendente.
    /// </summary>
    private void HandleMovement()
    {
        if (!isAutoMoving || isCasting)
        {
            // Se não está auto-movendo ou está conjurando, fica parado.
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (moveDestination.HasValue)
        {
            Vector2 currentPos = rb.position;
            Vector2 dest = moveDestination.Value;

            Vector2 dir = (dest - currentPos);
            float distance = dir.magnitude;

            if (distance <= stoppingDistance)
            {
                // Chegou ao destino principal.
                rb.linearVelocity = Vector2.zero;
                isAutoMoving = false;
                moveDestination = null;

                // Se está indo atacar uma skill pendente, checa range.
                if (pendingSkill != null && pendingSkillTarget != null && pendingSkillTarget.IsAlive)
                {
                    float distToTarget = Vector3.Distance(transform.position, pendingSkillTarget.TargetTransform.position);

                    if (distToTarget <= pendingSkill.range)
                    {
                        // Já está em alcance → executa a skill.
                        StartCoroutine(CastAndExecuteSkill(pendingSkill, pendingSkillTarget));
                    }
                }

                return;
            }

            // Move na direção do destino.
            Vector2 dirNormalized = dir.normalized;
            rb.linearVelocity = dirNormalized * moveSpeed;

            // Atualiza orientação visual do player.
            FaceTarget(dest);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// Faz o player "olhar" para uma posição (flip no eixo X).
    /// </summary>
    /// <param name="worldPosition">Posição de interesse (alvo).</param>
    private void FaceTarget(Vector3 worldPosition)
    {
        // Se o alvo estiver à direita, X positivo; se à esquerda, X negativo.
        if (worldPosition.x > transform.position.x)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    /// <summary>
    /// Atualiza animação de movimento (Idle/Walk).
    /// </summary>
    private void UpdateAnimation()
    {
        // Usa a velocidade atual do Rigidbody2D como base.
        float speed = rb.linearVelocity.magnitude;
        playerAnim.UpdateMovement(speed);
    }

    //==========================================================
    //  COMBAT MODE & DANO RECEBIDO
    //==========================================================

    /// <summary>
    /// Registra um evento de combate (ataque ou dano recebido).
    /// Faz o player entrar em modo de combate.
    /// </summary>
    private void RegisterCombatEvent()
    {
        lastCombatTime = Time.time;
        if (!isInCombat)
        {
            isInCombat = true;
            playerAnim.SetInCombat(true);
            // Aqui você pode ligar ícone de espadas cruzadas na HUD do player.
        }
    }

    /// <summary>
    /// Atualiza o modo de combate, saindo depois de X segundos
    /// sem atacar ou levar dano.
    /// </summary>
    private void UpdateCombatMode()
    {
        if (isInCombat && Time.time > lastCombatTime + combatFadeTime)
        {
            isInCombat = false;
            playerAnim.SetInCombat(false);

            // Também pode apagar o ícone de espadas cruzadas na HUD aqui.
        }

        // Extra: se o alvo se afastar demais, remove target.
        ITargetable currentTarget = TargetManager.Instance.CurrentTarget;
        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.TargetTransform.position);
            if (dist > maxTargetDistanceToKeep)
            {
                TargetManager.Instance.ClearTarget();
            }
        }
    }

    /// <summary>
    /// Receber dano do inimigo.
    /// </summary>
    /// <param name="amount">Quantidade de dano recebido.</param>
    public void TakeDamage(int amount)
    {
        amount = Mathf.Max(0, amount);

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Entrar em combate ao receber dano.
        RegisterCombatEvent();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Lógica de morte do player.
    /// (Você pode conectar com animação de morte e voltar ao menu, etc.)
    /// </summary>
    private void Die()
    {
        // Por enquanto, só desabilita o movimento.
        isAutoMoving = false;
        pendingSkill = null;
        pendingSkillTarget = null;
        rb.linearVelocity = Vector2.zero;

        // Aqui você pode disparar animação de morte e bloquear input.
        Debug.Log("PLAYER MORREU");
    }
}
