using System.Collections;
using UnityEngine;

/// <summary>
/// Controla movimento, entrada de comandos, ataques e sistema de vida do player.
/// </summary>
public class Player : MonoBehaviour
{
    //========================================================
    //  COMPONENTES / REFERÊNCIAS
    //========================================================

    [Header("Componentes")]

    // Referência para o Rigidbody2D usado para mover o player.
    [SerializeField] private Rigidbody2D rb;

    // SpriteRenderer usado para feedback visual (piscar ao levar dano).
    [SerializeField] private SpriteRenderer spriteRenderer;

    // Referência para o script de animação do player (opcional, mas útil).
    [SerializeField] private PlayerAnim playerAnim;

    //========================================================
    //  MOVIMENTO
    //========================================================

    [Header("Movimento")]

    // Velocidade base de movimento do player.
    [SerializeField] private float moveSpeed = 3f;

    // Direção normalizada do movimento (usada por outros scripts, ex: PlayerAnim).
    public Vector2 Direction { get; private set; }

    //========================================================
    //  ATAQUES (FLAGS PARA O ANIMATOR)
    //========================================================

    [Header("Ataques")]

    // Flag para o ataque básico (ex: bastão / melee).
    public bool IsBasicAttack { get; private set; }

    // Flag para o ataque mágico (ex: fireball).
    public bool IsMagicAttack { get; private set; }

    //========================================================
    //  VIDA / DANO
    //========================================================

    [Header("Vida")]

    // Vida máxima do player.
    [SerializeField] private int maxHealth = 100;

    // Duração da invulnerabilidade logo após levar um golpe.
    [SerializeField] private float invulnerabilityTime = 0.6f;

    // Vida atual do player (clamp entre 0 e maxHealth).
    private int currentHealth;

    // Flag que indica se o player está temporariamente invulnerável.
    private bool isInvulnerable = false;

    // Flag que indica se o player já morreu (evita processar inputs, dano etc.).
    public bool IsDead { get; private set; } = false;

    // Cor original do sprite (para restaurar após o flash de dano).
    private Color originalColor;

    //========================================================
    //  PROPRIEDADES DE LEITURA PÚBLICA
    //========================================================

    // Propriedade somente leitura da vida atual.
    public int CurrentHealth => currentHealth;

    // Propriedade somente leitura da vida máxima.
    public int MaxHealth => maxHealth;

    // Propriedade somente leitura se está invulnerável.
    public bool IsInvulnerable => isInvulnerable;

    //========================================================
    //  UNITY METHODS
    //========================================================

    private void Awake()
    {
        // Garante referências automáticas se não forem arrastadas no Inspector.
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (playerAnim == null)
            playerAnim = GetComponentInChildren<PlayerAnim>();

        // Salva a cor original para restaurar depois do efeito de dano.
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        // Inicia a vida no máximo.
        currentHealth = maxHealth;
    }

