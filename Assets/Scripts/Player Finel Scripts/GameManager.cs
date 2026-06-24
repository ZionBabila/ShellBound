using UnityEngine;

// The single, unified data structure for linking hazards to respawn points.
[System.Serializable]
public struct RespawnLink
{
    [Tooltip("The unique ID or Tag of the hazard (e.g., 'barrel_sewer_01', 'Lava', 'Spikes'). This is case-insensitive.")]
    public string sourceID;

    [Tooltip("The Transform point the player will be teleported to when hitting this hazard.")]
    public Transform respawnPoint;
}

public class GameManager : MonoBehaviour
{
    // Singleton - so we can access the GameManager from any other script easily
    public static GameManager Instance { get; private set; }

    [Header("Respawn & Teleport System")]
    [Tooltip("Links a hazard's ID or Tag to a specific respawn point.")]
    public RespawnLink[] respawnLinks;

    private bool isPlayerRespawning = false; // Flag to prevent multiple death sequences at once

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    // Function called by hazards (like AcidDrop or trigger zones) when they hit the player.
    // This is now the ONLY function for handling hazard collisions.
    public void HandleHazardCollision(string idOrTag, GameObject player, bool respectsShellProtection)
    {
        // If a respawn sequence is already running, do nothing.
        if (isPlayerRespawning || string.IsNullOrEmpty(idOrTag) || respawnLinks == null || respawnLinks.Length == 0) return;

        // Iterate over the array to find a matching ID or Tag
        foreach (RespawnLink link in respawnLinks)
        {
            // Use a case-insensitive and whitespace-trimmed comparison for robustness.
            // This prevents hard-to-find errors from typos like "Pit1" vs "pit1 " in the Inspector.
            if (link.sourceID != null && link.sourceID.Trim().Equals(idOrTag.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[GameManager] Hazard '{idOrTag}' hit player. RespectsShellProtection: {respectsShellProtection}.");

                // --- VULNERABILITY CHECK ---
                // Before starting the death sequence, check if the player is actually vulnerable.
                // This check is skipped if the hazard does NOT respect shell protection (e.g., lava).
                if (respectsShellProtection)
                {
                    if (!IsPlayerVulnerable(player))
                    {
                        Debug.Log($"[GameManager] Player hit by '{idOrTag}', but is protected by a shell. Ignoring.");
                        return; // Player is protected, do nothing.
                    }
                }
                
                if (link.respawnPoint != null)
                {
                    // Start the death sequence instead of teleporting immediately
                    StartCoroutine(PlayerDeathSequence(player, link.respawnPoint));
                }
                else
                {
                    Debug.LogWarning($"[GameManager] Respawn point for ID/Tag '{idOrTag}' is missing in the Inspector!");
                }
                return; // Found a match and handled it, stop searching
            }
        }
        Debug.LogWarning($"[GameManager] Hazard with ID/Tag '{idOrTag}' hit the player, but no matching link was found in GameManager.");
    }

    // Helper function to check if the player is vulnerable (not wearing a shell)
    private bool IsPlayerVulnerable(GameObject player)
    {
        PlayerShellSystem shellSystem = player.GetComponent<PlayerShellSystem>();
        if (shellSystem == null) {
            Debug.Log($"[GameManager.IsPlayerVulnerable] PlayerShellSystem NOT found on {player.name}. Player is vulnerable.");
            return true; // No shell system, player is vulnerable
        }
        Debug.Log($"[GameManager.IsPlayerVulnerable] PlayerShellSystem found on {player.name}. HasShell: {shellSystem.HasShell}.");
        return !shellSystem.HasShell; // Player is vulnerable if they have a shell system but no shell equipped.
    }
    private System.Collections.IEnumerator PlayerDeathSequence(GameObject player, Transform respawnPoint)
    {
        isPlayerRespawning = true;
        Debug.Log($"[GameManager] Player hit. Starting death sequence...");

        // --- Step 1: Freeze Player & Start Animation ---
        SimplePlayer playerController = player.GetComponent<SimplePlayer>();
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        
        if (playerController != null) playerController.isMovementDisabled = true;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.isKinematic = true;
        }

        // Play sound, etc.

        PlayerAnimation playerAnimation = player.GetComponentInChildren<PlayerAnimation>();
        if (playerAnimation != null)
        {
            // The animation itself will now call FinishPlayerRespawn when it's done.
            playerAnimation.PlayDeathAnimation(player, respawnPoint);
        }
        else
        {
            // Fallback if no animation system is found: wait a moment and then respawn.
            Debug.LogWarning("PlayerAnimation component not found. Using simple delay for respawn.");
            yield return new WaitForSeconds(0.5f);
            FinishPlayerRespawn(player, respawnPoint);
        }
    }

    /// <summary>
    /// This function is called by PlayerAnimation when the death animation is complete.
    /// It handles the actual teleport and state reset.
    /// </summary>
    public void FinishPlayerRespawn(GameObject player, Transform respawnPoint)
    {
        SimplePlayer playerController = player.GetComponent<SimplePlayer>();
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (rb != null) rb.isKinematic = false;
        TeleportPlayer(player, respawnPoint);

        if (playerController != null) playerController.isMovementDisabled = false;
        isPlayerRespawning = false;
    }
    private void TeleportPlayer(GameObject player, Transform targetPoint)
    {
        // 1. Reset physical velocity so the player doesn't continue "falling" or flying immediately after teleporting
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 2. Move the player to the requested point
        player.transform.position = targetPoint.position;
    }
}