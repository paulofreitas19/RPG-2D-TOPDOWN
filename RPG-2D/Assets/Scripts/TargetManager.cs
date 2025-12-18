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
/// Ele apenas decide QUAL entidade é o alvo.
/// </summary>
public class TargetManager : MonoBehaviour
{
    public static TargetManager Instance { get; private set; }

    /// <summary>Alvo atual (fonte de verdade).</summary>
    public ITargetable CurrentTarget { get; private set; }

    /// <summary>Evento disparado ao trocar de alvo.</summary>
    public event Action<ITargetable> OnTargetChanged;

    // ==========================================================
    // RUNA DA INTENÇÃO (Manual vs Auto)
    // ==========================================================
    private bool hasManualTarget = false;
    public bool HasManualTarget => hasManualTarget;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ==========================================================
    // APLICAÇÃO CENTRAL DO TARGET
    // ==========================================================
    private void ApplyTarget(ITargetable target)
    {
        if (CurrentTarget == target)
            return;

        CurrentTarget = target;
        OnTargetChanged?.Invoke(CurrentTarget);
    }

    public void SetTarget(ITargetable target)
    {
        if (hasManualTarget && CurrentTarget != null && CurrentTarget.IsAlive)
            return;

        if (target != null && !target.IsAlive)
            target = null;

        hasManualTarget = false;
        ApplyTarget(target);
    }

    public void SetManualTarget(ITargetable target)
    {
        if (target == null || !target.IsAlive)
        {
            ClearTarget();
            return;
        }

        hasManualTarget = true;
        ApplyTarget(target);
    }

    public void ClearTarget()
    {
        hasManualTarget = false;
        ApplyTarget(null);
    }

    // ==========================================================
    // 🔥 CORREÇÃO DEFINITIVA DO BUG
    // ----------------------------------------------------------
    // - Nunca confiamos diretamente no collider clicado
    // - Sempre subimos até o ROOT que implementa ITargetable
    // - Isso impede atacar outro inimigo por erro de collider
    // ==========================================================
    public ITargetable GetTargetUnderMouse(LayerMask enemyMask)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return null;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        Collider2D hit = Physics2D.OverlapPoint(mouseWorld, enemyMask);

        if (hit == null)
            return null;

        // 🔑 REGRA DE OURO:
        // Nunca confiar no collider direto
        ITargetable target = ResolveTargetFromCollider(hit);

        if (target == null || !target.IsAlive)
            return null;

        return target;
    }

    // ==========================================================
    // 🔑 RESOLUÇÃO SEGURA DE TARGET
    // ----------------------------------------------------------
    // Sobe a hierarquia até encontrar o verdadeiro dono do alvo
    // ==========================================================
    private ITargetable ResolveTargetFromCollider(Collider2D col)
    {
        // 1️⃣ Primeiro tenta no próprio objeto
        ITargetable t = col.GetComponent<ITargetable>();
        if (t != null)
            return t;

        // 2️⃣ Depois tenta nos pais (ROOT inimigo)
        t = col.GetComponentInParent<ITargetable>();
        if (t != null)
            return t;

        // 3️⃣ Nunca buscar em filhos (evita pegar outro inimigo próximo)
        return null;
    }

    // ==========================================================
    // TAB TARGET
    // ==========================================================
    public void CycleTargetWithTab(Vector3 center, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);

        if (hits.Length == 0)
        {
            ClearTarget();
            return;
        }

        var targets = new System.Collections.Generic.List<ITargetable>();

        foreach (var col in hits)
        {
            ITargetable t = ResolveTargetFromCollider(col);
            if (t != null && t.IsAlive && !targets.Contains(t))
                targets.Add(t);
        }

        if (targets.Count == 0)
        {
            ClearTarget();
            return;
        }

        if (CurrentTarget == null)
        {
            SetManualTarget(targets[0]);
            return;
        }

        int index = targets.IndexOf(CurrentTarget);
        index = (index + 1) % targets.Count;

        SetManualTarget(targets[index]);
    }
}
