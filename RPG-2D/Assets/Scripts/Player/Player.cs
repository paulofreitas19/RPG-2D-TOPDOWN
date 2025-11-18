using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Components")]

    private Rigidbody2D rig;


    [Header("Stats")]

    [SerializeField]private float speed;


    [Header("Control")]

    private Vector2 direction;


    [Header("Properties")]

    public Vector2 Direction
    {
        get { return direction; }
        set { direction = value; }
    }

    private void Awake()
    {
        rig = GetComponent<Rigidbody2D>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        OnDirectionInput();
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

    #endregion

}
