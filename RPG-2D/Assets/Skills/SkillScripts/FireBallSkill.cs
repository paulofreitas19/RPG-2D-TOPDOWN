using UnityEngine;

[CreateAssetMenu(fileName = "FireBallSkill", menuName = "Skills/FireBallSkill")]
public class FireBallSkill : SkillBase
{
    public GameObject projectilePrefab; // Prefab da fireball

    // Método de ativação da habilidade
    public override void Activate(SkillCaster caster)
    {
        Transform spawnPoint = caster.magicPointSide; // Ponto lateral, pode adaptar para cima também

        GameObject projectile = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity);
        FireBallProjectile fireball = projectile.GetComponent<FireBallProjectile>();
        fireball.SetTarget(caster.target); // Define o alvo da magia
    }

    // Preparação da habilidade (animação de cast)
    public override void Prepare(SkillCaster caster)
    {
        caster.GetComponent<Animator>().SetTrigger("cast");
    }
}