using UnityEngine;

public class FireBallProjectile : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float destroyAfter = 1.818f;

    private Transform target;
    private float speed;
    private float damage;
    private bool isLaunched = false;
    private bool hasHit = false;


    public void Init(Transform target, float speed, float damage)
    {
        this.target = target;
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
        hasHit = true;
        isLaunched = false;

        // 🛑 Desativa colisão — não é mais um projétil
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        // 🔥 Gruda no inimigo (ancora no centro)
        transform.SetParent(target);
        transform.localPosition = Vector3.zero;

        // 🔥 Dispara animação de explosão
        animator.SetTrigger("explode");

        // 🔥 Destroi após a animação
        Destroy(gameObject, destroyAfter);
    }


    private void Update()
    {
        if (!isLaunched || target == null) return;

        // Move em direção ao alvo
        transform.position = Vector2.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // Detecta se chegou no inimigo
        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            OnHitTarget();
        }
    }
}
