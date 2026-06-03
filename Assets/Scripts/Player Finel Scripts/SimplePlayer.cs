using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SimplePlayer : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("The force applied to move the player.")]
    public float speed = 50f;
    
    [Tooltip("The maximum movement speed.")]
    public float maxSpeed = 8f;
    
    [Tooltip("How fast the player slows down when there is no input.")]
    public float deceleration = 10f;

    [Header("Visuals & Slopes")]
    [Tooltip("The child object containing the SpriteRenderer. This will rotate to match slopes.")]
    public Transform visualsRoot;
    
    [Tooltip("How fast the visuals rotate to align with the slope.")]
    public float rotationSpeed = 10f;

    [Header("Ground Detection")]
    [Tooltip("Offset from the player's center to start the ground check raycast.")]
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    
    public float groundCheckDistance = 0.6f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private PlayerInputHandler inputHandler;
    private float moveInputX;
    private Vector2 surfaceNormal = Vector2.up;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputHandler = GetComponent<PlayerInputHandler>();
        
        // אנחנו נועלים את סיבוב הפיזיקה כדי למנוע באגים של התהפכות.
        // ההטיה לשיפועים תתבצע רק ברמת התצוגה (visualsRoot)
        rb.freezeRotation = true; 
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleGroundDetection();
    }

    private void Update()
    {
        // משיכת ערך התנועה אוטומטית ממערכת הקלט הקיימת
        if (inputHandler != null)
        {
            moveInputX = inputHandler.MoveValue.x;
        }

        HandleVisualRotation();
    }

    private void HandleMovement()
    {
        // הפעלת כוח בסיסי לתנועה לפי בקשתך
        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            rb.AddForce(new Vector2(moveInputX * speed, 0f), ForceMode2D.Force);
        }
        else
        {
            // עצירה חלקה כשאין קלט מהשחקן
            Vector2 vel = rb.linearVelocity;
            vel.x = Mathf.Lerp(vel.x, 0, Time.fixedDeltaTime * deceleration);
            rb.linearVelocity = vel;
        }

        // הגבלת מהירות התנועה כדי שהשחקן לא יאיץ עד אינסוף
        if (Mathf.Abs(rb.linearVelocity.x) > maxSpeed)
        {
            rb.linearVelocity = new Vector2(Mathf.Sign(rb.linearVelocity.x) * maxSpeed, rb.linearVelocity.y);
        }
    }

    private void HandleGroundDetection()
    {
        // קרן (Raycast) פשוטה לבדיקת זווית הרצפה שתחת השחקן
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        RaycastHit2D hit = Physics2D.Raycast(checkPos, Vector2.down, groundCheckDistance, groundLayer);

        if (hit.collider != null)
        {
            surfaceNormal = hit.normal;
        }
        else
        {
            surfaceNormal = Vector2.up;
        }
    }

    private void HandleVisualRotation()
    {
        if (visualsRoot == null) return;

        // חישוב זווית המטרה לפי הנורמל (השיפוע) שגילינו מהקרן
        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
        
        // סיבוב חלק של הספרייט כדי למנוע "קפיצות" חדות בתפרים בין רצפות
        visualsRoot.rotation = Quaternion.Lerp(visualsRoot.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        
        // הפיכת הספרייט (Flip) שמאלה או ימינה לפי כיוון ההליכה
        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            Vector3 scale = visualsRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(moveInputX);
            visualsRoot.localScale = scale;
        }
    }

    private void OnDrawGizmos()
    {
        // ציור הקרן בעורך כדי שיהיה קל לכוון את ההיסט (Offset) והמרחק
        Gizmos.color = Color.red;
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawLine(checkPos, checkPos + Vector2.down * groundCheckDistance);
    }
}