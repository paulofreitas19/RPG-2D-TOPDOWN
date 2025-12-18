using UnityEngine;

public class FireBallProjectile : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float destroyAfter = 1.818f;

    // ==========================================================
    // ✅ ALVO FIXO (Target Lock)
    // ----------------------------------------------------------
    // Guardamos o alvo como ITargetable (fixo), e usamos
    // TargetTransform apenas para posição/parent.
    // Isso impede a fireball de "trocar" de inimigo.
    // ==========================================================
    private ITargetable target;

    private float speed;
    private float damage;
    private bool isLaunched = false;
    private bool hasHit = false;

    public void Init(Transform target, float speed, float damage)
    {
        // ==========================================================
        // ✅ MODIFICAÇÃO MÍNIMA
        // ----------------------------------------------------------
        // Antes: guardava Transform direto (instável para target lock).
        // Agora: convertemos Transform -> ITargetable (uma vez).
        // ==========================================================
        if (target != null)
        {
            // Tenta pegar ITargetable no próprio objeto ou no pai (seguro para hierarquia).
            this.target = target.GetComponent<ITargetable>() ?? target.GetComponentInParent<ITargetable>();
        }
        else
        {
            this.target = null;
        }

        this.speed = speed;
        this.damage = damage;

        // Idle-cast já é o estado inicial no Animator
        // A Fireball aparece parada
    }

    public void Launch()
    {
        isLaunched = true;
        animator.SetTrigger("castComplete"); // Troca de idle-cast para shoot
    }

    private void OnHitTarget()
    {
        if (hasHit) return;
        hasHit = true;
        isLaunched = false;

        // 🔥 APLICAR DANO REAL AO ALVO (TRAVADO)
        // ----------------------------------------------------------
        // Agora aplicamos o dano DIRETO no ITargetable que foi travado
        // no momento do cast, e não em "quem estiver na frente".
        if (target != null && target.IsAlive)
        {
            target.TakeDamage(Mathf.RoundToInt(damage));
        }

        // 🛑 Desativa colisão — não é mais um projétil
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        // 🔥 Gruda no inimigo (ancora no centro)
        // ----------------------------------------------------------
        // Como target é interface, usamos o Transform do alvo.
        Transform targetTransform = (target != null) ? target.TargetTransform : null;

        if (targetTransform != null)
        {
            transform.SetParent(targetTransform);
            transform.localPosition = Vector3.zero;
        }

        // 🔥 Dispara animação de explosão
        animator.SetTrigger("explode");

        // 🔥 Destroi após a animação
        Destroy(gameObject, destroyAfter);
    }

    private void Update()
    {
        // Se não lançou ou não existe alvo válido → não faz nada.
        if (!isLaunched || target == null) return;

        // Se o alvo morreu no caminho, não tenta "trocar".
        if (!target.IsAlive) return;

        Transform targetTransform = target.TargetTransform;
        if (targetTransform == null) return;

        // Move em direção ao alvo
        transform.position = Vector2.MoveTowards(
            transform.position,
            targetTransform.position,
            speed * Time.deltaTime);

        // Detecta se chegou no inimigo
        if (Vector3.Distance(transform.position, targetTransform.position) < 0.1f)
        {
            OnHitTarget();
        }
    }
}
