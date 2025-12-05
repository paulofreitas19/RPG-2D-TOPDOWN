using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla a HUD do inimigo selecionado (nome, barra de vida, botão X, orbs etc).
/// Fica no Canvas.
/// </summary>
public class EnemyHUDController : MonoBehaviour
{
    // Singleton simples para facilitar acesso.
    public static EnemyHUDController Instance { get; private set; }

    [Header("Referências de UI")]
    [SerializeField] private GameObject panelRoot; // Painel inteiro (dragão + tudo).
    [SerializeField] private Text nameText;        // Texto com nome do inimigo.
    [SerializeField] private Image healthFill;     // Imagem de fill da barra (type = Filled).
    [SerializeField] private Image fullBarSprite;  // Sprite vermelho cheio (se você preferir usar mask).
    [SerializeField] private Button closeButton;   // Botão X para limpar alvo.

    [Header("Indicadores de Combate")]
    [SerializeField] private Image[] combatOrbs;   // Esferas que ficam vermelhas em combate.
    [SerializeField] private Color orbIdleColor = Color.gray;
    [SerializeField] private Color orbCombatColor = Color.red;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.25f; // Tempo do fade in/out em segundos.

    private ITargetable currentTarget;

    // Componentes auxiliares
    private CanvasGroup canvasGroup;      // Controla alpha/interação.
    private Coroutine fadeRoutine;        // Guarda a coroutine atual de fade.

    private void Awake()
    {
        // Singleton.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Garante CanvasGroup no painel.
        if (panelRoot != null)
        {
            canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        // Começa escondido.
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // Liga o botão X.
        if (closeButton != null)
            closeButton.onClick.AddListener(OnClickClose);

        // Escuta troca de alvo no TargetManager (se já existir na cena).
        if (TargetManager.Instance != null)
        {
            TargetManager.Instance.OnTargetChanged += HandleTargetChanged;
        }
    }

    private void OnDestroy()
    {
        if (TargetManager.Instance != null)
        {
            TargetManager.Instance.OnTargetChanged -= HandleTargetChanged;
        }
    }

    //==============================================================
    //  TARGET / SHOW / HIDE
    //==============================================================

    /// <summary>
    /// Quando o alvo muda no TargetManager.
    /// </summary>
    private void HandleTargetChanged(ITargetable newTarget)
    {
        currentTarget = newTarget;

        if (currentTarget == null)
        {
            // Sem alvo → fade-out e esconder.
            FadeOutHUD();
            return;
        }

        // Tem alvo → atualiza info e dá fade-in.
        RefreshForTarget(currentTarget);
        SetOrbsInCombat(false);
        FadeInHUD();
    }

    /// <summary>
    /// Faz o painel aparecer com fade-in.
    /// </summary>
    private void FadeInHUD()
    {
        if (panelRoot == null || canvasGroup == null)
            return;

        panelRoot.SetActive(true); // Ativa antes do fade.

        // Cancela fade anterior, se houver.
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeCanvasRoutine(0f, 1f, false));
    }

    /// <summary>
    /// Faz o painel desaparecer com fade-out.
    /// </summary>
    private void FadeOutHUD()
    {
        if (panelRoot == null || canvasGroup == null)
            return;

        // Cancela fade anterior, se houver.
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeCanvasRoutine(1f, 0f, true));
    }

    /// <summary>
    /// Coroutine genérica de fade do CanvasGroup.
    /// </summary>
    private System.Collections.IEnumerator FadeCanvasRoutine(float from, float to, bool disableOnEnd)
    {
        float t = 0f;

        // Garantir valores iniciais.
        canvasGroup.alpha = from;

        // Enquanto anima, bloqueia interação se estiver quase invisível.
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true; // Ainda bloqueia clique no fundo enquanto some.

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime; // Usa tempo "real", independente de Time.timeScale.
            float lerp = Mathf.Clamp01(t / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(from, to, lerp);
            yield return null;
        }

        canvasGroup.alpha = to;

        // Se terminou em 0 → desativa painel e bloqueio de clique.
        if (Mathf.Approximately(to, 0f))
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            if (disableOnEnd)
                panelRoot.SetActive(false);
        }
        else
        {
            // Terminei visível → libera interação da HUD normalmente.
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        fadeRoutine = null;
    }

    //==============================================================
    //  ATUALIZAÇÃO VISUAL
    //==============================================================

    /// <summary>
    /// Atualiza nome e barra de vida para o alvo especificado.
    /// </summary>
    public void RefreshForTarget(ITargetable target)
    {
        if (target == null) return;

        if (nameText != null)
            nameText.text = target.DisplayName;

        if (healthFill != null)
        {
            float pct = (float)target.CurrentHealth / target.MaxHealth;
            healthFill.fillAmount = pct;
        }

        // fullBarSprite é opcional. Se quiser usar mask/escala, dá pra implementar aqui.
    }

    /// <summary>
    /// Muda a cor das orbs (fora / em combate).
    /// </summary>
    public void SetOrbsInCombat(bool inCombat)
    {
        if (combatOrbs == null) return;

        Color c = inCombat ? orbCombatColor : orbIdleColor;

        foreach (var orb in combatOrbs)
        {
            if (orb != null)
                orb.color = c;
        }
    }

    //==============================================================
    //  BOTÃO X
    //==============================================================

    /// <summary>
    /// Chamado pelo botão X – limpa alvo no TargetManager.
    /// O próprio HandleTargetChanged vai cuidar do fade-out.
    /// </summary>
    public void OnClickClose()
    {
        if (TargetManager.Instance != null)
            TargetManager.Instance.ClearTarget();
    }
}
