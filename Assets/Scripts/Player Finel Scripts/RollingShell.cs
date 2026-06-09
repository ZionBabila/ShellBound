using UnityEngine;

public class RollingShell : BaseShell
{
    [Header("Visuals")]
    [Tooltip("The GameObject (sprite) shown when the shell is on the crab's back.")]
    public GameObject onBackVisuals;
    
    [Tooltip("The GameObject (sprite) shown when the crab is actively rolling.")]
    public GameObject rollingVisuals;

    public override void Equip(PlayerShellSystem player)
    {
        base.Equip(player);
        
        // Set default visual state when equipped
        if (onBackVisuals != null) onBackVisuals.SetActive(true);
        if (rollingVisuals != null) rollingVisuals.SetActive(false);
    }

    public override void ActivateAbility()
    {
        if (CurrentState != ShellState.OnBack) return;
        
        CurrentState = ShellState.InUse;
        
        // Swap visual objects
        if (onBackVisuals != null) onBackVisuals.SetActive(false);
        if (rollingVisuals != null) rollingVisuals.SetActive(true);
        
        // 1. Swap colliders
        if (playerSystem.Player.withShellCollider != null) 
            playerSystem.Player.withShellCollider.enabled = false;
            
        if (playerSystem.Player.rollingCollider != null) 
            playerSystem.Player.rollingCollider.enabled = true;
            
        // 2. Hide the main crab visuals
        playerSystem.SetCrabVisualsActive(false);

        // 3. Disable manual input so the shell rolls purely by physics (gravity/momentum)
        playerSystem.Player.isMovementDisabled = true;

        // 4. Unlock Z rotation for physical rolling
        playerSystem.Player.Rb.freezeRotation = false;

        Debug.Log("[RollingShell] Activated! Player is in roll mode.");
    }

    public override void DeactivateAbility()
    {
        if (CurrentState != ShellState.InUse) return;
        
        CurrentState = ShellState.OnBack;
        
        // Swap visual objects back
        if (onBackVisuals != null) onBackVisuals.SetActive(true);
        if (rollingVisuals != null) rollingVisuals.SetActive(false);
            
        // 1. Restore colliders
        if (playerSystem.Player.withShellCollider != null) 
            playerSystem.Player.withShellCollider.enabled = true;
            
        if (playerSystem.Player.rollingCollider != null) 
            playerSystem.Player.rollingCollider.enabled = false;
            
        // 2. Show the main crab visuals
        playerSystem.SetCrabVisualsActive(true);

        // 3. Re-enable manual input
        playerSystem.Player.isMovementDisabled = false;

        // 4. Lock Z rotation and snap upright
        playerSystem.Player.Rb.freezeRotation = true;
        playerSystem.Player.transform.rotation = Quaternion.identity;

        Debug.Log("[RollingShell] Deactivated! Back to walking.");
    }
}