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
    public static Player Instance { get; private set; }

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
    [SerializeField] private SpriteFlipper spriteFlipper; // Guardião do flip (SpriteRenderer + MagicPointAttack)

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
    [SerializeField] private int currentMana;

    [Header("XP / Level")]
    [SerializeField] private int level = 1;
    [SerializeField] private int xpToNextLevel = 50;
    private int currentXP;

    [Header("Idle Especial")]
    [SerializeField] private float idleDelay = 5f;

    // ✅ (ADIÇÃO) Duração do clip idle especial (para Unlock sem AnimationEvent)
    // Ajuste no Inspector conforme a duração real do seu clip "idle" especial.
    [SerializeField] private float idleSpecialClipDuration = 1.0f;

    private float nextIdleSpecialAt;
    private Coroutine idleSpecialRoutine;

    public int Level => level;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public int CurrentMana => currentMana;
    public int MaxMana => maxMana;
    public int CurrentXP => currentXP;
    public int XPToNextLevel => xpToNextLevel;

    // 🔒 Estado sagrado do cast
    // ------------------------------------------------------------------
    // Importante:
    // - Você já tinha a flag "isCasting" usada no fluxo inteiro do Player.
    // - Para manter compatibilidade e evitar quebrar o mundo,
    //   a propriedade pública IsCasting apenas reflete o isCasting.
    // ------------------------------------------------------------------
    public bool IsCasting => isCasting;

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
    //  SELO DE CONTROLE EXTERNO (Approach Lock)
    //==========================================================

    // 🔒 Este selo é usado por sistemas externos (ex: SkillCaster)
    // para bloquear INPUT do Player durante uma aproximação automática
    // (auto-approach), sem travar o movimento em si.
    //
    // Por que ele existe?
    // - O hard lock (isCasting) trava movimento, então não serve para 'andar até o alvo'.
    // - Durante o approach, queremos impedir: clique no chão, TAB, hotkeys, spam.
    // - Mas queremos permitir que um sistema externo continue chamando MoveTo() até entrar no range.
    //
    // Resultado:
    // - Player não obedece input do jogador durante approach
    // - mas continua se movendo automaticamente até o ponto desejado.
    private bool externalInputLock = false;

    /// <summary>
    /// True quando um sistema externo bloqueou o input do Player (approach).
    /// </summary>
    public bool IsExternallyInputLocked => externalInputLock;

    /// <summary>
    /// 🔒 Ativa o bloqueio de input externo (aproximação).
    /// Não trava o movimento automático; apenas impede o jogador de sobrescrever
    /// a intenção atual com cliques/hotkeys/TAB.
    /// </summary>
    public void BeginExternalInputLock()
    {
        if (externalInputLock) return;
        externalInputLock = true;

        // Coerência: durante um approach externo, não faz sentido manter
        // auto-attack interno ou pendências internas de skill rodando.
        StopBasicAttack();
        pendingSkill = null;
        pendingSkillTarget = null;

        // ✅ (ADIÇÃO) Approach externo é “ação do jogo”: reseta idle especial e interrompe se estiver tocando.
        RegisterPlayerAction();
    }

    /// <summary>
    /// 🔓 Desativa o bloqueio de input externo (fim da aproximação).
    /// </summary>
    public void EndExternalInputLock()
    {
        if (!externalInputLock) return;
        externalInputLock = false;
    }

    //==========================================================
    //  UNITY LIFECYCLE
    //==========================================================

    private void Awake()
    {
        // ===============================
        // RUNA DO AVATAR ÚNICO (Singleton)
        // ===============================
        // Garante que exista apenas um Player
        // acessível globalmente via Player.Instance
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (playerAnim == null) playerAnim = GetComponentInChildren<PlayerAnim>();
        if (spriteFlipper == null)
            spriteFlipper = GetComponentInChildren<SpriteFlipper>();

        currentHealth = maxHealth;
        currentMana = maxMana;
        currentXP = 0;
    }

    private void Start()
    {
        nextIdleSpecialAt = Time.time + idleDelay; // começa a contar do início do jogo
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

        CheckIdleSpecial();
    }

    private void FixedUpdate()
    {
        HandleMovement();
        UpdateAnimation();
    }

    //==========================================================
    //  HARD LOCK (Cast Lock)
    //==========================================================

    /// <summary>
    /// 🔒 RUNA DO SELAMENTO ARCANO (Hard Lock)
    /// ----------------------------------------------------------
    /// Durante o cast queremos bloquear:
    /// - Movimento por clique (click-to-move)
    /// - Iniciar outra skill (anti-spam)
    /// - Mudanças de direção por movimento (facing lock indireto)
    ///
    /// Observação:
    /// A direção para o alvo ainda é permitida no instante certo,
    /// pois ela ocorre via FaceTarget() dentro do próprio cast.
    /// </summary>
    private void LockCasting()
    {
        isCasting = true;

        // 👣 Selo do Movimento:
        // Ao iniciar cast, paramos completamente e limpamos destino.
        // Assim, nenhum "clique anterior" continua puxando o player.
        isAutoMoving = false;
        moveDestination = null;
        rb.linearVelocity = Vector2.zero;

        // Opcional, mas coerente com o combate:
        // durante cast não faz sentido manter auto-attack rodando.
        StopBasicAttack();
    }

    /// <summary>
    /// 🔓 RUNA DA LIBERTAÇÃO (fim do cast)
    /// ----------------------------------------------------------
    /// Ao terminar o cast, o Player volta a obedecer:
    /// - clique no chão para mover
    /// - hotkeys para conjurar outras skills
    /// </summary>
    private void UnlockCasting()
    {
        isCasting = false;
    }

    //==========================================================
    //  CAST EXTERNO (SkillCaster)
    //==========================================================

    /// <summary>
    /// 🧿 PORTAL ARCANO — INÍCIO DE CAST EXTERNO
    /// ----------------------------------------------------------
    /// Usado por sistemas externos (ex: SkillCaster)
    /// para informar ao Player:
    /// "Você está conjurando uma magia agora".
    /// </summary>
    public void BeginExternalCastLock()
    {
        if (isCasting) return;
        LockCasting();

        // ✅ (ADIÇÃO) Cast externo é ação: reseta idle especial e interrompe se estiver tocando.
        RegisterPlayerAction();
    }

    /// <summary>
    /// ✨ PORTAL ARCANO — FIM DE CAST EXTERNO
    /// ----------------------------------------------------------
    /// Libera o Player após o término do cast iniciado
    /// por sistemas externos (SkillCaster).
    /// </summary>
    public void EndExternalCastLock()
    {
        if (!isCasting) return;
        UnlockCasting();
    }

    /// <summary>
    /// Deve ser chamado sempre que o jogador realiza QUALQUER ação.
    /// Reseta o contador de idle especial.
    /// </summary>
    private void RegisterPlayerAction()
    {
        // Reinicia o “relógio” do idle especial
        nextIdleSpecialAt = Time.time + idleDelay;

        // Se estava tocando idle especial, interrompe imediatamente
        if (playerAnim != null && playerAnim.IsIdleSpecialLocked)
        {
            float speedMag = rb.linearVelocity.magnitude;
            playerAnim.ForceExitIdleSpecial(speedMag);
        }

        // Se havia coroutine de unlock rodando, cancela
        if (idleSpecialRoutine != null)
        {
            StopCoroutine(idleSpecialRoutine);
            idleSpecialRoutine = null;
        }
    }

    private void CheckIdleSpecial()
    {
        // Se está castando, não dispara
        if (IsCasting)
            return;

        // Se está se movendo, não dispara
        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            return;

        // Se ainda não chegou a hora, não dispara
        if (Time.time < nextIdleSpecialAt)
            return;

        // Se já está no idle especial, não dispara de novo
        if (playerAnim != null && playerAnim.IsIdleSpecialLocked)
            return;

        // Dispara o idle especial (1x)
        if (playerAnim != null)
            playerAnim.PlayIdleSpecial();

        // Agenda o próximo disparo para +5s (mesmo sem ação)
        nextIdleSpecialAt = Time.time + idleDelay;

        // Agenda unlock do lock visual após o tempo do clip
        if (playerAnim != null)
            idleSpecialRoutine = StartCoroutine(UnlockIdleSpecialAfterAnim());
    }

    private IEnumerator UnlockIdleSpecialAfterAnim()
    {
        // Coloque aqui a duração REAL do seu clip idle especial:
        // ✅ (ADIÇÃO) Agora configurável no Inspector (idleSpecialClipDuration).
        yield return new WaitForSeconds(idleSpecialClipDuration);

        if (playerAnim != null)
            playerAnim.UnlockIdleSpecial();

        idleSpecialRoutine = null;
    }

    //==========================================================
    //  INPUT (Mouse)
    //==========================================================

    private void HandleMouseInput()
    {
        // 🔒 Selo de Controle Externo (Approach Lock):
        // Durante aproximação automática iniciada por sistemas externos (SkillCaster),
        // ignoramos inputs do jogador para não quebrar o fluxo.
        if (externalInputLock)
            return;

        // 🔒 Enquanto castando: bloqueamos TAB e bloqueamos mover no chão,
        // mas ainda permitimos clique em inimigo para escolher target (apenas target).
        if (isCasting)
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            // ✅ (ADIÇÃO) Qualquer clique do jogador é ação:
            // reseta contador do idle especial e interrompe se estiver tocando.
            RegisterPlayerAction();

            // ==========================================================
            // RUNA ANTI-BUG (NÃO targetar por HOVER)
            // ----------------------------------------------------------
            // Antes havia um trecho que chamava GetTargetUnderMouse()
            // todo frame e já setava SetManualTarget(...) sem clique.
            //
            // Isso causava a "troca automática de target" quando o mouse
            // passava por outro inimigo.
            //
            // Agora a captura do alvo sob o mouse acontece APENAS no clique.
            // ==========================================================
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
                // ✅ Clique é intenção do jogador -> alvo MANUAL
                if (TargetManager.Instance != null)
                    TargetManager.Instance.SetManualTarget(clicked);

                // 🔒 Durante cast, não iniciamos auto-attack (senão vira “spam de loop”).
                if (!isCasting && isDoubleClick)
                {
                    StopBasicAttack(); // sempre reinicia o loop
                    autoAttackRoutine = StartCoroutine(BasicAttackLoop(clicked));
                }
            }
            else
            {
                // Clique no chão: cancela auto attack e move.
                // 🔒 Mas durante cast, NÃO move.
                if (isCasting)
                    return;

                StopBasicAttack();
                pendingSkill = null;
                pendingSkillTarget = null;

                Vector2 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                MoveTo(world);
            }
        }

        // TAB: troca target
        // 🔒 Bloqueado durante cast (evita o player “pensar em outro inimigo” no meio da conjuração)
        if (!isCasting && Input.GetKeyDown(KeyCode.Tab))
            SearchNextTarget();
    }

    //==========================================================
    //  INPUT (Skills)
    //==========================================================

    private void HandleSkillHotkeys()
    {
        // 🔒 Selo de Controle Externo (Approach Lock):
        if (externalInputLock)
            return;

        // 🔒 Selo da Conjuração (anti-spam):
        if (isCasting)
            return;

        // Ataque básico pelo slot (F1), se quiser:
        if (basicAttack != null && Input.GetKeyDown(basicAttack.key))
        {
            // ✅ (ADIÇÃO) Hotkey é ação do jogador:
            RegisterPlayerAction();

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
                    // ✅ (ADIÇÃO) Hotkey é ação do jogador:
                    RegisterPlayerAction();

                    TryUseSkillOnTarget(s);
                    return;
                }
            }
        }
    }

    private void TryUseSkillOnTarget(SkillSlot slot)
    {
        if (slot == null) return;

        // 🔒 Anti-spam / hard lock:
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

        // 🔥 PRIMEIRA RUNA: vira pro alvo IMEDIATAMENTE ao tentar usar.
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

        LockCasting();

        // Por segurança, olha pro alvo no instante do início.
        FaceTarget(target.TargetTransform.position);

        slot.lastUseTime = Time.time;
        RegisterCombatEvent();

        FaceTarget(target.TargetTransform.position);

        playerAnim.SetCasting(true);

        if (slot == basicAttack)
            playerAnim.PlayBasicAttack();

        yield return new WaitForSeconds(slot.castTime);

        if (target != null)
            FaceTarget(target.TargetTransform.position);

        if (target != null && target.IsAlive)
        {
            target.TakeDamage(slot.damage);

            if (enemyHud != null &&
                TargetManager.Instance != null &&
                TargetManager.Instance.CurrentTarget == target)
            {
                enemyHud.SetOrbsInCombat(true);
            }
        }

        playerAnim.SetCasting(false);

        UnlockCasting();

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
        if (isCasting)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!isAutoMoving)
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

            if (pendingSkill != null && pendingSkillTarget != null && pendingSkillTarget.IsAlive)
            {
                float distToTarget = Vector3.Distance(
                    transform.position,
                    pendingSkillTarget.TargetTransform.position);

                if (distToTarget <= pendingSkill.range)
                {
                    FaceTargetTransform(pendingSkillTarget.TargetTransform);
                    StartCoroutine(CastAndExecuteSkill(pendingSkill, pendingSkillTarget));
                }
            }

            return;
        }

        Vector2 dirNorm = dir.normalized;
        rb.linearVelocity = dirNorm * moveSpeed;

        if (Mathf.Abs(dirNorm.x) > 0.01f)
        {
            Vector3 lookPoint = transform.position + new Vector3(dirNorm.x, 0f, 0f);
            FaceTarget(lookPoint);
        }
    }

    public void MoveTo(Vector2 destination)
    {
        if (isCasting)
            return;

        // ✅ (ADIÇÃO) Movimento (mesmo iniciado por sistema externo) é uma ação que deve:
        // - interromper idle especial se estiver tocando
        // - resetar o relógio para não disparar idle durante intenção de movimento
        RegisterPlayerAction();

        isAutoMoving = true;
        moveDestination = destination;
    }

    //==========================================================
    //  ORIENTAÇÃO VISUAL (SEM MEXER NO TRANSFORM)
    //==========================================================

    private void FaceTarget(Vector3 worldPos)
    {
        if (spriteFlipper != null)
            spriteFlipper.FaceTarget(worldPos);
    }

    private void FaceTargetTransform(Transform target)
    {
        if (target == null) return;
        FaceTarget(target.position);
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

        TargetManager.Instance.CycleTargetWithTab(transform.position, tabSearchRadius);
    }

    //==========================================================
    //  ATAQUE BÁSICO (DOUBLE CLICK)
    //==========================================================

    private IEnumerator BasicAttackLoop(ITargetable target)
    {
        isAutoAttacking = true;

        while (isAutoAttacking)
        {
            if (target == null || !target.IsAlive)
            {
                StopBasicAttack();
                yield break;
            }

            float dist = Vector2.Distance(transform.position, target.TargetTransform.position);

            if (dist > basicAttack.range)
            {
                MoveTo(target.TargetTransform.position);
            }
            else
            {
                yield return CastAndExecuteSkill(basicAttack, target);
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

    public bool TryConsumeMana(int amount)
    {
        return ConsumeMana(amount);
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

    public void TakeDamage(int amount)
    {
        amount = Mathf.Max(0, amount);
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        RegisterCombatEvent();
        RaiseHealthChanged();

        if (currentHealth <= 0)
        {
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

    public void GainXP(int amount)
    {
        AddXP(amount);
    }

    private void LevelUp()
    {
        level++;
        xpToNextLevel = Mathf.RoundToInt(xpToNextLevel * 1.2f);

        currentHealth = maxHealth;
        currentMana = maxMana;

        RaiseHealthChanged();
        RaiseManaChanged();

        OnLevelChanged?.Invoke(level);
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

    public event Action<int> OnLevelChanged;
}
