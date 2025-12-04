//using System.Collections;
//using UnityEngine;

///// <summary>
///// Slime inimigo básico:
///// - Patrulha entre pontos (pathPoints)
///// - Quando vê o player, persegue
///// - Quando chega perto, executa um ataque com pulo em arco
/////   (vai até o player e depois retorna ao ponto de origem)
///// 
///// Esta classe herda de EnemyBase, então:
///// - Usa detectRadius/playerLayer/attackCooldown/moveSpeed da base
///// - Reaproveita TakeDamage / Die / detecção genérica
///// </summary>
//public class Slime : EnemyBase
//{
//    // =========================================================
//    //  ESCALA ORIGINAL (para flip usando localScale)
//    // =========================================================

//    // Guardamos a escala inicial do sprite para não deformar ao flipar.
//    private Vector3 originalScale;

//    // =========================================================
//    //  ATAQUE / DISTÂNCIA / COOLDOWN
//    // =========================================================

//    [Header("Attack Settings (Slime)")]
//    [Tooltip("Distância mínima entre slime e player para começar o ataque.")]
//    [SerializeField] public float stopDistance = 1f;

//    [Tooltip("Distância horizontal que o slime percorre no pulo de ataque.")]
//    [SerializeField] public float attackJumpDistance = 1f;

//    [Tooltip("Altura máxima do arco do pulo (efeito 'jump' no ataque).")]
//    [SerializeField] public float jumpHeight = 0.8f;

//    [Tooltip("Duração do pulo até o player.")]
//    [SerializeField] public float jumpDuration = 0.25f;

//    [Tooltip("Duração do pulo de retorno para a posição original.")]
//    [SerializeField] public float returnDuration = 0.25f;

//    [Tooltip("Ponto de colisão do ataque.")]
//    [SerializeField] public Transform attackPoint;

//    [Tooltip("Alcance de impacto do ataque")]
//    [SerializeField] public float attackRadius;

//    [Tooltip("Alcance de impacto do ataque")]
//    [SerializeField] public LayerMask playerLayer;


//    // Se true, o slime está executando um pulo em arco (ataque ou retorno).
//    private bool isJumping = false;

//    // Posição de onde o slime iniciou o ataque para conseguir voltar exatamente para lá.
//    private Vector2 attackStartPos;

//    /// <summary>
//    /// Esta flag indica que o slime ainda precisa retornar para a posição original do ataque.
//    /// É usada quando o ataque termina antes do retorno ocorrer, garantindo que o slime SEMPRE
//    /// volte ao ponto inicial antes de continuar a IA de patrulha ou iniciar outro ataque.
//    /// </summary>
//    private bool pendingReturn = false;

//    // =========================================================
//    //  MOVIMENTO / PATHFIND
//    // =========================================================

//    [Header("Pathfind Settings")]
//    [Tooltip("Pontos de patrulha entre os quais o slime anda quando não vê o player.")]
//    public Transform[] pathPoints;

//    // Índice atual dentro do array de pontos de patrulha.
//    private int currentPathIndex = 0;

//    // Indica se o slime está aguardando parado em um ponto de patrulha.
//    private bool waitingAtPoint = false;

//    [Tooltip("Tempo de espera parado em cada ponto de patrulha.")]
//    public float waitTime = 2f;

//    // =========================================================
//    //  ESTADOS INTERNOS (para o Animator: 0=Idle, 1=Move, 2=Attack)
//    // =========================================================

//    private enum State
//    {
//        Idle = 0,
//        Move = 1,
//        Attack = 2
//    }

//    // Estado atual do slime (usado para saber qual animação setar).
//    private State currentState = State.Idle;


//    // =========================================================
//    //  CICLO DE VIDA UNITY
//    // =========================================================

//    /// <summary>
//    /// Awake do Slime:
//    /// chamamos o Awake da base e guardamos a escala original do sprite.
//    /// </summary>
//    protected override void Awake()
//    {
//        // EnemyBase cuida de anim, rb, spriteTransform, etc.
//        base.Awake();

//        // Guarda a escala original para usar no flip sem deformar.
//        if (spriteTransform != null)
//            originalScale = spriteTransform.localScale;
//    }

//    /// <summary>
//    /// Update do Slime:
//    /// EnemyBase já chamou DetectPlayer() e agora delega para HandleUpdate().
//    /// </summary>
//    protected override void HandleUpdate()
//    {
//        // Se está no meio de um pulo de ataque/retorno, ignoramos toda a IA.
//        if (isJumping)
//            return;

//        // Se o player foi detectado (EnemyBase já atualizou a variável "player"):
//        if (player != null)
//        {
//            // Gira o sprite para olhar o player.
//            FacePlayer();
//            // Executa a lógica de perseguição / ataque.
//            HandleChase();
//            return;
//        }

//        // Se não há player por perto, seguimos a patrulha.
//        HandlePathfinding();
//    }

