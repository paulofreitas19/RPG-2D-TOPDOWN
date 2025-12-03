using UnityEngine;

/// <summary>
/// Controla toda a lógica do jogador em um RPG top-down estilo Lineage2:
/// - Movimento por clique (click-to-move).
/// - Sistema de target em inimigos.
/// - Sistema de auto-attack com cooldown.
/// - Chamadas para o sistema de animação do Player.
/// - Sistema de vida e dano básico.
/// </summary>
public class Player : MonoBehaviour
{
    //======================================================================
    //  COMPONENTES
    //======================================================================

    /// <summary>
    /// Referência para o Rigidbody2D responsável pelo movimento físico.
    /// </summary>
    [Header("Componentes")]
    [SerializeField] private Rigidbody2D rb;

    /// <summary>
    /// Referência para o script responsável pelas animações do player.
    /// </summary>
    [SerializeField] private PlayerAnim playerAnim;

    /// <summary>
    /// Câmera principal usada para converter posição da tela em mundo.
    /// </summary>
    [SerializeField] private Camera mainCamera;

    //======================================================================
    //  MOVIMENTO (CLICK-TO-MOVE)
    //======================================================================

    /// <summary>
    /// Velocidade de movimento do player em unidades por segundo.
    /// </summary>
    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 3f;

    /// <summary>
    /// Ponto do mundo para onde o player deve se mover (destino atual).
    /// </summary>
    private Vector2 moveTarget;

    /// <summary>
    /// Flag indicando se existe ou não um destino de movimento ativo.
    /// </summary>
    private bool hasMoveTarget = false;

    /// <summary>
    /// Distância mínima até o destino para considerar que "chegou".
    /// </summary>
    [SerializeField] private float arriveThreshold = 0.05f;

    //======================================================================
    //  COMBATE / TARGET / AUTO-ATTACK
    //======================================================================

    /// <summary>
    /// Camada usada para detectar inimigos quando clicamos neles.
    /// </summary>
    [Header("Combate / Target")]
    [SerializeField] private LayerMask enemyLayer;

    /// <summary>
    /// Alcance máximo para que o ataque corpo-a-corpo acerte o alvo.
    /// </summary>
    [SerializeField] private float attackRange = 1.5f;

    /// <summary>
    /// Tempo em segundos entre um ataque e outro (cooldown do auto-attack).
    /// </summary>
    [SerializeField] private float attackCooldown = 1.0f;

    /// <summary>
    /// Dano causado pelo ataque básico do player.
    /// </summary>
    [SerializeField] private int attackDamage = 10;

    /// <summary>
    /// Referência para o alvo atual (inimigo selecionado).
    /// </summary>
    private EnemyBase currentTarget;

    /// <summary>
    /// Momento em que o último ataque foi executado (em segundos desde o início do jogo).
    /// </summary>
    private float lastAttackTime = -999f;

    /// <summary>
    /// Se verdadeiro, o player executa ataques automáticos
    /// enquanto tiver um alvo em alcance.
    /// </summary>
    [SerializeField] private bool autoAttackEnabled = true;

    //======================================================================
    //  VIDA / DANO
    //======================================================================

    /// <summary>
    /// Vida máxima do player.
    /// </summary>
    [Header("Vida")]
    [SerializeField] private int maxHealth = 100;

    /// <summary>
    /// Vida atual do player.
    /// </summary>
    private int currentHealth;

    /// <summary>
    /// Propriedade somente leitura da vida atual.
    /// (Permite outros scripts lerem, mas não alterarem diretamente.)
    /// </summary>
    public int CurrentHealth => currentHealth;

    /// <summary>
    /// Propriedade somente leitura da vida máxima.
    /// </summary>
    public int MaxHealth => maxHealth;

    /// <summary>
    /// Flag simples para indicar se o player já morreu.
    /// </summary>
    private bool isDead = false;

    //======================================================================
    //  MÉTODOS UNITY
    //======================================================================

