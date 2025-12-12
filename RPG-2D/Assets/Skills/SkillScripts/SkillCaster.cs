using System.Collections;
using UnityEngine;

public class SkillCaster : MonoBehaviour
{
    private Animator animator;
    private PlayerAnim playerAnim;

    [SerializeField] private Transform magicAttackPoint;

    public Transform MagicAttackPoint => magicAttackPoint;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        playerAnim = GetComponentInChildren<PlayerAnim>();
    }

    public void CastSkill(SkillBase skill, Transform target)
    {
        StartCoroutine(Cast(skill, target));
    }

    private IEnumerator Cast(SkillBase skill, Transform target)
    {
        playerAnim.SetCasting(true);

        // 🔹 Começa animação do player
        animator.SetTrigger("idleCast");

        // 🔹 Prepara a skill (instancia e mostra a Fireball parada)
        skill.Prepare(gameObject, target);

        yield return new WaitForSeconds(skill.castTime);

        // 🔹 Termina cast, dispara a skill
        skill.Activate(gameObject, target);

        playerAnim.SetCasting(false);
    }
}
