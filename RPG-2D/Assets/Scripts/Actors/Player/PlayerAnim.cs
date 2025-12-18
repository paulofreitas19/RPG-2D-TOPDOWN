using UnityEngine;

/// <summary>
/// Responsável por conversar com o Animator do player.
/// Não sabe nada de input, HUD ou alvo – só recebe comandos de alto nível.
/// </summary>
public class PlayerAnim : MonoBehaviour
{
    [Header("Componentes")]
    [SerializeField] private Animator anim; // Referência ao Animator do player.

    // ============================================================
    // CONTROLE DE IDLE ESPECIAL
    // ============================================================

    // 🔒 Enquanto true, nenhuma outra animação pode sobrescrever
    // o idle especial (walk / idle padrão).
    private bool isIdleSpecialLocked = false;

    /// <summary>
    /// Indica se o idle especial está ativo/bloqueando transições.
    /// Usado pelo Player.cs.
    /// </summary>
    public bool IsIdleSpecialLocked => isIdleSpecialLocked;

    // Trigger do Animator para disparar o idle especial (1x)
    private static readonly int IdleSpecialTrigger =
        Animator.StringToHash("idleSpecial");

    // ============================================================
    // HASHES DO ANIMATOR (performance)
    // ============================================================

    private static readonly int TransitionHash =
        Animator.StringToHash("transition");

    private static readonly int IsCastingHash =
        Animator.StringToHash("isCasting");

    private static readonly int IsInCombatHash =
        Animator.StringToHash("isInCombat");

    private static readonly int AttackTrigger =
        Animator.StringToHash("attackTrigger");

    private static readonly int DeathTrigger =
        Animator.StringToHash("deathTrigger");

    // States base usados para forçar saída do idle especial
    private static readonly int IdlePadraoStateHash =
        Animator.StringToHash("idle-padrao");

    private static readonly int WalkStateHash =
        Animator.StringToHash("walk");

    // ============================================================
    // UNITY LIFECYCLE
    // ============================================================

    private void Awake()
    {
        // Se não foi setado no Inspector, tenta achar um Animator no mesmo objeto.
        if (anim == null)
            anim = GetComponent<Animator>();
    }

    // ============================================================
    // MOVIMENTO
    // ============================================================

    /// <summary>
    /// Atualiza animação de movimento (idle / walk).
    /// transition = 0 → parado | 1 → andando.
    /// </summary>
    public void UpdateMovement(float speedMagnitude)
    {
        // 🔒 Se idle especial estiver ativo,
        // não deixamos walk/idle sobrescrever.
        if (isIdleSpecialLocked)
            return;

        int state = speedMagnitude > 0.05f ? 1 : 0;
        anim.SetInteger(TransitionHash, state);
    }

    // ============================================================
    // IDLE ESPECIAL
    // ============================================================

    /// <summary>
    /// Dispara o idle especial (1x).
    /// O Animator deve retornar sozinho para idle-padrão via Exit Time.
    /// </summary>
    public void PlayIdleSpecial()
    {
        isIdleSpecialLocked = true;

        // Segurança extra: limpa trigger antes de setar
        anim.ResetTrigger(IdleSpecialTrigger);
        anim.SetTrigger(IdleSpecialTrigger);
    }

    /// <summary>
    /// Libera o bloqueio do idle especial após o fim do clip.
    /// Chamado por coroutine no Player.cs.
    /// </summary>
    public void UnlockIdleSpecial()
    {
        isIdleSpecialLocked = false;
    }

    /// <summary>
    /// Força a saída imediata do idle especial quando o jogador
    /// realiza qualquer ação (mover, atacar, cast, etc).
    /// </summary>
    public void ForceExitIdleSpecial(float currentSpeedMagnitude)
    {
        isIdleSpecialLocked = false;

        // Decide visual coerente imediatamente
        if (currentSpeedMagnitude > 0.05f)
        {
            anim.CrossFadeInFixedTime(WalkStateHash, 0.05f);
        }
        else
        {
            anim.CrossFadeInFixedTime(IdlePadraoStateHash, 0.05f);
        }
    }

    // ============================================================
    // COMBATE / AÇÕES
    // ============================================================

    /// <summary>
    /// Dispara trigger de ataque básico.
    /// </summary>
    public void PlayBasicAttack()
    {
        anim.SetTrigger(AttackTrigger);
    }

    /// <summary>
    /// Liga/desliga flag de "está conjurando" (cast).
    /// </summary>
    public void SetCasting(bool value)
    {
        anim.SetBool(IsCastingHash, value);
    }

    /// <summary>
    /// Liga/desliga modo de combate (postura de batalha).
    /// </summary>
    public void SetInCombat(bool value)
    {
        anim.SetBool(IsInCombatHash, value);
    }

    /// <summary>
    /// Dispara trigger de morte.
    /// </summary>
    public void PlayDeath()
    {
        anim.SetTrigger(DeathTrigger);
    }
}