//    /// <summary>
//    /// FixedUpdate específico do Slime.
//    /// </summary>
//    protected override void HandleFixedUpdate()
//    {
//        // Se está pulando, garantimos que o Rigidbody2D não interfira com o movimento manual.
//        if (isJumping && rb != null)
//        {
//            rb.linearVelocity = Vector2.zero;
//        }
//    }

//    // =========================================================
//    //  FLIP / VISUAL
//    // =========================================================

//    /// <summary>
//    /// Rotaciona/espelha o slime para olhar na direção do player,
//    /// usando a escala original para evitar deformação.
//    /// </summary>
//    private void FacePlayer()
//    {
//        if (player == null || spriteTransform == null)
//            return;

//        // Começamos da escala original.
//        Vector3 scale = originalScale;

//        // Se o player está à direita, invertendo X espelha o sprite.
//        if (player.position.x > transform.position.x)
//            scale.x = -Mathf.Abs(originalScale.x);
//        else
//            scale.x = Mathf.Abs(originalScale.x);

//        spriteTransform.localScale = scale;
//    }

//    // =========================================================
//    //  CHASE / PERSEGUIÇÃO E DECISÃO DE ATAQUE
//    // =========================================================

//    /// <summary>
//    /// Lógica de perseguição:
//    /// - Se perto o suficiente -> tenta atacar
//    /// - Se longe -> anda em direção ao player
//    /// </summary>
//    private void HandleChase()
//    {
//        if (player == null)
//            return;

//        // Distância atual entre slime e player.
//        float distance = Vector2.Distance(transform.position, player.position);

//        // Sempre garantimos que esteja virado corretamente para o player.
//        FacePlayer();

//        // Se está dentro da distância de parada (raio de ataque):
//        if (distance <= stopDistance)
//        {
//            // Para o movimento do Rigidbody.
//            if (rb != null)
//                rb.linearVelocity = Vector2.zero;

//            // Verifica se o cooldown já passou.
//            bool canAttack = Time.time >= lastAttackTime + attackCooldown;

//            if (canAttack)
//            {
//                // Troca animação para Attack.
//                SetTransition(State.Attack);

//                // Garante novamente que está olhando para o player no momento do ataque.
//                FacePlayer();
//            }
//            else
//            {
//                // Ainda em cooldown, então ficamos em Idle parado.
//                SetTransition(State.Idle);
//            }

//            return;
//        }

//        // Se ainda está longe, anda em direção ao player.
//        Vector2 dir = (player.position - transform.position).normalized;

//        if (rb != null)
//            rb.linearVelocity = dir * moveSpeed;

//        // Define animação de movimento.
//        SetTransition(State.Move);
//    }

//    // =========================================================
//    //  PATRULHA (PATHFINDING SIMPLES)
//    // =========================================================

//    /// <summary>
//    /// Lógica de patrulha entre pontos:
//    /// - Anda até o path atual
//    /// - Quando chega, espera alguns segundos em Idle
//    /// - Avança para o próximo ponto
//    /// </summary>
//    private void HandlePathfinding()
//    {
//        // Se está esperando parado em um ponto, não anda.
//        if (waitingAtPoint)
//        {
//            if (rb != null)
//                rb.linearVelocity = Vector2.zero;

//            SetTransition(State.Idle);
//            return;
//        }

//        // Se não há pontos de path, não faz nada.
//        if (pathPoints == null || pathPoints.Length == 0)
//            return;

//        // Ponto alvo atual da patrulha.
//        Transform target = pathPoints[currentPathIndex];

//        // Distância até esse ponto.
//        float dist = Vector2.Distance(transform.position, target.position);

//        // Se chegou bem pertinho do ponto:
//        if (dist < 0.1f)
//        {
//            // Começa a corrotina de espera.
//            StartCoroutine(WaitAtPoint());
//            return;
//        }

//        // Direção normalizada em direção ao ponto.
//        Vector2 dir = (target.position - transform.position).normalized;

//        // Aplica velocidade.
//        if (rb != null)
//            rb.linearVelocity = dir * moveSpeed;

//        // Define animação de movimento.
//        SetTransition(State.Move);
//    }

//    /// <summary>
//    /// Corrotina que faz o slime esperar parado em um ponto de patrulha.
//    /// </summary>
//    private IEnumerator WaitAtPoint()
//    {
//        // Marca que está esperando.
//        waitingAtPoint = true;

//        // Fica em Idle.
//        SetTransition(State.Idle);

//        // Garante que não se mova.
//        if (rb != null)
//            rb.linearVelocity = Vector2.zero;

//        // Espera o tempo configurado.
//        yield return new WaitForSeconds(waitTime);

//        // Vai para o próximo ponto da lista.
//        currentPathIndex++;
//        if (currentPathIndex >= pathPoints.Length)
//            currentPathIndex = 0;

//        // Libera para voltar a andar.
//        waitingAtPoint = false;
//    }

