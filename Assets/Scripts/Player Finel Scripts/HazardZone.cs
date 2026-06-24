using UnityEngine;

// =============================================================================
// HazardZone
// -----------------------------------------------------------------------------
// Role:    A generic component for any hazardous object (Lava, Spikes, etc.).
//          It communicates with the GameManager to trigger a player respawn.
// Usage:   Attach this to a GameObject with a Collider2D (set to Trigger).
//          - If 'Hazard ID' is set, it uses the specific ID-based respawn system.
//          - If 'Hazard ID' is empty, it falls back to the tag-based system.
// =============================================================================
[RequireComponent(typeof(Collider2D))]
public class HazardZone : MonoBehaviour
{
    [Header("Identification")]
    [Tooltip("OPTIONAL: A unique ID for this hazard zone (e.g., 'lava_pit_1'). If set, the GameManager will use the 'ID Links' system for a specific respawn. If empty, it will use the object's tag for the 'Hazard Links' fallback system.")]
    public string hazardID;

    // This component is designed to work primarily with Triggers
    private void OnTriggerEnter2D(Collider2D other)
    {
        // A more robust way to identify the player, instead of relying on a tag
        SimplePlayer player = other.GetComponentInParent<SimplePlayer>();
        if (player != null)
        {
            HandleCollision(player.gameObject);
        }
    }

    private void HandleCollision(GameObject player)
    {
        if (GameManager.Instance == null) return; // Can't do anything without a GameManager

        // Pass the collision to the GameManager. It will handle all logic,
        // including checking for shells and finding the right respawn point.
        if (!string.IsNullOrEmpty(hazardID))
        {   // HazardZone does NOT respect shell protection (e.g., lava kills you even with a shell)
            GameManager.Instance.HandleHazardCollision(hazardID, player, false); 
        }
        else
        {   // Fallback to tag, also does NOT respect shell protection
            GameManager.Instance.HandleHazardCollision(gameObject.tag, player, false);
        }
    }
}