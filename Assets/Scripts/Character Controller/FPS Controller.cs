using UnityEngine;

public class FPSController : MonoBehaviour
{
    public float xSens;
    public float ySens;
    public float Speed;
    public float fastSpeed;
    public float yRotation;
    public float xRotation;
    public Rigidbody rb;

    public bool physics = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        rb = this.GetComponent<Rigidbody>();
        if (!physics)
        {
            rb.useGravity = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.deltaTime < (1f/60f))
        {
            rotation();
            move();
        }
    }
    public void move()
    {
        float currspeed = Speed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currspeed = fastSpeed;
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            rb.AddForce(new Vector3(0,20,0),ForceMode.Impulse);
        }
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            rb.AddForce(new Vector3(0, -20, 0), ForceMode.Impulse);
        }
        float xmove = Input.GetAxisRaw("Horizontal");
        float ymove = Input.GetAxisRaw("Vertical");
        Vector2 moveDir = new Vector2(xmove, ymove).normalized;
        Vector3 moveSpeed = (transform.forward * moveDir.y * currspeed) + (transform.right * moveDir.x * currspeed);
        if (physics)
        {
            rb.linearVelocity = new Vector3(moveSpeed.x, rb.linearVelocity.y, moveSpeed.z);
        }
        else
        {
            transform.position += moveSpeed * Time.deltaTime;
        }

    }
    public void rotation()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * xSens;
        float mouseY = Input.GetAxisRaw("Mouse Y") * ySens;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
    }
}
