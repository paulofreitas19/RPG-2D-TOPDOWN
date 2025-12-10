using UnityEngine;

public class FireBallProjectile : MonoBehaviour
{
    private Transform target;
    private float speed;
    private float damage;

    public void Init(Transform _target, float _speed, float _damage)
    {
        target = _target;
        speed = _speed;
        damage = _damage;
    }

    void Update()
    {
        if (target == null) return;

        transform.position = Vector2.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, target.position) < 0.1f)
        {
            // Aqui você pode chamar a animação de explosão
            Destroy(gameObject);
        }
    }
}
