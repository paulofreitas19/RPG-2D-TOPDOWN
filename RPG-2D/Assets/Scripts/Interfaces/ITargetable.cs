using UnityEngine;

/// <summary>
/// Qualquer coisa que possa ser alvo do player (inimigos, NPCs etc).
/// </summary>
public interface ITargetable
{
    /// <summary>Transform usado como referência de posição (centro do alvo).</summary>
    Transform TargetTransform { get; }

    /// <summary>Nome para exibir na HUD.</summary>
    string DisplayName { get; }

    /// <summary>Vida atual e máxima, para HUD.</summary>
    int CurrentHealth { get; }
    int MaxHealth { get; }

    /// <summary>Se o alvo ainda está vivo.</summary>
    bool IsAlive { get; }

    /// <summary>Receber dano.</summary>
    void TakeDamage(int amount);
}
