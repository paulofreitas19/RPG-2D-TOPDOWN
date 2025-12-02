using UnityEngine;

/// <summary>
/// Classe-base genérica para qualquer inimigo do jogo.
/// Tudo que é comum a TODOS os inimigos deve morar aqui:
/// - Componentes básicos (Animator, Rigidbody2D, Transform do sprite)
/// - Vida / dano
/// - Detecção de player via OverlapCircle
/// - Cooldown básico de ataque
/// - Helpers de animação e de flip do sprite
/// 
/// Cada inimigo específico (Slime, Dino, etc.) herda desta classe
/// e implementa a sua IA em HandleUpdate/HandleFixedUpdate.
/// </summary>
public abstract class EnemyBase : MonoBehaviour
{
    // =========================================================
    //  COMPONENTES BÁSICOS
    // =========================================================

    [Header("Components")]
    [Tooltip("Animator responsável pelas animações do inimigo.")]
    protected Animator anim;

    [Tooltip("Rigidbody2D usado para movimento físico do inimigo.")]
    protected Rigidbody2D rb;

    [Tooltip("Transform que contém o sprite (usado para flip). Se vazio, será o próprio transform.")]
    [SerializeField] protected Transform spriteTransform;

    // =========================================================
    //  VIDA / STATUS
    // =========================================================

    [Header("Health")]
    [Tooltip("Vida máxima do inimigo.")]
    [SerializeField] protected float maxHealth = 1f;

    [Tooltip("Vida atual do inimigo em runtime.")]
    protected float currentHealth;

    // =========================================================
    //  MOVIMENTAÇÃO BÁSICA
    // =========================================================

    [Header("Movement Base")]
    [Tooltip("Velocidade de movimento padrão do inimigo.")]
    [SerializeField] protected float moveSpeed = 1f;

    // =========================================================
    //  DETECÇÃO DO PLAYER
    // =========================================================

    [Header("Player Detection")]
    [Tooltip("Raio de detecção do player (OverlapCircle). 0 = não usar detecção automática.")]
    [SerializeField] protected float detectRadius = 0f;

    [Tooltip("LayerMask usada para encontrar o player no OverlapCircle.")]
    [SerializeField] protected LayerMask playerLayer;

    [Tooltip("Transform do player detectado (se houver).")]
    protected Transform player;

    // =========================================================
    //  ATAQUE BÁSICO
    // =========================================================

    [Header("Attack Base")]
    [Tooltip("Tempo de recarga entre ataques consecutivos.")]
    [SerializeField] protected float attackCooldown = 1.5f;

    [Tooltip("Momento (Time.time) em que o último ataque aconteceu.")]
    protected float lastAttackTime = Mathf.NegativeInfinity;


    // =========================================================
    //  CICLO DE VIDA UNITY
    // =========================================================

    /// <summary>
    /// Awake é chamado antes de Start; ideal para cachear componentes.
    /// </summary>
    protected virtual void Awake()
    {
        // Procura o Animator em algum filho (muito comum ter o sprite como child).
        anim = GetComponentInChildren<Animator>();

        // Rigidbody2D fica normalmente no objeto raiz do inimigo.
        rb = GetComponent<Rigidbody2D>();

        // Se o spriteTransform não foi arrastado no Insppector, usamos o próprio transform.
        if (spriteTransform == null)
            spriteTransform = transform;
    }

    /// <summary>
    /// Start é chamado após Awake. Ideal para inicializar estados.
    /// </summary>
    protected virtual void Start()
    {
        // Começamos com vida cheia.
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Update padrão da classe-base:
    /// - Detecta o player (se detectRadius > 0)
    /// - Delega o comportamento para a subclasse
    /// </summary>
    protected virtual void Update()
    {
        // Atualiza a referência do player automaticamente.
        DetectPlayer();

        // Entrega o resto da lógica para o inimigo específico.
        HandleUpdate();
    }

    /// <summary>
    /// FixedUpdate padrão da classe-base.
    /// A subclasse pode sobrescrever HandleFixedUpdate se precisar.
    /// </summary>
    protected virtual void FixedUpdate()
    {
        HandleFixedUpdate();
    }

    // =========================================================
    //  MÉTODOS ABSTRATOS / VIRTUAIS PARA AS SUBCLASSES
    // =========================================================

    /// <summary>
    /// Lógica principal do inimigo por frame.
    /// Toda subclasse DEVE implementar.
    /// </summary>
    protected abstract void HandleUpdate();

    /// <summary>
    /// Lógica de física opcional. Subclasses sobrescrevem se necessário.
    /// </summary>
    protected virtual void HandleFixedUpdate() { }

    // =========================================================
    //  DETECÇÃO PADRÃO DE PLAYER
    // =========================================================

    /// <summary>
    /// Detecta o player usando OverlapCircle.
    /// Subclasses podem sobrescrever se quiserem outro tipo de visão.
    /// </summary>
    protected virtual void DetectPlayer()
    {
        // Se o raio é zero ou negativo, consideramos que este inimigo não usa detecção automática.
        if (detectRadius <= 0f)
        {
            player = null;
            return;
        }

        // OverlapCircle pega qualquer collider na layer do player.
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectRadius, playerLayer);

        // Se achou, guarda o Transform; senão zera.
        player = hit != null ? hit.transform : null;
    }

    // =========================================================
    //  DANO / MORTE
    // =========================================================

    /// <summary>
    /// Método genérico de tomar dano que qualquer inimigo pode reaproveitar.
    /// </summary>
    public virtual void TakeDamage(float amount)
    {
        // Proteção contra valores inválidos.
        if (amount <= 0f)
            return;

        // Subtrai vida.
        currentHealth -= amount;

        // Se chegou a zero ou abaixo, morre.
        if (currentHealth <= 0f)
            Die();
    }

    /// <summary>
    /// Lógica básica de morte.
    /// Subclasses podem sobrescrever (animação, drops, etc.) e chamar base.Die() no final.
    /// </summary>
    protected virtual void Die()
    {
        Destroy(gameObject);
    }

    // =========================================================
    //  HELPERS VISUAIS (ANIMAÇÃO / FLIP)
    // =========================================================

    /// <summary>
    /// Helper simples para mudar o inteiro "transition" do Animator.
    /// </summary>
    protected virtual void SetAnimationState(int transitionValue)
    {
        if (anim != null)
            anim.SetInteger("transition", transitionValue);
    }

    /// <summary>
    /// Helper para flipar o sprite olhando para o alvo (normalmente o player).
    /// </summary>
    protected virtual void FaceTarget(Transform target)
    {
        if (target == null || spriteTransform == null)
            return;

        // Pega a escala atual.
        Vector3 scale = spriteTransform.localScale;

        // Se o alvo está à direita, espelha o X para olhar à direita.
        if (target.position.x > transform.position.x)
            scale.x = -Mathf.Abs(scale.x);
        else
            // Se está à esquerda, espelha para a esquerda.
            scale.x = Mathf.Abs(scale.x);

        spriteTransform.localScale = scale;
    }

    // =========================================================
    //  GIZMOS (DEBUG)
    // =========================================================

    protected virtual void OnDrawGizmosSelected()
    {
        // Mostra o raio de detecção do player, se houver.
        if (detectRadius > 0f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectRadius);
        }
    }
}