    private void Update()
    {
        // Se já morreu, não processa entrada de movimento / ataque.
        if (IsDead)
        {
            // Garante que o player fique parado.
            Direction = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Lê input de movimento (WASD / setas).
        ReadMovementInput();

        // Lê input de ataque (por enquanto só flags simples).
        ReadAttackInput();
    }

    private void FixedUpdate()
    {
        // Se morreu, não move mais (apenas garantia extra).
        if (IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Aplica o movimento no Rigidbody2D.
        rb.linearVelocity = Direction * moveSpeed;
    }

    //========================================================
    //  INPUT
    //========================================================

    /// <summary>
    /// Lê o input de movimento do teclado e atualiza Direction.
    /// </summary>
    private void ReadMovementInput()
    {
        // Lê eixo horizontal (A/D ou setas).
        float h = Input.GetAxisRaw("Horizontal");

        // Lê eixo vertical (W/S ou setas).
        float v = Input.GetAxisRaw("Vertical");

        // Monta o vetor com base nos eixos.
        Vector2 input = new Vector2(h, v);

        // Normaliza para manter a mesma velocidade em diagonal.
        Direction = input.normalized;
    }

    /// <summary>
    /// Lê o input de ataque e liga/desliga as flags.
    /// Aqui você pode mapear teclas específicas (ex: J/K, mouse etc.).
    /// </summary>
    private void ReadAttackInput()
    {
        // Exemplo: ataque básico ao pressionar botão esquerdo do mouse.
        IsBasicAttack = Input.GetMouseButtonDown(0);

        // Exemplo: ataque mágico ao pressionar botão direito do mouse.
        IsMagicAttack = Input.GetMouseButtonDown(1);
    }

    //========================================================
    //  SISTEMA DE DANO
    //========================================================

    /// <summary>
    /// Aplica dano ao player.
    /// Esse método será chamado pelos inimigos (ex: Slime) quando acertarem o player.
    /// </summary>
    /// <param name="amount">Quantidade de dano a ser aplicada.</param>
    public void TakeDamage(int amount)
    {
        // Se já morreu ou está invulnerável, ignora o dano.
        if (IsDead || isInvulnerable)
            return;

        // Garante que o valor de dano é positivo.
        amount = Mathf.Abs(amount);

        // Subtrai da vida atual.
        currentHealth -= amount;

        // Faz clamp para nunca ficar abaixo de 0.
        currentHealth = Mathf.Max(currentHealth, 0);

        // Aqui você pode logar para debug, se quiser.
        // Debug.Log($"Player tomou {amount} de dano. Vida atual: {currentHealth}");

        // Dispara feedback visual de dano + invulnerabilidade temporária.
        StartCoroutine(DamageFeedbackRoutine());

        // Se a vida chegou a zero, executa a lógica de morte.
        if (currentHealth == 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Cura o player em um determinado valor.
    /// </summary>
    /// <param name="amount">Quantidade a ser curada.</param>
    public void Heal(int amount)
    {
        // Não cura se já estiver morto.
        if (IsDead)
            return;

        // Garante valor positivo.
        amount = Mathf.Abs(amount);

        // Soma a vida e faz clamp entre 0 e maxHealth.
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
    }

    /// <summary>
    /// Rotina de feedback visual de dano + invulnerabilidade.
    /// Faz o player piscar por alguns instantes.
    /// </summary>
    private IEnumerator DamageFeedbackRoutine()
    {
        // Marca que está invulnerável.
        isInvulnerable = true;

        // Tempo acumulado.
        float elapsed = 0f;

        // Pequeno intervalo entre cada piscar.
        float blinkInterval = 0.1f;

        // Enquanto não atingir o tempo total de invulnerabilidade...
        while (elapsed < invulnerabilityTime)
        {
            // Alterna a visibilidade do sprite (pisca).
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;

            // Espera um pouquinho.
            yield return new WaitForSeconds(blinkInterval);

            // Atualiza o tempo acumulado.
            elapsed += blinkInterval;
        }

        // Garante que o sprite volte a ficar visível.
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = originalColor;
        }

        // Libera para receber dano novamente.
        isInvulnerable = false;
    }

    /// <summary>
    /// Lógica de morte do player.
    /// </summary>
    private void Die()
    {
        // Marca o estado de morto.
        IsDead = true;

        // Zera o movimento e impede que continue deslizando.
        Direction = Vector2.zero;
        rb.linearVelocity = Vector2.zero;

        // Se existir um script de animação, podemos sinalizar uma animação de morte.
        if (playerAnim != null)
        {
            // Aqui você pode criar um método específico no PlayerAnim, ex:
            // playerAnim.PlayDeath();
            // Por enquanto vamos só expor o IsDead para ele ler.
        }

        // Aqui você pode:
        // - Desabilitar input
        // - Mostrar tela de Game Over
        // - Tocar som de morte
        // - Etc.
    }
}
