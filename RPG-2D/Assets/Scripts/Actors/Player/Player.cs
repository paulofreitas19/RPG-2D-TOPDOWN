using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

/// <summary>
/// Controla o player no estilo Lineage / Perfect World:
/// - Click-to-move
/// - Sistema de target (clique / TAB)
/// - Ataque básico e skills (F1, F2...)
/// - Auto-approach até o alcance da skill
/// - Modo de combate + integração com HUD de inimigo
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    //==========================================================
    //  COMPONENTES
    //==========================================================

    [Header("Componentes")]
    [SerializeField] private Rigidbody2D rb;              // Movimento físico 2D.
    [SerializeField] private PlayerAnim playerAnim;       // Controlador de animações.
    [SerializeField] private EnemyHUDController enemyHud; // HUD de inimigo (no Canvas).

    private Camera mainCam;                               // Referência à Camera.main.

    //==========================================================
    //  MOVIMENTO (CLICK-TO-MOVE)
    //==========================================================

    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 3f;          // Velocidade de caminhada.
    [SerializeField] private float stoppingDistance = 0.1f; // Distância mínima para "cheguei".

    private Vector3? moveDestination = null;                // Ponto para onde estamos indo.
    private bool isAutoMoving = false;                      // Se o player está em movimento automático.

    //==========================================================
    //  TARGET & INPUT DE MOUSE
    //==========================================================

    [Header("Target / Input")]
    [SerializeField] private LayerMask groundMask;          // Layer do chão.
    [SerializeField] private LayerMask enemyMask;           // Layer de inimigos (para clique).
    [SerializeField] private float tabSearchRadius = 20f;   // Raio para TAB.
    [SerializeField] private float maxTargetDistanceToKeep = 25f; // Distância máxima para manter target.

    [SerializeField] private float doubleClickThreshold = 0.25f;  // Delay para detectar duplo clique.

    private float lastClickTime = -999f;                   // Momento do último clique no inimigo.
    private ITargetable lastClickedTarget = null;          // Último alvo clicado.

    //==========================================================
    //  SKILLS E ATAQUES
    //==========================================================

    [System.Serializable]
    public class SkillSlot
    {
        [Header("Identificação")]
        public string skillName = "Skill";   // Nome para debug.

        [Header("Tecla")]
        public KeyCode key = KeyCode.F1;     // Tecla para disparar a skill.

        [Header("Combate")]
        public float range = 1.7f;           // Alcance da skill.
        public float cooldown = 2f;          // Cooldown.
        public float castTime = 0.3f;        // Tempo de conjuração.
        public int damage = 10;              // Dano causado.

        [HideInInspector] public float lastUseTime = -999f; // Última vez que foi usada.
    }

    [Header("Skills")]
    [SerializeField] private SkillSlot basicAttack;     // Skill usada no duplo clique.
    [SerializeField] private SkillSlot[] skills;        // Skills extras (F1, F2...).

    private bool isCasting = false;                    // Está conjurando alguma coisa?
    private SkillSlot pendingSkill = null;             // Skill que está esperando entrar em alcance.
    private ITargetable pendingSkillTarget = null;     // Alvo da skill pendente.

    //==========================================================
    //  VIDA / COMBATE
    //==========================================================

    [Header("Vida & Dano")]
    [SerializeField] private int maxHealth = 100;       // Vida máxima do player.
    private int currentHealth;                          // Vida atual.

    [Header("Combat Mode")]
    [SerializeField] private float combatFadeTime = 5f; // Tempo sem combate para sair do modo.

    private float lastCombatTime = -999f;               // Último evento de combate.
    private bool isInCombat = false;                    // Está em modo de combate?
    private bool isDead = false;                        // Player morreu?

    //==========================================================
    //  CICLO DE VIDA
    //==========================================================

    private void Awake()
    {
        // Garante referências.
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (playerAnim == null) playerAnim = GetComponentInChildren<PlayerAnim>();
        if (enemyHud == null) enemyHud = EnemyHUDController.Instance;

        mainCam = Camera.main;

        // Inicializa vida.
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (isDead) return;                    // Morto não faz nada.

        HandleMouseInput();                    // Clique para andar / selecionar alvo.
        HandleKeyboardInput();                 // TAB + skills.
        UpdateCombatMode();                    // Entrar/sair de combate.
        UpdateAnimation();                     // Atualizar Animator.
    }

    private void FixedUpdate()
    {
        if (isDead) return;                    // Morto não anda.

        HandleMovement();                      // Andar até o destino.
    }

    //==========================================================
    //  INPUT DE MOUSE
    //==========================================================

    private void HandleMouseInput()
    {
        // Evita que cliques em UI sejam processados como movimento
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            // Converte posição de tela para mundo.
            Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;

            //--------------------------------------------------
            // 1) Tentamos clicar em um inimigo.
            //--------------------------------------------------
            Collider2D enemyCol = Physics2D.OverlapPoint(mouseWorldPos, enemyMask);

            if (enemyCol != null)
            {
                ITargetable target = enemyCol.GetComponent<ITargetable>();
                if (target != null)
                {
                    HandleClickOnEnemy(target);
                    return; // Não processa clique no chão.
                }
            }

            //--------------------------------------------------
            // 2) Se não clicou em inimigo, tratamos como chão.
            //--------------------------------------------------
            Collider2D groundCol = Physics2D.OverlapPoint(mouseWorldPos, groundMask);

            if (groundCol != null)
            {
                moveDestination = mouseWorldPos; // Andar até o ponto clicado.
                isAutoMoving = true;

                // Não limpamos o target (comportamento Lineage-like).
                pendingSkill = null;
                pendingSkillTarget = null;
            }
        }
    }

    /// <summary>
    /// Lógica de clique em inimigo (1x seleciona, 2x ataca).
    /// </summary>
    private void HandleClickOnEnemy(ITargetable target)
    {
        float now = Time.time;

        bool isDoubleClick =
            (target == lastClickedTarget) &&
            (now - lastClickTime <= doubleClickThreshold);

        lastClickTime = now;
        lastClickedTarget = target;

        // Define alvo no TargetManager.
        if (TargetManager.Instance != null)
            TargetManager.Instance.SetTarget(target);

        // Se foi duplo clique → ataca.
        if (isDoubleClick)
        {
            TryUseSkillOnTarget(basicAttack, target);
        }
    }

    //==========================================================
    //  INPUT DE TECLADO
    //==========================================================

    private void HandleKeyboardInput()
    {
        // TAB: ciclo entre alvos.
        if (Input.GetKeyDown(KeyCode.Tab) && TargetManager.Instance != null)
        {
            TargetManager.Instance.CycleTargetWithTab(transform.position, tabSearchRadius);
        }

        // Skills adicionais (F1/F2/F3...).
        if (skills != null)
        {
            foreach (var slot in skills)
            {
                if (Input.GetKeyDown(slot.key))
                {
                    ITargetable currentTarget = TargetManager.Instance != null
                        ? TargetManager.Instance.CurrentTarget
                        : null;

                    if (currentTarget != null && currentTarget.IsAlive)
                    {
                        TryUseSkillOnTarget(slot, currentTarget);
                    }
                }
            }
        }
    }

    //==========================================================
    //  SKILLS / AUTO-APPROACH
    //==========================================================

    private void TryUseSkillOnTarget(SkillSlot slot, ITargetable target)
    {
        if (slot == null || target == null) return;

        // Cooldown.
        if (Time.time < slot.lastUseTime + slot.cooldown)
            return;

        // Define como skill pendente.
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
            // Já está em alcance → executa agora.
            StartCoroutine(CastAndExecuteSkill(slot, target));
        }
    }

    private IEnumerator CastAndExecuteSkill(SkillSlot slot, ITargetable target)
    {
        if (isCasting) yield break;          // Evita sobrepor casts.

        isCasting = true;
        slot.lastUseTime = Time.time;

        RegisterCombatEvent();               // Entra em modo de combate.

        isAutoMoving = false;               // Para de andar.
        moveDestination = null;
        rb.linearVelocity = Vector2.zero;

        // Olha para o alvo.
        FaceTarget(target.TargetTransform.position);

        // Liga flag de cast no Animator.
        playerAnim.SetCasting(true);

        // Se for ataque básico → dispara trigger.
        if (slot == basicAttack)
            playerAnim.PlayBasicAttack();

        // Espera o tempo de cast.
        yield return new WaitForSeconds(slot.castTime);

        // Aplica dano se o alvo ainda está vivo.
        if (target != null && target.IsAlive)
        {
            target.TakeDamage(slot.damage);

            // Marca orbs da HUD como "em combate".
            if (enemyHud != null &&
                TargetManager.Instance != null &&
                TargetManager.Instance.CurrentTarget == target)
            {
                enemyHud.SetOrbsInCombat(true);
            }
        }

        // Termina cast.
        playerAnim.SetCasting(false);
        isCasting = false;

        // Se essa skill era a pendente, limpa.
        if (pendingSkill == slot)
        {
            pendingSkill = null;
            pendingSkillTarget = null;
        }
    }

    //==========================================================
    //  MOVIMENTO
    //==========================================================

    private void HandleMovement()
    {
        // Se não está andando automaticamente ou está conjurando → parado.
        if (!isAutoMoving || isCasting)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!moveDestination.HasValue)
        {
            rb.linearVelocity = Vector2.zero;
            isAutoMoving = false;
            return;
        }

        Vector2 currentPos = rb.position;
        Vector2 dest = moveDestination.Value;

        // Direção até o destino.
        Vector2 dir = dest - currentPos;
        float distance = dir.magnitude;

        if (distance <= stoppingDistance)
        {
            // Chegou.
            rb.linearVelocity = Vector2.zero;
            isAutoMoving = false;
            moveDestination = null;

            // Se estava se aproximando para skill pendente, checa alcance.
            if (pendingSkill != null && pendingSkillTarget != null && pendingSkillTarget.IsAlive)
            {
                float distToTarget = Vector3.Distance(
                    transform.position,
                    pendingSkillTarget.TargetTransform.position);

                if (distToTarget <= pendingSkill.range)
                {
                    StartCoroutine(CastAndExecuteSkill(pendingSkill, pendingSkillTarget));
                }
            }

            return;
        }

        // Ainda longe → move na direção normalizada.
        Vector2 dirNorm = dir.normalized;
        rb.linearVelocity = dirNorm * moveSpeed;

        // Flipa o sprite para "olhar" na direção.
        FaceTarget(dest);
    }

    /// <summary>Faz o player olhar para uma posição no mundo (flip X).</summary>
    private void FaceTarget(Vector3 worldPos)
    {
        if (worldPos.x > transform.position.x)
            transform.localScale = new Vector3(-1f, 1f, 1f);
        else
            transform.localScale = new Vector3(1f, 1f, 1f);
    }

    /// <summary>Atualiza Animator com base na velocidade atual.</summary>
    private void UpdateAnimation()
    {
        float speed = rb.linearVelocity.magnitude;
        playerAnim.UpdateMovement(speed);
    }

    //==========================================================
    //  COMBAT MODE & DANO RECEBIDO
    //==========================================================

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

    private void UpdateCombatMode()
    {
        // Sai do modo de combate após X segundos sem eventos.
        if (isInCombat && Time.time > lastCombatTime + combatFadeTime)
        {
            isInCombat = false;
            playerAnim.SetInCombat(false);

            // HUD do inimigo pode voltar orbs para estado neutro.
            if (enemyHud != null)
                enemyHud.SetOrbsInCombat(false);
        }

        // Se o alvo se afastar demais, limpa target.
        if (TargetManager.Instance != null)
        {
            ITargetable currentTarget = TargetManager.Instance.CurrentTarget;
            if (currentTarget != null)
            {
                float dist = Vector3.Distance(
                    transform.position,
                    currentTarget.TargetTransform.position);

                if (dist > maxTargetDistanceToKeep)
                    TargetManager.Instance.ClearTarget();
            }
        }
    }

    /// <summary>Receber dano.</summary>
    public void TakeDamage(int amount)
    {
        amount = Mathf.Max(0, amount);
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        RegisterCombatEvent();

        if (currentHealth <= 0)
            Die();
    }

    /// <summary>Morte do player.</summary>
    private void Die()
    {
        isAutoMoving = false;
        pendingSkill = null;
        pendingSkillTarget = null;
        rb.linearVelocity = Vector2.zero;

        isDead = true;

        // Animação de morte, se quiser.
        playerAnim.PlayDeath();

        Debug.Log("PLAYER MORREU");
    }
}
