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

    [Tooltip("The 'Socket' point on the player where the shell's anchor will attach")]
    public Vector2 shellMountOffset = new Vector2(0, 0.5f); // Player's attachment point

    public float speedMultiplier  = 1.0f;

    private Rigidbody2D rb;
    private PlayerInputHandler input;
    private float currentVelocityX;
    private float moveTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        input = GetComponent<PlayerInputHandler>();

        //rb.freezeRotation = true;

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
        HandleMovement();
    }

    private void HandleGroundCheck()
    {
        Vector2 checkPos = transform.position + transform.TransformDirection(groundCheckOffset);
        
        // Use CircleCast instead of OverlapCircle so we can extract the exact surface normal
        RaycastHit2D hit = Physics2D.CircleCast(checkPos, groundCheckRadius, - transform.up, 0.05f, groundLayer);
        
        if (hit.collider != null)
        {
            IsGrounded = true;
            Debug.DrawLine(IsGrounded ? (Vector2)transform.position : checkPos, hit.point, Color.green);
            SurfaceNormal = hit.normal; // Save the angle of the wall/floor/ceiling
        }
        else
        {
            IsGrounded = false;
            SurfaceNormal = Vector2.up; // Default to flat ground if in the air
        }
    }

private void HandleMovement()
    {
        // חסימת תנועה אם הקונכייה (כמו פחית) שולטת בשחקן
        if (currentShell != null && currentShell.CurrentState == ShellState.InUse)
        {
            moveTimer = 0f;
            currentVelocityX = 0f;
            return;
        }
        
        float moveInputX = input.MoveValue.x;
        
        // החלת ההאטה של הקונכייה על המהירות הסופית
        float actualBaseSpeed = moveSpeed * speedMultiplier;
        float actualMaxSpeed = maxSpeed * speedMultiplier;
        
        // החלת ההאטה גם על התאוצה כדי שהסרטן "יתאמץ" להתחיל ללכת
        float actualBaseResp = baseResponsiveness * speedMultiplier;
        float actualAccelRate = accelerationRate * speedMultiplier;

        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            moveTimer += Time.fixedDeltaTime;

            float targetSpeed = moveInputX * actualBaseSpeed;
            float currentAccel = actualBaseResp; // שימוש בתאוצה המושפעת מהשריון

            if (moveTimer >= accelerationDelay)
            {
                targetSpeed = moveInputX * actualMaxSpeed;
                currentAccel = actualAccelRate; // שימוש בהאצה המושפעת מהשריון
            }

            currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, currentAccel * Time.fixedDeltaTime);
        }
        else
        {
            moveTimer = 0f;
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, 0f, actualBaseResp * Time.fixedDeltaTime);
        }

        // יישום המהירות הליניארית כפי שביקשת
        rb.linearVelocity = new Vector2(currentVelocityX, rb.linearVelocity.y);

        if (moveInputX > 0 && !facingRight) Flip();
        else if (moveInputX < 0 && facingRight) Flip();
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
        // Throw equipped shell
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

        // Pick up nearby shell
        Vector2 checkPosition = (Vector2)transform.position + interactCenterOffset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(checkPosition, interactRadius);

        foreach (Collider2D hit in hits)
        {
            Shell foundShell = hit.GetComponent<Shell>();
            if (foundShell != null && foundShell.CurrentState == ShellState.OnGround)
            {
                currentShell = foundShell;

                Transform attachParent = visualsRoot != null ? visualsRoot : transform;

                // Pass both the parent and the player's socket location
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
        Vector2 groundCheckPos = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireSphere(groundCheckPos, groundCheckRadius);

        Gizmos.color = Color.blue;
        Vector2 interactPos = (Vector2)transform.position + interactCenterOffset;
        Gizmos.DrawWireSphere(interactPos, interactRadius);

        // Draw Player Socket (Red Cross/Sphere)
        Gizmos.color = Color.red;
        Vector2 mountPos = (Vector2)transform.position + shellMountOffset;
        Gizmos.DrawWireSphere(mountPos, 0.1f);
        Gizmos.DrawLine(mountPos + Vector2.left * 0.2f, mountPos + Vector2.right * 0.2f);
        Gizmos.DrawLine(mountPos + Vector2.down * 0.2f, mountPos + Vector2.up * 0.2f);
    }
}