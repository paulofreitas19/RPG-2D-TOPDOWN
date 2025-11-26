using UnityEngine;
using System.Collections.Generic;

public class Slime : MonoBehaviour
{
    [Header("Components")]
    private Animator anim;
    private Rigidbody2D rig;

    [Header("Stats")]
    [SerializeField] private float health;
    [SerializeField] private float speed;
    [SerializeField] private float initialSpeed;
    [SerializeField] private int index;

    public List<Transform> paths = new List<Transform>();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        anim = GetComponent<Animator>();
        initialSpeed = speed;
    }

    // Update is called once per frame
    void Update()
    {
        OnMove();
    }

    void OnMove()
    {
        transform.position = Vector2.MoveTowards(transform.position, paths[index].position, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, paths[index].position) < 0.1)
        {
            if (index < paths.Count - 1)
            {
                //index++;
                index = Random.Range(0, paths.Count - 1);
            }

            else
            {
                index = 0;
            }
        }

        Vector2 direction = paths[index].position - transform.position;

        if (direction.x > 0)
        {
            transform.eulerAngles = new Vector2(0, 0);
        }

        if (direction.x < 0)
        {
            transform.eulerAngles = new Vector2(0, 180);
        }
    }

    void OnAttack()
    {

    }

    void OnHit()
    {

    }

}
