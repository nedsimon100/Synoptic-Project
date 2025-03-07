using UnityEngine;

public class Debuggingmovement : MonoBehaviour
{
    public Rigidbody2D rb;
    public float baseSpeed;
    public float sprintSpeed;
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            rb.linearVelocity = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")).normalized * sprintSpeed;
        }
        else
        {
            rb.linearVelocity = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")).normalized * baseSpeed;
        }
    }
}
