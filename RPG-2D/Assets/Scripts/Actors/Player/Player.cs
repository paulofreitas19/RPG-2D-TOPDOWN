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
    ///
    /// Internamente, isso ativa os mesmos selos usados
    /// pelo cast interno do Player:
    /// - bloqueia movimento
    /// - bloqueia input
    /// - bloqueia spam de skills
    ///
    /// Importante:
    /// - Não expõe flags
    /// - Não permite estados inválidos
    /// - Apenas delega para LockCasting()
    /// </summary>
    public void BeginExternalCastLock()
    {
        if (isCasting) return;
        LockCasting();
    }

    /// <summary>
    /// ✨ PORTAL ARCANO — FIM DE CAST EXTERNO
    /// ----------------------------------------------------------
    /// Libera o Player após o término do cast iniciado
    /// por sistemas externos (SkillCaster).
    ///
    /// Quebra os mesmos selos ativados no BeginExternalCastLock().
    /// </summary>
    public void EndExternalCastLock()
    {
        if (!isCasting) return;
        UnlockCasting();
    }


    //==========================================================
    //  INPUT (Mouse)
    //==========================================================

    private void HandleMouseInput()
    {
        // 🔒 Selo de Controle Externo (Approach Lock):
        // Durante aproximação automática iniciada por sistemas externos (SkillCaster),
        // ignoramos inputs do jogador (clique, TAB, hotkeys) para não quebrar o fluxo.
        if (externalInputLock)
            return;

        // 🔒 Selo do Movimento:
        // Enquanto castando, ignoramos clique no chão para mover.
        // (Mas ainda permitimos clicar em inimigo para targetar se você quiser.)
        if (isCasting)
        {
            // TAB também não deve trocar alvo durante cast (evita mudanças de intenção no meio da conjuração)
            if (Input.GetKeyDown(KeyCode.Tab))
                return;

            // Clique no chão durante cast não deve fazer nada:
            // ignoramos apenas a parte de "mover".
            // Ainda assim, deixamos o clique em inimigo funcionar (target apenas), caso você goste desse comportamento.
        }

        if (Input.GetMouseButtonDown(0))
        {
            // Clique em inimigo?
            // 🧙‍♂️ Agora esse feitiço realmente existe no TargetManager (e é responsabilidade dele).
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
        // Durante aproximação automática iniciada por sistemas externos (SkillCaster),
        // ignoramos hotkeys para não permitir spam/override de intenção.
        if (externalInputLock)
            return;

        // 🔒 Selo da Conjuração (anti-spam):
        // Enquanto castando, ignoramos hotkeys de skills.
        if (isCasting)
            return;

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

        // 🔒 Anti-spam / hard lock:
        // Não permite iniciar outra skill se já está em cast.
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
        // Mesmo se ainda tiver que andar pra chegar no range, o player já começa
        // a "se orientar" corretamente.
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
        // Se já estiver castando, não entra (anti-spam).
        if (isCasting) yield break;

        // 🔒 Ativa os 3 selos:
        // - trava movimento
        // - trava iniciar outra skill
        // - estabiliza o estado para o cast
        LockCasting();

        // Por segurança, olha pro alvo no instante do início.
        FaceTarget(target.TargetTransform.position);

        slot.lastUseTime = Time.time;

        RegisterCombatEvent();

        // 🔥 SEGUNDA RUNA: antes de qualquer animação / espera,
        // garantimos a orientação correta.
        FaceTarget(target.TargetTransform.position);

        // Liga flag de cast no Animator.
        playerAnim.SetCasting(true);

        // Se for ataque básico → dispara trigger.
        if (slot == basicAttack)
            playerAnim.PlayBasicAttack();

        // Espera o tempo de cast.
        yield return new WaitForSeconds(slot.castTime);

        // 🔥 TERCEIRA RUNA: imediatamente ANTES de aplicar o efeito,
        // vira de novo. Por quê?
        // - o alvo pode ter andado durante o cast
        // - o facing pode ter sido alterado por movimento / input
        // - e seu spawnpoint/offset depende do facing (SpriteFlipper)
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

        // 🔓 Quebra os selos no final do cast:
        UnlockCasting();

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
        // 🔒 Selo do Movimento:
        // Durante cast, o player NÃO se move.
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

            // Se estava se aproximando para skill pendente, checa alcance.
            if (pendingSkill != null && pendingSkillTarget != null && pendingSkillTarget.IsAlive)
            {
                float distToTarget = Vector3.Distance(
                    transform.position,
                    pendingSkillTarget.TargetTransform.position);

                if (distToTarget <= pendingSkill.range)
                {
                    // 🔥 RUNA FINAL: no exato instante que entra no alcance,
                    // vira pro alvo ANTES de iniciar o cast.
                    FaceTargetTransform(pendingSkillTarget.TargetTransform);

                    StartCoroutine(CastAndExecuteSkill(pendingSkill, pendingSkillTarget));
                }
            }

            return;
        }

        Vector2 dirNorm = dir.normalized;
        rb.linearVelocity = dirNorm * moveSpeed;

        // 🔮 RUNA DO MOVIMENTO CONSCIENTE
        // Só vira se realmente estiver se movendo no eixo X
        if (Mathf.Abs(dirNorm.x) > 0.01f)
        {
            Vector3 lookPoint = transform.position + new Vector3(dirNorm.x, 0f, 0f);
            FaceTarget(lookPoint);
        }
    }

    public void MoveTo(Vector2 destination)
    {
        // 🔒 Selo do Movimento:
        // Se está castando, ignora o comando de movimento.
        if (isCasting)
            return;

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
        // 🌙 Ao invés de mexer no scale do Player (teleport arcano),
        // nós pedimos ao SpriteFlipper para virar o visual e o spawnpoint corretamente.
        if (spriteFlipper != null)
            spriteFlipper.FaceTarget(worldPos);
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
        // ✅ Aqui é a forma mais consistente com seu projeto,
        // porque você já tem esse método pronto no TargetManager.
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

    // ==========================================================
    // RUNA DA COMPATIBILIDADE ANCESTRAL (XP)
    // ----------------------------------------------------------
    // Alguns pergaminhos antigos (ex: Enemy.cs) ainda invocam
    // o método GainXP(). Em versões mais novas, o nome evoluiu
    // para AddXP().
    //
    // Para NÃO quebrar o mundo e manter a arquitetura atual,
    // mantemos GainXP() como um "portal" para AddXP().
    // ==========================================================
    public void GainXP(int amount)
    {
        // Portal arcano: mesmo efeito, novo nome por baixo.
        AddXP(amount);
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

        // ==========================================================
        // RUNA DA ASCENSÃO
        // ----------------------------------------------------------
        // Após todos os cálculos de nível, XP e restauração,
        // notificamos o mundo:
        // "O mago evoluiu."
        //
        // O HUD escuta.
        // Sistemas futuros podem escutar.
        // O Player não precisa saber quem escuta.
        // ==========================================================
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

    // ==========================================================
    // RUNA DO DESPERTAR DE NÍVEL
    // ----------------------------------------------------------
    // Este evento é observado pelo HUD e outros sistemas
    // que precisam reagir quando o Player sobe de nível.
    //
    // ⚠️ Importante:
    // Mesmo que o Player funcione sem ele,
    // outros scripts JÁ ESPERAM essa runa existir.
    // Removê-la quebra o contrato arcano do sistema.
    // ==========================================================
    public event Action<int> OnLevelChanged;
}
