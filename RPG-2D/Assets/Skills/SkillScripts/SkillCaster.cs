using System.Collections;
using UnityEngine;

public class SkillCaster : MonoBehaviour
{
    private SkillBase currentSkill; // Habilidade atual sendo conjurada
    private bool isCasting = false; // Controle de conjuração
    private Animator animator; // Referência ao Animator

    [Header("Pontos de Spawn da Magia")]
    public Transform magicPointSide;
    public Transform magicPointTop;

    [Header("Alvo da magia")]
    public Transform target;

    void Start()
    {
        animator = GetComponent<Animator>(); // Captura o Animator do jogador
    }

    // Tenta conjurar uma habilidade
    public void TryCastSkill(SkillBase skill)
    {
        if (!isCasting)
        {
            currentSkill = skill;
            StartCoroutine(CastCoroutine());
        }
    }

    // Corroutine responsável pela preparação e ativação
    private IEnumerator CastCoroutine()
    {
        isCasting = true;
        currentSkill.Prepare(this);
        yield return new WaitForSeconds(currentSkill.castTime); // Tempo de conjuração
        currentSkill.Activate(this);
        isCasting = false;
    }
}