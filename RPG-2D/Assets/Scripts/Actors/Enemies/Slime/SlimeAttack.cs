using System.Collections;
using UnityEngine;

/// <summary>
/// Ataque específico do Slime:
/// - Dá um pulo em arco até perto do player
/// - Aplica dano se o player estiver no raio
/// - Volta para a posição original
/// Toda a lógica é controlada aqui, sem depender de Animator ou Brain.
/// </summary>
public class SlimeAttack : EnemyAttackBase
{
    [Header("Pulo do Slime")]
    [Tooltip("Altura máxima do arco do pulo.")]
    [SerializeField] private float jumpHeight = 0.8f;

    [Tooltip("Duração do pulo até o alvo.")]
    [SerializeField] private float jumpDuration = 0.25f;

    [Tooltip("Duração para retornar à posição original.")]
    [SerializeField] private float returnDuration = 0.25f;

    [Header("Dano")]
    [Tooltip("Raio para checar se o player foi atingido.")]
    [SerializeField] private float hitRadius = 0.6f;

    [Tooltip("Ponto de origem do dano (normalmente um filho chamado AttackPoint).")]
    [SerializeField] private Transform hitOrigin;

    [Tooltip("Quantidade de dano causada ao player.")]
    [SerializeField] private int damage = 10;

    [Tooltip("Layer do player para detecção do dano.")]
    [SerializeField] private LayerMask playerLayer;

    private Vector2 originalPosition;
    private Transform currentTarget;

    /// <summary>
    /// Chamado pelo EnemyBrain quando o slime está no alcance de ataque.
    /// </summary>
    public override void StartAttack(Transform target)
    {
        // Se já estiver atacando, não empilha outro ataque.
        if (IsAttacking || target == null || !enemy.IsAlive)
            return;

        // 🔹 Salva a posição EXATAMENTE no momento em que vai atacar
        originalPosition = transform.position;

        currentTarget = target;
        StartCoroutine(AttackRoutine());
    }


    /// <summary>
    /// 1) Pulo em arco até a proximidade do player
    /// 2) Checagem de dano
    /// 3) Pulo de volta à posição original
    /// </summary>
    private IEnumerator AttackRoutine()
    {
        IsAttacking = true;

        // Impede o movimento normal enquanto o ataque acontece.
        movement.Stop();

        // -----------------------------
        // 1) Pulo até perto do player
        // -----------------------------
        Vector2 startPos = transform.position;
        Vector2 targetPos = startPos;

        if (currentTarget != null)
        {
            Vector2 dir = ((Vector2)currentTarget.position - startPos).normalized;
            // Um pouco antes do player, para não "atravessar" totalmente.
            targetPos = (Vector2)currentTarget.position - dir * 0.8f;
        }

        // Pula em arco até o alvo.
        yield return StartCoroutine(JumpArc(startPos, targetPos, jumpDuration));

        // -----------------------------
        // 2) Aplicar dano
        // -----------------------------
        TryHitPlayer();

        // -----------------------------
        // 3) Voltar à posição inicial
        // -----------------------------
        yield return StartCoroutine(JumpArc(transform.position, originalPosition, returnDuration));

        // Ataque terminou.
        IsAttacking = false;
    }

    /// <summary>
    /// Realiza um pulo em arco entre dois pontos.
    /// </summary>
    private IEnumerator JumpArc(Vector2 from, Vector2 to, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);

            // Lerp da posição no plano.
            Vector2 pos = Vector2.Lerp(from, to, progress);

            // Arco vertical usando seno.
            float heightOffset = Mathf.Sin(progress * Mathf.PI) * jumpHeight;
            pos.y += heightOffset;

            transform.position = pos;

            yield return null;
        }

        transform.position = to;
    }

    /// <summary>
    /// Verifica se o player está dentro do raio de dano e aplica o dano.
    /// </summary>
    private void TryHitPlayer()
    {
        if (hitOrigin == null)
            hitOrigin = transform;

        Collider2D hit = Physics2D.OverlapCircle(hitOrigin.position, hitRadius, playerLayer);

        if (hit != null)
        {
            Player player = hit.GetComponent<Player>();
            if (player != null)
            {
                player.TakeDamage(damage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (hitOrigin == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(hitOrigin.position, hitRadius);
    }
}
