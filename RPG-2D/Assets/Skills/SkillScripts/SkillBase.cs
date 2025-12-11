using UnityEngine;

public abstract class SkillBase : ScriptableObject
{
    public float castTime = 0.5f; // Tempo de conjuração

    // Preparação da habilidade (ex: animação)
    public abstract void Prepare(SkillCaster caster);

    // Execução da habilidade
    public abstract void Activate(SkillCaster caster);
}