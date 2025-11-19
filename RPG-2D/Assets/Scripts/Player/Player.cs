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
    private bool isAttackBasic;

    #region Properties

    public Vector2 Direction
    {
        get { return direction; }
        set { direction = value; }
    }

    public bool IsAttackBasic
    {
        get { return isAttackBasic; }
        set { isAttackBasic = value; }
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
        OnAttackBasic();
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

    void OnAttackBasic()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isAttackBasic = true;
            speed = 0;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isAttackBasic = false;
            speed = initialSpeed;
        }
    }

    #endregion

}
