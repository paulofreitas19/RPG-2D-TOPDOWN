using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Controla o player no estilo Lineage / Perfect World:
/// - Click-to-move
/// - Sistema de target (clique / TAB)
/// - Ataque básico e skills (F1, F2...)
/// - Auto-approach até o alcance da skill
/// - Modo de combate + integração com HUD de inimigo
/// - Agora também: mana, XP, level e eventos para HUD do player.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    //==========================================================
    //  SINGLETON
    //==========================================================

    public static Player Instance { get; private set; }

    //==========================================================
    //  COMPONENTES
    //==========================================================

    [Header("Componentes")]
    [SerializeField] private Rigidbody2D rb;              // Movimento físico 2D.
    [SerializeField] private PlayerAnim playerAnim;       // Controlador de animações.
    [SerializeField] private EnemyHUDController enemyHud; // HUD de inimigo (no Canvas). :contentReference[oaicite:1]{index=1}  

    private Camera mainCam;                               // Referência à Camera.main.

    //==========================================================
    //  IDENTIFICAÇÃO
    //==========================================================

    [Header("Identificação")]
    [SerializeField] private string displayName = "Merlin";
    public string DisplayName => displayName;

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
    [SerializeField] private float maxTargetDistanceToKeep = 25f; // Distância máx para manter target.

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
        public int manaCost = 0;             // Custo de mana (opcional).

        [HideInInspector] public float lastUseTime = -999f; // Última vez que foi usada.
    }

    [Header("Skills")]
    [SerializeField] private SkillSlot basicAttack;     // Skill usada no duplo clique.
    [SerializeField] private SkillSlot[] skills;        // Skills extras (F1, F2...).

    private bool isCasting = false;                    // Está conjurando alguma coisa?
    private SkillSlot pendingSkill = null;             // Skill que está esperando entrar em alcance.
    private ITargetable pendingSkillTarget = null;     // Alvo da skill pendente.

    //==========================================================
    //  VIDA / MANA / XP
    //==========================================================

    [Header("Vida & Dano")]
    [SerializeField] private int maxHealth = 100;       // Vida máxima do player.
    [SerializeField] private int currentHealth;                          // Vida atual.

    [Header("Mana")]
    [SerializeField] private int maxMana = 50;
    private int currentMana;

    [Header("Experiência / Level")]
    [SerializeField] private int level = 1;
    [SerializeField] private int baseXPToNextLevel = 50;
    [SerializeField] private float xpGrowthFactor = 1.5f;

    private int currentXP;
    private int xpToNextLevel;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public int CurrentMana => currentMana;
    public int MaxMana => maxMana;
    public int CurrentXP => currentXP;
    public int XPToNextLevel => xpToNextLevel;
    public int Level => level;

    //==========================================================
    //  EVENTOS PARA HUD
    //==========================================================

    public event Action<int, int> OnHealthChanged;          // (current, max)
    public event Action<int, int> OnManaChanged;            // (current, max)
    public event Action<int, int> OnXPChanged;              // (currentXP, xpToNextLevel)
    public event Action<int> OnLevelChanged;           // (newLevel)

    //==========================================================
    //  COMBAT MODE
    //==========================================================

    [Header("Combat Mode")]
    [SerializeField] private float combatFadeTime = 5f; // Tempo sem combate p/ sair do modo.

    private float lastCombatTime = -999f;               // Último evento de combate.
    private bool isInCombat = false;                    // Está em modo de combate?
    private bool isDead = false;                        // Player morreu?

    //==========================================================
    //  CICLO DE VIDA
    //==========================================================

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Garante referências.
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (playerAnim == null) playerAnim = GetComponentInChildren<PlayerAnim>();
        if (enemyHud == null) enemyHud = EnemyHUDController.Instance;

        mainCam = Camera.main;

        // Inicializa recursos
        currentHealth = maxHealth;
        currentMana = maxMana;

        // XP para o nível atual
        xpToNextLevel = Mathf.RoundToInt(baseXPToNextLevel *
                                         Mathf.Pow(xpGrowthFactor, level - 1));

        // Dispara eventos iniciais para a HUD do player
        RaiseHealthChanged();
        RaiseManaChanged();
        RaiseXPChanged();
        OnLevelChanged?.Invoke(level);
    }

    private void Update()
    {
        if (isDead) return;

        HandleMouseInput();
        HandleKeyboardInput();
        UpdateCombatMode();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        HandleMovement();
    }

    //==========================================================
    //  INPUT DE MOUSE
    //==========================================================

    private void HandleMouseInput()
    {
        // Evita que cliques em UI sejam processados como movimento.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;

            // 1) Tentamos clicar em um inimigo.
            Collider2D enemyCol = Physics2D.OverlapPoint(mouseWorldPos, enemyMask);

            if (enemyCol != null)
            {
                ITargetable target = enemyCol.GetComponent<ITargetable>();
                if (target != null)
                {
                    HandleClickOnEnemy(target);
                    return;
                }
            }

            // 2) Se não clicou em inimigo, tratamos como chão.
            Collider2D groundCol = Physics2D.OverlapPoint(mouseWorldPos, groundMask);

            if (groundCol != null)
            {
                moveDestination = mouseWorldPos;
                isAutoMoving = true;

                // Não limpamos o target (comportamento Lineage-like).
                pendingSkill = null;
                pendingSkillTarget = null;
            }
        }
    }

    private void HandleClickOnEnemy(ITargetable target)
    {
        float now = Time.time;

        bool isDoubleClick =
            (target == lastClickedTarget) &&
            (now - lastClickTime <= doubleClickThreshold);

        lastClickTime = now;
        lastClickedTarget = target;

        if (TargetManager.Instance != null)
            TargetManager.Instance.SetTarget(target);

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

        // Cooldown
        if (Time.time < slot.lastUseTime + slot.cooldown)
            return;

        // Mana
        if (!TryConsumeMana(slot.manaCost))
            return;

        // Define como skill pendente
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
        if (isCasting) yield break;

        isCasting = true;
        slot.lastUseTime = Time.time;

        RegisterCombatEvent();

        isAutoMoving = false;
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

        playerAnim.SetCasting(false);
        isCasting = false;

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

        Vector2 dir = dest - currentPos;
        float distance = dir.magnitude;

        if (distance <= stoppingDistance)
        {
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

        Vector2 dirNorm = dir.normalized;
        rb.linearVelocity = dirNorm * moveSpeed;

        FaceTarget(dest);
    }

    private void FaceTarget(Vector3 worldPos)
    {
        if (worldPos.x > transform.position.x)
            transform.localScale = new Vector3(-1f, 1f, 1f);
        else
            transform.localScale = new Vector3(1f, 1f, 1f);
    }

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
            // Aqui dá para acender o ícone de "espadas" na HUD do player, se quiser.
        }
    }

    private void UpdateCombatMode()
    {
        if (isInCombat && Time.time > lastCombatTime + combatFadeTime)
        {
            isInCombat = false;
            playerAnim.SetInCombat(false);

            if (enemyHud != null)
                enemyHud.SetOrbsInCombat(false);
        }

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
        RaiseHealthChanged();

        if (currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        isAutoMoving = false;
        pendingSkill = null;
        pendingSkillTarget = null;
        rb.linearVelocity = Vector2.zero;

        isDead = true;

        playerAnim.PlayDeath();
        Debug.Log("PLAYER MORREU");
    }

    //==========================================================
    //  MANA / XP
    //==========================================================

    /// <summary>Tenta consumir mana. Retorna true se conseguiu.</summary>
    public bool TryConsumeMana(int amount)
    {
        if (amount <= 0) return true;

        if (currentMana < amount)
            return false;

        currentMana -= amount;
        currentMana = Mathf.Clamp(currentMana, 0, maxMana);
        RaiseManaChanged();
        return true;
    }

    public void RestoreMana(int amount)
    {
        if (amount <= 0) return;

        currentMana += amount;
        currentMana = Mathf.Clamp(currentMana, 0, maxMana);
        RaiseManaChanged();
    }

    /// <summary>Ganha experiência (usado pelos inimigos ao morrer).</summary>
    public void GainXP(int amount)
    {
        if (amount <= 0 || isDead) return;

        currentXP += amount;

        bool leveledUp = false;

        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            level++;
            leveledUp = true;

            xpToNextLevel = Mathf.RoundToInt(baseXPToNextLevel *
                                             Mathf.Pow(xpGrowthFactor, level - 1));
        }

        RaiseXPChanged();

        if (leveledUp)
        {
            OnLevelChanged?.Invoke(level);
        }
    }

    //==========================================================
    //  HELPERS DE EVENTO
    //==========================================================

    private void RaiseHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void RaiseManaChanged()
    {
        OnManaChanged?.Invoke(currentMana, maxMana);
    }

    private void RaiseXPChanged()
    {
        OnXPChanged?.Invoke(currentXP, xpToNextLevel);
    }
}
