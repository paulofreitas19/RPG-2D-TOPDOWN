using UnityEngine;

/// <summary>
/// "Cérebro" genérico para qualquer inimigo.
/// Responsável por:
/// - Patrulhar entre pontos
/// - Detectar o player
/// - Perseguir o player
/// - Parar no alcance e mandar atacar
/// Não sabe COMO o inimigo se move nem COMO ele ataca,
/// apenas conversa com EnemyMovement e EnemyAttackBase.
/// </summary>
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyAttackBase))]
public class EnemyBrain : MonoBehaviour
{
    // ---------------- CONFIGURAÇÃO GERAL ----------------

    [Header("Detecção do Player")]
    [Tooltip("Raio no qual o inimigo passa a enxergar o player.")]
    [SerializeField] private float detectRadius = 6f;

    [Tooltip("Tag usada pelo player (necessário taguear o Player).")]
    [SerializeField] private string playerTag = "Player";

    [Header("Distâncias de Comportamento")]
    [Tooltip("Distância mínima até o player para considerar que está no alcance de ataque.")]
    [SerializeField] private float attackRangeOverride = -1f; // -1 = usa valor do ataque

    [Tooltip("Distância máxima para começar a perseguir (se > 0, sobrescreve detectRadius).")]
    [SerializeField] private float chaseRangeOverride = -1f;

    [Header("Patrulha")]
    [Tooltip("Pontos entre os quais o inimigo patrulha quando não vê o player.")]
    public Transform[] patrolPoints;

    [Tooltip("Tempo parado em cada ponto de patrulha.")]
    [SerializeField] private float waitAtPointTime = 2f;

    // ---------------- REFERÊNCIAS INTERNAS ----------------

    private Enemy enemy;                 // Vida / HUD.
    private EnemyMovement movement;      // Movimento físico.
    private EnemyAttackBase attack;      // Ataque específico (SlimeAttack, OrcAttack etc).
    private Transform playerTransform;   // Referência ao Player (por tag).

    // ---------------- ESTADO DA IA ----------------

    private enum EnemyState { Idle, Patrol, Chase, Attack }
    private EnemyState state = EnemyState.Idle;

    private int patrolIndex = 0;         // Índice atual de patrulha.
    private float waitTimer = 0f;        // Contador de espera na patrulha.

    private float lastAttackTime = -999f;

    // ======================================================
    //  CICLO DE VIDA
    // ======================================================

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        movement = GetComponent<EnemyMovement>();
        attack = GetComponent<EnemyAttackBase>();
    }

    private void Start()
    {
        // Procura o player pela Tag apenas uma vez.
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
            playerTransform = playerObj.transform;

        // Se tiver pontos de patrulha, começamos patrulhando.
        if (patrolPoints != null && patrolPoints.Length > 0)
            state = EnemyState.Patrol;
        else
            state = EnemyState.Idle;
    }

    private void Update()
    {
        // Se já morreu, não faz nada.
        if (!enemy.IsAlive)
        {
            movement.Stop();
            return;
        }

        // Se não existe player na cena, só patrulha ou fica idle.
        if (playerTransform == null)
        {
            RunIdleOrPatrol();
            return;
        }

        // Se está atacando, o ataque assume o controle.
        if (attack.IsAttacking)
        {
            movement.Stop();
            return;
        }

        // Verifica distâncias.
        float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        float chaseRange = (chaseRangeOverride > 0) ? chaseRangeOverride : detectRadius;
        float attackRange = (attackRangeOverride > 0) ? attackRangeOverride : attack.AttackRange;

        // Decide o estado da IA.
        if (distToPlayer <= attackRange)
            state = EnemyState.Attack;
        else if (distToPlayer <= chaseRange)
            state = EnemyState.Chase;
        else
            state = (patrolPoints != null && patrolPoints.Length > 0)
                ? EnemyState.Patrol
                : EnemyState.Idle;

        // Executa o comportamento correspondente.
        switch (state)
        {
            case EnemyState.Idle:
                RunIdleOrPatrol(); // Idle simples.
                break;
            case EnemyState.Patrol:
                RunPatrol();
                break;
            case EnemyState.Chase:
                RunChase();
                break;
            case EnemyState.Attack:
                RunAttack();
                break;
        }
    }

    // ======================================================
    //  COMPORTAMENTOS
    // ======================================================

    /// <summary>
    /// Comportamento quando não há player por perto
    /// e não há pontos de patrulha.
    /// </summary>
    private void RunIdleOrPatrol()
    {
        // Se não tem pontos de patrulha → apenas para.
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            movement.Stop();
            return;
        }

        RunPatrol();
    }

    /// <summary>
    /// Patrulhar entre pontos quando não está vendo o player.
    /// </summary>
    private void RunPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            movement.Stop();
            return;
        }

        Transform targetPoint = patrolPoints[patrolIndex];

        // Distância até o ponto atual.
        float dist = Vector2.Distance(transform.position, targetPoint.position);

        // Se chegou muito perto, espera um tempo e muda pro próximo ponto.
        if (dist <= 0.1f)
        {
            movement.Stop();

            waitTimer += Time.deltaTime;
            if (waitTimer >= waitAtPointTime)
            {
                waitTimer = 0f;
                patrolIndex++;
                if (patrolIndex >= patrolPoints.Length)
                    patrolIndex = 0;
            }
        }
        else
        {
            // Ainda não chegou no ponto → continua indo.
            movement.MoveTowards(targetPoint.position);
        }
    }

    /// <summary>
    /// Perseguir o player até chegar no alcance de ataque.
    /// </summary>
    private void RunChase()
    {
        if (playerTransform == null)
        {
            movement.Stop();
            return;
        }

        movement.MoveTowards(playerTransform.position);
    }

    /// <summary>
    /// No alcance de ataque: para e dispara o ataque se o cooldown permitir.
    /// </summary>
    private void RunAttack()
    {
        movement.Stop();

        // Cooldown do ataque.
        if (Time.time < lastAttackTime + attack.AttackCooldown)
            return;

        lastAttackTime = Time.time;

        if (playerTransform != null)
            attack.StartAttack(playerTransform);
    }
}
