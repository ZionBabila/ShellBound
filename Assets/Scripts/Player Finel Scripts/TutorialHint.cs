using UnityEngine;

// =============================================================================
// TutorialHint
// -----------------------------------------------------------------------------
// Role:    Attach to any world element (shell pickup, light/heavy box).
//          While the player is within 'showRadius', it shows 'message' in the
//          single tutorial window for 'displayDuration' seconds, hides it, and
//          shows it again for as long as the player stays in range (a loop).
// Notes:   - Self-contained proximity check (no extra trigger collider needed).
//          - The "after wearing" line (e.g. SPACE to roll) is stored in
//            'wornMessage' and shown by the shell system, because this object is
//            destroyed on pickup.
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
    [Tooltip("Text shown on proximity. Example: 'Press E to wear'.")]
    [TextArea(2, 3)]
    public string message = "Press a key";

    [Tooltip("Text shown AFTER the player wears this (shells only), by the shell " +
             "system. Example: 'SPACE to roll'. Leave empty for boxes or single-line hints.")]
    [TextArea(2, 3)]
    public string wornMessage;

    [Header("Timing")]
    [Tooltip("Seconds the hint stays visible each time it appears. It then hides " +
             "for the same time and reappears while the player is still in range.")]
    public float displayDuration = 3f;

    [Header("Detection")]
    [Tooltip("How close (in units) the player must be for the hint to appear.")]
    public float showRadius = 2.5f;

    [Tooltip("Tag used to find the player object in the scene.")]
    public string playerTag = "Player";

    [Tooltip("Only show this hint when the player wears a specific shell (or none). " +
             "'Any' = no condition. Useful for hints tied to a puzzle that needs a certain shell.")]
    public ShellRequirement requiredShell = ShellRequirement.Any;

    private Transform player;
    private PlayerShellSystem shellSystem; // Used to check the worn shell for 'requiredShell'.
    private bool isVisible = false;
    private float cycleTimer = 0f;         // Counts down the current show/hide phase.

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            player = playerObj.transform;
            // The shell system lives on the player root; search up in case the tag is on a child collider.
            shellSystem = playerObj.GetComponentInParent<PlayerShellSystem>();
        }

        // DEBUG: report what this hint found on startup.
        Debug.Log($"[TutorialHint] '{name}' Start -> player found: {player != null}, " +
                  $"TutorialUI.Instance: {TutorialUI.Instance != null}", this);
    }

    private void Update()
    {
        if (player == null || TutorialUI.Instance == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        // Show only when both in range AND the worn-shell condition is met.
        // Both are re-checked every frame, so swapping shells nearby shows/hides the hint live.
        bool inRange = distance <= showRadius && IsShellRequirementMet();

        if (!inRange)
        {
            // Left range: hide and reset the cycle so it starts fresh next time.
            if (isVisible) SetVisible(false);
            cycleTimer = 0f;
            return;
        }

        // In range: run the show -> hide -> show loop. Each phase lasts 'displayDuration'.
        cycleTimer -= Time.deltaTime;
        if (cycleTimer <= 0f)
        {
            SetVisible(!isVisible);
            cycleTimer = displayDuration;
        }
    }

    // Toggles this hint's message in the shared tutorial window.
    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (visible)
        {
            // DEBUG: fires the moment the hint enters its "show" phase near the player.
            Debug.Log($"[TutorialHint] '{name}' SHOW -> \"{message}\"", this);
            TutorialUI.Instance.Show(message, this);
        }
        else
        {
            TutorialUI.Instance.Hide(this);
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

    // Make sure the hint disappears if this object is turned off or collected
    private void OnDisable()
    {
        if (isVisible && TutorialUI.Instance != null)
        {
            TutorialUI.Instance.Hide(this);
        }
        isVisible = false;
        cycleTimer = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the activation radius in the editor
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, showRadius);
    }
}
