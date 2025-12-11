using UnityEngine;

public class FireBallProjectile : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float destroyAfter = 2f;

    private Transform target;
    private float speed;
    private float damage;
    private bool isLaunched = false;

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

    private void Update()
    {
        if (!isLaunched || target == null) return;

        // Move em direção ao alvo
        transform.position = Vector2.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // Detecta se chegou no inimigo
        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            // Chama a animação de explosão
            animator.SetTrigger("explode");
            isLaunched = false;

            // Destruir após animação (pode usar animation event também)
            Destroy(gameObject, destroyAfter);
        }
    }
}
