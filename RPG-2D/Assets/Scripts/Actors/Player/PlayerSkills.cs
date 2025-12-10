using UnityEngine;

public class PlayerSkills : MonoBehaviour
{
    public SkillBase[] skills;
    private SkillCaster skillCaster;

    private void Awake()
    {
        skillCaster = GetComponent<SkillCaster>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
        {
            skillCaster.CastSkill(skills[0], GetClosestEnemy());
        }

        // Outras teclas para skills[1], skills[2], etc.
    }

    Transform GetClosestEnemy()
    {
        // Exemplo simples, você pode usar tags ou sistema de inimigos
        GameObject enemy = GameObject.FindWithTag("Enemy");
        return enemy?.transform;
    }
}
