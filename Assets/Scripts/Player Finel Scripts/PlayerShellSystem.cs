using UnityEngine;

[RequireComponent(typeof(SimplePlayer))]
public class PlayerShellSystem : MonoBehaviour
{
    [Header("Throw Settings")]
    [Tooltip("The force applied when throwing a shell.")]
    public float throwForce = 8f;
    
    [Tooltip("The direction angle for throwing.")]
    public Vector2 throwDirection = new Vector2(1f, 0.5f);

    // Core references
    public SimplePlayer Player { get; private set; }
    public Transform VisualsRoot => Player.visualsRoot;

    public BaseShell CurrentShell { get; private set; }
    public bool HasShell => CurrentShell != null;

    private PlayerInputHandler inputHandler;

    private void Awake()
    {
        Player = GetComponent<SimplePlayer>();
        inputHandler = GetComponent<PlayerInputHandler>();

        if (inputHandler != null)
        {
            inputHandler.OnAbility += HandleAbility;
        }
    }

    private void OnDestroy()
    {
        if (inputHandler != null)
        {
            inputHandler.OnAbility -= HandleAbility;
        }
    }

    public void EquipShell(BaseShell newShell)
    {
        if (HasShell) return;
        
        CurrentShell = newShell;
        
        // 1. Shut down all physics on the shell so it doesn't alter the player's pivot or mass
        Rigidbody2D shellRb = CurrentShell.GetComponent<Rigidbody2D>();
        if (shellRb != null) shellRb.simulated = false;

        Collider2D[] shellColliders = CurrentShell.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in shellColliders) col.enabled = false;

        // BaseShell.Equip handles parenting it to VisualsRoot and applying offsets.
        CurrentShell.Equip(this);
        
        Debug.Log($"[PlayerShellSystem] Equipped {newShell.gameObject.name}");
    }

    public void ThrowCurrentShell()
    {
        if (!HasShell) return;

        // If the shell is currently active (e.g. player is hiding or rolling), deactivate it first
        if (CurrentShell.CurrentState == ShellState.InUse)
        {
            CurrentShell.DeactivateAbility();
        }

        // 1. Disconnect the shell from the player
        CurrentShell.transform.SetParent(null);

        // 2. Re-enable physics and visuals so it can fly and land
        Rigidbody2D shellRb = CurrentShell.GetComponent<Rigidbody2D>();
        if (shellRb != null) shellRb.simulated = true;

        Collider2D[] shellColliders = CurrentShell.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in shellColliders) col.enabled = true;

        // Determine throw direction based on where the player is facing
        float facingDir = VisualsRoot.localScale.x > 0 ? 1f : -1f;
        Vector2 throwVelocity = new Vector2(facingDir * throwDirection.x, throwDirection.y).normalized * throwForce;

        CurrentShell.Throw(throwVelocity);
        CurrentShell = null;
        
        Debug.Log("[PlayerShellSystem] Shell thrown.");
    }

    private void HandleAbility()
    {
        if (!HasShell) return;

        if (CurrentShell.CurrentState == ShellState.OnBack)
        {
            CurrentShell.ActivateAbility();
        }
        else if (CurrentShell.CurrentState == ShellState.InUse)
        {
            CurrentShell.DeactivateAbility();
        }
    }
}