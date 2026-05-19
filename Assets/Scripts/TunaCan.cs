using UnityEngine;

public class TunaCan : Shell
{
    [Header("Attachment Settings")]
    [Tooltip("Control exactly where the can sits on the crab's back offset (inherited from Shell.anchorOffset)")]
    // Note: TunaCan uses the base anchorOffset for attachment

    [Header("Tuna Can Physics (Hamster Ball)")]
    public Vector2 playerInsideOffset = new Vector2(0, 0); 
    
    [Header("Sprites & Rotation")]
    public Sprite canSideSprite;
    public Sprite canTopSprite;

    [Header("Organic Physics Control")]
    [Tooltip("כוח הסיבוב הטהור שמופעל על הפחית. החיכוך עם הרצפה יהפוך את זה לתנועה.")]
    public float torqueForce = 25f;
    [Tooltip("בלימה טבעית (חיכוך זוויתי) כשעוזבים את המקשים.")]
    public float angularDragWhenStopping = 3f;

    [Header("Hamster Ball Environment")]
    public LayerMask groundLayer;
    public float jumpForce = 7f;
    private float lastJumpTime;

    private PlayerInputHandler input;

    protected override void Awake()
    {
        base.Awake();
        if (groundLayer.value == 0)
        {
            Debug.LogWarning("<color=orange>🥫 TUNA CAN WARNING:</color> Ground Layer is 'Nothing'! Ground detection and Roll Assist won't work.");
        }
    }

    private void Update()
    {
        // ביטלנו את החלפת הספרייטים (HandleSpriteRotation) בזמן התגלגלות.
        // פחית שמתגלגלת כמו גלגל צריכה פשוט להסתובב פיזית בצורה חלקה עם הספרייט העגול שלה,
        // ולא להחליף תמונות כל 45 מעלות (מה שיצר תחושה של חוסר סנכרון).
    }

    private void FixedUpdate()
    {
        if (currentState == ShellState.InUse)
        {
            ApplyRollingPhysics();
        }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate(); // Handles OnBack alignment automatically
        
        // 1. מצב התגלגלות (InUse): סנכרון מיקום השחקן לתוך הפחית
        if (currentState == ShellState.InUse && playerInside != null)
        {
            SyncPlayerPosition();
        }
    }

    public override void OnCollect(PlayerController player)
    {
        base.OnCollect(player);
        
        if (playerInside != null)
        {
            input = playerInside.GetComponent<PlayerInputHandler>();
        }
    }

    public override void OnActivate()
    {
        currentState = ShellState.InUse;
        
        // שחרור הפחית למרחב
        transform.SetParent(null);
        transform.localScale = originalScale; 
        transform.rotation = Quaternion.identity; 
        
        // הפעלת הפיזיקה של הפחית
        rb.bodyType = RigidbodyType2D.Dynamic;
        shellCollider.enabled = true;
        shellCollider.isTrigger = false;
        gameObject.layer = LayerMask.NameToLayer("ShellActive"); //[cite: 1]

        if (playerInside != null)
        {
            Collider2D[] playerCols = playerInside.GetComponents<Collider2D>();
            foreach (var col in playerCols) col.enabled = false;

            Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.bodyType = RigidbodyType2D.Kinematic;
                playerRb.linearVelocity = Vector2.zero;
            }

            // סנכרון ראשוני
            SyncPlayerPosition();

            // הפיכת הפחית לתצוגה העגולה (Top) כדי שתתגלגל חלק כמו כדור אוגר
            if (spriteRenderer != null && canTopSprite != null)
            {
                spriteRenderer.sprite = canTopSprite;
            }
        }

        if (input != null) input.OnInteract += CheckThrowInput;
        
