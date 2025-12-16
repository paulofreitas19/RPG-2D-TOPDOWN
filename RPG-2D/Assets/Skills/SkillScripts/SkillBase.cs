using UnityEngine;

public abstract class SkillBase : ScriptableObject
{
    public enum SkillTargetType
    {
        TargetRequired, // precisa de alvo válido
        Self,           // conjura sempre em si mesmo (buff/heal)
        Optional        // se tiver alvo, usa; senão conjura em si mesmo
    }

    [Header("Identificação")]
    public string skillName;

    [Header("Tempo")]
    public float castTime;

    [Header("Alvo")]
    public SkillTargetType targetType = SkillTargetType.TargetRequired;

    [Tooltip("Alcance máximo para conjurar quando a skill exige alvo. Para Self, este valor é ignorado.")]
    public float castRange = 1.7f;

    [Header("Visual")]
    public GameObject skillPrefab;
    public Sprite icon;

    public virtual void Prepare(GameObject caster, Transform target) { }

    public abstract void Activate(GameObject caster, Transform target);
}
