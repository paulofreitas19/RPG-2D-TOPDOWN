using UnityEngine;

/// <summary>
/// Responsável por conversar com o Animator do player.
/// Não sabe nada de input, HUD ou alvo – só recebe comandos de alto nível.
/// </summary>
public class PlayerAnim : MonoBehaviour
{
    [Header("Componentes")]
    [SerializeField] private Animator anim; // Referência ao Animator do player.

    // Hashes (mais performáticos do que strings diretas).
    private static readonly int TransitionHash = Animator.StringToHash("transition");
    private static readonly int IsCastingHash = Animator.StringToHash("isCasting");
    private static readonly int IsInCombatHash = Animator.StringToHash("isInCombat");
    private static readonly int AttackTrigger = Animator.StringToHash("attackTrigger");
    private static readonly int DeathTrigger = Animator.StringToHash("deathTrigger");

    private void Awake()
    {
        // Se não foi setado no Inspector, tenta achar um Animator no mesmo objeto.
        if (anim == null)
            anim = GetComponent<Animator>();
    }

    /// <summary>
    /// Atualiza animação de movimento (idle / walk).
    /// transition = 0 → parado | 1 → andando.
    /// </summary>
    public void UpdateMovement(float speedMagnitude)
    {
        // Se estiver praticamente parado, consideramos idle.
        int state = speedMagnitude > 0.05f ? 1 : 0;
        anim.SetInteger(TransitionHash, state);
    }

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
    /// Dispara trigger de morte (se você tiver animação).
    /// </summary>
    public void PlayDeath()
    {
        anim.SetTrigger(DeathTrigger);
    }
}
