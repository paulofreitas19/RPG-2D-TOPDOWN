using UnityEngine;
using System.Collections;

/// <summary>
/// Inimigo simples que pode ser alvo do player e aparece na EnemyHUD.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour, ITargetable
{
    [Header("Configuração")]
    [SerializeField] private string displayName = "Slime"; // Nome na HUD.
    [SerializeField] private int maxHealth = 50;           // Vida máxima.

    [Header("Recompensa")]
    [SerializeField] private int xpReward = 10;            // XP dado ao player ao morrer.

    [SerializeField] private int currentHealth;                             // Vida atual.

    // Implementação da interface ITargetable.
    public Transform TargetTransform => transform;
    public string DisplayName => displayName;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Aplica dano neste inimigo.
    /// </summary>
    public void TakeDamage(int amount)
    {
        amount = Mathf.Max(0, amount);
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Atualiza HUD, se este inimigo for o alvo atual.
        if (TargetManager.Instance != null &&
            TargetManager.Instance.CurrentTarget == this &&
            EnemyHUDController.Instance != null)
        {
            EnemyHUDController.Instance.RefreshForTarget(this);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (Player.Instance != null)
            Player.Instance.GainXP(xpReward);

        // Para o inimigo não interagir mais
        GetComponent<Collider2D>().enabled = false;

        // Para garantir que a IA pare
        if (TryGetComponent<EnemyBrain>(out var brain))
            brain.enabled = false;

        // CONFIAMOS que EnemyAnimator vai tocar a animação,
        // então só destruímos depois.
        StartCoroutine(DestroyAfterDeathAnimation());
    }

    private IEnumerator DestroyAfterDeathAnimation()
    {
        // Tempo da animação "Die" → ajuste se necessário
        yield return new WaitForSeconds(1.0f);

        Destroy(gameObject);
    }

}
