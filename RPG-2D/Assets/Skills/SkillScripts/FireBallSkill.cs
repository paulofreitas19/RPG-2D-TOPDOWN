using UnityEngine;

[CreateAssetMenu(menuName = "Skills/Fireball Skill")]
public class FireBallSkill : SkillBase
{
    public float speed;
    public float damage;

    public override void Activate(GameObject caster, Transform target)
    {
        GameObject obj = Instantiate(skillPrefab, caster.transform.position, Quaternion.identity);
        FireBallProjectile proj = obj.GetComponent<FireBallProjectile>();
        proj.Init(target, speed, damage);
    }
}