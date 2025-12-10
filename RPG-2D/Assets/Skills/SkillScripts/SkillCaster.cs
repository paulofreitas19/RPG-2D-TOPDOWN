using System.Collections;
using UnityEngine;

public class SkillCaster : MonoBehaviour
{
    public void CastSkill(SkillBase skill, Transform target)
    {
        StartCoroutine(Cast(skill, target));
    }

    private IEnumerator Cast(SkillBase skill, Transform target)
    {
        // Animação de cast (até frame 4 por exemplo)
        Debug.Log($"Casting {skill.skillName}...");
        yield return new WaitForSeconds(skill.castTime);

        skill.Activate(gameObject, target);
    }
}
