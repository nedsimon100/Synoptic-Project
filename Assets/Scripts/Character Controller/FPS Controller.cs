using UnityEngine;

public class FPSController : MonoBehaviour
{
    public float xSens;
    public float ySens;
    public float Speed;
    public float fastSpeed;
    public float yRotation;
    public float xRotation;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        float currspeed = Speed*Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currspeed = fastSpeed*currspeed;
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            transform.position = new Vector3(transform.position.x, 1000, transform.position.y);
        }
        float xmove = Input.GetAxisRaw("Horizontal");
        float ymove = Input.GetAxisRaw("Vertical");
        Vector2 moveDir = new Vector2(xmove, ymove).normalized;
        transform.position += (transform.forward * moveDir.y * currspeed)+(transform.right*moveDir.x*currspeed);

    }
    public void rotation()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * xSens;
        float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * ySens;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
    }
}
