using UnityEngine;
using System;

/// <summary>
/// Classe base para qualquer inimigo do jogo.
/// Contém vida, dano e eventos comuns que todos compartilham.
/// </summary>
public abstract class EnemyBase : MonoBehaviour, ITargetable
{
    //==========================================================
    //  CAMPOS DE VIDA
    //==========================================================

    [Header("Enemy Stats")]
    [SerializeField] private string displayName = "Enemy";
    [SerializeField] private int maxHealth = 100;

    /// <summary>
    /// Vida atual interna.
    /// </summary>
    protected int currentHealth;

    /// <summary>
    /// Indica se o inimigo já morreu.
    /// </summary>
    protected bool isDead = false;

    //==========================================================
    //  EVENTOS
    //==========================================================

    /// <summary>
    /// Disparado sempre que o inimigo leva dano (vida mudou).
    /// </summary>
    public event Action<EnemyBase> OnHealthChanged;

    /// <summary>
    /// Disparado quando o inimigo morre.
    /// </summary>
    public event Action<EnemyBase> OnDeath;

    //==========================================================
    //  PROPRIEDADES ITargetable
    //==========================================================

    /// <inheritdoc/>
    public Transform TargetTransform => transform;

    /// <inheritdoc/>
    public string DisplayName => displayName;

    /// <inheritdoc/>
    public int CurrentHealth => currentHealth;

    /// <inheritdoc/>
    public int MaxHealth => maxHealth;

    /// <inheritdoc/>
    public bool IsAlive => !isDead;

    //==========================================================
    //  CICLO DE VIDA
    //==========================================================

    /// <summary>
    /// Inicializa vida ao iniciar.
    /// </summary>
    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }

    //==========================================================
    //  DANO
    //==========================================================

    /// <summary>
    /// Aplica dano ao inimigo.
    /// </summary>
    /// <param name="amount">Valor de dano recebido.</param>
    public virtual void TakeDamage(int amount)
    {
        if (isDead) return;

        // Garante que não entra valor negativo.
        amount = Mathf.Max(0, amount);

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Notifica ouvintes (HUD, etc).
        OnHealthChanged?.Invoke(this);

        // Se chegou a zero, morre.
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Lógica padrão de morte.  
    /// Você pode sobreescrever em heranças para animar, soltar loot, etc.
    /// </summary>
    protected virtual void Die()
    {
        if (isDead) return;

        isDead = true;

        // Dispara evento para HUD/TargetManager removerem esse alvo.
        OnDeath?.Invoke(this);

        // Por padrão, apenas destrói o GameObject após um delay.
        Destroy(gameObject, 1.5f);
    }
}
