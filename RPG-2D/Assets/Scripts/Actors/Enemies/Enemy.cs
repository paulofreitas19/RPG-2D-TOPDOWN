using UnityEngine;
using System.Collections;

/// <summary>
/// Classe base de qualquer inimigo.
/// Controla vida, dano, morte e integração com a HUD.
/// Também implementa ITargetable para que o Player possa atacar corretamente.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour, ITargetable
{
    // -------------------------------------------------------------
    //  CONFIGURAÇÃO BÁSICA
    // -------------------------------------------------------------

    [Header("Configuração")]
    [Tooltip("Nome exibido na HUD do inimigo.")]
    [SerializeField] private string displayName = "Slime";     // Nome na HUD.

    [Tooltip("Vida máxima do inimigo.")]
    [SerializeField] private int maxHealth = 50;               // Vida máxima.

    [Header("Recompensa")]
    [Tooltip("XP dado ao player quando este inimigo morre.")]
    [SerializeField] private int xpReward = 10;                // XP dado ao player ao morrer.

    [Tooltip("Vida atual (apenas para debug no Inspector).")]
    [SerializeField] private int currentHealth;                // Vida atual.

    // -------------------------------------------------------------
    //  CONFIGURAÇÃO VISUAL DA MORTE (ANIMAÇÃO + FADE)
    // -------------------------------------------------------------

    [Header("Morte Visual")]
    [Tooltip("Tempo da animação de morte (clip 'Die'). Ajuste para casar com o Animation Clip.")]
    [SerializeField] private float deathAnimDuration = 0.4f;   // Quanto tempo dura o clip Die.

    [Tooltip("Tempo do efeito de fade para o inimigo sumir após morrer.")]
    [SerializeField] private float fadeOutDuration = 0.6f;     // Quanto tempo leva para desaparecer.

    // -------------------------------------------------------------
    //  IMPLEMENTAÇÃO DA INTERFACE ITargetable
    // -------------------------------------------------------------

    /// <summary>
    /// Transform usado pelo sistema de alvo (mira, HUD etc.).
    /// </summary>
    public Transform TargetTransform => transform;

    /// <summary>
    /// Nome exibido na HUD.
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// Vida atual (lida pela HUD).
    /// </summary>
    public int CurrentHealth => currentHealth;

    /// <summary>
    /// Vida máxima (lida pela HUD).
    /// </summary>
    public int MaxHealth => maxHealth;

    /// <summary>
    /// Indica se o inimigo ainda está vivo.
    /// Usado por EnemyBrain, EnemyAnimator, etc.
    /// </summary>
    public bool IsAlive => currentHealth > 0;

    /// <summary>
    /// Evento disparado sempre que a vida muda.
    /// A HUD usa isso para atualizar a barra sem precisar ficar dando Update().
    /// </summary>
    public event System.Action OnHealthChanged;

    // -------------------------------------------------------------
    //  CICLO DE VIDA
    // -------------------------------------------------------------

    private void Awake()
    {
        // Começa sempre com a vida cheia.
        currentHealth = maxHealth;
    }

    // -------------------------------------------------------------
    //  DANO / MORTE
    // -------------------------------------------------------------

    /// <summary>
    /// Aplica dano neste inimigo.
    /// </summary>
    public void TakeDamage(int amount)
    {
        // Garante que não entra dano negativo.
        amount = Mathf.Max(0, amount);

        // Subtrai vida.
        currentHealth -= amount;

        // Garante que a vida fica entre 0 e maxHealth.
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Avisa a HUD (via evento) que a vida mudou.
        OnHealthChanged?.Invoke();

        // Se este inimigo é o alvo atual, força a HUD a recalcular
        // imediatamente, usando os valores novos.
        if (TargetManager.Instance != null &&
            TargetManager.Instance.CurrentTarget == this &&
            EnemyHUDController.Instance != null)
        {
            EnemyHUDController.Instance.RefreshForTarget(this);
        }

        // Checa morte.
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Lógica de morte do inimigo.
    /// Para: concede XP, para IA, toca animação e inicia o fade para desaparecer.
    /// </summary>
    private void Die()
    {
        // ---------------------------------------------------------
        // 1) Se este inimigo é o alvo atual, limpa o alvo
        //    → TargetManager dispara OnTargetChanged(null)
        //    → EnemyHUDController recebe e faz FadeOut da HUD.
        // ---------------------------------------------------------
        if (TargetManager.Instance != null &&
            TargetManager.Instance.CurrentTarget == this)
        {
            TargetManager.Instance.ClearTarget();
        }

        // ---------------------------------------------------------
        // 2) Dá XP para o player (se existir instância).
        // ---------------------------------------------------------
        if (Player.Instance != null)
            Player.Instance.GainXP(xpReward);

        // ---------------------------------------------------------
        // 3) Garante que o inimigo não interaja mais com o mundo.
        // ---------------------------------------------------------

        // Para de colidir com tudo.
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        // Para a IA.
        if (TryGetComponent<EnemyBrain>(out var brain))
            brain.enabled = false;

        // ---------------------------------------------------------
        // 4) Deixa o EnemyAnimator tocar a animação de morte.
        //    Depois inicia o efeito de fade e destrói o GameObject.
        // ---------------------------------------------------------
        StartCoroutine(DestroyAfterDeathAnimation());
    }

    /// <summary>
    /// Espera o tempo da animação de morte e depois faz um fade suave
    /// dos SpriteRenderers antes de destruir o inimigo.
    /// </summary>
    private IEnumerator DestroyAfterDeathAnimation()
    {
        // 1) Espera o tempo da animação de morte (clip "Die").
        //    Assim o inimigo toca a animação completa antes de sumir.
        if (deathAnimDuration > 0f)
            yield return new WaitForSeconds(deathAnimDuration);

        // 2) Pega todos os SpriteRenderers do inimigo (incluindo filhos).
        //    Isso cobre:
        //      - Sprite do corpo
        //      - Partes extras (armas, sombreado, etc.)
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        // 3) Guarda as cores originais para não acumular erro de floating point
        //    caso o mesmo prefab seja reutilizado / poolado no futuro.
        Color[] originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                originalColors[i] = renderers[i].color;
        }

        // 4) Se o tempo de fade for zero ou negativo, destrói direto.
        if (fadeOutDuration <= 0f)
        {
            Destroy(gameObject);
            yield break;
        }

        // 5) Faz o fade: alpha vai de 1 → 0 ao longo de fadeOutDuration.
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            float alpha = 1f - t; // 1 → 0

            // Aplica o alpha em todos os SpriteRenderers.
            for (int i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (sr == null) continue;

                Color c = originalColors[i];
                c.a = alpha;
                sr.color = c;
            }

            // Espera o próximo frame.
            yield return null;
        }

        // 6) Garante que o inimigo está totalmente invisível
        //    e então destrói o GameObject.
        for (int i = 0; i < renderers.Length; i++)
        {
            var sr = renderers[i];
            if (sr == null) continue;

            Color c = originalColors[i];
            c.a = 0f;
            sr.color = c;
        }

        Destroy(gameObject);
    }
}
