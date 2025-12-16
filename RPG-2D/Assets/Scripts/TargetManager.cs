using UnityEngine;
using System;

/// <summary>
/// Gerencia o alvo atual do player (como em Lineage / MMOs).
/// 
/// 🧙‍♂️ Filosofia Arcana:
/// - O TargetManager é o "Grimório do Alvo Atual".
/// - Ele NÃO é o Player.
/// - Ele NÃO movimenta.
/// - Ele NÃO ataca.
/// 
/// Ele só mantém e troca o target com consistência.
/// </summary>
public class TargetManager : MonoBehaviour
{
    // Singleton.
    public static TargetManager Instance { get; private set; }

    /// <summary>Alvo atual.</summary>
    public ITargetable CurrentTarget { get; private set; }

    /// <summary>Evento disparado ao trocar de alvo.</summary>
    public event Action<ITargetable> OnTargetChanged;

    private void Awake()
    {
        // Implementação simples de singleton.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Opcional.
    }

    /// <summary>Define um novo alvo.</summary>
    public void SetTarget(ITargetable target)
    {
        if (CurrentTarget == target)
            return;

        CurrentTarget = target;
        OnTargetChanged?.Invoke(CurrentTarget);
    }

    /// <summary>Limpa o alvo atual.</summary>
    public void ClearTarget()
    {
        if (CurrentTarget == null)
            return;

        CurrentTarget = null;
        OnTargetChanged?.Invoke(null);
    }

    /// <summary>
    /// 🪄 Captura o alvo que está sob o mouse (se existir e for válido).
    /// 
    /// Por que esse feitiço mora aqui?
    /// - Porque o Player já chama TargetManager.GetTargetUnderMouse(...)
    /// - Mantém a responsabilidade "target" concentrada no TargetManager
    /// - Evita duplicação e inconsistências
    /// </summary>
    public ITargetable GetTargetUnderMouse(LayerMask enemyMask)
    {
        // Se não houver câmera, não há visão, e sem visão não há alvo.
        Camera cam = Camera.main;
        if (cam == null)
            return null;

        // Convertemos o mouse para coordenadas do mundo 2D.
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        // Procuramos um collider inimigo naquele ponto.
        Collider2D hit = Physics2D.OverlapPoint(mouseWorld, enemyMask);
        if (hit == null)
            return null;

        // Tentamos extrair a essência "ITargetable" daquele collider.
        ITargetable target = hit.GetComponent<ITargetable>();

        // Se for nulo ou morto, não serve como target.
        if (target == null || !target.IsAlive)
            return null;

        return target;
    }

    /// <summary>
    /// Cicla o alvo com TAB (pega o mais perto dentro de um raio).
    /// Simples: procura todos os colliders na Layer de inimigo.
    /// </summary>
    public void CycleTargetWithTab(Vector3 center, float radius)
    {
        // Pega todos os colliders 2D num raio.
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);

        if (hits.Length == 0)
        {
            ClearTarget();
            return;
        }

        // Converte para lista de ITargetable válidos.
        var targets = new System.Collections.Generic.List<ITargetable>();

        foreach (var col in hits)
        {
            var t = col.GetComponent<ITargetable>();
            if (t != null && t.IsAlive)
                targets.Add(t);
        }

        if (targets.Count == 0)
        {
            ClearTarget();
            return;
        }

        // Se não há alvo, pega o primeiro.
        if (CurrentTarget == null)
        {
            SetTarget(targets[0]);
            return;
        }

        // Senão, pega o próximo depois do atual.
        int index = targets.IndexOf(CurrentTarget);
        index = (index + 1) % targets.Count;
        SetTarget(targets[index]);
    }
}
