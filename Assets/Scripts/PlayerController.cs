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
    [Tooltip("המרחק שהקרן בודקת כלפי מטה. בשיפועים צריך מרחק גדול יותר כי הפיזיקה ישרה.")]
    public float groundCheckDistance = 0.4f;
    
    public bool IsGrounded { get; private set; }

    [Header("Physics Stability")]
    [Tooltip("המהירות שבה התמונה של הסרטן מסתובבת כדי להתאים לשיפוע הרצפה.")]
    public float visualRotationSpeed = 15f;

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

    // הערה: speedMultiplier הוסר מכאן כי עכשיו הוא מחושב דינמית מתוך הקונכייה

    [Header("Push / Pull (Movable)")]
    [Tooltip("Items on this layer can be grabbed with Ctrl for push/pull.")]
    public LayerMask movableLayer;

    [Tooltip("Local-space offset from the player toward the facing direction where the grab probe is placed. X is mirrored when the crab faces left.")]
    public Vector2 grabCheckOffset = new Vector2(0.5f, 0.0f);

    [Tooltip("Radius of the grab probe used to find a Movable in front of the crab.")]
    public float grabCheckRadius = 0.4f;

    [Tooltip("The maximum mass of an object the player can push/pull normally.")]
    public float baseMaxPushMass = 3.0f;
    [HideInInspector] public float currentMaxPushMass;

    // Active joint while the player is holding a Movable. Null when nothing is grabbed.
    private FixedJoint2D grabJoint;

    private Rigidbody2D rb;
    private PlayerInputHandler input;
    private float currentVelocityX;
    private float moveTimer;
    private bool wasGrounded;

    void Awake()
    {
        currentMaxPushMass = baseMaxPushMass; // מתחילים עם יכולת הדחיפה הבסיסית
        rb = GetComponent<Rigidbody2D>();
        input = GetComponent<PlayerInputHandler>();

        // נועלים את סיבוב הפיזיקה לחלוטין - הסרטן לעולם לא יתהפך פיזית
        rb.constraints |= RigidbodyConstraints2D.FreezeRotation;

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
        
        // סיבוב ויזואלי בלבד (הפיזיקה נשארת ישרה)
        if (visualsRoot != null)
        {
            // מחשבים את הזווית הרצויה לפי השיפוע של הרצפה
            Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, SurfaceNormal);
            // מסובבים בהדרגתיות כדי שזה ייראה חלק וטבעי
            visualsRoot.rotation = Quaternion.Lerp(visualsRoot.rotation, targetRotation, Time.deltaTime * visualRotationSpeed);
        }
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleGroundCheck()
    {
        // מחשבים את נקודת תחילת הקרן
        Vector2 checkPos = (Vector2)transform.position + (Vector2)transform.TransformDirection(groundCheckOffset);
        
        // יורים קרן (Raycast) כלפי מטה, ובודקים את כל האובייקטים שפגענו בהם
        RaycastHit2D[] hits = Physics2D.RaycastAll(checkPos, Vector2.down, groundCheckDistance);
        
        bool foundGround = false;

        foreach (RaycastHit2D hit in hits)
        {
            // מתעלמים מהשחקן עצמו, מטריגרים, ומהקונכייה שעל הגב
            if (hit.collider.gameObject == gameObject || hit.collider.isTrigger) continue;
            if (currentShell != null && hit.collider.gameObject == currentShell.gameObject) continue;

            IsGrounded = true;
            SurfaceNormal = hit.normal; // שמירת זווית השיפוע
            foundGround = true;
            break; // מצאנו רצפה ולידית, אין צורך להמשיך לבדוק
        }

        if (!foundGround)
        {
            IsGrounded = false;
            SurfaceNormal = Vector2.up; // ברירת מחדל כשאנחנו באוויר
        }

        wasGrounded = IsGrounded;
    }

    private void HandleMovement()
    {
        // Block normal movement if a shell (like Tuna Can) takes over physics
        if (currentShell != null && currentShell.CurrentState == ShellState.InUse)
        {
            moveTimer = 0f;
            // קריטי: שומרים את המהירות הנוכחית של הפיזיקה כדי שכשנחזור לשלוט, לא נעצור באוויר במקום!
            currentVelocityX = rb.linearVelocity.x;
            return;
        }
        
        float moveInputX = input.MoveValue.x;
        
        // Base speeds affected by the shell's weight penalty
        float currentSpeedMultiplier = currentShell != null ? currentShell.MovementSpeedMultiplier : 1.0f;
        float actualBaseSpeed = moveSpeed * currentSpeedMultiplier;
        float actualMaxSpeed = maxSpeed * currentSpeedMultiplier;
        float actualBaseResp = baseResponsiveness * currentSpeedMultiplier;
        float actualAccel = accelerationRate * currentSpeedMultiplier;

        float targetSpeed = 0f;
        float currentAccel = actualBaseResp;

        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            moveTimer += Time.fixedDeltaTime;

            if (moveTimer >= accelerationDelay)
            {
                targetSpeed = moveInputX * actualMaxSpeed;
                currentAccel = actualAccel;
            }
            else
            {
                targetSpeed = moveInputX * actualBaseSpeed;
                currentAccel = actualBaseResp;
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

        // בדיקת מסה - האם האובייקט כבד מדי?
        if (targetRb.mass > currentMaxPushMass)
        {
            Debug.Log($"<color=orange>TOO HEAVY:</color> Object mass ({targetRb.mass}) exceeds player's push limit ({currentMaxPushMass}). You need heavy armor!");
            return;
        }

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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // העברת אירוע ההתנגשות לקונכייה (חשוב לקונכיות כמו השריון שמועכות חפצים)
        if (currentShell != null)
        {
            currentShell.OnPlayerCollisionEnter(collision);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw Ground Check (Line)
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawLine(groundCheckPos, groundCheckPos + Vector2.down * groundCheckDistance);

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