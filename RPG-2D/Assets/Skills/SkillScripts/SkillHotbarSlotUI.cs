using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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

    private Coroutine glowRoutine;

    [Header("Mana Feedback")]
    [SerializeField] private Color noManaColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color normalColor = Color.white;

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

        bool hasMana = caster.HasManaFor(skill);

        if (!hasMana)
        {
            icon.color = noManaColor;
        }
        else
        {
            icon.color = normalColor;
        }

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

        // Evita múltiplas corrotinas rodando
        if (glowRoutine != null)
            StopCoroutine(glowRoutine);

        glowRoutine = StartCoroutine(ReadyGlowPulse());
    }

    private IEnumerator ReadyGlowPulse()
    {
        const int pulses = 2;
        const float onTime = 0.03f;
        const float offTime = 0.03f;

        for (int i = 0; i < pulses; i++)
        {
            readyGlow.SetActive(true);
            yield return new WaitForSeconds(onTime);

            readyGlow.SetActive(false);
            yield return new WaitForSeconds(offTime);
        }

        glowRoutine = null;
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
