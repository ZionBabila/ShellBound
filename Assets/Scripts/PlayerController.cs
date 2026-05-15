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
    public LayerMask shellLayer;

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
                return;
            }
            else if (currentShell.CurrentState == ShellState.InUse)
            {
                return; // The player is using the shell, can't throw it right now
            }
            else
            {
                // TRAP FIX: If you accidentally dragged the shell into the Inspector, it silently blocked pickup. 
                // This clears it automatically.
                currentShell = null;
            }
        }

        // 2. Pick up a nearby shell from the ground
        float facingMul = facingRight ? 1f : -1f;
        Vector2 localOffset = new Vector2(interactCenterOffset.x * facingMul, interactCenterOffset.y);
        Vector2 checkPosition = (Vector2)transform.TransformPoint(localOffset);

        // הכרחת המנוע למצוא טריגרים - קריטי כי הקונכיות מוגדרות כ-Trigger כשהן על הרצפה!
        bool originalHitTriggers = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;
        
        // ניסיון חיפוש ראשון לפי השכבה המוגדרת
        Collider2D[] colliders = Physics2D.OverlapCircleAll(checkPosition, interactRadius, shellLayer);

        // אם לא נמצא כלום, רשת ביטחון: חיפוש בכל השכבות כדי לעקוף בעיות של הגדרות בעורך
        if (colliders.Length == 0)
        {
            colliders = Physics2D.OverlapCircleAll(checkPosition, interactRadius, ~0);
        }

        Physics2D.queriesHitTriggers = originalHitTriggers;

        foreach (Collider2D col in colliders)
        {
            if (col.gameObject == gameObject) continue; // מתעלם מעצמנו

            Shell foundShell = col.GetComponentInParent<Shell>();
            if (foundShell == null) foundShell = col.GetComponentInChildren<Shell>();

            if (foundShell != null && foundShell.CurrentState == ShellState.OnGround)
            {
                currentShell = foundShell;
                
                currentShell.OnCollect(this);
                Debug.Log($"<color=green>SUCCESS:</color> Grabbed {foundShell.gameObject.name} (Found on Layer: {LayerMask.LayerToName(col.gameObject.layer)})!");
                return;
            }
        }

        if (colliders.Length > 0)
            Debug.Log($"⚠️ Interact pressed. Found {colliders.Length} objects nearby, but none were a valid 'OnGround' Shell.");
        else
            Debug.Log($"⚠️ Interact pressed, but absolutely nothing was found within radius {interactRadius}.");
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

    private void TryStartGrab()
    {
        // Block grabbing while a shell has taken over physics (e.g. inside a RollingShell).
        if (currentShell != null && currentShell.CurrentState == ShellState.InUse) return;

        // Already holding something — ignore.
        if (grabJoint != null) return;

        float facingMul = facingRight ? 1f : -1f;
        
        // מתחילים ממרכז השחקן (כולל ההיסט לגובה)
        Vector2 origin = (Vector2)transform.TransformPoint(new Vector2(0, grabCheckOffset.y));
        // כיוון מדויק קדימה לפי לאן שהשחקן פונה
        Vector2 direction = transform.right * facingMul;

        // שימוש ב-CircleCast (קרן עבה) שפונה קדימה בלבד - מונע תפיסה מהגב
        RaycastHit2D hit = Physics2D.CircleCast(origin, grabCheckRadius, direction, grabCheckOffset.x, movableLayer);
        if (hit.collider == null) return;

        Rigidbody2D targetRb = hit.collider.attachedRigidbody;
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
        float facingMul = facingRight ? 1f : -1f;
        Vector2 localOffset = new Vector2(interactCenterOffset.x * facingMul, interactCenterOffset.y);
        Vector2 interactPos = (Vector2)transform.TransformPoint(localOffset);
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
        Vector2 origin = (Vector2)transform.TransformPoint(new Vector2(0, grabCheckOffset.y));
        Vector2 direction = transform.right * facingMul;
        Vector2 grabPos = origin + direction * grabCheckOffset.x;

        // The radius circle = the actual CircleCast sweep area end.
        Gizmos.DrawWireSphere(grabPos, grabCheckRadius);
        // Line from the player origin out to the probe shows the cast length/direction.
        Gizmos.DrawLine(origin, grabPos);
    }
}