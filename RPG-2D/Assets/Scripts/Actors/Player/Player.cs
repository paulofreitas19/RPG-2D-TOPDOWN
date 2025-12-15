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
    //  REFERENCES (componentes / integrações)
    //==========================================================

    [Header("Componentes")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerAnim playerAnim;
    [SerializeField] private EnemyHUDController enemyHud;

    // OBS: SpriteFlipper é o “oráculo” do facing.
    // Ele é o único autorizado a flipar o SpriteRenderer,
    // e opcionalmente reposicionar o MagicPointAttack (spawnpoint).
    [SerializeField] private SpriteFlipper spriteFlipper;

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
    [SerializeField] private float stoppingDistance = 0.1f; // Distância para parar perto do destino.

    private bool isAutoMoving = false;
    private Vector2? moveDestination = null;

    //==========================================================
    //  TARGET / INPUT
    //==========================================================

    [Header("Target / Input")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float tabSearchRadius = 20f;
    [SerializeField] private float maxTargetDistanceToKeep = 25f;

    [Header("Double Click")]
    [SerializeField] private float doubleClickThreshold = 0.25f;

    private float lastClickTime = -1f;
    private ITargetable lastClickedTarget = null;

    //==========================================================
    //  SKILLS / COMBATE
    //==========================================================

    [Serializable]
    public class SkillSlot
    {
        [Header("Identificação")]
        public string skillName = "Skill";

        [Header("Tecla")]
        public KeyCode key = KeyCode.F1;

        [Header("Combate")]
        public float range = 1.7f;
        public float cooldown = 2f;
        public float castTime = 0.3f;
        public int damage = 10;
        public int manaCost = 0;

        [NonSerialized] public float lastUseTime = -999f;
    }

    [Header("Skills")]
    [SerializeField] private SkillSlot basicAttack;
    [SerializeField] private SkillSlot[] skills;        // Skills extras (F1, F2...).

    private bool isCasting = false;                     // Trava durante cast
    private bool isAutoAttacking = false;
    private Coroutine autoAttackRoutine;

    private SkillSlot pendingSkill = null;              // Skill que será usada quando chegar no alvo
    private ITargetable pendingSkillTarget = null;      // Alvo pendente (para auto-approach)

    //==========================================================
    //  VIDA / MANA / XP
    //==========================================================

    [Header("Vida & Dano")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    [Header("Mana")]
    [SerializeField] private int maxMana = 100;
    private int currentMana;

    [Header("XP / Level")]
    [SerializeField] private int level = 1;
    [SerializeField] private int xpToNextLevel = 50;
    private int currentXP;

    public int Level => level;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public int CurrentMana => currentMana;
    public int MaxMana => maxMana;
    public int CurrentXP => currentXP;
    public int XPToNextLevel => xpToNextLevel;

    public event Action<int, int> OnHealthChanged;
    public event Action<int, int> OnManaChanged;
    public event Action<int, int> OnXPChanged;

    //==========================================================
    //  COMBAT MODE (tempo desde última ação)
    //==========================================================

    [Header("Combat Mode")]
    [SerializeField] private float combatTimeout = 5f;

    private float lastCombatTime = -999f;

    //==========================================================
    //  UNITY LIFECYCLE
    //==========================================================

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (playerAnim == null) playerAnim = GetComponentInChildren<PlayerAnim>();
        if (spriteFlipper == null) spriteFlipper = GetComponent<SpriteFlipper>();

        currentHealth = maxHealth;
        currentMana = maxMana;
        currentXP = 0;
    }

    private void Start()
    {
        RaiseHealthChanged();
        RaiseManaChanged();
        RaiseXPChanged();
    }

    private void Update()
    {
        // Ignora input quando está sobre UI (evita clicar na HUD e andar).
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        HandleMouseInput();
        HandleSkillHotkeys();
        HandleTargetKeepAlive();

        UpdateCombatMode();
    }

    private void FixedUpdate()
    {
        HandleMovement();
        UpdateAnimation();
    }

    //==========================================================
    //  INPUT (Mouse)
    //==========================================================

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Clique em inimigo?
            ITargetable clicked = TargetManager.Instance != null
                ? TargetManager.Instance.GetTargetUnderMouse(enemyMask)
                : null;

            // Double click → auto attack
            bool isDoubleClick = false;

            float now = Time.time;
            if (clicked != null && clicked == lastClickedTarget && (now - lastClickTime) <= doubleClickThreshold)
                isDoubleClick = true;

            lastClickTime = now;
            lastClickedTarget = clicked;

            if (clicked != null)
            {
                if (TargetManager.Instance != null)
                    TargetManager.Instance.SetTarget(clicked);

                if (isDoubleClick)
                {
                    StopBasicAttack(); // sempre reinicia o loop
                    autoAttackRoutine = StartCoroutine(BasicAttackLoop(clicked));
                }
            }
            else
            {
                // Clique no chão: cancela auto attack e move.
                StopBasicAttack();
                pendingSkill = null;
                pendingSkillTarget = null;

                Vector2 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                MoveTo(world);
            }
        }

        // TAB: troca target
        if (Input.GetKeyDown(KeyCode.Tab))
            SearchNextTarget();
    }

    //==========================================================
    //  INPUT (Skills)
    //==========================================================

    private void HandleSkillHotkeys()
    {
        // Ataque básico pelo slot (F1), se quiser:
        if (basicAttack != null && Input.GetKeyDown(basicAttack.key))
        {
            TryUseSkillOnTarget(basicAttack);
            return;
        }

        // Skills adicionais (F1/F2/F3...).
        if (skills != null)
        {
            foreach (var s in skills)
            {
                if (s != null && Input.GetKeyDown(s.key))
                {
                    TryUseSkillOnTarget(s);
                    return;
                }
            }
        }
    }

    private void TryUseSkillOnTarget(SkillSlot slot)
    {
        if (slot == null) return;
        if (isCasting) return;

        // Cooldown check
        if (Time.time - slot.lastUseTime < slot.cooldown)
            return;

        ITargetable target = TargetManager.Instance != null ? TargetManager.Instance.CurrentTarget : null;
        if (target == null || !target.IsAlive)
            return;

        // Mana check
        if (currentMana < slot.manaCost)
            return;

        float dist = Vector2.Distance(transform.position, target.TargetTransform.position);

        // 🔥 Importante: já vira pro alvo aqui (primeira garantia).
        // Porém, sozinha ela NÃO basta, porque o player pode chegar no alvo
        // e iniciar o cast com facing desatualizado (por isso reforçamos depois).
        FaceTarget(target.TargetTransform.position);

        // Se está no alcance → cast agora
        if (dist <= slot.range)
        {
            ConsumeMana(slot.manaCost);
            StartCoroutine(CastAndExecuteSkill(slot, target));
        }
        else
        {
            // Se não está no alcance → aproxima e guarda a intenção (auto-approach)
            pendingSkill = slot;
            pendingSkillTarget = target;

            MoveTo(target.TargetTransform.position);
        }
    }

    //==========================================================
    //  CAST / EXECUÇÃO (DANO)
    //==========================================================

    private IEnumerator CastAndExecuteSkill(SkillSlot slot, ITargetable target)
    {
        if (isCasting) yield break;

        isCasting = true;
        slot.lastUseTime = Time.time;

        RegisterCombatEvent();

        // Para tudo: aqui começa a liturgia do cast.
        isAutoMoving = false;
        moveDestination = null;
        rb.linearVelocity = Vector2.zero;

        // 🧭 Bússola arcana do Stark: "se vou disparar algo, eu olho pro alvo".
        // Aqui a orientação NÃO é estética: ela influencia o SpriteFlipper e o alinhamento do MagicPointAttack.
        // Em outras palavras: o facing certo aqui evita fireball nascendo no lado errado.
        FaceTarget(target.TargetTransform.position);

        // Liga flag de cast no Animator.
        playerAnim.SetCasting(true);

        // Se for ataque básico → dispara trigger.
        if (slot == basicAttack)
            playerAnim.PlayBasicAttack();

        // Espera o tempo de cast.
        yield return new WaitForSeconds(slot.castTime);

        // ⚡ Reforço de orientação (por que isso existe?):
        // - Durante o cast, o alvo pode se mover.
        // - O player pode ter recebido uma atualização de facing por outros sistemas.
        // - E principalmente: seu spawnpoint (MagicPointAttack) depende do facing.
        // Então, imediatamente antes de executar o efeito (dano / projétil), a gente "re-trava" o olhar no alvo.
        if (target != null)
            FaceTarget(target.TargetTransform.position);

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

        // Desliga cast no Animator.
        playerAnim.SetCasting(false);
        isCasting = false;

        // Se essa skill era pendente, limpa.
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
                    // 🔥 Momento Tony Stark: antes de conjurar, garante que o mago está olhando pro alvo.
                    // Isso é vital porque o SpriteFlipper ajusta o visual *e* o MagicPointAttack (spawnpoint).
                    // Se a orientação estiver errada, o projétil nasce no "lado espelhado" (principalmente com padding).
                    FaceTargetTransform(pendingSkillTarget.TargetTransform);

                    StartCoroutine(CastAndExecuteSkill(pendingSkill, pendingSkillTarget));
                }
            }

            return;
        }

        Vector2 dirNorm = dir.normalized;
        rb.linearVelocity = dirNorm * moveSpeed;

        FaceTarget(dest);
    }

    public void MoveTo(Vector2 destination)
    {
        isAutoMoving = true;
        moveDestination = destination;
    }

    //==========================================================
    //  ORIENTAÇÃO VISUAL (SEM MEXER NO TRANSFORM)
    //==========================================================

    /// <summary>
    /// Vira o player para olhar para uma posição no mundo.
    /// IMPORTANTÍSSIMO: aqui não mexemos em scale/transform do Player.
    /// O flip é responsabilidade do SpriteFlipper (SpriteRenderer.flipX).
    /// </summary>
    private void FaceTarget(Vector3 worldPos)
    {
        float dirX = worldPos.x - transform.position.x;

        if (spriteFlipper != null)
            spriteFlipper.FaceDirection(dirX);
    }

    /// <summary>
    /// “Versão segura” pra mirar em Transform (alvo),
    /// reaproveitando o método oficial FaceTarget.
    /// </summary>
    private void FaceTargetTransform(Transform target)
    {
        if (target == null) return;
        FaceTarget(target.position); // reaproveita seu método oficial (SpriteFlipper)
    }

    private void UpdateAnimation()
    {
        float speed = rb.linearVelocity.magnitude;
        playerAnim.UpdateMovement(speed);
    }

    //==========================================================
    //  COMBAT MODE & TARGET KEEP ALIVE
    //==========================================================

    private void RegisterCombatEvent()
    {
        lastCombatTime = Time.time;
    }

    private void UpdateCombatMode()
    {
        bool inCombat = (Time.time - lastCombatTime) <= combatTimeout;

        if (enemyHud != null)
            enemyHud.SetOrbsInCombat(inCombat);
    }

    private void HandleTargetKeepAlive()
    {
        if (TargetManager.Instance == null) return;

        ITargetable target = TargetManager.Instance.CurrentTarget;
        if (target == null) return;

        float dist = Vector2.Distance(transform.position, target.TargetTransform.position);
        if (dist > maxTargetDistanceToKeep)
        {
            TargetManager.Instance.ClearTarget();
            StopBasicAttack();
        }
    }

    private void SearchNextTarget()
    {
        if (TargetManager.Instance == null) return;

        ITargetable next = TargetManager.Instance.FindNearestTarget(enemyMask, tabSearchRadius);
        if (next != null)
            TargetManager.Instance.SetTarget(next);
    }

    //==========================================================
    //  ATAQUE BÁSICO (DOUBLE CLICK)
    //==========================================================

    private IEnumerator BasicAttackLoop(ITargetable target)
    {
        isAutoAttacking = true;

        while (isAutoAttacking)
        {
            // Se o alvo morreu, encerra
            if (target == null || !target.IsAlive)
            {
                StopBasicAttack();
                yield break;
            }

            float dist = Vector2.Distance(transform.position, target.TargetTransform.position);

            // Se está longe demais → aproximar
            if (dist > basicAttack.range)
            {
                MoveTo(target.TargetTransform.position);
            }
            else
            {
                // Está em alcance → ataca
                yield return CastAndExecuteSkill(basicAttack, target);

                // Cooldown entre ataques automáticos
                yield return new WaitForSeconds(basicAttack.cooldown);
            }

            yield return null;
        }
    }

    public void StopBasicAttack()
    {
        isAutoAttacking = false;

        if (autoAttackRoutine != null)
        {
            StopCoroutine(autoAttackRoutine);
            autoAttackRoutine = null;
        }
    }

    //==========================================================
    //  MANA
    //==========================================================

    private bool ConsumeMana(int amount)
    {
        if (amount <= 0) return true;
        if (currentMana < amount) return false;

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

    //==========================================================
    //  VIDA / DANO / XP
    //==========================================================

    /// <summary>Receber dano.</summary>
    public void TakeDamage(int amount)
    {
        amount = Mathf.Max(0, amount);
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        RegisterCombatEvent();
        RaiseHealthChanged();

        if (currentHealth <= 0)
        {
            // Morte (você pode expandir isso depois).
            playerAnim.PlayDeath();
        }
    }

    public void AddXP(int amount)
    {
        if (amount <= 0) return;

        currentXP += amount;

        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            LevelUp();
        }

        RaiseXPChanged();
    }

    private void LevelUp()
    {
        level++;

        // Exemplo simples de progressão:
        xpToNextLevel = Mathf.RoundToInt(xpToNextLevel * 1.2f);

        // Recupera um pouco ao upar (opcional)
        currentHealth = maxHealth;
        currentMana = maxMana;

        RaiseHealthChanged();
        RaiseManaChanged();
    }

    //==========================================================
    //  HUD EVENTS
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
