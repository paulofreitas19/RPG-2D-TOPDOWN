using UnityEngine;

/// <summary>
/// Responsável apenas por conversar com o Animator do player,
/// mantendo o script Player focado em lógica de jogo.
/// 
/// Padrão usado:
/// - Parâmetro inteiro "transition" controla estados base (idle / walk / etc).
///   • 0 = Idle
///   • 2 = Walk (movimento)
/// - Trigger "basicAttack" para o ataque básico.
/// - Trigger "death" para animação de morte.
/// 
/// Ajuste os nomes/valores de acordo com o seu Animator Controller.
/// </summary>
public class PlayerAnim : MonoBehaviour
{
    //======================================================================
    //  COMPONENTES
    //======================================================================

    /// <summary>
    /// Referência para o Animator que controla as animações do player.
    /// </summary>
    [Header("Componentes")]
    [SerializeField] private Animator animator;

    /// <summary>
    /// SpriteRenderer usado para fazer flip do sprite (virar para direita/esquerda).
    /// </summary>
    [SerializeField] private SpriteRenderer spriteRenderer;

    //======================================================================
    //  HASHES DE PARÂMETROS (OTIMIZAÇÃO)
    //======================================================================

    /// <summary>
    /// Hash do parâmetro inteiro "transition".
    /// </summary>
    private readonly int hashTransition = Animator.StringToHash("transition");

    /// <summary>
    /// Hash do trigger "basicAttack".
    /// </summary>
    private readonly int hashBasicAttack = Animator.StringToHash("basicAttack");

    /// <summary>
    /// Hash do trigger "death".
    /// </summary>
    private readonly int hashDeath = Animator.StringToHash("death");

    //======================================================================
    //  MÉTODOS UNITY
    //======================================================================

    /// <summary>
    /// Garante que as referências estejam configuradas.
    /// </summary>
    private void Awake()
    {
        // Se não foi atribuído via Inspector, tenta pegar automaticamente.
        if (animator == null)
            animator = GetComponent<Animator>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    //======================================================================
    //  CONTROLE DE MOVIMENTO (IDLE / WALK)
    //======================================================================

    /// <summary>
    /// Recebe a velocidade atual do player e decide se anima como Idle ou Walk.
    /// </summary>
    /// <param name="speed">Módulo da velocidade (sempre positivo).</param>
    public void SetMoveSpeed(float speed)
    {
        // Se a velocidade é praticamente zero, considera parado (Idle).
        if (speed <= 0.01f)
        {
            animator.SetInteger(hashTransition, 0); // Idle
        }
        else
        {
            animator.SetInteger(hashTransition, 2); // Walk
        }
    }

    /// <summary>
    /// Ajusta o flip do sprite para olhar na direção correta.
    /// Consideramos um mundo 2D top-down, mas olhando "para os lados".
    /// </summary>
    /// <param name="direction">
    /// Vetor direção para onde o player está indo ou onde está o alvo.
    /// </param>
    public void SetLookDirection(Vector2 direction)
    {
        // Se a direção for praticamente zero, não muda o flip.
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        // Se o movimento/alvo está para a esquerda, flip desativado.
        // Se está para a direita, flip ativado.
        if (direction.x > 0f)
        {
            spriteRenderer.flipX = true;
        }
        else if (direction.x < 0f)
        {
            spriteRenderer.flipX = false;
        }
    }

    //======================================================================
    //  ATAQUE BÁSICO
    //======================================================================

    /// <summary>
    /// Dispara a animação de ataque básico.
    /// É chamado pelo script Player quando o ataque é executado.
    /// </summary>
    public void PlayBasicAttack()
    {
        // Opcionalmente, podemos forçar o estado base para "ataque".
        // Mas geralmente só o trigger já resolve, dependendo do Animator.
        animator.SetTrigger(hashBasicAttack);
    }

    //======================================================================
    //  MORTE
    //======================================================================

    /// <summary>
    /// Dispara a animação de morte.
    /// </summary>
    public void PlayDeath()
    {
        animator.SetTrigger(hashDeath);

        // Você pode também travar o parâmetro de movimento em Idle, se quiser:
        animator.SetInteger(hashTransition, 0);
    }
}
