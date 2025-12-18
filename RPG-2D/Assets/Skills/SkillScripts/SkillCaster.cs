using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

/// <summary>
/// SkillCaster é o ORQUESTRADOR de skills baseadas em ScriptableObject.
/// 
/// Responsabilidades:
/// - Resolver alvo (self / target / optional)
/// - Decidir se precisa se aproximar
/// - Controlar locks corretos no Player
/// - Executar o cast no momento certo
/// - Gerenciar COOLDOWN (runtime) das skills
/// 
/// Importante:
/// - NÃO movimenta diretamente (delegado ao Player)
/// - NÃO aplica lógica de input
/// - NÃO decide dano (isso é da Skill)
/// - NÃO guarda cooldown no Player (estado é da skill em uso)
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

    // ==========================================================
    //  COOLDOWN RUNTIME (estado do uso da skill)
    // ==========================================================

    // Armazena quando cada skill estará pronta novamente
    // key   → SkillBase
    // value → Time.time em que o cooldown termina
    private readonly Dictionary<SkillBase, float> nextReadyTime = new();

    /// <summary>
    /// Disparado quando uma skill entra em cooldown.
    /// Útil para HUD dar feedback imediato (opacity, fill, etc).
    /// </summary>
    public event Action<SkillBase, float> OnCooldownStarted;

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

        // 🔒 Gate de cooldown
        if (IsOnCooldown(skill))
            return;

        Transform finalTarget = ResolveTarget(skill, target);
        if (finalTarget == null) return;

        StartCoroutine(ApproachAndCast(skill, finalTarget));
    }

    public bool HasManaFor(SkillBase skill)
    {
        if (skill == null) return false;
        if (player == null) return false;

        return player.CurrentMana >= skill.manaCost;
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
        // ✅ Checagem de mana (antes do hard lock, para evitar travar à toa)
        if (skill.manaCost > 0)
        {
            if (!player.TryConsumeMana(skill.manaCost))
                yield break;
        }

        // ⏱️ Início do cooldown (cast REAL começou)
        StartCooldown(skill);

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
    //  COOLDOWN — API PARA HUD / SISTEMAS
    // ==========================================================

    /// <summary>
    /// Retorna quanto tempo falta para a skill sair do cooldown.
    /// </summary>
    public float GetCooldownRemaining(SkillBase skill)
    {
        if (skill == null) return 0f;
        if (!nextReadyTime.TryGetValue(skill, out float readyAt)) return 0f;
        return Mathf.Max(0f, readyAt - Time.time);
    }

    /// <summary>
    /// Retorna a duração total do cooldown da skill.
    /// </summary>
    public float GetCooldownDuration(SkillBase skill)
    {
        return skill != null ? Mathf.Max(0f, skill.cooldown) : 0f;
    }

    /// <summary>
    /// Indica se a skill está atualmente em cooldown.
    /// </summary>
    public bool IsOnCooldown(SkillBase skill)
    {
        return GetCooldownRemaining(skill) > 0f;
    }

    /// <summary>
    /// Registra o início do cooldown de uma skill.
    /// </summary>
    private void StartCooldown(SkillBase skill)
    {
        float cd = Mathf.Max(0f, skill.cooldown);
        if (cd <= 0f) return;

        float readyAt = Time.time + cd;
        nextReadyTime[skill] = readyAt;

        OnCooldownStarted?.Invoke(skill, cd);
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

        // ==========================================================
        // 🔒 REGRA ABSOLUTA:
        // Se a skill exige target, usamos EXCLUSIVAMENTE
        // o TargetManager (target manual do jogador).
        // ==========================================================
        if (skill.targetType == SkillBase.SkillTargetType.TargetRequired)
        {
            if (TargetManager.Instance == null)
                return null;

            ITargetable current = TargetManager.Instance.CurrentTarget;
            if (current == null || !current.IsAlive)
                return null;

            return current.TargetTransform;
        }

        // Optional → só usa explicitTarget se não houver target manual
        if (skill.targetType == SkillBase.SkillTargetType.Optional)
        {
            if (TargetManager.Instance != null)
            {
                var current = TargetManager.Instance.CurrentTarget;
                if (current != null && current.IsAlive)
                    return current.TargetTransform;
            }

            return explicitTarget != null ? explicitTarget : transform;
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
