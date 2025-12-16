using System.Collections;
using UnityEngine;

public class SkillCaster : MonoBehaviour
{
    private Animator animator;
    private PlayerAnim playerAnim;

    // 🧙‍♂️ O Guardião do Flip vive no filho "Sprite", não no Player raiz.
    // Então precisamos buscá-lo nos filhos (senão ele vira null e nada vira).
    private SpriteFlipper spriteFlipper;

    [SerializeField] private Transform magicAttackPoint;
    public Transform MagicAttackPoint => magicAttackPoint;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        playerAnim = GetComponentInChildren<PlayerAnim>();

        // ✨ Runa do acerto: o SpriteFlipper está no filho "Sprite"
        spriteFlipper = GetComponentInChildren<SpriteFlipper>(true);
    }

    public void CastSkill(SkillBase skill, Transform target)
    {
        StartCoroutine(Cast(skill, target));
    }

    private IEnumerator Cast(SkillBase skill, Transform target)
    {
        // 🧭 ─────────────────────────────────────────────
        // FASE 0 — ALINHAMENTO ARCANO DO CAST
        // Antes de conjurar qualquer magia:
        // o mago DEVE olhar para o alvo.
        //
        // Aqui evitamos chamar FaceTarget(...) porque sua assinatura pode variar.
        // Usamos FaceDirection(dirX), que é a runa mais robusta:
        // - Se alvo está à direita => dirX > 0 => vira para direita
        // - Se alvo está à esquerda => dirX < 0 => vira para esquerda
        // ─────────────────────────────────────────────
        FaceTargetSafely(target);

        // 🟣 ─────────────────────────────────────────────
        // FASE 1 — CONJURAÇÃO (CAST)
        // O mago canaliza energia enquanto a magia
        // permanece presa à sua mão.
        // ─────────────────────────────────────────────
        playerAnim.SetCasting(true);

        // Começa animação do player
        animator.SetTrigger("idleCast");

        // ✅ Garantia extra:
        // imediatamente antes de preparar (spawn do preview), garante que já está virado.
        FaceTargetSafely(target);

        // 🔮 Prepara a skill (instancia e gruda na mão)
        skill.Prepare(gameObject, target);

        yield return new WaitForSeconds(skill.castTime);

        // 🔥 ─────────────────────────────────────────────
        // FASE 2 — DISPARO (SHOOT)
        // A magia é liberada ao mundo físico.
        // ─────────────────────────────────────────────
        // ✅ Garantia extra:
        // imediatamente antes de disparar, garante que ainda está virado pro alvo.
        FaceTargetSafely(target);

        skill.Activate(gameObject, target);

        // ⚔️ Animação de ataque básico
        playerAnim.PlayBasicAttack();

        // 🟢 Finaliza o estado de cast
        playerAnim.SetCasting(false);
    }

    /// <summary>
    /// 🧿 Feitiço utilitário ultra-resistente:
    /// vira o player para o alvo sem depender de assinatura específica do SpriteFlipper.
    /// </summary>
    private void FaceTargetSafely(Transform target)
    {
        if (spriteFlipper == null || target == null) return;

        // Direção horizontal do player até o alvo (a runa do "pra onde devo olhar?")
        float dirX = target.position.x - transform.position.x;

        // Se for praticamente zero, não vira (evita tremedeira/flip infinito)
        if (Mathf.Abs(dirX) < 0.001f) return;

        spriteFlipper.FaceDirection(dirX);
    }
}
