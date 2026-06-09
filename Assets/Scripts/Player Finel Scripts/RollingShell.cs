using UnityEngine;

public class RollingShell : BaseShell
{
    [Header("Rolling Settings")]
    [Tooltip("The mass of the player while rolling (affects gravity and torque feel).")]
    public float rollingMass = 3f;

    [Header("Visuals")]
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
        
        // 2. Swap colliders
        if (player.standingCollider != null) player.standingCollider.enabled = false;
        
        // Store original mass and center of mass
        originalPlayerMass = player.Rb.mass;
        player.Rb.mass = rollingMass;

        // 3. Enable the pre-configured rolling collider on the player
        if (player.rollingCollider != null) player.rollingCollider.enabled = true;

        Debug.Log("[RollingShell] Activated! Player is in roll mode.");
    }

    public override void DeactivateAbility()
    {
        if (CurrentState != ShellState.InUse) return;
        
        CurrentState = ShellState.OnBack;
        
        if (spriteRenderer != null && defaultSprite != null) spriteRenderer.sprite = defaultSprite;
            
        var player = playerSystem.Player;
        
        // 1. Turn the main player collider back on
        if (player.standingCollider != null) player.standingCollider.enabled = true;
        
        // 2. Turn off the rolling collider
        if (player.rollingCollider != null) player.rollingCollider.enabled = false;
        
        // Restore original mass
        player.Rb.mass = originalPlayerMass;

        // 3. Return control to the walking system
        player.isPhysicsOverridden = false;

        Debug.Log("[RollingShell] Deactivated! Back to walking.");
    }
}