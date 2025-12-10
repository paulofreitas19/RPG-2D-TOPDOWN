using UnityEngine;

public abstract class SkillBase : ScriptableObject
{
    public string skillName;
    public float castTime;
    public GameObject skillPrefab;
    public Sprite icon;

    public abstract void Activate(GameObject caster, Transform target);
}
