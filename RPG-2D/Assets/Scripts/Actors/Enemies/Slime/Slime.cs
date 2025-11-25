using UnityEngine;

public class Slime : MonoBehaviour
{
    [Header("Components")]
    private Rigidbody2D rig;

    [Header("Stats")]
    [SerializeField] private float health;
    [SerializeField] private float speed;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rig = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnMove()
    {

    }

    void OnAttack()
    {

    }

    void OnHit()
    {

    }

}
