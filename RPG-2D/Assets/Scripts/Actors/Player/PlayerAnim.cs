using UnityEngine;

/// <summary>
/// Responsável por conversar com o Animator do player.
/// Ele não sabe nada de Input ou alvo, só recebe comandos de "alto nível".
/// </summary>
public class PlayerAnim : MonoBehaviour
{
    [Header("Componentes")]
    [SerializeField] private Animator anim;

    // Hashes para ficar mais performático que strings diretas.
    private static readonly int TransitionHash = Animator.StringToHash("transition");
    private static readonly int IsCastingHash = Animator.StringToHash("isCasting");
    private static readonly int IsInCombatHash = Animator.StringToHash("isInCombat");
    private static readonly int AttackTrigger = Animator.StringToHash("attackTrigger");

    private void Awake()
    {
        // Se não foi setado via Inspector, tenta achar um Animator no mesmo objeto.
        if (anim == null)
            anim = GetComponent<Animator>();
    }

    /// <summary>
    /// Atualiza animação de movimento (idle / walk).
    /// </summary>
    /// <param name="speed">Magnitude da velocidade do player.</param>
    public void UpdateMovement(float speed)
    {
        // Exemplo simples:
        // 0 = idle, 1 = andando.
        int state = speed > 0.05f ? 1 : 0;
        anim.SetInteger(TransitionHash, state);
    }

    /// <summary>
    /// Dispara o trigger de ataque básico.
    /// </summary>
    public void PlayBasicAttack()
    {
        anim.SetTrigger(AttackTrigger);
    }

    /// <summary>
    /// Liga/desliga flag de "está conjurando" (cast de skill).
    /// </summary>
    public void SetCasting(bool value)
    {
        anim.SetBool(IsCastingHash, value);
    }

    /// <summary>
    /// Liga/desliga modo de combate para o Animator
    /// (por exemplo, mudar a postura enquanto está em combate).
    /// </summary>
    public void SetInCombat(bool value)
    {
        anim.SetBool(IsInCombatHash, value);
    }
}