    /// <summary>
    /// Chamado assim que o objeto é instanciado.
    /// Aqui garantimos que as referências principais estejam configuradas.
    /// </summary>
    private void Awake()
    {
        // Se não foi arrastado pelo Inspector, tenta pegar automaticamente.
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        // Se não foi arrastado pelo Inspector, tenta pegar em um filho.
        if (playerAnim == null)
            playerAnim = GetComponentInChildren<PlayerAnim>();

        // Se a câmera não foi atribuída, usa a Camera.main.
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Inicia a vida atual no valor máximo.
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Chamado a cada frame.
    /// Ideal para ler input do jogador.
    /// </summary>
    private void Update()
    {
        if (isDead) return; // Se já morreu, não processa mais nada.

        HandleMouseInput();   // Lida com clique para movimento/target.
        HandleAutoAttack();   // Lida com auto-attack se tiver alvo.
        HandleManualSkills(); // Exemplo de skills em teclas (F1, F2 etc).
        UpdateAnimationState(); // Atualiza animações com base no estado atual.
    }

    /// <summary>
    /// Chamado em intervalos fixos, ideal para código de física.
    /// </summary>
    private void FixedUpdate()
    {
        if (isDead) return;

        HandleMovement(); // Aplica o movimento físico.
    }

    //======================================================================
    //  INPUT DO MOUSE (CLICK-TO-MOVE + TARGET)
    //======================================================================

    /// <summary>
    /// Lê o input do mouse e decide se é para:
    /// - Mover o player até um ponto do cenário.
    /// - Ou selecionar um inimigo como target.
    /// </summary>
    private void HandleMouseInput()
    {
        // Botão esquerdo do mouse: selecionar alvo (ou ground para andar).
        if (Input.GetMouseButtonDown(0))
        {
            // Converte posição do clique na tela para mundo.
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mouseWorld2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

            // Tenta detectar um inimigo exatamente sob o clique.
            Collider2D hit = Physics2D.OverlapPoint(mouseWorld2D, enemyLayer);

            if (hit != null)
            {
                // Achou inimigo: pega EnemyBase e seta como alvo.
                EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();

                if (enemy != null)
                {
                    SetTarget(enemy);
                    // Opcional: também mover em direção ao alvo.
                    SetMoveTarget(enemy.transform.position);
                }
            }
            else
            {
                // Não clicou em inimigo: limpa target e move até o ponto clicado.
                ClearTarget();
                SetMoveTarget(mouseWorld2D);
            }
        }

        // Botão direito do mouse: apenas movimento, sem mexer no target.
        if (Input.GetMouseButtonDown(1))
        {
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mouseWorld2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

            SetMoveTarget(mouseWorld2D);
        }
    }

    /// <summary>
    /// Define um novo ponto alvo para o movimento.
    /// </summary>
    /// <param name="destination">Posição no mundo 2D para onde o player irá.</param>
    private void SetMoveTarget(Vector2 destination)
    {
        moveTarget = destination;
        hasMoveTarget = true;
    }

    /// <summary>
    /// Remove o destino de movimento atual.
    /// </summary>
    private void ClearMoveTarget()
    {
        hasMoveTarget = false;
        rb.linearVelocity = Vector2.zero;
    }

    //======================================================================
    //  MOVIMENTO
    //======================================================================

    /// <summary>
    /// Calcula e aplica o movimento em direção ao destino atual (se existir).
    /// </summary>
    private void HandleMovement()
    {
        if (!hasMoveTarget)
        {
            // Sem destino: zera velocidade.
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Direção (vetor) do player até o destino.
        Vector2 direction = moveTarget - rb.position;

        // Distância até o destino.
        float distance = direction.magnitude;

        // Se já está bem próximo, considera que chegou.
        if (distance <= arriveThreshold)
        {
            ClearMoveTarget();
            return;
        }

        // Normaliza a direção (transforma em vetor unitário).
        direction.Normalize();

        // Aplica velocidade constante na direção do destino.
        rb.linearVelocity = direction * moveSpeed;

        // Atualiza a direção visual do player (flip do sprite).
        playerAnim.SetLookDirection(direction);
    }

    //======================================================================
    //  SISTEMA DE TARGET
    //======================================================================

    /// <summary>
    /// Define um novo inimigo como alvo atual.
    /// </summary>
    /// <param name="enemy">Instância do inimigo (EnemyBase).</param>
    private void SetTarget(EnemyBase enemy)
    {
        currentTarget = enemy;
        // Aqui daria para acionar um highlight no inimigo, se quiser.
    }

    /// <summary>
    /// Remove completamente o alvo atual.
    /// </summary>
    private void ClearTarget()
    {
        currentTarget = null;
    }

    /// <summary>
    /// Retorna a distância atual até o alvo, ou um valor grande
    /// se não existir alvo (para simplificar checks).
    /// </summary>
    private float DistanceToTarget()
    {
        if (currentTarget == null)
            return Mathf.Infinity;

        return Vector2.Distance(transform.position, currentTarget.transform.position);
    }

    //======================================================================
    //  AUTO-ATTACK + COOLDOWN + SKILLS
    //======================================================================

    /// <summary>
    /// Lógica de auto-attack:
    /// - Se há alvo.
    /// - Se está no alcance.
    /// - Se o cooldown já passou.
    /// - Então executa o ataque básico automaticamente.
    /// </summary>
    private void HandleAutoAttack()
    {
        if (!autoAttackEnabled) return;
        if (currentTarget == null) return;

        // Distância até o alvo.
        float distance = DistanceToTarget();

        // Se está longe demais, apenas anda em direção ao alvo.
        if (distance > attackRange)
        {
            SetMoveTarget(currentTarget.transform.position);
            return;
        }

        // Está perto o bastante: para o movimento.
        ClearMoveTarget();

        // Vira para o alvo.
        Vector2 dir = (currentTarget.transform.position - transform.position).normalized;
        playerAnim.SetLookDirection(dir);

        // Verifica cooldown.
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            PerformBasicAttack();
        }
    }

    /// <summary>
    /// Exemplo de sistema de skills manuais com cooldown:
    /// aqui usamos teclas F1/F2 só para ilustrar o conceito.
    /// </summary>
    private void HandleManualSkills()
    {
        // F1: liga/desliga o auto-attack.
        if (Input.GetKeyDown(KeyCode.F1))
        {
            autoAttackEnabled = !autoAttackEnabled;
            // Aqui você poderia exibir um ícone/feedback visual.
        }

        // F2: exemplo de "skill especial" manual (a implementar depois).
        // if (Input.GetKeyDown(KeyCode.F2)) { ... }
    }

    /// <summary>
    /// Executa o ataque básico:
    /// - Dispara a animação de ataque.
    /// - Aplica dano no alvo atual.
    /// - Atualiza o tempo do último ataque (para o cooldown).
    /// </summary>
    private void PerformBasicAttack()
    {
        if (currentTarget == null) return;

        // Dispara a animação de ataque.
        playerAnim.PlayBasicAttack();

        // Aplica dano no inimigo, se ainda existir.
        currentTarget.TakeDamage(attackDamage);

        // Atualiza o tempo do último ataque.
        lastAttackTime = Time.time;
    }

    //======================================================================
    //  VIDA / DANO / MORTE
    //======================================================================

    /// <summary>
    /// Aplica dano ao player.
    /// Esse método deve ser chamado pelos inimigos quando acertarem o player.
    /// </summary>
    /// <param name="amount">Quantidade de dano bruto.</param>
    public void TakeDamage(int amount)
    {
        if (isDead) return;

        // Garante que o dano não seja negativo.
        amount = Mathf.Max(0, amount);

        currentHealth -= amount;

        // Se chegou em zero ou menos, morre.
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
        else
        {
            // Aqui você poderia tocar uma animação de "hit",
            // som, screen shake etc.
        }
    }

    /// <summary>
    /// Lógica de morte do player.
    /// </summary>
    private void Die()
    {
        if (isDead) return;

        isDead = true;

        // Para qualquer movimento.
        ClearMoveTarget();

        // Dispara animação de morte.
        playerAnim.PlayDeath();

        // Aqui você pode desabilitar inputs, abrir tela de gameover etc.
    }

    //======================================================================
    //  ATUALIZAÇÃO DE ANIMAÇÃO GENÉRICA
    //======================================================================

    /// <summary>
    /// Atualiza o "estado base" da animação (Idle / Walk)
    /// de acordo com a velocidade atual do player.
    /// </summary>
    private void UpdateAnimationState()
    {
        if (isDead)
        {
            // Se quiser impedir que volte para Idle/Walk depois de morrer,
            // basta retornar aqui.
            return;
        }

        // Velocidade atual em módulo (sem direção).
        float speed = rb.linearVelocity.magnitude;

        // Informa ao PlayerAnim o valor atual de velocidade,
        // para que ele decida entre Idle e Walk.
        playerAnim.SetMoveSpeed(speed);
    }
}