//    // =========================================================
//    //  ATAQUE — PULO EM ARCO (IDA E VOLTA)
//    // =========================================================

//    /// <summary>
//    /// Chamado por Animation Event no início da animação de ataque.
//    /// Faz o slime pular em arco em direção ao player.
//    /// </summary>
//    public void BeginAttackJump()
//    {
//        // Se não temos player ou já estamos pulando, ignoramos.
//        if (player == null || isJumping)
//            return;

//        // Marca o tempo do ataque para o cooldown.
//        lastAttackTime = Time.time;

//        // Guarda a posição de onde o slime iniciou o ataque,
//        // para conseguir voltar exatamente para esse ponto depois.
//        attackStartPos = transform.position;

//        // Garante que esteja olhando para o player antes do pulo.
//        FacePlayer();

//        // Direção do slime até o player.
//        Vector2 dir = (player.position - transform.position).normalized;

//        // Ponto alvo do pulo (horizontal).
//        Vector2 target = attackStartPos + dir * attackJumpDistance;

//        // Inicia a corrotina que faz o pulo em arco até o player.
//        StartCoroutine(JumpArc(target, jumpDuration));
//    }

//    /// <summary>
//    /// Chamado por Animation Event no final da animação de ataque.
//    /// Faz o slime retornar para o ponto de origem do ataque.
//    /// </summary>
//    public void ReturnFromAttack()
//    {
//        // Se ainda está no meio de um JumpArc, marcamos que o retorno está pendente.
//        if (isJumping)
//        {
//            pendingReturn = true;
//            return;
//        }

//        // Se não está pulando, podemos iniciar imediatamente o pulo de retorno.
//        StartCoroutine(JumpArc(attackStartPos, returnDuration));
//    }

//    /// <summary>
//    /// Corrotina genérica de pulo em arco.
//    /// Ela leva o slime de onde ele está até 'targetPos',
//    /// aplicando um arco vertical (jumpHeight) usando um seno.
//    /// </summary>
//    private IEnumerator JumpArc(Vector2 targetPos, float duration)
//    {
//        // Marca que estamos pulando (para pausar a IA e a física).
//        isJumping = true;

//        // Garante que o Rigidbody2D não empurre o slime.
//        if (rb != null)
//            rb.linearVelocity = Vector2.zero;

//        // Ponto inicial do pulo.
//        Vector2 startPos = transform.position;

//        // Timer do pulo.
//        float time = 0f;

//        // Loop até completar a duração.
//        while (time < duration)
//        {
//            // t vai de 0 a 1.
//            float t = time / duration;

//            // Seno para gerar um arco (0 → 1 → 0).
//            float height = Mathf.Sin(t * Mathf.PI) * jumpHeight;

//            // Interpola posição horizontal entre startPos e targetPos.
//            Vector2 pos = Vector2.Lerp(startPos, targetPos, t);
//            // Adiciona o offset vertical do arco.
//            pos.y += height;

//            // Aplica posição no mundo.
//            transform.position = pos;

//            // Avança o tempo.
//            time += Time.deltaTime;
//            yield return null;
//        }

//        // Garante que terminou exatamente no alvo.
//        transform.position = targetPos;

//        // Libera o estado de pulo.
//        isJumping = false;

//        // Se durante o pulo alguém havia pedido um retorno (pendingReturn),
//        // iniciamos agora o pulo de volta para a posição de origem do ataque.
//        if (pendingReturn)
//        {
//            pendingReturn = false;
//            StartCoroutine(JumpArc(attackStartPos, returnDuration));
//        }
//    }

//    public void Attack()
//    {
//        Collider2D hit = Physics2D.OverlapCircle(attackPoint.position, attackRadius, playerLayer);
//        if (hit != null)
//        {
//            Player player = hit.GetComponent<Player>();
//            if (player != null)
//            {
//                // 10 é só um exemplo de valor de dano.
//                player.TakeDamage(10);
//            }
//        }
//    }

//    // =========================================================
//    //  CONTROLE DE ESTADO / ANIMATOR
//    // =========================================================

//    /// <summary>
//    /// Troca o estado interno e seta o parâmetro "transition" do Animator.
//    /// </summary>
//    private void SetTransition(State newState)
//    {
//        // Evita setar o mesmo estado repetidas vezes.
//        if (currentState == newState)
//            return;

//        currentState = newState;

//        // Usa o helper da base para setar o inteiro do Animator.
//        SetAnimationState((int)newState);
//    }

//    // =========================================================
//    //  GIZMOS (DEBUG)
//    // =========================================================

//    protected override void OnDrawGizmosSelected()
//    {
//        // Chama o gizmo da classe base (raio de detecção).
//        base.OnDrawGizmosSelected();

//        // Desenha também o raio de distância de ataque (stopDistance).
//        Gizmos.color = Color.red;
//        Gizmos.DrawWireSphere(transform.position, stopDistance);
//    }

//    private void OnDrawGizmos()
//    {
//        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
//    }
//}
