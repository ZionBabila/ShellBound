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
        // The heavy ability is engaged only while in the InUse state.
        if (CurrentState != ShellState.InUse) return;

        // A ground pound runs to completion on its own (see OnPlayerCollision).
        if (isGroundPounding)
        {
            // Safety: if we settled on the ground without a top-down hit, end the pound.
            if (playerSystem.Player.IsGrounded) EndGroundPound();
            return;
        }

        bool spaceHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

        if (!spaceHeld)
        {
            // Key released: drop any grabbed object and return to idle.
            playerSystem.Player.ReleaseGrab();
            CurrentState = ShellState.OnBack;
            return;
        }

        // Space is held.
        if (playerSystem.Player.IsGrounded)
        {
            // On the ground: grab and pull/push a Movable object.
            playerSystem.Player.TryStartGrab();
        }
        else if (!playerSystem.Player.IsGrabbing)
        {
            // In the air with Space held, and NOT holding an object: slam down.
            // Skipping the pound while grabbing avoids an accidental slam (and its skull-spin
            // animation) when the player just steps off a ledge while dragging an object.
            StartGroundPound();
        }
    }

    public override void Throw()
    {
        // Reset player stats before throwing
        if (playerSystem != null && playerSystem.Player != null)
        {
            playerSystem.Player.currentSpeedMultiplier = 1f;
            playerSystem.Player.CanPushHeavyObjects = false;
            playerSystem.Player.IsGroundPounding = false; // Safety: clear the pound flag if thrown mid-slam.
        }

        isGroundPounding = false;
        base.Throw();
    }

    public override void ActivateAbility()
    {
        if (CurrentState != ShellState.OnBack) return;

        // Engage the heavy ability. While Space is held, Update() decides the behavior:
        // grab & pull on the ground, or a ground pound in the air.
        CurrentState = ShellState.InUse;
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

        // Drop any held object before slamming down.
        playerSystem.Player.ReleaseGrab();

        isGroundPounding = true;
        CurrentState = ShellState.InUse; // Player is busy ground-pounding

        // Tell the animator to play the SkullSpin animation while pounding.
        playerSystem.Player.IsGroundPounding = true;

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

        // Stop the SkullSpin animation.
        playerSystem.Player.IsGroundPounding = false;

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