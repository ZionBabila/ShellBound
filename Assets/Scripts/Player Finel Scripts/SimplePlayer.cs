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
    [Tooltip("The main empty GameObject containing the rig. Used for slope rotation and flipping.")]
    public Transform visualsRoot;

    [Tooltip("How fast the visuals rotate to align with the slope.")]
    public float rotationSpeed = 10f;

    [Header("Push / Pull (Movable)")]
    [Tooltip("Items on this layer can be grabbed with Ctrl for push/pull.")]
    public LayerMask movableLayer;

    [Tooltip("Local-space offset from the player toward the facing direction where the grab probe is placed.")]
    public Vector2 grabCheckOffset = new Vector2(0.5f, 0.0f);

    [Tooltip("Radius of the grab probe used to find a Movable in front of the crab.")]
    public float grabCheckRadius = 0.4f;

    [Tooltip("Objects with mass greater than this are considered 'heavy' and require a special shell.")]
    public float heavyMassThreshold = 3.0f;

    [Header("Push / Pull Weight Mapping")]
    [Tooltip("The maximum mass an object can have for the weight calculation (e.g., 10).")]
    public float maxPushMass = 10f;

    [Tooltip("Curve defining speed slowdown. X axis: Mass ratio (0 to 1). Y axis: Speed multiplier (0 to 1). Default gives gentle slowdown for mass 1-3, heavy slowdown at 10.")]
    public AnimationCurve pushSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1f),     // Mass 0: 100% speed
        new Keyframe(0.3f, 0.85f), // Mass 3: 85% speed (Gentle slowdown)
        new Keyframe(0.7f, 0.4f),  // Mass 7: 40% speed (Starts getting very heavy)
        new Keyframe(1f, 0.1f)     // Mass 10: 10% speed (Barely moves)
    );


    [Header("Components")]
    [Tooltip("The regular collider used for walking without a shell.")]
    public Collider2D standingCollider;

    [Tooltip("The collider used when carrying a shell on the back. Taller/Wider so it gets stuck in narrow paths.")]
    public Collider2D withShellCollider;

    [Tooltip("The round collider used for rolling. MUST be on this main GameObject.")]
    public Collider2D rollingCollider;

    [Header("Debug")]
    [Tooltip("Print movement forces and speeds to the console.")]
    public bool showSpeedDebug = false;

    [Header("Ground Detection")]
    [Tooltip("Offset from the player's center to start the ground check raycast.")]
    public Vector2 groundCheckOffset = new Vector2(0, 0f); // Start from player center to avoid getting stuck in slopes
    
    [Tooltip("Distance between the parallel raycasts (should match the collider's width).")]
    public float groundCheckWidth = 0.5f;

    public float groundCheckDistance = 1.0f; // Lengthen raycast to reach the ground safely
    public LayerMask groundLayer;
    
    private Rigidbody2D rb;
    private PlayerInputHandler inputHandler;
    private float moveInputX;
    private Vector2 surfaceNormal = Vector2.up;

    private FixedJoint2D grabJoint;

    [HideInInspector] public float currentSpeedMultiplier = 1f;

    [SerializeField, HideInInspector] 
    private bool _isMovementDisabled = false;

    public bool isMovementDisabled 
    { 
        get => _isMovementDisabled; 
        set 
        { 
            _isMovementDisabled = value; 
            if (value) ReleaseGrab(); // Safety check (Code Review 1.2)
        } 
    }

    // Public read-only properties (for animation system and other scripts)
    // Ignoring micro-movements so the animator gets a "clean" 0 when the player barely moves
    public float CurrentSpeed => Mathf.Abs(rb.linearVelocity.x) < 0.15f ? 0f : Mathf.Abs(rb.linearVelocity.x);
    public bool IsGrounded { get; private set; }
    
    // Property to be controlled by shells
    public bool CanPushHeavyObjects { get; set; } = false;
    public Rigidbody2D Rb => rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputHandler = GetComponent<PlayerInputHandler>();
        
        if (inputHandler != null)
        {
            inputHandler.OnGrabStart += TryStartGrab;
            inputHandler.OnGrabEnd += ReleaseGrab;
        }

        // Lock physics rotation to prevent flipping bugs.
        // All rotation is handled purely visually via visualsRoot.
        rb.freezeRotation = true; 
    }

    private void OnDestroy()
    {
        if (inputHandler != null)
        {
            inputHandler.OnGrabStart -= TryStartGrab;
            inputHandler.OnGrabEnd -= ReleaseGrab;
        }
    }

    private void FixedUpdate()
    {
        if (!isMovementDisabled)
        {
            HandleMovement();
        }
        HandleGroundDetection();
    }

    private void Update()
    {
        // Pull movement value automatically from the existing input system
        if (inputHandler != null)
        {
            moveInputX = isMovementDisabled ? 0f : inputHandler.MoveValue.x;
        }

        if (!isMovementDisabled)
        {
            HandleVisualRotation();
        }
    }

    private void HandleMovement()
    {
        // 1. Calculate the base maximum speed considering the equipped shell (e.g. Heavy Armor slows us down)
        float actualMaxSpeed = maxSpeed * currentSpeedMultiplier;

        // 2. Check the mass of the grabbed object and reduce speed accordingly
        if (grabJoint != null && grabJoint.connectedBody != null)
        {
            float massRatio = Mathf.Clamp01(grabJoint.connectedBody.mass / maxPushMass);
            float dynamicGrabMultiplier = pushSpeedCurve.Evaluate(massRatio);
            actualMaxSpeed *= dynamicGrabMultiplier;
        }

        // 3. Handle Movement strictly through linearVelocity
        Vector2 vel = rb.linearVelocity;

        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            // Accelerate towards target speed using 'speed' as the acceleration rate
            float targetSpeedX = moveInputX * actualMaxSpeed;
            float currentSpeedX = Mathf.MoveTowards(vel.x, targetSpeedX, speed * Time.fixedDeltaTime);

            // Adjust velocity along slopes if grounded to prevent bouncing
            if (IsGrounded && surfaceNormal != Vector2.up)
            {
                Vector2 moveDirection = new Vector2(surfaceNormal.y, -surfaceNormal.x).normalized;
                vel = moveDirection * currentSpeedX;
            }
            else
            {
                vel.x = currentSpeedX;
            }

            if (showSpeedDebug)
            {
                Debug.Log($"<color=green>Current Vel X:</color> {vel.x:F2} | <color=yellow>Target Max Speed:</color> {actualMaxSpeed:F2}");
            }
        }
        else
        {
            vel.x = Mathf.Lerp(vel.x, 0, Time.fixedDeltaTime * deceleration);
            
            if (Mathf.Abs(vel.x) < 0.05f) vel.x = 0f;
        }

        rb.linearVelocity = vel;
    }

    private void HandleGroundDetection()
    {
        Vector2 centerPos = (Vector2)transform.position + groundCheckOffset;
        Vector2 leftPos = centerPos + Vector2.left * (groundCheckWidth / 2f);
        Vector2 rightPos = centerPos + Vector2.right * (groundCheckWidth / 2f);

        // Array of origins: Center, Left, Right
        Vector2[] origins = { centerPos, leftPos, rightPos };
        
        bool foundGround = false;
        Vector2 combinedNormal = Vector2.zero;
        int hitCount = 0;

        foreach (Vector2 origin in origins)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, groundCheckDistance, groundLayer);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider.isTrigger || hit.collider.gameObject == gameObject) continue;
                
                combinedNormal += hit.normal;
                foundGround = true;
                hitCount++;
                break; // Only care about the first valid hit for this specific ray
            }
        }

        IsGrounded = foundGround;

        if (foundGround)
        {
            // Average the normals to create a smooth transition over small bumps and seams
            surfaceNormal = (combinedNormal / hitCount).normalized;
        }
        else
        {
            surfaceNormal = Vector2.up;
        }
    }

    private void HandleVisualRotation()
    {
        if (visualsRoot == null) return;

        // Calculate target rotation based on the normal (slope) discovered by the raycast
        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
        
        // Smoothly rotate the sprite to prevent sharp "jumps" at floor seams
        visualsRoot.rotation = Quaternion.Lerp(visualsRoot.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        
        // Flip visuals left or right
        // We freeze flipping while grabbing so the crab doesn't spin around when pulling backwards
        if (Mathf.Abs(moveInputX) > 0.01f && visualsRoot != null && grabJoint == null)
        {
            Vector3 scale = visualsRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(moveInputX);
            visualsRoot.localScale = scale;
        }
    }

    private void TryStartGrab()
    {
        if (isMovementDisabled || grabJoint != null) return;

        float facingMul = (visualsRoot != null && visualsRoot.localScale.x < 0) ? -1f : 1f;
        Vector2 origin = (Vector2)transform.position + new Vector2(0, grabCheckOffset.y);
        Vector2 direction = Vector2.right * facingMul;

        // Dynamically increase reach if the player is wearing a shell (which has a wider collider)
        float castDistance = grabCheckOffset.x;
        if (withShellCollider != null && withShellCollider.enabled)
        {
            castDistance += 0.5f;
        }

        RaycastHit2D hit = Physics2D.CircleCast(origin, grabCheckRadius, direction, castDistance, movableLayer);
        if (hit.collider == null) return;

        Rigidbody2D targetRb = hit.collider.attachedRigidbody;
        if (targetRb == null || targetRb.bodyType == RigidbodyType2D.Static) return;

        grabJoint = gameObject.AddComponent<FixedJoint2D>();
        grabJoint.connectedBody = targetRb;
        grabJoint.autoConfigureConnectedAnchor = true;
        grabJoint.enableCollision = true;
        grabJoint.breakForce = Mathf.Infinity;
        grabJoint.breakTorque = Mathf.Infinity;
    }

    public void ReleaseGrab()
    {
        if (grabJoint == null) return;
        Destroy(grabJoint);
        grabJoint = null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        PlayerShellSystem shellSystem = GetComponent<PlayerShellSystem>();
        if (shellSystem != null && shellSystem.CurrentShell != null)
        {
            shellSystem.CurrentShell.OnPlayerCollision(collision);
        }
    }

    private void OnDrawGizmos()
    {
        // Draw the raycasts in the editor to easily adjust the offset, distance, and width
        Gizmos.color = Color.red;
        Vector2 centerPos = (Vector2)transform.position + groundCheckOffset;
        Vector2 leftPos = centerPos + Vector2.left * (groundCheckWidth / 2f);
        Vector2 rightPos = centerPos + Vector2.right * (groundCheckWidth / 2f);

        Gizmos.DrawLine(centerPos, centerPos + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(leftPos, leftPos + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(rightPos, rightPos + Vector2.down * groundCheckDistance);

        // Draw Grab Probe (Yellow = Free, Magenta = Grabbing)
        Gizmos.color = grabJoint != null ? Color.magenta : Color.yellow;
        float facingMul = (visualsRoot != null && visualsRoot.localScale.x < 0) ? -1f : 1f;
        Vector2 grabOrigin = (Vector2)transform.position + new Vector2(0, grabCheckOffset.y);
        Vector2 grabDir = Vector2.right * facingMul;
        Vector2 grabPos = grabOrigin + grabDir * grabCheckOffset.x;

        Gizmos.DrawWireSphere(grabPos, grabCheckRadius);
        Gizmos.DrawLine(grabOrigin, grabPos);
    }
}