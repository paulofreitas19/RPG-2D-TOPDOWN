using System.Collections;
using UnityEngine;

public class SkillCaster : MonoBehaviour
{
    private Animator animator;
    private PlayerAnim playerAnim;

    // 🧙‍♂️ O Guardião do Flip vive no filho "Sprite", não no Player raiz.
    // Então precisamos buscá-lo nos filhos (senão ele vira null e nada vira).
    private SpriteFlipper spriteFlipper;

    // 🔒 Referência ao Player para travar/destravar input/movimento durante cast
    private Player player;

    [SerializeField] private Transform magicAttackPoint;
    public Transform MagicAttackPoint => magicAttackPoint;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        playerAnim = GetComponentInChildren<PlayerAnim>();

        // ✨ Runa do acerto: o SpriteFlipper está no filho "Sprite"
        spriteFlipper = GetComponentInChildren<SpriteFlipper>(true);

        // 🔒 Player está no objeto raiz (mesmo GO do SkillCaster)
        player = GetComponent<Player>();
    }

    public void CastSkill(SkillBase skill, Transform target)
    {
        // 🔒 Anti-spam: se o Player já está castando, não inicia outro cast.
        if (player != null && player.IsCasting)
            return;

        StartCoroutine(Cast(skill, target));
    }

    private IEnumerator Cast(SkillBase skill, Transform target)
    {
        // 🔒 Ativa hard lock no Player
        if (player != null)
            player.BeginExternalCastLock();

        // 🧭 Alinha visual antes do cast
        FaceTargetSafely(target);

        // 🔮 Conjuração
        playerAnim.SetCasting(true);
        animator.SetTrigger("idleCast");

        FaceTargetSafely(target);

        // Prepara a skill (fica na mão)
        skill.Prepare(gameObject, target);

        yield return new WaitForSeconds(skill.castTime);

        // 🔥 Disparo
        FaceTargetSafely(target);
        skill.Activate(gameObject, target);

        playerAnim.PlayBasicAttack();
        playerAnim.SetCasting(false);

        // 🔓 Libera hard lock
        if (player != null)
            player.EndExternalCastLock();
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
