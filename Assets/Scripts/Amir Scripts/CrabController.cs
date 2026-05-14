using UnityEngine;

[RequireComponent(typeof(PlayerInputHandler))]
public class CrabController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 300f; // מהירות סיבוב במעלות לשנייה
    public LayerMask walkableLayer;

    [Header("Raycast Settings")]
    public float rayLength = 1.5f;
    public float distanceFromCenter = 0.5f; // המרחק בין שתי הקרניים (רוחב הסרטן)
    public float rayOffsetY = 0.5f; // הגבהת נקודת מוצא הקרן כדי שלא תתקע בתוך הרצפה

    private PlayerInputHandler inputHandler;
    private Rigidbody2D rb;
    private bool isGrounded;
    private Vector2 averageNormal;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputHandler = GetComponent<PlayerInputHandler>();
        rb.gravityScale = 0;
    }

    void FixedUpdate()
    {
        UpdateSurfaceInfo();
        HandleMovement();
    }

    void UpdateSurfaceInfo()
    {
        // נקודות המוצא של שתי הקרניים (ימין ושמאל)
        Vector3 leftOrigin = transform.position + (transform.up * rayOffsetY) - (transform.right * distanceFromCenter);
        Vector3 rightOrigin = transform.position + (transform.up * rayOffsetY) + (transform.right * distanceFromCenter);

        RaycastHit2D leftHit = Physics2D.Raycast(leftOrigin, -transform.up, rayLength, walkableLayer);
        RaycastHit2D rightHit = Physics2D.Raycast(rightOrigin, -transform.up, rayLength, walkableLayer);

        if (leftHit.collider != null && rightHit.collider != null)
        {
            isGrounded = true;
            // ממוצע ה-Normal של שני המשטחים (בדיוק כמו במאמר)
            averageNormal = (leftHit.normal + rightHit.normal).normalized;

            // סיבוב השחקן בצורה חלקה לעבר הממוצע
            float targetAngle = Mathf.Atan2(averageNormal.y, averageNormal.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

            // הצמדה למשטח - כוח משיכה מקומי
            rb.AddForce(-averageNormal * 15f);
        }
        else if (leftHit.collider != null || rightHit.collider != null)
        {
            // אם רק צד אחד נוגע, משתמשים ב-Normal שלו
            isGrounded = true;
            averageNormal = leftHit.collider != null ? leftHit.normal : rightHit.normal;
            float targetAngle = Mathf.Atan2(averageNormal.y, averageNormal.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, 0, targetAngle), rotationSpeed * Time.fixedDeltaTime);
            rb.AddForce(-averageNormal * 15f);
        }
        else
        {
            isGrounded = false;
            rb.AddForce(Vector2.down * 15f); // נפילה חופשית
        }
    }

    void HandleMovement()
    {
        float moveInput = inputHandler.MoveValue.x;
        if (isGrounded)
        {
            // תנועה לפי הכיוון שהסרטן פונה אליו כרגע
            rb.linearVelocity = transform.right * moveInput * moveSpeed;
        }
        else
        {
            rb.AddForce(new Vector2(moveInput * moveSpeed, 0));
        }
    }

    // ציור הקרניים לדיבאג ב-Editor
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 leftOrigin = transform.position + (transform.up * rayOffsetY) - (transform.right * distanceFromCenter);
        Vector3 rightOrigin = transform.position + (transform.up * rayOffsetY) + (transform.right * distanceFromCenter);
        Gizmos.DrawRay(leftOrigin, -transform.up * rayLength);
        Gizmos.DrawRay(rightOrigin, -transform.up * rayLength);
    }
}