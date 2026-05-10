using UnityEngine;

public class TunaCan : Shell
{
    [Header("Tuna Can Physics")]
    public Vector2 playerInsideOffset = new Vector2(0, 0); 
    public float playerMoveRangeX = 0.3f;
    public float torqueForce = 6.0f;
    public float maxAngularVelocity = 15.0f;
    [Range(0f, 1f)] public float rollFriction = 0.85f;
    
    [Header("Sprites & Rotation")]
    public Sprite canSideSprite;
    public Sprite canTopSprite;
    public float spriteAngleThreshold = 45.0f;

    [Header("Ricochet Settings")]
    public bool canRicochet = true;
    public float ricochetBounciness = 0.4f;

    private PlayerController playerInside;
    private PlayerInputHandler input;
    private float currentRollVelocity = 0f;

    private void Update()
    {
        if (currentState == ShellState.InUse && playerInside != null)
        {
            HandleRollingInput();
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
        }
    }

    public override void OnActivate()
    {
        currentState = ShellState.InUse;
        
        transform.SetParent(null);
        transform.localScale = originalScale; 
        
        rb.bodyType = RigidbodyType2D.Dynamic;
        shellCollider.enabled = true;
        shellCollider.isTrigger = false;
        
        if (playerInside != null)
        {
            playerInside.transform.position = transform.position + (Vector3)playerInsideOffset;
        }

        if (input != null) input.OnInteract += CheckThrowInput;
    }

    public override void OnDeactivate()
    {
        currentState = ShellState.OnBack;
        rb.bodyType = RigidbodyType2D.Kinematic;
        shellCollider.enabled = false;
        shellCollider.isTrigger = true;
        
        if (playerInside != null)
        {
            Transform attachParent = playerInside.visualsRoot != null ? playerInside.visualsRoot : playerInside.transform;
            transform.SetParent(attachParent);
            
            ApplyCompensatedScale(attachParent);
            
            // UPDATED: Now calls the new method that handles both position and rotation
            UpdateAttachmentTransform(playerInside.shellMountOffset);
        }

        if (input != null) input.OnInteract -= CheckThrowInput;
    }

    private void HandleRollingInput()
    {
        if (input == null) return;

        float moveInputX = input.MoveValue.x;
        
        if (Mathf.Abs(moveInputX) > 0.1f)
        {
            currentRollVelocity += moveInputX * torqueForce * Time.deltaTime;
            currentRollVelocity = Mathf.Clamp(currentRollVelocity, -maxAngularVelocity, maxAngularVelocity);
        }
        else
        {
            currentRollVelocity *= rollFriction; 
        }
    }

    private void ApplyRollingPhysics()
    {
        rb.angularVelocity = -currentRollVelocity * Mathf.Rad2Deg;
        rb.linearVelocity = new Vector2(currentRollVelocity, rb.linearVelocity.y);

        if (playerInside != null)
        {
            Vector3 targetPos = transform.position + (Vector3)playerInsideOffset;
            targetPos.x += (currentRollVelocity / maxAngularVelocity) * playerMoveRangeX;
            
            playerInside.transform.position = targetPos;
            playerInside.transform.rotation = Quaternion.identity; 
        }
    }

    private void HandleSpriteRotation()
    {
        if (spriteRenderer == null) return;

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
            
            playerInside.currentShell = null;
            playerInside = null;

            OnThrow(throwMomentum);

            if (canRicochet && shellCollider != null)
            {
                PhysicsMaterial2D bounceMat = new PhysicsMaterial2D();
                bounceMat.bounciness = ricochetBounciness;
                shellCollider.sharedMaterial = bounceMat;
            }
        }
    }
}