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

    [Tooltip("Extra braking force applied when turning around, to prevent 'ice skating'. Higher values give a snappier feel.")]
    [Range(0f, 1f)]
    public float turnAroundBrake = 0.5f;

    [Header("Visuals & Slopes")]
    [Tooltip("The main empty GameObject containing the rig. Used for slope rotation and flipping.")]
    public Transform visualsRoot;

    [Tooltip("How fast the visuals rotate to align with the slope.")]
    public float rotationSpeed = 10f;

    [Header("Slope Handling")]
    [Tooltip("Slopes steeper than this angle (degrees) make the player slide down instead of gripping.")]
    public float maxSlopeAngle = 45f;

    [Tooltip("Downhill force applied while sliding on a steep slope.")]
    public float slideForce = 30f;

    [Tooltip("Maximum speed reached while sliding down a steep slope.")]
    public float maxSlideSpeed = 12f;

    [Header("Push / Pull (Movable)")]
    [Tooltip("Items on this layer can be grabbed with Ctrl for push/pull.")]
    public LayerMask movableLayer;

    [Tooltip("Local-space offset from the player toward the facing direction where the grab probe is placed.")]
    public Vector2 grabCheckOffset = new Vector2(0.5f, 0.0f);

    [Tooltip("Radius of the grab probe used to find a Movable in front of the crab.")]
    public float grabCheckRadius = 0.4f;

    [Tooltip("Objects with mass greater than this are considered 'heavy' and require a special shell.")]
    public float heavyMassThreshold = 3.0f;


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

    // Cached gravity so the grip logic can toggle it to 0 and restore it safely.
    private float defaultGravityScale = 1f;

    // Timer to temporarily disable aggressive braking for momentum preservation.
    private float preserveMomentumUntil = 0f;

    private TargetJoint2D grabJoint;
    private float grabDirection = 0f; // 0 = not grabbing, 1 = grabbed right, -1 = grabbed left

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
    public bool IsGrabbing => grabJoint != null;
    
    // Property to be controlled by shells
    public bool CanPushHeavyObjects { get; set; } = false;
    public Rigidbody2D Rb => rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravityScale = rb.gravityScale;
        inputHandler = GetComponent<PlayerInputHandler>();

        // Lock physics rotation to prevent flipping bugs.
        // All rotation is handled purely visually via visualsRoot.
        rb.freezeRotation = true; 
    }

    private void OnDestroy()
    {
        // Input event subscriptions removed as grab is now handled by the shell.
    }

    private void FixedUpdate()
    {
        if (!isMovementDisabled)
        {
            HandleMovement();
        }
        else
        {
            // While physics-controlled (rolling/hiding), never leave gravity zeroed by the grip logic.
            rb.gravityScale = defaultGravityScale;
        }

        if (IsGrabbing)
        {
            UpdateGrabJointTarget();
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
        // Measure how steep the current ground is (0 = flat, 90 = vertical wall)
        float slopeAngle = Vector2.Angle(surfaceNormal, Vector2.up);
        bool onSteepSlope = IsGrounded && slopeAngle > maxSlopeAngle;

        // CASE A: Steep slope -> lose grip and slide downhill, ignoring player input
        if (onSteepSlope)
        {
            rb.gravityScale = defaultGravityScale;

            // Tangent of the slope, forced to point downhill (negative Y)
            Vector2 downSlope = new Vector2(surfaceNormal.y, -surfaceNormal.x).normalized;
            if (downSlope.y > 0) downSlope = -downSlope;

            rb.AddForce(downSlope * slideForce, ForceMode2D.Force);

            // Cap the slide so it doesn't accelerate forever
            if (rb.linearVelocity.magnitude > maxSlideSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSlideSpeed;
            }

            if (showSpeedDebug)
            {
                Debug.Log($"<color=red>SLIDING</color> | Angle: {slopeAngle:F1} > Max: {maxSlopeAngle} | Speed: {rb.linearVelocity.magnitude:F2}");
            }
            return;
        }

        float actualMaxSpeed = maxSpeed * currentSpeedMultiplier;

        // CASE B: Player is actively moving on walkable ground
        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            // Normal gravity while moving so walking off ledges feels natural
            rb.gravityScale = defaultGravityScale;

            // Calculate movement direction (parallel to slope if grounded)
            Vector2 moveDirection = Vector2.right * Mathf.Sign(moveInputX);

            if (IsGrounded && surfaceNormal != Vector2.up)
            {
                // Calculate the tangent to the slope to push along the surface
                moveDirection = new Vector2(surfaceNormal.y, -surfaceNormal.x).normalized * Mathf.Sign(moveInputX);

                // Ensure the vector points in the intended horizontal direction
                if (moveInputX < 0 && moveDirection.x > 0) moveDirection *= -1;
                if (moveInputX > 0 && moveDirection.x < 0) moveDirection *= -1;
            }

            // --- Counter-Movement Force (Anti-Skating) ---
            // If the player is trying to move in the opposite direction of their current velocity,
            // apply a braking force to make the turn feel snappier.
            float currentVelocityX = rb.linearVelocity.x;
            if (Mathf.Sign(moveInputX) != Mathf.Sign(currentVelocityX) && Mathf.Abs(currentVelocityX) > 0.1f)
            {
                rb.AddForce(Vector2.right * -currentVelocityX * (speed * turnAroundBrake), ForceMode2D.Force);
            }

            // Apply physics force - this automatically handles the weight of pushed objects natively!
            rb.AddForce(moveDirection * speed, ForceMode2D.Force);


            // Cap the maximum horizontal speed so the player doesn't accelerate infinitely
            Vector2 vel = rb.linearVelocity;
            if (Mathf.Abs(vel.x) > actualMaxSpeed)
            {
                vel.x = Mathf.Sign(vel.x) * actualMaxSpeed;
                rb.linearVelocity = vel;
            }

            if (showSpeedDebug)
            {
                Debug.Log($"<color=green>Velocity X:</color> {rb.linearVelocity.x:F2} / <color=yellow>Max:</color> {actualMaxSpeed:F2} | <color=cyan>Force Applied:</color> {speed} | <color=orange>Shell Multiplier:</color> {currentSpeedMultiplier:F2}");
            }
        }
        // CASE C: No input while grounded on a walkable slope -> GRIP (no drift)
        else if (IsGrounded)
        {
            // If an external system requested to preserve momentum, skip the aggressive brake.
            if (Time.time < preserveMomentumUntil)
            {
                rb.gravityScale = defaultGravityScale; // Let natural friction and gravity take over
                return;
            }

            // Zero gravity removes the force that causes downhill sliding entirely
            rb.gravityScale = 0f;

            // Smoothly bleed off any leftover momentum so stopping still feels natural
            Vector2 vel = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * deceleration);
            if (vel.magnitude < 0.05f) vel = Vector2.zero;
            rb.linearVelocity = vel;
        }
        // CASE D: No input in the air -> normal gravity, just damp horizontal drift
        else
        {
            rb.gravityScale = defaultGravityScale;

            Vector2 vel = rb.linearVelocity;
            vel.x = Mathf.Lerp(vel.x, 0, Time.fixedDeltaTime * deceleration);
            if (Mathf.Abs(vel.x) < 0.05f) vel.x = 0;
            rb.linearVelocity = vel;
        }
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

        RaycastHit2D closestHit = new RaycastHit2D(); // Store the closest valid hit

        foreach (Vector2 origin in origins)
        {
            // Reverted to RaycastAll for robustness (it correctly filters out triggers).
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, groundCheckDistance, groundLayer);

            foreach (RaycastHit2D hit in hits)
            {
                // Ignore triggers and the player's own collider
                if (hit.collider.isTrigger || hit.collider.gameObject == gameObject) continue;

                // If this is the first valid ground we've found, or if this hit is closer
                // than the previous closest one, it becomes our new reference point.
                if (!foundGround || hit.distance < closestHit.distance)
                {
                    closestHit = hit;
                }

                foundGround = true; // We found at least one valid ground surface
            }
        }

        IsGrounded = foundGround;

        if (foundGround)
        {
            // Use the normal from ONLY the closest hit point for maximum stability.
            // This prevents averaging weird normals from collider seams.
            surfaceNormal = closestHit.normal;
        }
        else
        {
            surfaceNormal = Vector2.up;
        }
    }

    private void HandleVisualRotation()
    {
        if (visualsRoot == null) return;

        Quaternion targetRotation;

        if (IsGrabbing)
        {
            // When grabbing, force the player to be upright to prevent the joint from breaking.
            targetRotation = Quaternion.identity;
        }
        else
        {
            // When not grabbing, align to the slope as usual.
            targetRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
        }
        
        // Smoothly rotate the sprite to prevent sharp "jumps" at floor seams
        visualsRoot.localRotation = Quaternion.Lerp(visualsRoot.localRotation, targetRotation, Time.deltaTime * rotationSpeed);
        
        // Flip visuals left or right
        if (Mathf.Abs(moveInputX) > 0.01f && visualsRoot != null)
        {
            Vector3 scale = visualsRoot.localScale;
            float targetFacingDirection;

            if (IsGrabbing)
            {
                // When grabbing, logic is more complex: PUSH vs PULL
                // With TargetJoint2D, the joint is ON the object, so we get its position directly.
                // The connectedBody property is not used and will be null.
                float objectDirection = Mathf.Sign(grabJoint.transform.position.x - transform.position.x);
                float moveDirection = Mathf.Sign(moveInputX);

                // If moving TOWARDS the object (pushing), face the object.
                // If moving AWAY from the object (pulling), face away from it.
                if (moveDirection == objectDirection) // Pushing
                {
                    targetFacingDirection = objectDirection;
                }
                else // Pulling
                {
                    targetFacingDirection = moveDirection;
                }
            }
            else
            {
                targetFacingDirection = Mathf.Sign(moveInputX);
            }
            scale.x = Mathf.Abs(scale.x) * targetFacingDirection;
            visualsRoot.localScale = scale;
        }
    }

    public void PreserveMomentumFor(float duration)
    {
        preserveMomentumUntil = Time.time + duration;
    }

    public void ToggleGrab()
    {
        if (grabJoint != null) ReleaseGrab();
        else TryStartGrab();
    }

    public void TryStartGrab()
    {
        if (isMovementDisabled || grabJoint != null) return;
        
        Movable targetMovable = FindGrabbableObject();
        if (targetMovable != null)
        {
            Rigidbody2D targetRb = targetMovable.GetComponent<Rigidbody2D>();

            // Set grab direction on successful grab
            grabDirection = (visualsRoot != null && visualsRoot.localScale.x < 0) ? -1f : 1f;
            grabJoint = targetRb.gameObject.AddComponent<TargetJoint2D>();
            grabJoint.autoConfigureTarget = false; // We will set the target manually
            grabJoint.connectedBody = targetRb;
            grabJoint.anchor = targetRb.centerOfMass; // Grab the object from its center

            // --- CRITICAL FIX for TargetJoint2D strength ---
            // Set the spring-like properties of the joint so it's strong enough to pull the object.
            grabJoint.frequency = 10f; // How quickly the object tries to return to the target (spring stiffness)
            grabJoint.dampingRatio = 0.7f; // How much it resists overshooting (0=bouncy, 1=no bounce)

            // --- NEW: Instant Flip on Grab ---
            // Force the player to face the object they just grabbed.
            float objectDirection = Mathf.Sign(targetMovable.transform.position.x - transform.position.x);
            Vector3 scale = visualsRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * objectDirection;
            visualsRoot.localScale = scale;

            Debug.Log($"[SimplePlayer] Grabbed {targetMovable.name}");
        }
    }

    private void UpdateGrabJointTarget()
    {
        if (grabJoint == null) return;

        // The target for the joint is a point slightly in front of the player.
        // This makes the object follow the player smoothly.
        float facingMul = (visualsRoot.localScale.x > 0) ? 1f : -1f;
        Vector2 targetPos = (Vector2)transform.position + new Vector2(grabCheckOffset.x * facingMul, grabCheckOffset.y);
        grabJoint.target = targetPos;
    }

    public void ReleaseGrab()
    {
        if (grabJoint == null) return;

        // The joint is on the object, not the player, so we need to get it from there.
        Debug.Log($"[SimplePlayer] Released {grabJoint.gameObject.name}");

        Destroy(grabJoint);
        grabJoint = null;
        grabDirection = 0f; // Reset grab direction
    }

    /// <summary>
    /// Checks if a movable object is in range to be grabbed, without actually grabbing it.
    /// </summary>
    /// <returns>True if a valid, non-static movable object is in front of the player.</returns>
    public bool CanGrabObject()
    {
        return FindGrabbableObject() != null && grabJoint == null && !isMovementDisabled;
    }

    /// <summary>
    /// A simple, direct check for a grabbable object in front of the player.
    /// </summary>
    /// <returns>The Movable component if found, otherwise null.</returns>
    private Movable FindGrabbableObject()
    {
        // Temporarily enable trigger detection for the check
        bool originalQueriesHitTriggers = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;

        // float facingMul = (visualsRoot != null && visualsRoot.localScale.x < 0) ? -1f : 1f; // OLD: Always used current visual direction
        float facingMul;
        if (IsGrabbing)
        {
            // If we are already grabbing, the check MUST stay locked to the original grab direction.
            facingMul = grabDirection;
        }
        else
        {
            // If we are not grabbing, the check depends on the current visual facing direction.
            facingMul = (visualsRoot != null && visualsRoot.localScale.x < 0) ? -1f : 1f;
        }
        Vector2 checkCenter = (Vector2)transform.position + new Vector2(grabCheckOffset.x * facingMul, grabCheckOffset.y);

        // Find ALL colliders in a small circle in front of the player
        Collider2D[] hits = Physics2D.OverlapCircleAll(checkCenter, grabCheckRadius, movableLayer);

        // Restore original setting immediately
        Physics2D.queriesHitTriggers = originalQueriesHitTriggers;

        foreach (var hit in hits)
        {
            // Check if the found collider is a grab handle of a Movable object
            Movable movable = hit.GetComponentInParent<Movable>();
            if (movable != null && hit == movable.grabHandleTrigger)
            {
                return movable; // Found it!
            }
        }

        return null; // Nothing found
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
        // float facingMul = (visualsRoot != null && visualsRoot.localScale.x < 0) ? -1f : 1f; // OLD: Gizmo always followed visuals
        float facingMul;
        if (IsGrabbing)
        {
            // If grabbing, the gizmo MUST show the locked check direction.
            facingMul = grabDirection;
        }
        else
        {
            // If not grabbing, the gizmo follows the current visual direction.
            facingMul = (visualsRoot != null && visualsRoot.localScale.x < 0) ? -1f : 1f;
        }
        Vector2 grabOrigin = (Vector2)transform.position + new Vector2(0, grabCheckOffset.y);
        Vector2 grabDir = Vector2.right * facingMul;
        Vector2 grabPos = grabOrigin + grabDir * grabCheckOffset.x;

        Gizmos.DrawWireSphere(grabPos, grabCheckRadius);
        Gizmos.DrawLine(grabOrigin, grabPos);
    }
}