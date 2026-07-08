using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// TutorialHint
// -----------------------------------------------------------------------------
// Role:    Attach to any world element (shell pickup, light/heavy box).
//          When the player gets within 'showRadius', it shows 'message' in the
//          PRIMARY tutorial window (e.g. 'Press E to wear'). Hides on leave.
// Notes:   - Self-contained proximity check (no extra trigger collider needed).
//          - Uses a SHARED (static) show-count keyed by the message, so every
//            prefab instance (picked up, thrown, future copies) shares one count.
//            Once shown 'maxShows' times it never shows again this session.
//          - The "after wearing" line (SPACE to roll) is handled by the shell
//            system, not here, because this object is destroyed on pickup.
// =============================================================================
// Condition on the player's currently worn shell for a hint to appear.
public enum ShellRequirement
{
    Any,          // No condition: show regardless of shell (default).
    NoShell,      // Show only when the player wears NO shell.
    RollingShell, // Show only when the player wears the rolling shell.
    HeavyShell    // Show only when the player wears the heavy armor shell.
}

public class TutorialHint : MonoBehaviour
{
    [Header("Hint Content")]
    [Tooltip("Window 1: primary text shown on proximity. Example: 'Press E to wear'.")]
    [TextArea(2, 3)]
    public string message = "Press a key";

    [Tooltip("Window 2: secondary text shown AFTER the player wears this (shells only). " +
             "Example: 'SPACE to roll'. Leave empty for boxes or single-line hints.")]
    [TextArea(2, 3)]
    public string wornMessage;

    [Header("Show Limit")]
    [Tooltip("How many times this hint may appear before it stops showing for good. 0 = unlimited.")]
    public int maxShows = 1;

    [Header("Detection")]
    [Tooltip("How close (in units) the player must be for the hint to appear.")]
    public float showRadius = 2.5f;

    [Tooltip("Tag used to find the player object in the scene.")]
    public string playerTag = "Player";

    [Tooltip("Only show this hint when the player wears a specific shell (or none). " +
             "'Any' = no condition. Useful for hints tied to a puzzle that needs a certain shell.")]
    public ShellRequirement requiredShell = ShellRequirement.Any;

    // Shared across ALL instances so a respawned/new prefab does not reset the count.
    // Resets automatically when entering Play mode (default domain reload).
    private static readonly Dictionary<string, int> shownCounts = new Dictionary<string, int>();

    private Transform player;
    private PlayerShellSystem shellSystem; // Used to check the worn shell for 'requiredShell'.
    private bool isShown = false;

    // Group the count by the message text so all copies of the same hint share it
    private string Key => message;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            player = playerObj.transform;
            // The shell system lives on the player root; search up in case the tag is on a child collider.
            shellSystem = playerObj.GetComponentInParent<PlayerShellSystem>();
        }
    }

    private void Update()
    {
        if (player == null || TutorialUI.Instance == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        // Show only when both in range AND the worn-shell condition is met.
        // Both are re-checked every frame, so swapping shells nearby shows/hides the hint live.
        bool shouldShow = distance <= showRadius && IsShellRequirementMet();

        if (shouldShow && !isShown)
        {
            // Stop showing once the shared limit has been reached
            if (HasReachedLimit()) return;

            isShown = true;
            shownCounts[Key] = GetCount() + 1; // Count this appearance
            TutorialUI.Instance.ShowPrimary(message, this);
        }
        else if (!shouldShow && isShown)
        {
            isShown = false;
            TutorialUI.Instance.HidePrimary(this);
        }
    }

    // Checks whether the player's currently worn shell matches this hint's requirement.
    private bool IsShellRequirementMet()
    {
        if (requiredShell == ShellRequirement.Any) return true;

        // No shell system found -> treat the player as having no shell.
        bool hasShell = shellSystem != null && shellSystem.HasShell;

        switch (requiredShell)
        {
            case ShellRequirement.NoShell:
                return !hasShell;
            case ShellRequirement.RollingShell:
                return hasShell && shellSystem.CurrentShell is RollingShell;
            case ShellRequirement.HeavyShell:
                return hasShell && shellSystem.CurrentShell is HeavyArmorShell;
            default:
                return true;
        }
    }

    private int GetCount()
    {
        return shownCounts.TryGetValue(Key, out int count) ? count : 0;
    }

    private bool HasReachedLimit()
    {
        return maxShows > 0 && GetCount() >= maxShows;
    }

    // Make sure the hint disappears if this object is turned off or collected
    private void OnDisable()
    {
        if (isShown && TutorialUI.Instance != null)
        {
            TutorialUI.Instance.HidePrimary(this);
        }
        isShown = false;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the activation radius in the editor
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, showRadius);
    }
}
