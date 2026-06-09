using UnityEngine;

public class HeavyArmorShell : BaseShell
{
    [Header("Push & Crush Settings")]
    [Tooltip("Layer mask for breakable objects.")]
    public LayerMask crushLayer;
    
    [Tooltip("The mass the player can push while wearing the armor.")]
    public float armorPushMass = Mathf.Infinity;
    
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
        playerSystem.Player.currentMaxPushMass = armorPushMass;
        playerSystem.Player.currentSpeedMultiplier = armorSpeedMultiplier;
    }

    public override void Throw()
    {
        // Remove Heavy Armor stats
        playerSystem.Player.currentMaxPushMass = playerSystem.Player.baseMaxPushMass;
        playerSystem.Player.currentSpeedMultiplier = 1f;
        
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
            // Break logic fixed according to Code Review 2.1 (Needs to be a hard fall)
            if (contact.normal.y > 0.5f && collision.relativeVelocity.y < -3f)
            {
                // Trigger the generic Break method on the Breakable.cs
                collision.gameObject.SendMessage("Break", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}