using UnityEngine;

public class HeavyArmorShell : BaseShell
{
    [Header("Push & Crush Settings")]
    [Tooltip("Layer mask for breakable objects.")]
    public LayerMask crushLayer;

    [Range(0.1f, 1f)]
    [Tooltip("How much the armor slows the player down.")]
    public float armorSpeedMultiplier = 0.5f;

    [Header("Visuals")]
    [Tooltip("The GameObject (sprite) shown when the shell is on the crab's back.")]
    public GameObject onBackVisuals;
    
    [Tooltip("The GameObject (sprite) shown when the crab is hiding inside the armor.")]
    public GameObject hidingVisuals;

    public override void Equip(PlayerShellSystem player)
    {
        base.Equip(player);
        
        // Set default visual state when equipped
        if (onBackVisuals != null) onBackVisuals.SetActive(true);
        if (hidingVisuals != null) hidingVisuals.SetActive(false);

        // Apply Heavy Armor stats
        playerSystem.Player.currentSpeedMultiplier = armorSpeedMultiplier;
        playerSystem.Player.CanPushHeavyObjects = true;
    }

    private void Update()
    {
        // Sync the stats continuously so the slider works in real-time while playing in the Editor!
        if (playerSystem != null && CurrentState == ShellState.OnBack)
        {
            playerSystem.Player.currentSpeedMultiplier = armorSpeedMultiplier;
        }
    }

    public override void Throw()
    {
        // Remove Heavy Armor stats
        playerSystem.Player.currentSpeedMultiplier = 1f;
        playerSystem.Player.CanPushHeavyObjects = false;
        
        base.Throw();
    }

    public override void ActivateAbility()
    {
        if (CurrentState != ShellState.OnBack) return;
        
        CurrentState = ShellState.InUse;
        
        // Swap visual objects
        if (onBackVisuals != null) onBackVisuals.SetActive(false);
        if (hidingVisuals != null) hidingVisuals.SetActive(true);
            
        // Hide the main crab visuals
        playerSystem.SetCrabVisualsActive(false);

        // Disable manual input so the crab drops straight down to crush things
        playerSystem.Player.isMovementDisabled = true;

        Debug.Log("[HeavyArmorShell] Activated! Crab is hiding.");
    }

    public override void DeactivateAbility()
    {
        if (CurrentState != ShellState.InUse) return;
        
        CurrentState = ShellState.OnBack;
        
        // Swap visual objects back
        if (onBackVisuals != null) onBackVisuals.SetActive(true);
        if (hidingVisuals != null) hidingVisuals.SetActive(false);
            
        // Show the main crab visuals
        playerSystem.SetCrabVisualsActive(true);

        // Re-enable manual input
        playerSystem.Player.isMovementDisabled = false;

        Debug.Log("[HeavyArmorShell] Deactivated! Crab is carrying the shell.");
    }

    public override void OnPlayerCollision(Collision2D collision)
    {
        // Only crush things if hiding inside the armor
        if (CurrentState != ShellState.InUse) return;

        // Check if the collided object is in the crush layer
        if (((1 << collision.gameObject.layer) & crushLayer) != 0)
        {
            ContactPoint2D contact = collision.contacts[0];
            // Fix: Use magnitude because Unity's relativeVelocity is often positive when hitting static objects
            if (contact.normal.y > 0.5f && collision.relativeVelocity.magnitude >= 2f)
            {
                // Support the Breakable script exactly like the old ArmorShell did
                Breakable breakableObj = collision.gameObject.GetComponent<Breakable>();
                if (breakableObj != null)
                {
                    breakableObj.Smash();
                }
                else
                {
                    Destroy(collision.gameObject); // Fallback: regular destroy
                }
            }
        }
    }
}