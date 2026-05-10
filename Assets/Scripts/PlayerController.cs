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
    public Vector2 shellMountOffset = new Vector2(0, 0.5f); 

    public float speedMultiplier  = 1.0f;

    [Header("Push & Pull Mechanics")]
    [Tooltip("The layer containing objects the crab can push/pull")]
    public LayerMask movableLayer; 
    [Tooltip("How far the crab can reach to grab an object")]
    public float grabDistance = 0.6f; 
    [Tooltip("Speed multiplier while dragging an object")]
    public float pushPullSpeedMultiplier = 0.5f; 
    
    // Grab state variables
    private bool isGrabbing = false;
    private FixedJoint2D grabJoint;

    private Rigidbody2D rb;
    private PlayerInputHandler input;
    private float currentVelocityX;
    private float moveTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        input = GetComponent<PlayerInputHandler>();

        if (input != null)
        {
            input.OnInteract += TryInteractShell;
            input.OnAbility += TryUseAbility;
        }
    }

    void OnDestroy()
    {
        if (input != null)
        {
            input.OnInteract -= TryInteractShell;
            input.OnAbility -= TryUseAbility;
        }
    }

    void Update()
    {
        HandleGroundCheck();
    }

    void FixedUpdate()
    {
        HandleGrabbing();
        HandleMovement();
    }

    private void HandleGroundCheck()
    {
        Vector2 checkPos = (Vector2)transform.position + (Vector2)transform.TransformDirection(groundCheckOffset);
        RaycastHit2D hit = Physics2D.CircleCast(checkPos, groundCheckRadius, -transform.up, 0.05f, groundLayer);
        
        if (hit.collider != null)
        {
            IsGrounded = true;
            SurfaceNormal = hit.normal; 
        }
        else
        {
            IsGrounded = false;
            SurfaceNormal = Vector2.up; 
        }
    }

    // --- NEW PUSH/PULL LOGIC ---
    private void HandleGrabbing()
    {
        // For simplicity, using LeftShift as the grab button. 
        // You can map this to your PlayerInputHandler later!
        bool isHoldingGrab = Input.GetKey(KeyCode.LeftShift);

        if (isHoldingGrab && !isGrabbing && IsGrounded)
        {
            // Calculate forward direction based on where the crab is facing and surface rotation
            Vector2 forwardDir = facingRight ? transform.TransformDirection(Vector2.right) : transform.TransformDirection(Vector2.left);
            Vector2 checkPos = (Vector2)transform.position + (Vector2)transform.TransformDirection(interactCenterOffset);
            
            // Cast a ray forward to detect movable objects
            RaycastHit2D hit = Physics2D.Raycast(checkPos, forwardDir, grabDistance, movableLayer);

            if (hit.collider != null && hit.rigidbody != null)
            {
                isGrabbing = true;
                
                // Dynamically add a joint to connect the player and the box
                grabJoint = gameObject.AddComponent<FixedJoint2D>();
                grabJoint.connectedBody = hit.rigidbody;
                
                Debug.Log($"<color=orange>✋ GRABBED:</color> Started pulling {hit.collider.gameObject.name}");
            }
        }
        else if (!isHoldingGrab && isGrabbing)
        {
            // Release the object
            isGrabbing = false;
            if (grabJoint != null)
            {
                Destroy(grabJoint);
                Debug.Log("<color=orange>✋ RELEASED:</color> Stopped pulling.");
            }
        }
    }

    private void HandleMovement()
    {
        if (currentShell != null && currentShell.CurrentState == ShellState.InUse)
        {
            moveTimer = 0f;
            currentVelocityX = 0f;
            return;
        }
        
        float moveInputX = input.MoveValue.x;
        
        // Calculate total speed multiplier (combines armor penalty AND dragging penalty)
        float totalMultiplier = speedMultiplier * (isGrabbing ? pushPullSpeedMultiplier : 1.0f);
        
        float actualBaseSpeed = moveSpeed * totalMultiplier;
        float actualBaseResp = baseResponsiveness * totalMultiplier;

        float targetSpeed = 0f;
        float currentAccel = actualBaseResp;

        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            moveTimer += Time.fixedDeltaTime;

            // Tank Logic / Dragging Logic (Disable running if heavy or pulling)
            if (totalMultiplier < 1.0f)
            {
                targetSpeed = moveInputX * actualBaseSpeed; 
                currentAccel = actualBaseResp;              
            }
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

            currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, currentAccel * Time.fixedDeltaTime);
        }
        else
        {
            moveTimer = 0f;
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, 0f, actualBaseResp * Time.fixedDeltaTime);
        }

        rb.linearVelocity = new Vector2(currentVelocityX, rb.linearVelocity.y);

        // --- PREVENT FLIPPING WHILE GRABBING ---
        // If we are holding a box, we want to walk backwards, not flip around!
        if (!isGrabbing)
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
        if (currentShell != null)
        {
            if (currentShell.CurrentState == ShellState.OnBack)
            {
                float throwDirX = facingRight ? 1f : -1f;
                Vector2 throwVelocity = new Vector2(throwDirX, 0.5f).normalized * 8f;
                currentShell.OnThrow(throwVelocity);
                currentShell = null;
            }
            return;
        }

        Vector2 checkPosition = (Vector2)transform.position + interactCenterOffset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(checkPosition, interactRadius);

        foreach (Collider2D hit in hits)
        {
            Shell foundShell = hit.GetComponent<Shell>();
            if (foundShell != null && foundShell.CurrentState == ShellState.OnGround)
            {
                currentShell = foundShell;
                Transform attachParent = visualsRoot != null ? visualsRoot : transform;
                currentShell.OnCollect(attachParent, shellMountOffset);
                break;
            }
        }
    }

    private void TryUseAbility()
    {
        if (currentShell != null && currentShell.CurrentState == ShellState.OnBack)
        {
            currentShell.OnActivate();
        }
        else if (currentShell != null && currentShell.CurrentState == ShellState.InUse)
        {
            currentShell.OnDeactivate();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = (Vector2)transform.position + (Vector2)transform.TransformDirection(groundCheckOffset);
        Gizmos.DrawWireSphere(groundCheckPos, groundCheckRadius);

        Gizmos.color = Color.blue;
        Vector2 interactPos = (Vector2)transform.position + interactCenterOffset;
        Gizmos.DrawWireSphere(interactPos, interactRadius);

        Gizmos.color = Color.red;
        Vector2 mountPos = (Vector2)transform.position + (Vector2)transform.TransformDirection(shellMountOffset);
        Gizmos.DrawWireSphere(mountPos, 0.1f);
        Gizmos.DrawLine(mountPos + Vector2.left * 0.2f, mountPos + Vector2.right * 0.2f);
        Gizmos.DrawLine(mountPos + Vector2.down * 0.2f, mountPos + Vector2.up * 0.2f);

        // --- NEW: Draw Grab Raycast (Orange) ---
        Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
        Vector2 forwardDir = facingRight ? Vector2.right : Vector2.left;
        Gizmos.DrawLine(interactPos, interactPos + forwardDir * grabDistance);
    }
}