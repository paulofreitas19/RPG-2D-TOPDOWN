using UnityEngine;
using UnityEngine.UI;

public class SkillHotbarSlotUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SkillCaster caster;
    [SerializeField] private SkillBase skill;

    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private CanvasGroup iconGroup;
    [SerializeField] private Image cooldownOverlay; // Image setado como Radial 360
    [SerializeField] private GameObject readyGlow;

    [Header("Visual")]
    [Range(0f, 1f)][SerializeField] private float cooldownOpacity = 0.35f;

    private bool wasOnCooldown = false;

    private void Awake()
    {
        if (readyGlow != null) readyGlow.SetActive(false);
        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
    }

    private void Update()
    {
        if (caster == null || skill == null) return;

        float remaining = caster.GetCooldownRemaining(skill);
        float duration = caster.GetCooldownDuration(skill);

        bool onCd = remaining > 0f;

        // Opacidade do ícone
        if (iconGroup != null)
            iconGroup.alpha = onCd ? cooldownOpacity : 1f;

        // Fill radial: 1 -> 0 enquanto carrega (ou invertido, você escolhe)
        if (cooldownOverlay != null)
        {
            if (onCd && duration > 0f)
            {
                float t = Mathf.Clamp01(remaining / duration);
                cooldownOverlay.fillAmount = t; // cheio no começo, esvaziando
            }
            else
            {
                cooldownOverlay.fillAmount = 0f;
            }
        }

        // Brilho quando termina (transição onCd -> offCd)
        if (wasOnCooldown && !onCd)
        {
            TriggerReadyGlow();
        }

        wasOnCooldown = onCd;
    }

    private void TriggerReadyGlow()
    {
        if (readyGlow == null) return;

        readyGlow.SetActive(true);
        CancelInvoke(nameof(HideGlow));
        Invoke(nameof(HideGlow), 0.02f);
    }

    private void HideGlow()
    {
        if (readyGlow != null) readyGlow.SetActive(false);
    }

    // Opcional: chamado por botão da UI
    public void OnClick()
    {
        if (caster != null && skill != null)
            caster.CastSkill(skill);
    }
}
