using UnityEngine;

/// <summary>
/// Controla os parâmetros do Animator do inimigo
/// com base no movimento e no estado de ataque.
/// </summary>
[RequireComponent(typeof(Animator))]
public class EnemyAnimator : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private EnemyMovement movement;
    [SerializeField] private EnemyAttackBase attack;
    [SerializeField] private Enemy enemy;

    [Header("Parâmetros do Animator")]
    [SerializeField] private string speedParam = "Speed";        // float
    [SerializeField] private string isMovingParam = "IsMoving";  // bool
    [SerializeField] private string attackTrigger = "Attack";    // trigger
    [SerializeField] private string deathTrigger = "Death";      // trigger

    private Animator anim;
    private bool attackAnimPlaying = false;
    private bool deathPlayed = false;

    private void Awake()
    {
        anim = GetComponent<Animator>();

        if (movement == null)
            movement = GetComponentInParent<EnemyMovement>();

        if (attack == null)
            attack = GetComponentInParent<EnemyAttackBase>();

        if (enemy == null)
            enemy = GetComponentInParent<Enemy>();
    }

    private void Update()
    {
        if (enemy == null || anim == null) return;

        // 1) Morte
        if (!enemy.IsAlive)
        {
            if (!deathPlayed && !string.IsNullOrEmpty(deathTrigger))
            {
                anim.SetTrigger(deathTrigger);
                deathPlayed = true;
            }
            return;
        }

        // 2) Movimento (idle/walk)
        Vector2 vel = movement != null ? movement.CurrentVelocity : Vector2.zero;
        float speed = vel.magnitude;

        if (!string.IsNullOrEmpty(speedParam))
            anim.SetFloat(speedParam, speed);

        if (!string.IsNullOrEmpty(isMovingParam))
            anim.SetBool(isMovingParam, speed > 0.05f);

        // 3) Ataque
        if (attack != null && attack.IsAttacking)
        {
            if (!attackAnimPlaying && !string.IsNullOrEmpty(attackTrigger))
            {
                anim.SetTrigger(attackTrigger);
                attackAnimPlaying = true;
            }
        }
        else
        {
            attackAnimPlaying = false;
        }
    }
}
