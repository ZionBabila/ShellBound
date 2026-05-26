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
    [Tooltip("The pure torque applied to the can. Ground friction will convert this into movement.")]
    public float torqueForce = 25f;
    [Tooltip("Natural braking (angular drag) when movement keys are released.")]
    public float angularDragWhenStopping = 3f;

    [Header("Hamster Ball Environment")]
    public float jumpForce = 7f;
    private float lastJumpTime;

    private PlayerInputHandler input;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Update()
    {
        // Sprite swapping during rolling is disabled. 
        // A rolling can should physically rotate smoothly with its round sprite.
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
        
        // 1. Rolling mode (InUse): Sync player position into the can
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
        
        // Release the can into the world space
        transform.SetParent(null);
        transform.localScale = originalScale; 
        transform.rotation = Quaternion.identity; 
        
        // Enable can physics
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

            // Initial sync
            SyncPlayerPosition();

            // Switch can to the round (Top) sprite so it rolls smoothly
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
        
        // Disable can physics
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

        // Return to side view sprite when on back or thrown
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

        // 1. Ground sensor is now purely for jumping/braking logic (movement is pure physics)
        bool isGrounded = false;
        Vector2 groundNormal = Vector2.up;
        float radius = 0.5f; // Fallback radius in case collider is missing
        if (shellCollider != null)
        {
            radius = shellCollider.bounds.extents.y;
            
            // Shoot a straight raycast slightly above the can's bottom downwards
            Vector2 origin = (Vector2)transform.position + new Vector2(0, -radius + 0.2f);
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 0.3f);
            
            foreach (RaycastHit2D hit in hits)
            {
                // Ignore the can itself, the player inside, and triggers
                if (hit.collider.gameObject == gameObject || hit.collider.isTrigger) continue;
                if (playerInside != null && hit.collider.gameObject == playerInside.gameObject) continue;

                isGrounded = true;
                groundNormal = hit.normal;
                break;
            }
        }

        // 2. Jump (Up button - W or Up Arrow)
        bool jumpedThisFrame = false;
        if (isGrounded && moveInputY > 0.5f && Time.time > lastJumpTime + 0.3f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            lastJumpTime = Time.time;
            jumpedThisFrame = true;
            isGrounded = false; // Disconnect from slope this frame so jump goes straight up
        }

        // 3. Organic movement based entirely on torque and friction!
        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            rb.angularDamping = 0.05f; // Minimal drag while moving
            // Add torque (minus because right input = clockwise rotation)
            rb.AddTorque(-moveInputX * torqueForce * rb.mass); 
        }
        else if (isGrounded)
        {
            // When releasing keys, increase angular drag so the can brakes naturally
            rb.angularDamping = angularDragWhenStopping;
        }
    }

    private void SyncPlayerPosition()
    {
        // 1. Smoothly center player inside the can
        playerInside.transform.position = transform.position + (Vector3)playerInsideOffset;
        
        // 2. Keep the player upright relative to the surface normal
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
        // Critical safeguard: If the can is destroyed while in use, restore player physics!
        if (currentState == ShellState.InUse)
        {
            RestorePlayerPhysics();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (shellCollider != null)
        {
            Gizmos.color = Color.cyan;
            float radius = shellCollider.bounds.extents.y;
            Vector2 origin = (Vector2)transform.position + new Vector2(0, -radius + 0.2f);
            // Draw the ground sensor raycast
            Gizmos.DrawLine(origin, origin + Vector2.down * 0.3f);
        }
    }
}