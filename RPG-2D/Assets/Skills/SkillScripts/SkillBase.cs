using UnityEngine;

public abstract class SkillBase : ScriptableObject
{
    public string skillName;
    public float castTime;
    public GameObject skillPrefab;
    public Sprite icon;

    public virtual void Prepare(GameObject caster, Transform target) { }

    public abstract void Activate(GameObject caster, Transform target);
}
