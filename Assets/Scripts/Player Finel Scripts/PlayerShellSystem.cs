using System.Collections.Generic;
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

    // Remembered "while worn" tutorial line, read from the picked-up object (which gets destroyed)
    private string currentWornHint = "";

    // Worn hints the player has already used (pressed the ability once) — never shown again.
    // Shared/static so it persists across throwing and re-picking shells. Resets on Play mode.
    private static readonly HashSet<string> consumedWornHints = new HashSet<string>();

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

                // Read the "while worn" tutorial line from the picked-up object before it is destroyed
                TutorialHint hint = pickup.GetComponentInChildren<TutorialHint>();
                currentWornHint = hint != null ? hint.wornMessage : "";

                // Show the "while worn" tutorial line (e.g. 'SPACE to roll')
                ShowWornHint();

                // Swap to the larger 'with shell' collider
                Debug.Log($"[PlayerShellSystem] Equipped {pickup.shellID}. CurrentShell set to {CurrentShell.name}. HasShell: {HasShell}");
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

        // Hide the "while worn" tutorial line, the shell is leaving the player
        if (TutorialUI.Instance != null) TutorialUI.Instance.Hide(this);
        currentWornHint = "";

        // If the shell is currently active (e.g. player is hiding or rolling), deactivate it first
        Debug.Log($"[PlayerShellSystem] Throwing shell. CurrentShell was {CurrentShell?.name}. HasShell: {HasShell}");
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
            // Throw the shell BACKWARD, over the shoulder, opposite to the player's facing direction.
            // Use the authoritative FacingDirection (not visualsRoot.localScale, which may not flip),
            // and the absolute X so the Inspector sign doesn't matter; facing alone decides the direction.
            float throwDir = -Player.FacingDirection;
            Vector2 throwVelocity = new Vector2(throwDir * Mathf.Abs(throwDirection.x), throwDirection.y).normalized * throwForce;
            
            // Spawn slightly above the player to avoid clipping into the ground immediately
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            GameObject spawned = Instantiate(CurrentShell.worldPrefab, spawnPos, Quaternion.identity);
            
            Rigidbody2D rb = spawned.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = throwVelocity;
        }

        // If the player was moving, let them slide a bit after the throw
        if (Player.CurrentSpeed > 1f) Player.PreserveMomentumFor(0.5f);

        // 2. Turn off the Rig shell
        CurrentShell.Throw();
        CurrentShell = null;
        Debug.Log($"[PlayerShellSystem] Shell thrown. CurrentShell is now null. HasShell: {HasShell}");
        
        Debug.Log("[PlayerShellSystem] Shell thrown.");
    }

    private void HandleAbility()
    {
        if (!HasShell) return;

        // Since we only have one event, we'll toggle the state.
        // Activate if on back, deactivate if in use.
        if (CurrentShell.CurrentState == ShellState.OnBack)
        {
            // Only block *activation* while the player isn't in control (e.g. mid pipe warp),
            // so a Space press during a teleport can't trigger a ground pound that never lands.
            // Deactivation (below) must always work: shells like RollingShell disable movement
            // themselves, so blocking it here would trap the player in the rolling state.
            if (Player.isMovementDisabled) return;

            CurrentShell.ActivateAbility();

            // Ability used once: hide the "SPACE to roll" hint and never show it again
            if (TutorialUI.Instance != null) TutorialUI.Instance.Hide(this);
            if (!string.IsNullOrEmpty(currentWornHint)) consumedWornHints.Add(currentWornHint);
        }
        else if (CurrentShell.CurrentState == ShellState.InUse)
        {
            CurrentShell.DeactivateAbility();
        }
    }

    // Shows the remembered "while worn" tutorial line, unless the player already used the ability
    private void ShowWornHint()
    {
        if (consumedWornHints.Contains(currentWornHint)) return;

        if (TutorialUI.Instance != null)
        {
            TutorialUI.Instance.Show(currentWornHint, this);
        }
    }

    public void SetCrabVisualsActive(bool isActive)
    {
        if (crabVisuals != null) crabVisuals.SetActive(isActive);
    }
}