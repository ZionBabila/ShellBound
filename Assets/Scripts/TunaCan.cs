using UnityEngine;

public class TunaCan : Shell
{
    [Header("Attachment Settings")]
    [Tooltip("Control exactly where the can sits on the crab's back offset")]
    public Vector2 canAttachOffset = new Vector2(0, 0.4f);

    [Header("Tuna Can Physics (Hamster Ball)")]
    public Vector2 playerInsideOffset = new Vector2(0, 0); 
    public float torqueForce = 150f; // Note: Torque needs higher values than linear force
    public float maxAngularVelocity = 800f; // Max rotation speed
    
    [Header("Sprites & Rotation")]
    public Sprite canSideSprite;
    public Sprite canTopSprite;
    public float spriteAngleThreshold = 45.0f;

    [Header("Ricochet Settings")]
    public bool canRicochet = true;
    public float ricochetBounciness = 0.4f;

    private PlayerController playerInside;
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

    public override void OnCollect(Transform parentTransform, Vector2 playerMountOffset)
    {
        base.OnCollect(parentTransform, playerMountOffset);
        
        playerInside = parentTransform.GetComponentInParent<PlayerController>();
        if (playerInside != null)
        {
            input = playerInside.GetComponent<PlayerInputHandler>();
            
            // Apply the custom offset for the Tuna Can specifically
            transform.localPosition = canAttachOffset;
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
            // --- התיקון: כיבוי ההתנגשויות והפיזיקה של השחקן ---
            Collider2D[] playerCols = playerInside.GetComponents<Collider2D>();
            foreach (var col in playerCols) col.enabled = false;

            Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.bodyType = RigidbodyType2D.Kinematic;
                playerRb.linearVelocity = Vector2.zero;
            }

            // מיקום השחקן במרכז
            playerInside.transform.position = transform.position + (Vector3)playerInsideOffset;
        }

        if (input != null) input.OnInteract += CheckThrowInput;
        
        Debug.Log("<color=green>🥫 TUNA CAN ACTIVE:</color> Hamster ball mode engaged without clipping!");
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
            // --- התעוררות הפיזיקה וההתנגשויות של השחקן ---
            Collider2D[] playerCols = playerInside.GetComponents<Collider2D>();
            foreach (var col in playerCols) col.enabled = true;

            Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
            if (playerRb != null) playerRb.bodyType = RigidbodyType2D.Dynamic;

            Transform attachParent = playerInside.visualsRoot != null ? playerInside.visualsRoot : playerInside.transform;
            transform.SetParent(attachParent);
            
            transform.localPosition = canAttachOffset;
            transform.localRotation = Quaternion.identity;
        }

        if (input != null) input.OnInteract -= CheckThrowInput;
    }

    private void ApplyRollingPhysics()
    {
        if (input == null || rb == null) return;

        float moveInputX = input.MoveValue.x;

        // 1. ADD TORQUE: Spin the can based on input
        // Negative sign because moving Right (Positive X) means spinning Clockwise (Negative Z)
        if (Mathf.Abs(moveInputX) > 0.1f)
        {
            rb.AddTorque(-moveInputX * torqueForce);
        }

        // 2. CLAMP SPEED: Prevent infinite acceleration
        if (Mathf.Abs(rb.angularVelocity) > maxAngularVelocity)
        {
            rb.angularVelocity = Mathf.Sign(rb.angularVelocity) * maxAngularVelocity;
        }

        // 3. SYNC PLAYER: Lock the crab inside the can, but keep the crab upright
        if (playerInside != null)
        {
            // The player's position is the can't position + any offset
            playerInside.transform.position = transform.position + (Vector3)playerInsideOffset;
            
            // Keep the crab standing straight up, ignoring the can's rotation
            playerInside.transform.rotation = Quaternion.FromToRotation(Vector3.up, playerInside.SurfaceNormal);
        }
    }

    private void HandleSpriteRotation()
    {
        if (spriteRenderer == null) return;

        // Determine which sprite to show based on the Z rotation of the can
        float zAngle = Mathf.Abs(transform.rotation.eulerAngles.z) % 180;
        if (zAngle < spriteAngleThreshold || zAngle > (180 - spriteAngleThreshold))
            spriteRenderer.sprite = canSideSprite;
        else
            spriteRenderer.sprite = canTopSprite;
    }

  private void CheckThrowInput()
    {
        if (currentState == ShellState.InUse && playerInside != null)
        {
            if (input != null) input.OnInteract -= CheckThrowInput;

            Vector2 throwMomentum = rb.linearVelocity;
            
            // --- החזרת הפיזיקה לשחקן רגע לפני הזריקה ---
            Collider2D[] playerCols = playerInside.GetComponents<Collider2D>();
            foreach (var col in playerCols) col.enabled = true;

            Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
            if (playerRb != null) playerRb.bodyType = RigidbodyType2D.Dynamic;

            // הרמה קטנה למעלה כדי שהסרטן לא ייתקע ברצפה כשהוא יוצא מהפחית
            playerInside.transform.position += Vector3.up * 0.15f;

            playerInside.currentShell = null;
            playerInside = null;

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
}