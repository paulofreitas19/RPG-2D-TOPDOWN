using UnityEngine;
using System.Collections;

public class PlayerAnim : MonoBehaviour
{
    [Header("Components")]
    private Animator anim;
    private Player player;

    [Header("Idle Timer Settings")]
    [SerializeField]private float idleTimer = 0f;
    private float idleInterval = 3.5f;
    private bool isIdlePlaying = false;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        player = GetComponentInParent<Player>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        HandleMovement();
        HandleIdleTimer();
    }

    #region Movement

    void HandleMovement()
    {
        if (player.Direction.sqrMagnitude > 0.01f)
        {
            anim.SetInteger("transition", 2);

            idleTimer = 0f;
            isIdlePlaying = false;
        }
        else
        {
            // Só seta idle-padrão se NÃO estiver rodando a animação especial
            if (!isIdlePlaying)
            {
                anim.SetInteger("transition", 0);
            }
        }

        // Flip
        if (player.Direction.x > 0)
            transform.eulerAngles = new Vector2(0, 180);

        if (player.Direction.x < 0)
            transform.eulerAngles = new Vector2(0, 0);
    }


    void HandleIdleTimer()
    {
        // Se mover → não conta
        if (player.Direction.sqrMagnitude > 0.01f)
            return;

        // Se a animação idle especial estiver tocando, não contar tempo
        if (isIdlePlaying)
            return;

        // Contando o tempo parado
        idleTimer += Time.deltaTime;

        if (idleTimer >= idleInterval)
        {
            anim.SetInteger("transition", 1);
            isIdlePlaying = true;
            idleTimer = 0f; // zera para reiniciar completamente o ciclo
        }
    }
    public void IdleAnimationFinished()
    {
        isIdlePlaying = false;
    }

    IEnumerator ResetIdleState()
    {
        // Espera o tempo da animação idle (ajuste conforme duração real)
        yield return new WaitForSeconds(3.321f);

        isIdlePlaying = false;
    }


    #endregion

}
