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
        amount = Mathf.Max(0, amount);                // Garante dano >= 0.
        currentHealth -= amount;                      // Subtrai vida.
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Atualiza HUD, se este inimigo for o alvo atual.
        if (TargetManager.Instance != null &&
            TargetManager.Instance.CurrentTarget == this &&
            EnemyHUDController.Instance != null)
        {
            EnemyHUDController.Instance.RefreshForTarget(this);
        }

        // Morte.
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // Aqui você pode tocar animação, soltar loot, etc.
        // Por enquanto, só desativamos o collider.
        GetComponent<Collider2D>().enabled = false;
    }
}
