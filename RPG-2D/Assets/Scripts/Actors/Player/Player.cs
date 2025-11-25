using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Components")]

    private Rigidbody2D rig;


    [Header("Stats")]

    [SerializeField]private float speed;
    [SerializeField]private float initialSpeed;


    [Header("Control")]

    private Vector2 direction;
    private bool isBasicAttack;
    private bool isMagicAttack;

    #region Properties


    public Vector2 Direction
    {
        get { return direction; }
        set { direction = value; }
    }

    public bool IsBasicAttack
    {
        get { return isBasicAttack; }
        set { isBasicAttack = value; }
    }

    public bool IsMagicAttack
    {
        get { return isMagicAttack; }
        set { isMagicAttack = value; }
    }

    #endregion

    private void Awake()
    {
        rig = GetComponent<Rigidbody2D>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        initialSpeed = speed;
    }

    // Update is called once per frame
    void Update()
    {
        OnDirectionInput();
        OnBasicAttack();
        OnMagicAttack();
    }

    private void FixedUpdate()
    {
        OnMove();
    }

    #region Movement

    void OnDirectionInput()
    {
        direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    void OnMove()
    {
        rig.MovePosition(rig.position + direction * speed * Time.fixedDeltaTime);
    }

    void OnBasicAttack()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isBasicAttack = true;
            speed = 0;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isBasicAttack = false;
            speed = initialSpeed;
        }
    }

    void OnMagicAttack()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isMagicAttack = true;
            speed = 0;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isMagicAttack = false;
            speed = initialSpeed;
        }
    }

    #endregion

}
