using UnityEngine;

public class Archer : MonoBehaviour
{
    Animator animator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
        Debug.Log(animator);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey("w"))
        {
            animator.SetBool("Walk", true);
        }
        if (!Input.GetKey("w"))
        {
            animator.SetBool("Walk", false);
        }
        if (Input.GetKey("r"))
        {
            animator.SetBool("Run", true);
        }
        if (!Input.GetKey("r"))
        {
            animator.SetBool("Run", false);
        }
        if (Input.GetKey("a"))
        {
            animator.SetBool("Attack", true);

        }
        if (!Input.GetKey("a"))
        {
            animator.SetBool("Attack", false);
        }
        if (Input.GetKey("d"))
        {
            animator.SetBool("Death", true);
        }

    }
}
