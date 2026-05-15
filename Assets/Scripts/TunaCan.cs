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
    public float spriteAngleThreshold = 45.0f;

    [Header("Rolling Control")]
    [Tooltip("How strongly the player can steer the tuna can while rolling.")]
    public float rollAssistForce = 18f;

    [Tooltip("How much torque is applied from left/right input while rolling.")]
    public float torqueForce = 1500f; 
    public float maxAngularVelocity = 1200f; 

    [Header("Ricochet Settings")]
    public bool canRicochet = true;
    public float ricochetBounciness = 0.4f;

    private PlayerInputHandler input;

    private void Update()
    {
        if (currentState == ShellState.InUse && playerInside != null)
        {
            HandleSpriteRotation();
        }
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
        float deadzone = 0.1f;

        // Use a stronger steering assist so the can responds well to left/right input.
        if (Mathf.Abs(moveInputX) > deadzone)
        {
            rb.AddTorque(-moveInputX * torqueForce * rb.mass);
            rb.AddForce(new Vector2(moveInputX * rollAssistForce, 0f), ForceMode2D.Force);
        }
        else
        {
            // Apply a small damping when the player is not steering.
            rb.angularVelocity = Mathf.MoveTowards(rb.angularVelocity, 0f, 200f * Time.fixedDeltaTime);
        }

        if (Mathf.Abs(rb.angularVelocity) > maxAngularVelocity)
        {
            rb.angularVelocity = Mathf.Sign(rb.angularVelocity) * maxAngularVelocity;
        }
    }

    private void SyncPlayerPosition()
    {
        // 1. מיקום השחקן במרכז הפחית בצורה חלקה
        playerInside.transform.position = transform.position + (Vector3)playerInsideOffset;
        
        // 2. שמירת השחקן זקוף תמיד ביחס למשטח (כפי שנדרש במסמך העיצוב)
        playerInside.transform.rotation = Quaternion.FromToRotation(Vector3.up, playerInside.SurfaceNormal); //[cite: 1]
    }

    private void HandleSpriteRotation()
    {
        if (spriteRenderer == null) return;

        float zAngle = Mathf.Abs(transform.rotation.eulerAngles.z) % 180;
        
        Sprite targetSprite = (zAngle < spriteAngleThreshold || zAngle > (180 - spriteAngleThreshold)) ? canSideSprite : canTopSprite;
        
        // החלפת תמונה רק אם נדרש, לחסוך קריאות מיותרות
        if (spriteRenderer.sprite != targetSprite)
        {
            spriteRenderer.sprite = targetSprite;
        }
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

            if (canRicochet && shellCollider != null)
            {
                PhysicsMaterial2D bounceMat = new PhysicsMaterial2D();
                bounceMat.bounciness = ricochetBounciness;
                bounceMat.friction = 0.5f; 
                shellCollider.sharedMaterial = bounceMat;
            }
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
}