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
        // Tenta pegar o ponto de conjuração (mão) pelo SkillCaster do player
        SkillCaster casterComp = caster.GetComponent<SkillCaster>();

        Transform castPoint =
            (casterComp != null && casterComp.MagicAttackPoint != null)
            ? casterComp.MagicAttackPoint
            : caster.transform; // fallback seguro

        // Instancia na mão
        GameObject obj = Instantiate(skillPrefab, castPoint.position, castPoint.rotation);

        // Enquanto está "carregando" (idle cast), gruda na mão
        obj.transform.SetParent(castPoint, true);

        projectileInstance = obj.GetComponent<FireBallProjectile>();
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
            // Solta da mão antes de lançar
            projectileInstance.transform.SetParent(null, true);

            projectileInstance.Launch();
        }
    }
}
