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

    // Shared across ALL instances so a respawned/new prefab does not reset the count.
    // Resets automatically when entering Play mode (default domain reload).
    private static readonly Dictionary<string, int> shownCounts = new Dictionary<string, int>();

    private Transform player;
    private bool isShown = false;

    // Group the count by the message text so all copies of the same hint share it
    private string Key => message;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null) player = playerObj.transform;
    }

    private void Update()
    {
        if (player == null || TutorialUI.Instance == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= showRadius && !isShown)
        {
            // Stop showing once the shared limit has been reached
            if (HasReachedLimit()) return;

            isShown = true;
            shownCounts[Key] = GetCount() + 1; // Count this appearance
            TutorialUI.Instance.ShowPrimary(message, this);
        }
        else if (distance > showRadius && isShown)
        {
            isShown = false;
            TutorialUI.Instance.HidePrimary(this);
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
