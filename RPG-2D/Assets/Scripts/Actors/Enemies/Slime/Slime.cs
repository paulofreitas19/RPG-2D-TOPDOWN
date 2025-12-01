using System.Collections;
using UnityEngine;

public class Slime : MonoBehaviour
{
    private Vector3 originalScale;

    [SerializeField] private Transform spriteTransform;

    // =========================================================
    //  ATAQUE / COOLDOWN
    // =========================================================

    [Header("Attack Settings")]
    [SerializeField] private float attackCooldown = 2f;
    private float lastAttackTime = Mathf.NegativeInfinity;
    public float stopDistance = 1f;

    // Esta flag indica que o slime ainda precisa retornar para a posição original do ataque.
    // É usada quando o ataque termina antes do retorno ocorrer, garantindo que o slime SEMPRE volte
    // ao ponto inicial antes de continuar a IA de patrulha ou iniciar outro ataque.
    private bool pendingReturn = false;

    // =========================================================
    //  PULO EM ARCO DO ATAQUE
    // =========================================================

    private Vector2 attackStartPos;
    public float attackJumpDistance = 1f;
    public float jumpHeight = 0.8f;
    public float jumpDuration = 0.25f;
    public float returnDuration = 0.25f;
    private bool isJumping = false;

    // =========================================================
    //  COMPONENTES
    // =========================================================

    [Header("Components")]
    private Animator anim;
    private Rigidbody2D rb;

    // =========================================================
    //  DETECÇÃO DO PLAYER
    // =========================================================

    [Header("Player Detect")]
    public float detectRadius = 4f;
    public LayerMask playerLayer;
    private Transform player;

    // =========================================================
    //  MOVIMENTO / PATHFIND
    // =========================================================

    [Header("Movement")]
    public float moveSpeed = 1.2f;
    public Transform[] pathPoints;
    private int currentPathIndex = 0;

    private bool waitingAtPoint = false;
    public float waitTime = 2f;

    // =========================================================
    //  ESTADOS (Animator usa "transition" int)
    // =========================================================

    private enum State
    {
        Idle = 0,
        Move = 1,
        Attack = 2
    }

    private State currentState = State.Idle;


    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        originalScale = spriteTransform.localScale;
    }

    private void Update()
    {
        if (isJumping)
            return;

        DetectPlayer();

        if (player != null)
        {
            FacePlayer();   // 🟩 ADIÇÃO — virar sprite sempre que detectar o player
            HandleChase();
            return;
        }

        HandlePathfinding();
    }

    private void FixedUpdate()
    {
        if (isJumping)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }
    }

    // =========================================================
    //  ROTACIONAR O SLIME PARA O PLAYER
    // =========================================================

    // ROTACIONAR O SLIME PARA O PLAYER (apenas flip visual)
    private void FacePlayer()
    {
        if (player == null) return;

        if (player.position.x > transform.position.x)
            spriteTransform.localScale = new Vector3(-originalScale.x, originalScale.y, originalScale.z);
        else
            spriteTransform.localScale = new Vector3(originalScale.x, originalScale.y, originalScale.z);
    }

    // =========================================================
    //  DETECÇÃO DO PLAYER
    // =========================================================

    void DetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectRadius, playerLayer);

        if (hit != null)
            player = hit.transform;
        else
            player = null;
    }


    // =========================================================
    //  CHASE / LÓGICA DE PERSEGUIÇÃO E ATAQUE
    // =========================================================

    void HandleChase()
    {
        float distance = Vector2.Distance(transform.position, player.position);

        // 🟩 ADIÇÃO — virar para o player antes de decidir atacar ou andar
        FacePlayer();

        if (distance <= stopDistance)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            bool canAttack = Time.time >= lastAttackTime + attackCooldown;

            if (canAttack)
            {
                SetTransition(State.Attack);

                // 🟩 ADIÇÃO — virar novamente no momento do ataque
                FacePlayer();
            }
            else
            {
                SetTransition(State.Idle);
            }

            return;
        }

        Vector2 dir = (player.position - transform.position).normalized;

        if (rb != null)
            rb.linearVelocity = dir * moveSpeed;

        SetTransition(State.Move);
    }

    // =========================================================
    //  PATHFINDING
    // =========================================================

    void HandlePathfinding()
    {
        if (waitingAtPoint)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            SetTransition(State.Idle);
            return;
        }

        if (pathPoints == null || pathPoints.Length == 0)
            return;

        Transform target = pathPoints[currentPathIndex];
        float dist = Vector2.Distance(transform.position, target.position);

        if (dist < 0.1f)
        {
            StartCoroutine(WaitAtPoint());
            return;
        }

        Vector2 dir = (target.position - transform.position).normalized;

        if (rb != null)
            rb.linearVelocity = dir * moveSpeed;

        SetTransition(State.Move);
    }

    IEnumerator WaitAtPoint()
    {
        waitingAtPoint = true;
        SetTransition(State.Idle);

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(waitTime);

        currentPathIndex++;
        if (currentPathIndex >= pathPoints.Length)
            currentPathIndex = 0;

        waitingAtPoint = false;
    }

    // =========================================================
    //  ATAQUE — PULO EM ARCO
    // =========================================================

    public void BeginAttackJump()
    {
        if (player == null || isJumping)
            return;

        lastAttackTime = Time.time;

        attackStartPos = transform.position;

        // 🟩 ADIÇÃO — sempre virar para o player no início do ataque
        FacePlayer();

        Vector2 dir = (player.position - transform.position).normalized;
        Vector2 target = attackStartPos + dir * attackJumpDistance;

        StartCoroutine(JumpArc(target, jumpDuration));
    }

    public void ReturnFromAttack()
    {
        if (isJumping)
        {
            pendingReturn = true;
            return;
        }

        StartCoroutine(JumpArc(attackStartPos, returnDuration));
    }

    IEnumerator JumpArc(Vector2 targetPos, float duration)
    {
        isJumping = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        Vector2 startPos = transform.position;
        float time = 0f;

        while (time < duration)
        {
            float t = time / duration;
            float height = Mathf.Sin(t * Mathf.PI) * jumpHeight;

            Vector2 pos = Vector2.Lerp(startPos, targetPos, t);
            pos.y += height;

            transform.position = pos;

            time += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        isJumping = false;

        if (pendingReturn)
        {
            pendingReturn = false;
            StartCoroutine(JumpArc(attackStartPos, returnDuration));
        }
    }

    // =========================================================
    //  CONTROLE DO ANIMATOR
    // =========================================================

    void SetTransition(State newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        anim.SetInteger("transition", (int)newState);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
