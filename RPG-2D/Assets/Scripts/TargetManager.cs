using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Gerencia o alvo atual do player, troca de target
/// (clique ou TAB) e avisa HUD / outros sistemas.
/// </summary>
public class TargetManager : MonoBehaviour
{
    //==========================================================
    //  SINGLETON BÁSICO
    //==========================================================

    /// <summary>
    /// Instância única global (estilo serviço).
    /// </summary>
    public static TargetManager Instance { get; private set; }

    private void Awake()
    {
        // Garante singleton simples.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    //==========================================================
    //  ALVO ATUAL
    //==========================================================

    /// <summary>
    /// Alvo atual (inimigo ou NPC).
    /// </summary>
    public ITargetable CurrentTarget { get; private set; }

    /// <summary>
    /// Evento disparado quando o alvo é alterado.
    /// </summary>
    public event Action<ITargetable> OnTargetChanged;

    //==========================================================
    //  API PÚBLICA
    //==========================================================

    /// <summary>
    /// Seleciona um novo alvo.
    /// </summary>
    public void SetTarget(ITargetable newTarget)
    {
        if (newTarget == CurrentTarget) return;

        // Desinscreve de eventos do alvo anterior se for EnemyBase.
        UnsubscribeFromOldTargetEvents();

        CurrentTarget = newTarget;

        // Se for inimigo, assina eventos para saber se morreu.
        SubscribeToNewTargetEvents();

        OnTargetChanged?.Invoke(CurrentTarget);
    }

    /// <summary>
    /// Remove o target atual (por exemplo, botão X da HUD).
    /// </summary>
    public void ClearTarget()
    {
        if (CurrentTarget == null) return;

        UnsubscribeFromOldTargetEvents();
        CurrentTarget = null;
        OnTargetChanged?.Invoke(null);
    }

    /// <summary>
    /// Usa TAB para alternar target entre inimigos mais próximos.
    /// </summary>
    /// <param name="playerPosition">Posição atual do player.</param>
    /// <param name="searchRadius">Raio de busca para encontrar inimigos.</param>
    public void CycleTargetWithTab(Vector3 playerPosition, float searchRadius)
    {
        // Encontra todos os inimigos no raio.
        Collider2D[] hits = Physics2D.OverlapCircleAll(playerPosition, searchRadius);

        List<ITargetable> candidates = new List<ITargetable>();

        foreach (var hit in hits)
        {
            var targetable = hit.GetComponent<ITargetable>();
            if (targetable != null && targetable.IsAlive)
            {
                candidates.Add(targetable);
            }
        }

        if (candidates.Count == 0)
        {
            ClearTarget();
            return;
        }

        // Ordena por distância.
        candidates.Sort((a, b) =>
        {
            float da = Vector3.SqrMagnitude(a.TargetTransform.position - playerPosition);
            float db = Vector3.SqrMagnitude(b.TargetTransform.position - playerPosition);
            return da.CompareTo(db);
        });

        // Se não há alvo ainda, pega o mais próximo.
        if (CurrentTarget == null)
        {
            SetTarget(candidates[0]);
            return;
        }

        // Procura próximo da lista.
        int currentIndex = candidates.FindIndex(t => t == CurrentTarget);

        // Se não achar, pega o primeiro novamente.
        if (currentIndex == -1)
        {
            SetTarget(candidates[0]);
        }
        else
        {
            int nextIndex = (currentIndex + 1) % candidates.Count;
            SetTarget(candidates[nextIndex]);
        }
    }

    //==========================================================
    //  EVENTOS DE MORTE DO INIMIGO
    //==========================================================

    /// <summary>
    /// Desinscreve eventos do alvo anterior se for EnemyBase.
    /// </summary>
    private void UnsubscribeFromOldTargetEvents()
    {
        if (CurrentTarget is EnemyBase enemy)
        {
            enemy.OnDeath -= HandleTargetDeath;
        }
    }

    /// <summary>
    /// Assina eventos do novo alvo se for EnemyBase.
    /// </summary>
    private void SubscribeToNewTargetEvents()
    {
        if (CurrentTarget is EnemyBase enemy)
        {
            enemy.OnDeath += HandleTargetDeath;
        }
    }

    /// <summary>
    /// Chamado quando o alvo atual morre.
    /// </summary>
    private void HandleTargetDeath(EnemyBase deadEnemy)
    {
        // Limpa target ao morrer.
        ClearTarget();
    }
}
