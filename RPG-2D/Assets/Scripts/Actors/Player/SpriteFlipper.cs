using UnityEngine;

/// <summary>
/// 🧙‍♂️ SPRITE FLIPPER
/// ------------------------------------------------------------
/// Este script é o responsável por UMA COISA APENAS:
/// 👉 Controlar o lado visual que o personagem está olhando
/// 👉 Garantir que pontos de ataque (ex: MagicPointAttack)
///     acompanhem corretamente esse flip, SEM mover o Transform
///
/// ❌ NÃO move o Player
/// ❌ NÃO escala o GameObject
/// ❌ NÃO interfere em física
///
/// ✔️ Apenas vira o SpriteRenderer
/// ✔️ Apenas ajusta offsets locais de sockets (spawnpoints)
///
/// Isso evita:
/// - Teleportes estranhos
/// - Bugs de padding de sprite
/// - Spawn de magia do lado errado
/// - Desalinhamento ao virar o personagem
/// </summary>
public class SpriteFlipper : MonoBehaviour
{
    // ---------------------------------------------------------
    // REFERÊNCIAS
    // ---------------------------------------------------------

    [Header("Referências visuais")]

    // SpriteRenderer que será flipado.
    // IMPORTANTE: flipamos o Sprite, NÃO o Transform.
    [SerializeField] private SpriteRenderer spriteRenderer;

    // Ponto de onde magias / ataques são instanciados.
    // Ex: a mão do mago, ponta da arma, boca do dragão, etc.
    [SerializeField] private Transform magicPointAttack;

    // ---------------------------------------------------------
    // AJUSTES DE OFFSET (COMPENSAÇÃO DE PADDING)
    // ---------------------------------------------------------

    [Header("Offsets de spawn da magia (compensação de padding)")]

    // Ajuste fino quando o personagem estiver olhando PARA A DIREITA
    // Use valores pequenos (ex: 0.1, 0.2, -0.1)
    [SerializeField] private float magicPointOffsetRight = 0f;

    // Ajuste fino quando o personagem estiver olhando PARA A ESQUERDA
    [SerializeField] private float magicPointOffsetLeft = 0f;

    // ---------------------------------------------------------
    // ESTADO INTERNO
    // ---------------------------------------------------------

    // Posição LOCAL original do MagicPointAttack.
    // Ela é nossa "verdade absoluta".
    // Nunca movemos o ponto relativo ao mundo, apenas recalculamos
    // a posição local espelhada a partir daqui.
    private Vector3 originalMagicPointLocalPos;

    // ---------------------------------------------------------
    // CICLO DE VIDA
    // ---------------------------------------------------------

    private void Awake()
    {
        // Segurança arcana:
        // Se alguém esquecer de arrastar o SpriteRenderer no Inspector,
        // tentamos encontrar automaticamente nos filhos.
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Guardamos a posição LOCAL original do ponto de ataque.
        // Isso é CRÍTICO para evitar drift acumulado.
        if (magicPointAttack != null)
            originalMagicPointLocalPos = magicPointAttack.localPosition;
    }

    // ---------------------------------------------------------
    // API PÚBLICA — O FEITIÇO PRINCIPAL 🪄
    // ---------------------------------------------------------

    /// <summary>
    /// Vira o personagem para a direção desejada.
    /// 
    /// dirX > 0  → olhando para a direita
    /// dirX < 0  → olhando para a esquerda
    /// 
    /// Esse método:
    /// ✔️ Flipa o SpriteRenderer
    /// ✔️ Reposiciona corretamente o MagicPointAttack
    /// ❌ NÃO move o Transform do Player
    /// </summary>
    public void FaceDirection(float dirX)
    {
        // Se não há direção, não fazemos nada.
        // Evita flips desnecessários quando o personagem está parado.
        if (dirX == 0f)
            return;

        // Verdade booleana absoluta do universo:
        bool lookingRight = dirX > 0f;

        // -----------------------------------------------------
        // 1️⃣ FLIP VISUAL
        // -----------------------------------------------------
        // Aqui acontece a magia visual.
        // Flipamos APENAS o SpriteRenderer.
        // Nada de mexer em scale.x (isso quebra o mundo).
        spriteRenderer.flipX = lookingRight;

        // -----------------------------------------------------
        // 2️⃣ AJUSTE DO PONTO DE ATAQUE (SOCKET)
        // -----------------------------------------------------
        if (magicPointAttack != null)
        {
            // Pegamos o valor absoluto da posição X original.
            // Isso nos dá uma base neutra, independente do lado.
            float baseX = Mathf.Abs(originalMagicPointLocalPos.x);

            // Calculamos o novo X baseado na direção atual:
            //
            // ➡️ Direita:
            //   +baseX + offsetRight
            //
            // ⬅️ Esquerda:
            //   -baseX + offsetLeft
            //
            // Isso garante:
            // - Espelhamento perfeito
            // - Ajuste fino independente por lado
            float newX = lookingRight
                ? (baseX + magicPointOffsetRight)
                : (-baseX + magicPointOffsetLeft);

            // Aplicamos a nova posição LOCAL.
            // Y e Z permanecem intactos.
            magicPointAttack.localPosition = new Vector3(
                newX,
                originalMagicPointLocalPos.y,
                originalMagicPointLocalPos.z
            );
        }
    }
}
