using UnityEngine;

/// <summary>
/// Respons�vel APENAS por movimento f�sico do inimigo.
/// IA n�o fica aqui, apenas:
/// - Calcular dire��o
/// - Aplicar velocity no Rigidbody2D
/// - Virar o sprite na dire��o correta
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Sprite")]
    [Tooltip("Transform que ser� espelhado no eixo X. Se ficar vazio, usa o pr�prio transform.")]
    [SerializeField] private Transform spriteTransform;

    private Rigidbody2D rb;

    // Velocidade atual, �til para anima��o.
    public Vector2 CurrentVelocity => rb.linearVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (spriteTransform == null)
            spriteTransform = transform;
    }

    /// <summary>
    /// Move o inimigo em dire��o a um ponto.
    /// </summary>
    public void MoveTowards(Vector3 targetPosition)
    {
        Vector2 currentPos = rb.position;
        Vector2 targetPos2D = new Vector2(targetPosition.x, targetPosition.y);

        Vector2 dir = targetPos2D - currentPos;

        if (dir.sqrMagnitude < 0.0001f)
        {
            Stop();
            return;
        }

        dir.Normalize();

        rb.linearVelocity = dir * moveSpeed;

        // Espelha o sprite conforme dire��o.
        if (dir.x != 0)
        {
            Vector3 scale = spriteTransform.localScale;
            scale.x = dir.x > 0 ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            spriteTransform.localScale = scale;
        }
    }

    /// <summary>
    /// Para completamente o inimigo.
    /// </summary>
    public void Stop()
    {
        rb.linearVelocity = Vector2.zero;
    }
}
