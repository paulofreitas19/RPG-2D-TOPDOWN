using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla a HUD do inimigo selecionado:
/// nome, barra de vida e "esferas" de estado de combate.
/// </summary>
public class EnemyHUDController : MonoBehaviour
{
    [Header("Referências de UI")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image[] combatOrbs; // 2 esferas cinza/vermelho
    [SerializeField] private Button closeButton;

    [Header("Cores das esferas")]
    [SerializeField] private Color orbIdleColor = Color.gray;
    [SerializeField] private Color orbInCombatColor = Color.red;

    /// <summary>
    /// Alvo atual sendo exibido na HUD.
    /// </summary>
    private ITargetable currentTarget;

    /// <summary>
    /// Referência ao player (usada só para medir distância, se quiser).
    /// </summary>
    [SerializeField] private Transform playerTransform;

    [Header("Configuração de distância máxima")]
    [SerializeField] private float maxDistanceToKeepHUD = 15f;

    private void Awake()
    {
        // Começa com a HUD escondida.
        rootPanel.SetActive(false);

        // Botão X fecha a HUD (limpa o target).
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() =>
            {
                TargetManager.Instance.ClearTarget();
            });
        }
    }

    private void OnEnable()
    {
        // Ouve mudanças de alvo.
        TargetManager.Instance.OnTargetChanged += HandleTargetChanged;
    }

    private void OnDisable()
    {
        // Remove inscrição ao desativar.
        if (TargetManager.Instance != null)
            TargetManager.Instance.OnTargetChanged -= HandleTargetChanged;
    }

    /// <summary>
    /// Atualiza HUD quando o alvo muda.
    /// </summary>
    private void HandleTargetChanged(ITargetable newTarget)
    {
        currentTarget = newTarget;

        if (currentTarget == null)
        {
            rootPanel.SetActive(false);
            return;
        }

        // Atualiza nome.
        nameText.text = currentTarget.DisplayName;

        // Configura slider de vida.
        healthSlider.maxValue = currentTarget.MaxHealth;
        healthSlider.value = currentTarget.CurrentHealth;

        // Esferas começam "cinza" (target adquirido, ainda sem ataque).
        SetOrbsInCombat(false);

        rootPanel.SetActive(true);
    }

    private void Update()
    {
        if (currentTarget == null) return;

        // Atualiza barra de vida em tempo real.
        healthSlider.value = currentTarget.CurrentHealth;

        // Se estiver muito longe, some HUD (opcional).
        if (playerTransform != null)
        {
            float dist = Vector3.Distance(playerTransform.position, currentTarget.TargetTransform.position);

            if (dist > maxDistanceToKeepHUD)
            {
                TargetManager.Instance.ClearTarget();
            }
        }
    }

    /// <summary>
    /// Ajusta cor das esferas de acordo com estado de combate.
    /// </summary>
    /// <param name="inCombat">Se true, ficam vermelhas; senão cinzas.</param>
    public void SetOrbsInCombat(bool inCombat)
    {
        if (combatOrbs == null) return;

        Color colorToUse = inCombat ? orbInCombatColor : orbIdleColor;

        foreach (var orb in combatOrbs)
        {
            if (orb != null)
            {
                orb.color = colorToUse;
            }
        }
    }
}
