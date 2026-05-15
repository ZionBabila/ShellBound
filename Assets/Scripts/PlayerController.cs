using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerController : MonoBehaviour
{
    [Header("Basic Movement")]
    public float moveSpeed = 4.0f;
    public float maxSpeed = 7.5f;
    public float accelerationDelay = 1.0f;
    public float accelerationRate = 2.0f;
    public float baseResponsiveness = 20.0f;
    
    public Vector2 SurfaceNormal { get; private set; } = Vector2.up;
    
    [Header("Ground Detection")]
    public Vector2 groundCheckOffset = new Vector2(0, -0.2f);
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    
    public bool IsGrounded { get; private set; }

    [Header("Visuals (PSB & Bones)")]
    public Transform visualsRoot;
    public bool facingRight = true;

    [Header("Shell System & Interaction")]
    public Shell currentShell;
    public Vector2 interactCenterOffset = new Vector2(0, 0);
    public float interactRadius = 1.5f;

    [Header("Throw Settings")]
    public Vector2 throwDirection = new Vector2(1f, 0.5f);
    public float throwForce = 8f;

    [Tooltip("The 'Socket' point on the player where the shell's anchor will attach")]
    public Vector2 shellMountOffset = new Vector2(0, 0.5f); // Player's attachment point

    // This variable is modified by shells (like ArmorShell) to slow the player down
    public float speedMultiplier  = 1.0f;

    [Header("Push / Pull (Movable)")]
    [Tooltip("Items on this layer can be grabbed with Ctrl for push/pull.")]
    public LayerMask movableLayer;

    [Tooltip("Local-space offset from the player toward the facing direction where the grab probe is placed. X is mirrored when the crab faces left.")]
    public Vector2 grabCheckOffset = new Vector2(0.5f, 0.0f);

    [Tooltip("Radius of the grab probe used to find a Movable in front of the crab.")]
    public float grabCheckRadius = 0.4f;

    // Active joint while the player is holding a Movable. Null when nothing is grabbed.
    private FixedJoint2D grabJoint;

    private Rigidbody2D rb;
    private PlayerInputHandler input;
    private float currentVelocityX;
    private float moveTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        input = GetComponent<PlayerInputHandler>();

        // Subscribe to input events
        if (input != null)
        {
            input.OnInteract += TryInteractShell;
            input.OnAbility += TryUseAbility;
            input.OnGrabStart += TryStartGrab;
            input.OnGrabEnd += ReleaseGrab;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from input events to prevent memory leaks
        if (input != null)
        {
            input.OnInteract -= TryInteractShell;
            input.OnAbility -= TryUseAbility;
            input.OnGrabStart -= TryStartGrab;
            input.OnGrabEnd -= ReleaseGrab;
        }
    }

    void Update()
    {
        HandleGroundCheck();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleGroundCheck()
    {
        // Calculate the check position relative to the crab's rotation
        Vector2 checkPos = (Vector2)transform.position + (Vector2)transform.TransformDirection(groundCheckOffset);
        
        // Use CircleCast to detect the ground and extract the exact surface normal
        RaycastHit2D hit = Physics2D.CircleCast(checkPos, groundCheckRadius, -transform.up, 0.05f, groundLayer);
        
        if (hit.collider != null)
        {
            IsGrounded = true;
            Debug.DrawLine(checkPos, hit.point, Color.green);
            SurfaceNormal = hit.normal; // Save the angle of the surface
        }
        else
        {
            IsGrounded = false;
            SurfaceNormal = Vector2.up; // Default to flat ground if in the air
        }
    }

    private void HandleMovement()
    {
        // Block normal movement if a shell (like Tuna Can) takes over physics
        if (currentShell != null && currentShell.CurrentState == ShellState.InUse)
        {
            moveTimer = 0f;
            currentVelocityX = 0f;
            return;
        }
        
        float moveInputX = input.MoveValue.x;
        
        // Base speeds affected by the shell's weight penalty
        float actualBaseSpeed = moveSpeed * speedMultiplier;
        float actualBaseResp = baseResponsiveness * speedMultiplier;

        float targetSpeed = 0f;
        float currentAccel = actualBaseResp;

        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            moveTimer += Time.fixedDeltaTime;

            // TANK LOGIC: If wearing heavy armor (multiplier < 1), disable running completely.
            if (speedMultiplier < 1.0f)
            {
                targetSpeed = moveInputX * actualBaseSpeed; // Locked to slow walk speed
                currentAccel = actualBaseResp;              // Slower, heavier acceleration
            }
            // NORMAL LOGIC: Start walking, then accelerate to max sprint speed.
            else
            {
                if (moveTimer >= accelerationDelay)
                {
                    targetSpeed = moveInputX * maxSpeed;
                    currentAccel = accelerationRate;
                }
                else
                {
                    targetSpeed = moveInputX * moveSpeed;
                    currentAccel = baseResponsiveness;
                }
            }

            // Smoothly transition current velocity towards the target speed
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, currentAccel * Time.fixedDeltaTime);
        }
        else
        {
            // Decelerate smoothly when no input is pressed
            moveTimer = 0f;
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, 0f, actualBaseResp * Time.fixedDeltaTime);
        }

        // Apply the calculated horizontal velocity while keeping the current vertical velocity (gravity)
        rb.linearVelocity = new Vector2(currentVelocityX, rb.linearVelocity.y);

        // Handle visual sprite flipping — but freeze facing while grabbing a Movable so
        // pulling backward doesn't make the crab spin around to face the box.
        if (grabJoint == null)
        {
            if (moveInputX > 0 && !facingRight) Flip();
            else if (moveInputX < 0 && facingRight) Flip();
        }
    }

    private void Flip()
    {
        facingRight = !facingRight;

        if (visualsRoot != null)
        {
            Vector3 scale = visualsRoot.localScale;
            scale.x *= -1;
            visualsRoot.localScale = scale;
        }
    }

    private void TryInteractShell()
    {
        // 1. Throw currently equipped shell
        if (currentShell != null)
        {
            if (currentShell.CurrentState == ShellState.OnBack)
            {
                float throwDirX = facingRight ? 1f : -1f;
                Vector2 throwVelocity = new Vector2(throwDirX * throwDirection.x, throwDirection.y).normalized * throwForce;

                currentShell.OnThrow(throwVelocity);
                currentShell = null;
            }
            return;
        }

        // 2. Pick up a nearby shell from the ground
        float facingMul = facingRight ? 1f : -1f;
        Vector2 checkPosition = (Vector2)transform.position + new Vector2(interactCenterOffset.x * facingMul, interactCenterOffset.y);

        // Use ContactFilter2D to GUARANTEE we detect Triggers, regardless of Unity's Project Settings
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        
        Collider2D[] hits = new Collider2D[10];
        int hitCount = Physics2D.OverlapCircle(checkPosition, interactRadius, filter, hits);

        bool foundAnyShell = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            // Updated to catch colliders on child objects
            Shell foundShell = hit.GetComponentInParent<Shell>();
            if (foundShell != null && foundShell.CurrentState == ShellState.OnGround)
            {
                foundAnyShell = true;
                currentShell = foundShell;
                Transform attachParent = visualsRoot != null ? visualsRoot : transform;
                
                // Pass the attach parent and the exact socket offset
                currentShell.OnCollect(attachParent, shellMountOffset);
                break;
            }
        }

        if (!foundAnyShell && hitCount > 0)
        {
            Debug.Log($"⚠️ Interact pressed. Found {hitCount} colliders, but none were a valid Shell in 'OnGround' state.");
        }
        else if (!foundAnyShell)
        {
            Debug.Log("⚠️ Interact pressed, but no colliders were within the interact radius.");
        }
    }

    private void TryUseAbility()
    {
        // Toggle the unique ability of the equipped shell
        if (currentShell != null && currentShell.CurrentState == ShellState.OnBack)
        {
            currentShell.OnActivate();
        }
        else if (currentShell != null && currentShell.CurrentState == ShellState.InUse)
        {
            currentShell.OnDeactivate();
        }
    }

    private Vector2 GetGrabProbePosition()
    {
        // Mirror the X offset so the probe sits in front of whichever direction the crab is facing.
        float facingMul = facingRight ? 1f : -1f;
        Vector2 localOffset = new Vector2(grabCheckOffset.x * facingMul, grabCheckOffset.y);
        return (Vector2)transform.TransformPoint(localOffset);
    }

    private void TryStartGrab()
    {
        // Block grabbing while a shell has taken over physics (e.g. inside a RollingShell).
        if (currentShell != null && currentShell.CurrentState == ShellState.InUse) return;

        // Already holding something — ignore.
        if (grabJoint != null) return;

        Vector2 probePos = GetGrabProbePosition();
        Collider2D hit = Physics2D.OverlapCircle(probePos, grabCheckRadius, movableLayer);
        if (hit == null) return;

        Rigidbody2D targetRb = hit.attachedRigidbody;
        // Need a non-static Rigidbody2D so the joint can actually move it.
        if (targetRb == null || targetRb.bodyType == RigidbodyType2D.Static) return;

        grabJoint = gameObject.AddComponent<FixedJoint2D>();
        grabJoint.connectedBody = targetRb;
        grabJoint.autoConfigureConnectedAnchor = true;
        grabJoint.enableCollision = true;
        // Let the joint break if something extreme happens, but keep the threshold high for normal play.
        grabJoint.breakForce = Mathf.Infinity;
        grabJoint.breakTorque = Mathf.Infinity;
    }

    private void ReleaseGrab()
    {
        if (grabJoint == null) return;
        Destroy(grabJoint);
        grabJoint = null;
    }

    private void OnDrawGizmosSelected()
    {
        // Draw Ground Check (Green)
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireSphere(groundCheckPos, groundCheckRadius);

        // Draw Interact Radius (Blue)
        Gizmos.color = Color.blue;
        Vector2 interactPos = (Vector2)transform.TransformPoint(interactCenterOffset);
        Gizmos.DrawWireSphere(interactPos, interactRadius);

        // Draw Player Socket / Mount Point (Red Cross)
        Gizmos.color = Color.red;
        Vector2 mountPos = (Vector2)transform.TransformPoint(shellMountOffset);
        Gizmos.DrawWireSphere(mountPos, 0.1f);
        Gizmos.DrawLine(mountPos + Vector2.left * 0.2f, mountPos + Vector2.right * 0.2f);
        Gizmos.DrawLine(mountPos + Vector2.down * 0.2f, mountPos + Vector2.up * 0.2f);
    }

    // Always-visible gizmos so the grab probe is easy to spot without selecting the player.
    private void OnDrawGizmos()
    {
        // Yellow when free, Magenta when actively holding something.
        Gizmos.color = grabJoint != null ? Color.magenta : Color.yellow;

        float facingMul = facingRight ? 1f : -1f;
        Vector2 grabLocal = new Vector2(grabCheckOffset.x * facingMul, grabCheckOffset.y);
        Vector2 grabPos = (Vector2)transform.TransformPoint(grabLocal);

        // The radius circle = the actual OverlapCircle area.
        Gizmos.DrawWireSphere(grabPos, grabCheckRadius);
        // Line from the player origin out to the probe shows the offset length/direction.
        Gizmos.DrawLine(transform.position, grabPos);
    }
}