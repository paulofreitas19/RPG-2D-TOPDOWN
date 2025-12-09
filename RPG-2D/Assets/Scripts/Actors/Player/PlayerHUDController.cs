using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla a HUD do Player (nome, level, HP, MP, XP).
/// Fica no Canvas e escuta eventos do Player.
/// </summary>
public class PlayerHUDController : MonoBehaviour
{
    public static PlayerHUDController Instance { get; private set; }

    [Header("Referências")]
    [SerializeField] private GameObject panelRoot;  // Painel inteiro (frame de madeira).
    [SerializeField] private Text nameText;         // Nome do player (ex: MERLIN).
    [SerializeField] private TextMeshProUGUI levelText;        // Level (ex: Level: 5).

    [Header("Barras")]
    [SerializeField] private Image hpFill;          // Barra de vida (type = Filled, Horizontal).
    [SerializeField] private Image mpFill;          // Barra de mana.
    [SerializeField] private Image xpFill;          // Barra de experiência.

    [Header("Player")]
    [SerializeField] private Player player;         // Referência ao Player na cena.

    private void Awake()
    {
        // Singleton simples
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (panelRoot == null)
            panelRoot = gameObject; // fallback: usa o próprio objeto
    }

    private void Start()
    {
        // Se não foi setado no Inspector, tenta pegar o Player.Instance
        if (player == null)
            player = Player.Instance;

        if (player == null)
        {
            Debug.LogWarning("PlayerHUDController: Player não encontrado na cena.");
            panelRoot.SetActive(false);
            return;
        }

        // Assina eventos do Player
        player.OnHealthChanged += HandleHealthChanged;
        player.OnManaChanged += HandleManaChanged;
        player.OnXPChanged += HandleXPChanged;
        player.OnLevelChanged += HandleLevelChanged;

        // Atualiza HUD uma vez com o estado inicial
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.OnHealthChanged -= HandleHealthChanged;
            player.OnManaChanged -= HandleManaChanged;
            player.OnXPChanged -= HandleXPChanged;
            player.OnLevelChanged -= HandleLevelChanged;
        }
    }

    // ----------------------------------------------------
    // Atualizações vindas do Player
    // ----------------------------------------------------

    private void RefreshAll()
    {
        if (nameText != null)
            nameText.text = player.DisplayName;

        HandleLevelChanged(player.Level);
        HandleHealthChanged(player.CurrentHealth, player.MaxHealth);
        HandleManaChanged(player.CurrentMana, player.MaxMana);
        HandleXPChanged(player.CurrentXP, player.XPToNextLevel);
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (hpFill == null) return;

        float pct = max > 0 ? (float)current / max : 0f;
        hpFill.fillAmount = Mathf.Clamp01(pct);
    }

    private void HandleManaChanged(int current, int max)
    {
        if (mpFill == null) return;

        float pct = max > 0 ? (float)current / max : 0f;
        mpFill.fillAmount = Mathf.Clamp01(pct);
    }

    private void HandleXPChanged(int currentXP, int xpToNextLevel)
    {
        if (xpFill == null) return;

        float pct = xpToNextLevel > 0 ? (float)currentXP / xpToNextLevel : 0f;
        xpFill.fillAmount = Mathf.Clamp01(pct);
    }

    private void HandleLevelChanged(int newLevel)
    {
        if (levelText != null)
            levelText.text = $"{newLevel}";
    }
}
