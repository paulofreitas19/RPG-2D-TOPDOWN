using UnityEngine;

/// <summary>
/// Responsável por gerenciar o input do jogador e iniciar a conjuração das habilidades.
/// Este script fica no GameObject do Player.
/// </summary>
public class PlayerSkills : MonoBehaviour
{
    // Lista de habilidades disponíveis para o player (podem ser atribuídas via Inspector)
    [SerializeField] private SkillBase[] skills;

    // Referência ao SkillCaster, que realiza o processo de conjuração
    private SkillCaster skillCaster;

    /// <summary>
    /// Ao iniciar o jogo, pegamos a referência para o componente SkillCaster no mesmo GameObject.
    /// </summary>
    private void Awake()
    {
        skillCaster = GetComponent<SkillCaster>();
    }

    /// <summary>
    /// Verifica os inputs de teclado a cada frame para disparar as habilidades.
    /// </summary>
    private void Update()
    {
        // Se a tecla F2 for pressionada, e houver uma skill na posição [0], tenta conjurar
        if (Input.GetKeyDown(KeyCode.F2) && skills.Length > 0 && skills[0] != null)
        {
            // Obtém o alvo mais próximo com tag "Enemy"
            Transform target = GetClosestEnemy();

            // Chama o método de conjuração passando a skill e o alvo
            skillCaster.CastSkill(skills[0], target);
        }

        // Você pode adicionar outros atalhos para skills[1], skills[2], etc.
    }

    /// <summary>
    /// Localiza e retorna o Transform do inimigo mais próximo usando a tag "Enemy".
    /// Essa lógica pode ser expandida com distância mínima, múltiplos inimigos, etc.
    /// </summary>
    /// <returns>Transform do inimigo mais próximo ou null se não encontrar</returns>
    private Transform GetClosestEnemy()
    {
        GameObject enemy = GameObject.FindWithTag("Enemy");
        return enemy != null ? enemy.transform : null;
    }
}
