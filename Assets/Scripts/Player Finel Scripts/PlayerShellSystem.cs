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

    [Header("Rig Shells")]
    [Tooltip("Assign all the shell script components located on the player's Rig here.")]
    public BaseShell[] rigShells;

    [Header("Player Visuals")]
    [Tooltip("The main GameObject containing the crab's rig/sprites. Hidden when rolling or hiding.")]
    public GameObject crabVisuals;

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

        // Ensure all rig shells start disabled properly to avoid Awake() lifecycle bugs
        foreach (BaseShell shell in rigShells)
        {
            if (shell != null) shell.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (inputHandler != null)
        {
            inputHandler.OnAbility -= HandleAbility;
        }
    }

    public void EquipShell(ShellPickup pickup)
    {
        if (HasShell) return;
        
        // Find the matching shell in the Rig
        foreach (BaseShell rigShell in rigShells)
        {
            if (rigShell.shellID == pickup.shellID)
            {
                CurrentShell = rigShell;
                CurrentShell.Equip(this);
                
                // Swap to the larger 'with shell' collider
                if (Player.standingCollider != null) Player.standingCollider.enabled = false;
                if (Player.withShellCollider != null) Player.withShellCollider.enabled = true;

                // Destroy the world pickup object completely
                Destroy(pickup.gameObject);
                
                Debug.Log($"[PlayerShellSystem] Equipped {pickup.shellID}");
                return;
            }
        }
        
        Debug.LogWarning($"[PlayerShellSystem] Shell ID '{pickup.shellID}' not found in the Rig!");
    }

    public void ThrowCurrentShell()
    {
        if (!HasShell) return;

        // If the shell is currently active (e.g. player is hiding or rolling), deactivate it first
        if (CurrentShell.CurrentState == ShellState.InUse)
        {
            CurrentShell.DeactivateAbility();
        }

        // Swap back to the regular standing collider
        if (Player.withShellCollider != null) Player.withShellCollider.enabled = false;
        if (Player.standingCollider != null) Player.standingCollider.enabled = true;

        // 1. Instantiate the world prefab
        if (CurrentShell.worldPrefab != null)
        {
            float facingDir = VisualsRoot.localScale.x > 0 ? 1f : -1f;
            Vector2 throwVelocity = new Vector2(facingDir * throwDirection.x, throwDirection.y).normalized * throwForce;
            
            // Spawn slightly above the player to avoid clipping into the ground immediately
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            GameObject spawned = Instantiate(CurrentShell.worldPrefab, spawnPos, Quaternion.identity);
            
            Rigidbody2D rb = spawned.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = throwVelocity;
        }

        // 2. Turn off the Rig shell
        CurrentShell.Throw();
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

    public void SetCrabVisualsActive(bool isActive)
    {
        if (crabVisuals != null) crabVisuals.SetActive(isActive);
    }
}