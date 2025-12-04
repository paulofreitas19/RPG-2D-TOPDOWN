using UnityEngine;

/// <summary>
/// Interface simples que representa algo que pode ser "targetado"
/// pelo player (inimigos, NPCs, etc).
/// </summary>
public interface ITargetable
{
    /// <summary>
    /// Transform principal usado para posição / distância.
    /// </summary>
    Transform TargetTransform { get; }

    /// <summary>
    /// Nome a ser exibido na HUD.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Vida atual.
    /// </summary>
    int CurrentHealth { get; }

    /// <summary>
    /// Vida máxima.
    /// </summary>
    int MaxHealth { get; }

    /// <summary>
    /// Verdadeiro se ainda estiver vivo.
    /// </summary>
    bool IsAlive { get; }

    /// <summary>
    /// Aplica dano ao alvo.
    /// </summary>
    /// <param name="amount">Quantidade de dano bruto.</param>
    void TakeDamage(int amount);
}
