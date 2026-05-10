using UnityEngine;

public class CrabController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public LayerMask walkableLayer; // כאן נבחר את ה-Layer של המשטחים הדביקים

    [Header("Physics Settings")]
    public float rayDistance = 1.5f; // אורך הקרן שבודקת רצפה
    public float stickyForce = 10f; // הכוח שמצמיד אותו למשטח

    private Rigidbody2D rb;
    private bool isGrounded;
    private Vector2 surfaceNormal;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // אנחנו מבטלים את כוח המשיכה הרגיל כי אנחנו בונים אחד "מגנטי" משלנו
        rb.gravityScale = 0;
    }

    void FixedUpdate()
    {
        HandleSurfaceAlignment();
        HandleMovement();
    }

    void HandleSurfaceAlignment()
    {
        // ירידת קרן למטה (לכיוון ה"בטן" של הסרטן)
        RaycastHit2D hit = Physics2D.Raycast(transform.position, -transform.up, rayDistance, walkableLayer);

        if (hit.collider != null)
        {
            isGrounded = true;
            surfaceNormal = hit.normal;

            // 1. סיבוב הסרטן כך שיהיה מקביל למשטח
            float targetRotation = Mathf.Atan2(surfaceNormal.y, surfaceNormal.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetQuat = Quaternion.Euler(0, 0, targetRotation);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetQuat, Time.deltaTime * rotationSpeed);

            // 2. הצמדת הסרטן למשטח (כוח משיכה עצמאי)
            rb.AddForce(-surfaceNormal * stickyForce);
        }
        else
        {
            isGrounded = false;
            // אם הוא באוויר, אפשר להחזיר כוח משיכה עולמי רגיל
            rb.AddForce(Vector2.down * 9.81f);
        }
    }

    void HandleMovement()
    {
        float moveInput = Input.GetAxis("Horizontal");

        if (isGrounded)
        {
            // תנועה יחסית לזווית של המשטח
            Vector2 moveDirection = new Vector2(surfaceNormal.y, -surfaceNormal.x);
            rb.linearVelocity = moveDirection * moveInput * moveSpeed;
        }
    }

    // ציור הקרן ב-Editor כדי שתוכלו לראות ולכוון
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, -transform.up * rayDistance);
    }
}