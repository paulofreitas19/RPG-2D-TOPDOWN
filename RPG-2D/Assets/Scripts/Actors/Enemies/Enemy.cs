using UnityEngine;

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

    private int currentHealth;                             // Vida atual.

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
        // Dá XP ao player.
        if (Player.Instance != null)
        {
            Player.Instance.GainXP(xpReward);
        }

        // Aqui você pode tocar animação, soltar loot, etc.
        GetComponent<Collider2D>().enabled = false;
    }
}
