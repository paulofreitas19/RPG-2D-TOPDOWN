using UnityEngine;

/// <summary>
/// SPRITE FLIPPER — MAGIA DE ORIENTAÇÃO ESPACIAL (versão estável)
///
/// Missão deste artefato arcano:
/// 1) Virar o VISUAL do player usando SpriteRenderer.flipX (sem mexer em Transform.scale).
/// 2) Fazer o MagicPointAttack (spawnpoint) acompanhar a virada corretamente.
/// 3) Não permitir “flip fantasma” no início do Play Mode (ordem de execução / Animator / scripts).
///
/// Regra de Ouro:
/// - "Flipar" visualmente não é mover o mundo.
/// - O spawnpoint não deve ser "empurrado" por offsets aleatórios.
/// - Ele deve ser ESPELHADO no X (x = -x) e depois ajustado com offsets finos.
/// </summary>
public class SpriteFlipper : MonoBehaviour
{
    // =========================
    // REFERÊNCIAS ARCANAS
    // =========================

    [Header("Referências Arcanas")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    // O ponto onde a magia nasce (spawnpoint). Ele pode estar fora do GameObject do Sprite.
    [SerializeField] private Transform magicPointAttack;

    // =========================
    // CONFIGURAÇÃO DO UNIVERSO
    // =========================

    [Header("Configuração do Sprite (muito importante!)")]
    [Tooltip("Marque se a arte do seu personagem, SEM flipX, já está olhando para a ESQUERDA.")]
    [SerializeField] private bool spriteFacesLeftByDefault = true;

    // =========================
    // AJUSTE FINO DO SPAWNPOINT
    // =========================

    [Header("Ajuste fino do Spawnpoint (opcional)")]
    [Tooltip("Offset extra aplicado quando o player está virado para a DIREITA.")]
    [SerializeField] private float extraOffsetRightX = 0f;

    [Tooltip("Offset extra aplicado quando o player está virado para a ESQUERDA.")]
    [SerializeField] private float extraOffsetLeftX = 0f;

    // =========================
    // MEMÓRIAS DO FEITIÇO
    // =========================

    // A posição base do spawnpoint em LOCAL SPACE.
    // Essa é a “inscrição original” do ponto no altar (prefab/scene).
    private Vector3 baseMagicPointLocalPos;

    // Nosso estado arcano: pra que lado o personagem DEVE estar olhando (a verdade).
    private bool isFacingRight;

    // Pequena runa anti-bagunça:
    // garante que só vamos sincronizar completamente depois que o mundo acordar (Start).
    private bool hasInitialized;

    // =========================
    // CICLO DE VIDA ARCANO
    // =========================

    private void Awake()
    {
        // Se não foi arrastado no Inspector, tentamos encontrar automaticamente.
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Guardamos a posição base do spawnpoint ANTES de qualquer magia de flip.
        if (magicPointAttack != null)
            baseMagicPointLocalPos = magicPointAttack.localPosition;

        // Descobrimos o estado inicial do facing olhando o flipX atual.
        // (Isso evita o “flip inicial fantasma” quando você entra em Play Mode.)
        isFacingRight = RendererFlipXMeansFacingRight(spriteRenderer != null && spriteRenderer.flipX);
    }

    private void Start()
    {
        // Agora que tudo já iniciou (Animator, outros scripts, etc),
        // nós aplicamos a verdade do facing e garantimos que o spawnpoint
        // esteja no lugar correto.
        ApplyFacing(isFacingRight);

        hasInitialized = true;
    }

    private void LateUpdate()
    {
        // Runa Anti-Animator / Anti-Script Fantasma:
        // Se qualquer entidade (Animator, outro script, importação) tentar mexer no flipX,
        // nós reescrevemos a realidade para ficar alinhado com o "isFacingRight".
        //
        // Isso é seguro porque:
        // - nós NÃO mexemos em Transform do Player raiz
        // - só controlamos o flip do SpriteRenderer e a posição local do spawnpoint
        if (!hasInitialized || spriteRenderer == null)
            return;

        bool currentFacingRight = RendererFlipXMeansFacingRight(spriteRenderer.flipX);

        // Se o visual foi alterado por fora, a gente re-sincroniza.
        if (currentFacingRight != isFacingRight)
        {
            ApplyFacing(isFacingRight);
        }
    }

    // =========================
    // API PÚBLICA — MAGIAS DE CONTROLE
    // =========================

    /// <summary>
    /// Vira baseado num eixo X (ex: movimento).
    /// dirX > 0 => olhar direita
    /// dirX < 0 => olhar esquerda
    /// dirX = 0 => não altera
    /// </summary>
    public void FaceDirection(float dirX)
    {
        if (Mathf.Approximately(dirX, 0f))
            return;

        SetFacingRight(dirX > 0f);
    }

    /// <summary>
    /// Vira para um alvo no mundo (usando a posição do alvo).
    /// Ideal para: "antes de conjurar, olhar pro inimigo".
    /// </summary>
    public void FaceTarget(Vector3 targetWorldPos)
    {
        float dirX = targetWorldPos.x - transform.position.x;

        if (Mathf.Approximately(dirX, 0f))
            return;

        SetFacingRight(dirX > 0f);
    }

    /// <summary>
    /// Sobrecarga confortável: vira para um Transform alvo.
    /// </summary>
    public void FaceTarget(Transform target)
    {
        if (target == null) return;
        FaceTarget(target.position);
    }

    /// <summary>
    /// Método explícito: define o facing diretamente.
    /// </summary>
    public void SetFacingRight(bool facingRight)
    {
        if (isFacingRight == facingRight)
        {
            // Mesmo se não mudou, garantimos que o spawnpoint está certo.
            // Isso ajuda em casos onde você ajustou offsets e quer “reaplicar”.
            ApplyMagicPoint();
            return;
        }

        isFacingRight = facingRight;
        ApplyFacing(isFacingRight);
    }

    /// <summary>
    /// Para outros sistemas consultarem a direção atual.
    /// </summary>
    public bool IsFacingRight() => isFacingRight;

    // =========================
    // NÚCLEO DO FEITIÇO
    // =========================

    /// <summary>
    /// Aplica flip visual + posiciona spawnpoint.
    /// </summary>
    private void ApplyFacing(bool facingRight)
    {
        if (spriteRenderer != null)
        {
            // Convertemos "facingRight" -> flipX, respeitando a arte default.
            spriteRenderer.flipX = FacingRightMeansRendererFlipX(facingRight);
        }

        ApplyMagicPoint();
    }

    /// <summary>
    /// Posiciona o MagicPointAttack corretamente para o lado atual.
    /// Aqui mora a correção principal:
    /// - Espelhamos o X (x = -x) quando vira para o outro lado
    /// - Depois aplicamos offset fino por lado
    /// </summary>
    private void ApplyMagicPoint()
    {
        if (magicPointAttack == null)
            return;

        Vector3 pos = baseMagicPointLocalPos;

        // ESPELHO ARCANO:
        // Se o ponto base foi desenhado no lado esquerdo (ex: x = -0.8),
        // ao olhar para a direita ele deve ir para (x = +0.8).
        //
        // Isso é flip real: trocar o sinal.
        if (isFacingRight)
            pos.x = -baseMagicPointLocalPos.x;
        else
            pos.x = baseMagicPointLocalPos.x;

        // Ajuste fino (opcional) por lado.
        pos.x += isFacingRight ? extraOffsetRightX : extraOffsetLeftX;

        magicPointAttack.localPosition = pos;
    }

    // =========================
    // CONVERSORES DE REALIDADE
    // =========================

    /// <summary>
    /// Interpreta se o flipX do SpriteRenderer significa "olhando para a direita".
    /// Isso depende da arte default.
    /// </summary>
    private bool RendererFlipXMeansFacingRight(bool rendererFlipX)
    {
        // Se a arte olha pra esquerda por padrão:
        // - flipX false => esquerda
        // - flipX true  => direita
        if (spriteFacesLeftByDefault)
            return rendererFlipX;

        // Se a arte olha pra direita por padrão:
        // - flipX false => direita
        // - flipX true  => esquerda
        return !rendererFlipX;
    }

    /// <summary>
    /// Converte "olhando para a direita" em flipX do SpriteRenderer.
    /// </summary>
    private bool FacingRightMeansRendererFlipX(bool facingRight)
    {
        if (spriteFacesLeftByDefault)
            return facingRight; // direita => flipX true

        return !facingRight;    // se arte já olha direita, direita => flipX false
    }
}
