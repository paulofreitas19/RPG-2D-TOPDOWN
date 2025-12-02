using UnityEngine;

/// <summary>
/// Controla as animações do player com base na lógica do Player.
/// </summary>
public class PlayerAnim : MonoBehaviour
{
    //========================================================
    //  COMPONENTES / REFERÊNCIAS
    //========================================================

    [Header("Componentes")]

    // Referência para o Animator anexado ao objeto Sprite do player.
    private Animator anim;

    // Referência para o script Player (dono da lógica de movimento/ataque).
    private Player player;

    //========================================================
    //  IDLE TIMER (ANIMAÇÃO ESPECIAL A CADA X SEGUNDOS PARADO)
    //========================================================

    [Header("Idle Timer Settings")]

    // Acumulador de tempo parado.
    [SerializeField] private float idleTimer = 0f;

    // Intervalo entre animações de idle especial.
    [SerializeField] private float idleInterval = 5f;

    // Flag opcional para saber se o idle especial está tocando (se quiser usar).
    private bool isIdlePlaying = false;

    //========================================================
    //  UNITY METHODS
    //========================================================

    private void Awake()
    {
        // Pega referência automática para o Animator.
        anim = GetComponent<Animator>();

        // Pega referência para o Player no objeto pai.
        player = GetComponentInParent<Player>();
    }

    private void Update()
    {
        // Se por algum motivo não temos player, não anima nada.
        if (player == null)
            return;

        // Se o player morreu, podemos setar um estado de transição
        // ou simplesmente travar as animações de movimento.
        if (player.IsDead)
        {
            // Aqui você pode setar um parâmetro "isDeath" no Animator, se tiver:
            // anim.SetBool("isDeath", true);

            // E garantir que não continue chamando as outras lógicas.
            anim.SetInteger("transition", 0);
            return;
        }

        // Atualiza a animação de movimento (idle ou walk).
        HandleMovement();

        // Atualiza o sistema de idle especial de tempos em tempos.
        HandleIdleTimer();
    }

    //========================================================
    //  MOVIMENTO / TRANSIÇÕES BÁSICAS
    //========================================================

    /// <summary>
    /// Ajusta parâmetros do Animator com base na direção do player.
    /// </summary>
    private void HandleMovement()
    {
        // Se a magnitude ao quadrado da direção for maior que um pequeno limiar,
        // consideramos que está se movendo.
        if (player.Direction.sqrMagnitude > 0.01f)
        {
            // Estado "walk" (do seu Animator) – você já configurou como 2.
            anim.SetInteger("transition", 2);

            // Reseta o sistema de idle especial, pois está andando.
            idleTimer = 0f;
            isIdlePlaying = false;
        }
        else
        {
            // Se não está se movendo, usa o idle padrão (transition == 0).
            // O idle especial vai ser forçado pelo HandleIdleTimer quando der o tempo.
            if (!isIdlePlaying)
            {
                anim.SetInteger("transition", 0);
            }
        }
    }

    //========================================================
    //  IDLE ESPECIAL A CADA X SEGUNDOS PARADO
    //========================================================

    /// <summary>
    /// Conta o tempo parado e, a cada idleInterval segundos,
    /// dispara a animação especial de idle.
    /// </summary>
    private void HandleIdleTimer()
    {
        // Se o player está se movendo, não contamos o tempo parado.
        if (player.Direction.sqrMagnitude > 0.01f)
            return;

        // Se o player está morto, também não precisamos do idle especial.
        if (player.IsDead)
            return;

        // Acumula o tempo parado.
        idleTimer += Time.deltaTime;

        // Quando passa do intervalo desejado...
        if (idleTimer >= idleInterval)
        {
            // Toca a animação de idle especial (você configurou como "1").
            anim.SetInteger("transition", 1);

            // Marca que o idle especial está tocando.
            isIdlePlaying = true;

            // Zera o timer para recomeçar a contagem.
            idleTimer = 0f;
        }
    }
}
