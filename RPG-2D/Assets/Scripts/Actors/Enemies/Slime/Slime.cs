using System.Collections;
using UnityEngine;

public class Slime : MonoBehaviour
{
    [Header("Components")]
    private Animator anim;
    private Rigidbody2D rb;

    [Header("Player Detect")]
    public float detectRadius = 4f;
    public float stopDistance = 1.5f;
    public LayerMask playerLayer;
    private Transform player;

    [Header("Movement")]
    public float moveSpeed = 1.2f;
    public Transform[] pathPoints;
    private int currentPathIndex = 0;
    private bool waitingAtPoint = false;
    public float waitTime = 2f;

    private enum State { Idle = 0, Move = 1, Attack = 2 }
    private State currentState = State.Idle;

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        DetectPlayer();

        if (player != null)
        {
            HandleChase();
            return;
        }

        HandlePathfinding();
    }

    // =============================================
    // PLAYER DETECTION
    // =============================================
    void DetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectRadius, playerLayer);

        if (hit != null)
        {
            player = hit.transform;
        }
        else
        {
            player = null;
        }
    }

    // =============================================
    // CHASING PLAYER
    // =============================================
    void HandleChase()
    {
        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= stopDistance)
        {
            SetTransition(State.Attack);
            transform.localScale = new Vector2(2, 2);
            rb.linearVelocity = Vector2.zero;
            return;
        }

        else
        {
            transform.localScale = new Vector2(1, 1);
        }

        // Move towards player
        Vector2 dir = (player.position - transform.position).normalized;
        rb.linearVelocity = dir * moveSpeed;

        SetTransition(State.Move);
    }

    // =============================================
    // PATHFINDING WITH WAITING
    // =============================================
    void HandlePathfinding()
    {
        if (waitingAtPoint)
        {
            rb.linearVelocity = Vector2.zero;
            SetTransition(State.Idle);
            return;
        }

        if (pathPoints.Length == 0) return;

        Transform target = pathPoints[currentPathIndex];
        float dist = Vector2.Distance(transform.position, target.position);

        if (dist < 0.1f)
        {
            StartCoroutine(WaitAtPoint());
            return;
        }

        Vector2 dir = (target.position - transform.position).normalized;
        rb.linearVelocity = dir * moveSpeed;

        SetTransition(State.Move);
    }

    IEnumerator WaitAtPoint()
    {
        waitingAtPoint = true;
        SetTransition(State.Idle);
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(waitTime);

        currentPathIndex++;
        if (currentPathIndex >= pathPoints.Length)
            currentPathIndex = 0;

        waitingAtPoint = false;
    }

    // =============================================
    // ANIMATION SWITCH
    // =============================================
    void SetTransition(State newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        anim.SetInteger("transition", (int)newState);
    }

    // =============================================
    // VISUAL DEBUG
    // =============================================
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
