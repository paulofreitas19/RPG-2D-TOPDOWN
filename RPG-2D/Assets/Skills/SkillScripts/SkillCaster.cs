using System.Collections;
using UnityEngine;

/// <summary>
/// SkillCaster é o ORQUESTRADOR de skills baseadas em ScriptableObject.
/// 
/// Responsabilidades:
/// - Resolver alvo (self / target / optional)
/// - Decidir se precisa se aproximar
/// - Controlar locks corretos no Player
/// - Executar o cast no momento certo
/// 
/// Importante:
/// - NÃO movimenta diretamente (delegado ao Player)
/// - NÃO aplica lógica de input
/// - NÃO decide dano (isso é da Skill)
/// </summary>
public class SkillCaster : MonoBehaviour
{
    private Animator animator;
    private PlayerAnim playerAnim;

    // 🧙‍♂️ O Guardião do Flip vive no filho "Sprite", não no Player raiz.
    private SpriteFlipper spriteFlipper;

    // 🔒 Player raiz (locks + movimento)
    private Player player;

    [SerializeField] private Transform magicAttackPoint;
    public Transform MagicAttackPoint => magicAttackPoint;

    // ==========================================================
    //  ESTADO INTERNO (anti-spam e consistência)
    // ==========================================================

    private bool isBusy = false; // true durante approach OU cast

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        playerAnim = GetComponentInChildren<PlayerAnim>();

        spriteFlipper = GetComponentInChildren<SpriteFlipper>(true);
        player = GetComponent<Player>();
    }

    // ==========================================================
    //  API PÚBLICA (Hotkey / UI)
    // ==========================================================

    /// <summary>
    /// Versão simples: resolve o alvo automaticamente.
    /// Ideal para hotkeys e botões de UI.
    /// </summary>
    public void CastSkill(SkillBase skill)
    {
        if (skill == null) return;

        Transform resolvedTarget = ResolveTarget(skill);
        CastSkill(skill, resolvedTarget);
    }

    /// <summary>
    /// Versão explícita: recebe um target direto.
    /// Útil para sistemas futuros (IA, scripts, combos).
    /// </summary>
    public void CastSkill(SkillBase skill, Transform target)
    {
        if (skill == null) return;
        if (player == null) return;

        // 🔒 Anti-spam global do SkillCaster
        if (isBusy) return;

        // 🔒 Se o Player já está castando algo, não inicia outro fluxo
        if (player.IsCasting) return;

        Transform finalTarget = ResolveTarget(skill, target);
        if (finalTarget == null) return;

        StartCoroutine(ApproachAndCast(skill, finalTarget));
    }

    // ==========================================================
    //  CORE: APROXIMAÇÃO + CAST
    // ==========================================================

    private IEnumerator ApproachAndCast(SkillBase skill, Transform target)
    {
        isBusy = true;

        // 🔒 Durante o approach:
        // - bloqueia input do jogador
        // - NÃO trava movimento
        player.BeginExternalInputLock();

        // ===============================
        // 1️⃣ SKILL SELF → cast direto
        // ===============================
        if (skill.targetType == SkillBase.SkillTargetType.Self)
        {
            yield return StartCoroutine(Cast(skill, transform));
            Cleanup();
            yield break;
        }

        // ===============================
        // 2️⃣ SKILL COM ALVO
        // ===============================

        float range = Mathf.Max(0.05f, skill.castRange);

        // Enquanto não estiver no range, continua se aproximando
        while (target != null)
        {
            float dist = Vector2.Distance(transform.position, target.position);
            if (dist <= range)
                break;

            // Move continuamente (alvo pode andar)
            player.MoveTo(target.position);
            FaceTargetSafely(target);

            yield return null;
        }

        // Se o alvo sumiu/morreu no caminho → aborta
        if (target == null)
        {
            Cleanup();
            yield break;
        }

        // ===============================
        // 3️⃣ CAST REAL
        // ===============================
        yield return StartCoroutine(Cast(skill, target));

        Cleanup();
    }

    private void Cleanup()
    {
        player.EndExternalInputLock();
        isBusy = false;
    }

    // ==========================================================
    //  CAST REAL (Hard Lock)
    // ==========================================================

    private IEnumerator Cast(SkillBase skill, Transform target)
    {
        // 🔒 Agora sim: hard lock total (para movimento)
        player.BeginExternalCastLock();

        // Orientação inicial
        FaceTargetSafely(target);

        // 🔮 Animação de cast
        playerAnim.SetCasting(true);
        animator.SetTrigger("idleCast");

        FaceTargetSafely(target);

        // Prepare (spawn na mão, carregar magia, etc.)
        skill.Prepare(gameObject, target);

        yield return new WaitForSeconds(skill.castTime);

        // 🔥 Disparo
        FaceTargetSafely(target);
        skill.Activate(gameObject, target);

        playerAnim.PlayBasicAttack();
        playerAnim.SetCasting(false);

        // 🔓 Libera hard lock
        player.EndExternalCastLock();
    }

    // ==========================================================
    //  RESOLUÇÃO DE ALVO
    // ==========================================================

    private Transform ResolveTarget(SkillBase skill, Transform explicitTarget = null)
    {
        if (skill == null) return null;

        // Self → sempre o caster
        if (skill.targetType == SkillBase.SkillTargetType.Self)
            return transform;

        // Se já veio explícito, usa
        if (explicitTarget != null)
            return explicitTarget;

        // Caso contrário, tenta TargetManager
        if (TargetManager.Instance == null)
            return null;

        ITargetable current = TargetManager.Instance.CurrentTarget;

        // Target obrigatório
        if (skill.targetType == SkillBase.SkillTargetType.TargetRequired)
        {
            if (current == null || !current.IsAlive)
                return null;

            return current.TargetTransform;
        }

        // Optional → se não tem alvo, conjura em si
        if (skill.targetType == SkillBase.SkillTargetType.Optional)
        {
            if (current != null && current.IsAlive)
                return current.TargetTransform;

            return transform;
        }

        return null;
    }

    // ==========================================================
    //  ORIENTAÇÃO VISUAL
    // ==========================================================

    /// <summary>
    /// Vira o player para o alvo sem depender de assinatura específica
    /// do SpriteFlipper.
    /// </summary>
    private void FaceTargetSafely(Transform target)
    {
        if (spriteFlipper == null || target == null) return;

        float dirX = target.position.x - transform.position.x;
        if (Mathf.Abs(dirX) < 0.001f) return;

        spriteFlipper.FaceDirection(dirX);
    }
}
