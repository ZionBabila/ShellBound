using UnityEngine;

public class RollingShell : BaseShell
{
    [Header("Rolling Settings")]
    [Tooltip("The maximum rolling speed.")]
    public float maxRollSpeed = 12f;
    
    [Tooltip("The mass of the player while rolling (affects gravity and torque feel).")]
    public float rollingMass = 3f;

    [Header("Visuals (Temp)")]
    [Tooltip("Sprite used when the crab is actively rolling.")]
    public Sprite activeSprite;
    
    private Sprite defaultSprite;
    private SpriteRenderer spriteRenderer;
    private float originalPlayerMass = 1f;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            defaultSprite = spriteRenderer.sprite;
        }
    }

    public override void ActivateAbility()
    {
        if (CurrentState != ShellState.OnBack) return;
        
        CurrentState = ShellState.InUse;
        
        if (spriteRenderer != null && activeSprite != null) spriteRenderer.sprite = activeSprite;
        
        var player = playerSystem.Player;
        
        // 1. Ask the player to stop regular walking physics
        player.isPhysicsOverridden = true;
        
        // איפוס הזווית והכיוון של התמונה כדי שהכדור יסתובב בדיוק סביב המרכז שלו ולא ירגיש אליפטי
        if (player.visualsRoot != null)
        {
            player.visualsRoot.localRotation = Quaternion.identity;
            Vector3 scale = player.visualsRoot.localScale;
            scale.x = Mathf.Abs(scale.x); // ביטול ה-Flip כדי שהפיזיקה והוויזואליה יסתובבו לאותו כיוון
            player.visualsRoot.localScale = scale;
        }
        
        // 2. Disable the player's main regular collider
        if (player.standingCollider != null) player.standingCollider.enabled = false;
        
        // 3. Enable the pre-configured rolling collider on the player
        if (player.rollingCollider != null) player.rollingCollider.enabled = true;
        
        // Store original mass and apply rolling mass
        originalPlayerMass = player.Rb.mass;
        player.Rb.mass = rollingMass;

        // Move the shell graphic to the true center of the player so it spins perfectly in place
        transform.localPosition = Vector3.zero;

        // 4. Release the player's rotation lock so they can physically roll
        player.Rb.freezeRotation = false;
            
        Debug.Log("[RollingShell] Activated! Player is rolling with perfect physics.");
    }

    public override void DeactivateAbility()
    {
        if (CurrentState != ShellState.InUse) return;
        
        CurrentState = ShellState.OnBack;
        
        if (spriteRenderer != null && defaultSprite != null) spriteRenderer.sprite = defaultSprite;
            
        var player = playerSystem.Player;
        
        // 1. Restore rotation lock and straighten the player
        player.Rb.freezeRotation = true;
        player.transform.rotation = Quaternion.identity;
        
        // 2. Turn the main player collider back on
        if (player.standingCollider != null) player.standingCollider.enabled = true;
        
        // 3. Turn off the rolling collider
        if (player.rollingCollider != null) player.rollingCollider.enabled = false;
        
        // Restore original mass
        player.Rb.mass = originalPlayerMass;

        // Return the shell graphic back to its offset on the back
        transform.localPosition = equippedOffset;

        // 4. Return control to the walking system
        player.isPhysicsOverridden = false;
            
        Debug.Log("[RollingShell] Deactivated! Back to walking.");
    }

    private void FixedUpdate()
    {
        // Apply pure physical torque only when we are inside the shell
        if (CurrentState == ShellState.InUse && playerSystem != null)
        {
            Rigidbody2D pRb = playerSystem.Player.Rb;

            // Clamp the maximum rolling speed
            if (Mathf.Abs(pRb.linearVelocity.x) > maxRollSpeed)
            {
                pRb.linearVelocity = new Vector2(Mathf.Sign(pRb.linearVelocity.x) * maxRollSpeed, pRb.linearVelocity.y);
            }
        }
    }
}