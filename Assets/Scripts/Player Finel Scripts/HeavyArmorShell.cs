using UnityEngine;
using UnityEngine.InputSystem; // Required for the new Input System

public class HeavyArmorShell : BaseShell
{
    [Header("Ground Pound Settings")]
    [Tooltip("The downward force applied during a ground pound.")]
    public float groundPoundForce = 20f;

    [Tooltip("Minimum fall speed required to break objects with a ground pound.")]
    public float breakSpeedThreshold = 2f;

    [Tooltip("Layer mask for objects that can be broken by a ground pound.")]
    public LayerMask breakableLayer;

    [Range(0.1f, 1f)]
    [Tooltip("How much the armor slows the player down.")]
    public float armorSpeedMultiplier = 0.5f;

    [Header("Visuals")]
    [Tooltip("The GameObject (sprite) shown when the shell is on the crab's back.")]
    public GameObject defaultVisuals;

    // Internal state for ground pound
    private bool isGroundPounding = false;

    public override void Equip(PlayerShellSystem player)
    {
        base.Equip(player);
        
        // Set default visual state when equipped
        if (defaultVisuals != null) defaultVisuals.SetActive(true);

        // Apply Heavy Armor stats
        playerSystem.Player.currentSpeedMultiplier = armorSpeedMultiplier;
        playerSystem.Player.CanPushHeavyObjects = true;

        // Ensure crab visuals are active when equipping
        playerSystem.SetCrabVisualsActive(true);
    }

    private void Update()
    {
        // This Update handles the "press and hold" logic for grabbing.
        // It only runs when the shell is in the special "InUse" state on the ground.
        if (CurrentState != ShellState.InUse || isGroundPounding) return;

        // Check if the ability key (Space) is being held down using the new Input System.
        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
        {
            // If the key is held, try to maintain the grab.
            playerSystem.Player.TryStartGrab();
        }
        else
        {
            // If the key is released, release the object and exit the "InUse" state.
            playerSystem.Player.ReleaseGrab();
            CurrentState = ShellState.OnBack;
            Debug.Log("[HeavyArmorShell] Grab released, exiting InUse state.");
        }
    }

    public override void Throw()
    {
        // Reset player stats before throwing
        if (playerSystem != null && playerSystem.Player != null)
        {
            playerSystem.Player.currentSpeedMultiplier = 1f;
            playerSystem.Player.CanPushHeavyObjects = false;
        }

        base.Throw();
    }

    public override void ActivateAbility()
    {
        if (CurrentState != ShellState.OnBack) return;

        // --- Animation Trigger ---
        // Find the animation component on the player and trigger the specific animation for this ability.
        PlayerAnimation playerAnimation = playerSystem.Player.GetComponentInChildren<PlayerAnimation>();
        if (playerAnimation != null)
        {
            playerAnimation.TriggerHeavyAbility();
        }

        // --- BEHAVIOR 1: Ground Pound (if in the air) ---
        if (!playerSystem.Player.IsGrounded)
        {
            StartGroundPound();
            return;
        }

        // --- BEHAVIOR 2: Grab/Release Movable Object (if on the ground) ---
        // Enter "InUse" state to enable the press-and-hold logic in Update().
        CurrentState = ShellState.InUse;
        Debug.Log("[HeavyArmorShell] Entered grab-ready state. Hold SPACE to grab.");
    }

    public override void DeactivateAbility()
    {
        // This is now called by a second press of the ability key (toggle).
        // It serves as a manual override to release the grab and exit the "InUse" state.
        if (CurrentState == ShellState.InUse)
        {
            playerSystem.Player.ReleaseGrab();
            CurrentState = ShellState.OnBack;
            Debug.Log("[HeavyArmorShell] Ability deactivated manually. Releasing grab.");
        }
        // The ground pound is a one-shot and resets itself on landing, so it doesn't need deactivation.
    }

    private void StartGroundPound()
    {
        if (isGroundPounding) return;

        isGroundPounding = true;
        CurrentState = ShellState.InUse; // Player is busy ground-pounding

        // Apply a strong downward force. Renamed from Rb.linearVelocity to Rb.velocity for clarity.
        playerSystem.Player.Rb.linearVelocity = new Vector2(playerSystem.Player.Rb.linearVelocity.x, 0); // Reset vertical speed for consistent pound
        playerSystem.Player.Rb.AddForce(Vector2.down * groundPoundForce, ForceMode2D.Impulse);

        Debug.Log("[HeavyArmorShell] Ground Pound initiated!");
    }

    private void EndGroundPound()
    {
        if (!isGroundPounding) return;

        isGroundPounding = false;
        CurrentState = ShellState.OnBack;
        Debug.Log("[HeavyArmorShell] Ground Pound finished.");
    }

    // This is called by SimplePlayer's OnCollisionEnter2D
    public override void OnPlayerCollision(Collision2D collision)
    {
        // Only check for ground pound collisions if the ability is active
        if (!isGroundPounding) return;

        // Check if we landed on something
        ContactPoint2D contact = collision.contacts[0];
        if (contact.normal.y > 0.5f) // Hit from above
        {
            // Check if the object is breakable and we hit it fast enough
            if (((1 << collision.gameObject.layer) & breakableLayer) != 0 && collision.relativeVelocity.magnitude >= breakSpeedThreshold)
            {
                Breakable breakableObj = collision.gameObject.GetComponent<Breakable>();
                if (breakableObj != null)
                {
                    breakableObj.Smash();
                }
            }

            // We landed, so the ground pound is over, regardless of what we hit.
            EndGroundPound();
        }
    }
}