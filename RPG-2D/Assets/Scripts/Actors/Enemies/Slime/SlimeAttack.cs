using System.Collections;
using UnityEngine;

/// <summary>
/// Ataque específico do Slime:
/// - Realiza um pulo em arco até perto do player
/// - Aplica dano caso o player esteja no raio de impacto
/// - Retorna para a posição original
/// 
/// Totalmente independente de Animator.
/// </summary>
public class SlimeAttack : EnemyAttackBase
{
    [Header("Pulo do Slime")]
    [Tooltip("Altura máxima do arco do pulo.")]
    [SerializeField] private float jumpHeight = 0.8f;

    [Tooltip("Duração do pulo até o alvo.")]
    [SerializeField] private float jumpDuration = 0.25f;

    [Tooltip("Duração do pulo de volta.")]
    [SerializeField] private float returnDuration = 0.25f;

    [Header("Dano")]
    [Tooltip("Raio utilizado para checar o impacto.")]
    [SerializeField] private float hitRadius = 0.6f;

    [Tooltip("Ponto de origem da checagem de dano.")]
    [SerializeField] private Transform hitOrigin;

    [Tooltip("Quantidade de dano causado no player.")]
    [SerializeField] private int damage = 10;

    [Tooltip("Layer utilizada para detectar o Player.")]
    [SerializeField] private LayerMask playerLayer;

    // Guarda a posição original exata antes do pulo.
    private Vector2 originalPosition;

    // Alvo atual recebido do EnemyBrain.
    private Transform currentTarget;


    // ======================================================================
    // =========================  INÍCIO DO ATAQUE  =========================
    // ======================================================================

    /// <summary>
    /// Iniciado pelo EnemyBrain quando o player entra no alcance.
    /// </summary>
    public override void StartAttack(Transform target)
    {
        if (IsAttacking || target == null || !enemy.IsAlive)
            return;

        // Salva posição exata no momento do ataque.
        originalPosition = transform.position;

        currentTarget = target;

        StartCoroutine(AttackRoutine());
    }


    // ======================================================================
    // =========================   ROTINA COMPLETA   =========================
    // ======================================================================

    private IEnumerator AttackRoutine()
    {
        IsAttacking = true;
        movement.Stop(); // desativa locomoção durante o ataque

        // 1) Pulo até o alvo
        Vector2 startPos = transform.position;
        Vector2 targetPos = startPos;

        if (currentTarget != null)
        {
            Vector2 dir = ((Vector2)currentTarget.position - startPos).normalized;
            targetPos = (Vector2)currentTarget.position - dir * 0.8f;
        }

        yield return StartCoroutine(JumpArc(startPos, targetPos, jumpDuration));

        // 2) Checagem de dano
        TryHitPlayer();

        // 3) Retorno à posição original
        yield return StartCoroutine(JumpArc(transform.position, originalPosition, returnDuration));

        IsAttacking = false;
    }


    // ======================================================================
    // =========================     ARCO DO PULO     ========================
    // ======================================================================

    private IEnumerator JumpArc(Vector2 from, Vector2 to, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);

            Vector2 pos = Vector2.Lerp(from, to, progress);
            pos.y += Mathf.Sin(progress * Mathf.PI) * jumpHeight;

            transform.position = pos;

            yield return null;
        }

        transform.position = to;
    }


    // ======================================================================
    // ======================   DETECÇÃO E APLICAÇÃO DO DANO   ===============
    // ======================================================================

    private void TryHitPlayer()
    {
        if (hitOrigin == null)
            hitOrigin = transform;

        // ------------ 🔥 VALIDAÇÃO CRÍTICA DO LAYER MASK 🔥 ------------
        // Garante que APENAS a layer definida no Inspector seja usada.
        int maskValue = playerLayer.value;

        if (maskValue == 0)
        {
            Debug.LogWarning($"[SlimeAttack] Nenhuma Layer selecionada para playerLayer no Slime '{name}'.");
        }
        // -----------------------------------------------------------------

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


    // Gizmo para visualizar o raio do ataque no Editor
    private void OnDrawGizmosSelected()
    {
        if (hitOrigin == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(hitOrigin.position, hitRadius);
    }
}
