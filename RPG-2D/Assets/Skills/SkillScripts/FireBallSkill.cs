using UnityEngine;

[CreateAssetMenu(menuName = "Skills/Fireball Skill")]
public class FireBallSkill : SkillBase
{
    public float speed;
    public float damage;

    // Referência da instância criada durante o cast
    private FireBallProjectile projectileInstance;

    /// <summary>
    /// Chamado no início da conjuração.
    /// Aqui instanciamos a fireball parada e tocamos idle-cast.
    /// </summary>
    public override void Prepare(GameObject caster, Transform target)
    {
        // Instancia a fireball no ponto de magia do player
        GameObject obj = Instantiate(
            skillPrefab,
            caster.transform.position,
            Quaternion.identity
        );

        // Guarda referência do projétil
        projectileInstance = obj.GetComponent<FireBallProjectile>();

        // Inicializa SEM lançar ainda
        projectileInstance.Init(target, speed, damage);
    }

    /// <summary>
    /// Chamado após o tempo de cast.
    /// Aqui a fireball começa a se mover (shoot).
    /// </summary>
    public override void Activate(GameObject caster, Transform target)
    {
        // Dispara a fireball somente após o cast
        if (projectileInstance != null)
        {
            projectileInstance.Launch();
        }
    }
}
