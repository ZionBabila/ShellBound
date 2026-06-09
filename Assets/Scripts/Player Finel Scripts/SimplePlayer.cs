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
    public Vector2 groundCheckOffset = new Vector2(0, 0f); // Start from player center to avoid getting stuck in slopes
    
    public float groundCheckDistance = 1.0f; // Lengthen raycast to reach the ground safely
    public LayerMask groundLayer;
    
    [Header("Colliders")]
    [Tooltip("The regular collider used for walking (e.g., Capsule or Box).")]
    public Collider2D standingCollider;
    [Tooltip("The round collider used for rolling.")]
    public Collider2D rollingCollider;

    // TODO: Handle sprite rotation in the future when the player goes down a slope (currently the raycast might miss the ground and straighten the player)
    // TODO: Handle jumps/stutters when Raycast hits vertical seams between adjacent colliders.

    private Rigidbody2D rb;
    private PlayerInputHandler inputHandler;
    private float moveInputX;
    private Vector2 surfaceNormal = Vector2.up;

    // Public read-only properties (for animation system and other scripts)
    // Ignoring micro-movements so the animator gets a "clean" 0 when the player barely moves
    public float CurrentSpeed => Mathf.Abs(rb.linearVelocity.x) < 0.15f ? 0f : Mathf.Abs(rb.linearVelocity.x);
    public bool IsGrounded { get; private set; }

    // מאפיינים ציבוריים לקונכיות שרוצות להשתלט על הפיזיקה (כמו קונכיית גלגול)
    [HideInInspector] public bool isPhysicsOverridden = false;
    public Rigidbody2D Rb => rb;
    public PlayerInputHandler Input => inputHandler;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputHandler = GetComponent<PlayerInputHandler>();
        
        if (standingCollider == null) standingCollider = GetComponent<Collider2D>();
        if (rollingCollider != null) rollingCollider.enabled = false; // מוודאים שהעגול כבוי בהתחלה
        
        // Lock physics rotation to prevent flipping bugs.
        // Slope tilting will be handled only visually (visualsRoot)
        rb.freezeRotation = true; 
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleGroundDetection();
    }

    private void Update()
    {
        // Pull movement value automatically from the existing input system
        if (inputHandler != null)
        {
            moveInputX = inputHandler.MoveValue.x;
        }

        HandleVisualRotation();
    }

    private void HandleMovement()
    {
        // ביטול ההליכה הרגילה אם קונכייה השתלטה על הפיזיקה
        if (isPhysicsOverridden) return;

        // Apply basic movement force
        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            // Calculate Tangent to the slope angle to move the player parallel to the ground
            // This prevents "getting stuck" into the slope and creates smoother, more organic movement
            Vector2 moveDirection = new Vector2(surfaceNormal.y, -surfaceNormal.x).normalized;
            
            rb.AddForce(moveDirection * (moveInputX * speed), ForceMode2D.Force);
        }
        else
        {
            // Smooth stop when there is no player input
            Vector2 vel = rb.linearVelocity;
            vel.x = Mathf.Lerp(vel.x, 0, Time.fixedDeltaTime * deceleration);
            
            // Snap to 0 to prevent infinite Lerp trails and small slides on slopes
            if (Mathf.Abs(vel.x) < 0.05f) vel.x = 0f;
            
            rb.linearVelocity = vel;
        }

        // Limit movement speed so the player doesn't accelerate infinitely
        if (Mathf.Abs(rb.linearVelocity.x) > maxSpeed)
        {
            rb.linearVelocity = new Vector2(Mathf.Sign(rb.linearVelocity.x) * maxSpeed, rb.linearVelocity.y);
        }
    }

    private void HandleGroundDetection()
    {
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        // Switch to using RaycastAll to filter triggers and self-collisions
        RaycastHit2D[] hits = Physics2D.RaycastAll(checkPos, Vector2.down, groundCheckDistance, groundLayer);
        
        bool foundGround = false;
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.isTrigger || hit.collider.gameObject == gameObject) continue;
            
            surfaceNormal = hit.normal;
            foundGround = true;
            break;
        }

        IsGrounded = foundGround;

        if (!foundGround)
        {
            surfaceNormal = Vector2.up;
        }
    }

    private void HandleVisualRotation()
    {
        // Disable manual rotation and flipping if physics is overriding the player
        if (isPhysicsOverridden) return;

        if (visualsRoot == null) return;

        // Calculate target rotation based on the normal (slope) discovered by the raycast
        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
        
        // Smoothly rotate the sprite to prevent sharp "jumps" at floor seams
        visualsRoot.rotation = Quaternion.Lerp(visualsRoot.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        
        // Flip the sprite left or right based on walking direction
        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            Vector3 scale = visualsRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(moveInputX);
            visualsRoot.localScale = scale;
        }
    }

    private void OnDrawGizmos()
    {
        // Draw the raycast in the editor to easily adjust the offset and distance
        Gizmos.color = Color.red;
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawLine(checkPos, checkPos + Vector2.down * groundCheckDistance);

        // 🛠️ דיבאג: ציור נקודת מרכז המסה (Center of Mass) כדי לעזור לאפס את קוליידר הגלגול והריג
        Rigidbody2D editorRb = GetComponent<Rigidbody2D>();
        if (editorRb != null)
        {
            Gizmos.color = Color.yellow;
            Vector2 centerOfMassWorld = transform.TransformPoint(editorRb.centerOfMass);
            Gizmos.DrawWireSphere(centerOfMassWorld, 0.15f);
            // קו קטן כדי לראות את כיוון הסיבוב הפיזיקלי
            Gizmos.DrawLine(centerOfMassWorld, centerOfMassWorld + (Vector2)(transform.up * 0.3f));
        }
    }
}