        Debug.Log("<color=green>🥫 TUNA CAN ACTIVE:</color> Physics ready!");
    }

    public override void OnDeactivate()
    {
        currentState = ShellState.OnBack;
        
        // כיבוי הפיזיקה של הפחית
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        shellCollider.enabled = false;
        shellCollider.isTrigger = true;
        gameObject.layer = LayerMask.NameToLayer("ShellOnPlayer"); //[cite: 1]

        if (playerInside != null)
        {
            RestorePlayerPhysics();
            Transform attachParent = playerInside.visualsRoot != null ? playerInside.visualsRoot : playerInside.transform;
            transform.SetParent(attachParent);
            UpdateAttachmentTransform(playerInside);
        }

        // חזרה לתצוגת הצד כשהיא על הגב או נזרקת
        if (spriteRenderer != null && canSideSprite != null)
        {
            spriteRenderer.sprite = canSideSprite;
        }

        if (input != null) input.OnInteract -= CheckThrowInput;
    }

    private void RestorePlayerPhysics()
    {
        if (playerInside == null) return;
        Collider2D[] playerCols = playerInside.GetComponents<Collider2D>();
        foreach (var col in playerCols) col.enabled = true;

        Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
        if (playerRb != null) playerRb.bodyType = RigidbodyType2D.Dynamic;
    }

    private void ApplyRollingPhysics()
    {
        if (input == null || rb == null) return;

        float moveInputX = input.MoveValue.x;
        float moveInputY = input.MoveValue.y;

        // 1. חיישן רצפה נועד כעת נטו בשביל לאפשר קפיצה או בלימה (התנועה עצמה קורית מפיזיקה טהורה)
        bool isGrounded = false;
        Vector2 groundNormal = Vector2.up;
        float radius = 0.5f; // הגנת רדיוס למקרה שהקוליידר חסר
        if (shellCollider != null)
        {
            radius = shellCollider.bounds.extents.y;
            
            // יורים קרן מעגלית (CircleCast) קצת מעל תחתית הפחית כלפי מטה כדי למצוא את המשטח המדויק
            Vector2 origin = (Vector2)transform.position + new Vector2(0, -radius + 0.2f);
            RaycastHit2D hit = Physics2D.CircleCast(origin, 0.15f, Vector2.down, 0.3f, groundLayer);
            
            if (hit.collider != null)
            {
                isGrounded = true;
                groundNormal = hit.normal;
            }
        }

        // 2. קפיצה (כפתור למעלה - W או חץ עליון)
        bool jumpedThisFrame = false;
        if (isGrounded && moveInputY > 0.5f && Time.time > lastJumpTime + 0.3f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            lastJumpTime = Time.time;
            jumpedThisFrame = true;
            isGrounded = false; // מנתקים מהשיפוע בפריים הזה כדי שהקפיצה תתבצע למעלה
        }

        // 3. תנועה אורגנית מבוססת מומנט סיבוב (Torque) וחיכוך בלבד!
        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            rb.angularDamping = 0.05f; // התנגדות מינימלית בזמן תנועה
            // הוספת סיבוב - מינוס כי חץ ימינה אומר סיבוב עם כיוון השעון
            rb.AddTorque(-moveInputX * torqueForce * rb.mass); 
        }
        else if (isGrounded)
        {
            // כשהשחקן עוזב את המקשים, אנחנו מעלים את ה"חיכוך הזוויתי" כדי שהפחית תבלום בצורה טבעית
            rb.angularDamping = angularDragWhenStopping;
        }
    }

    private void SyncPlayerPosition()
    {
        // 1. מיקום השחקן במרכז הפחית בצורה חלקה
        playerInside.transform.position = transform.position + (Vector3)playerInsideOffset;
        
        // 2. שמירת השחקן זקוף תמיד ביחס למשטח (כפי שנדרש במסמך העיצוב)
        playerInside.transform.rotation = Quaternion.FromToRotation(Vector3.up, playerInside.SurfaceNormal); //[cite: 1]
    }

    private void CheckThrowInput()
    {
        if (currentState == ShellState.InUse && playerInside != null)
        {
            if (input != null) input.OnInteract -= CheckThrowInput;

            Vector2 throwMomentum = rb.linearVelocity;
            
            RestorePlayerPhysics();
            
            playerInside.transform.position += Vector3.up * 0.15f;

            playerInside.currentShell = null;

            OnThrow(throwMomentum);
        }
    }

    private void OnDestroy()
    {
        // הגנה קריטית: אם הפחית מושמדת תוך כדי שימוש, חובה להחזיר לשחקן קוליידרים ופיזיקה!
        if (currentState == ShellState.InUse)
        {
            RestorePlayerPhysics();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // ציור החיישן ביוניטי כדי שתוכל לראות בעיניים שהעיגול באמת נוגע ברצפה
        if (shellCollider != null)
        {
            Gizmos.color = Color.cyan;
            float radius = shellCollider.bounds.extents.y;
            Vector2 origin = (Vector2)transform.position + new Vector2(0, -radius + 0.2f);
            Gizmos.DrawWireSphere(origin, 0.15f);
            // ציור הקרן של החיישן
            Gizmos.DrawLine(origin, origin + Vector2.down * 0.3f);
        }
    }
}