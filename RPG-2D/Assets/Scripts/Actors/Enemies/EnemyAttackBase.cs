using UnityEngine;

/// <summary>
/// Classe base abstrata para qualquer tipo de ataque de inimigo.
/// Não conhece Animator, nem Brain. Apenas:
/// - referência ao Enemy (vida / estado)
/// - referência ao EnemyMovement (para parar / mover se precisar)
/// Cada inimigo concreto implementa StartAttack do seu jeito.
/// </summary>
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(EnemyMovement))]
public abstract class EnemyAttackBase : MonoBehaviour
{
    [Header("Ataque Base")]
    [Tooltip("Alcance padrão do ataque. O EnemyBrain usa isso para saber quando pode atacar.")]
    [SerializeField] protected float attackRange = 1.5f;

    [Tooltip("Cooldown padrão do ataque em segundos.")]
    [SerializeField] protected float attackCooldown = 1.5f;

    protected Enemy enemy;
    protected EnemyMovement movement;

    /// <summary>Alcance público do ataque (lido pelo EnemyBrain).</summary>
    public float AttackRange => attackRange;

    /// <summary>Cooldown público do ataque (lido pelo EnemyBrain).</summary>
    public float AttackCooldown => attackCooldown;

    /// <summary>Se o inimigo está, neste momento, executando um ataque.</summary>
    public bool IsAttacking { get; protected set; }

    protected virtual void Awake()
    {
        enemy = GetComponent<Enemy>();
        movement = GetComponent<EnemyMovement>();
    }

    /// <summary>
    /// Inicia o ataque contra um alvo.
    /// A implementação concreta (SlimeAttack, OrcAttack, etc)
    /// decide como animar, pular, atirar, etc.
    /// </summary>
    public abstract void StartAttack(Transform target);
}
