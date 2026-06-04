using UnityEngine;

// Data structure that creates the requested fields in the Inspector
[System.Serializable]
public struct TeleportZone
{
    [Tooltip("The hazard's tag (e.g., 'Spikes', 'Water', 'FallZone')")]
    public string hazardTag;
    
    [Tooltip("The Transform point the player will be teleported to when hitting this tag")]
    public Transform respawnPoint;
}

public class GameManager : MonoBehaviour
{
    // Singleton - so we can access the GameManager from any other script easily
    public static GameManager Instance { get; private set; }

    [Header("Respawn & Teleport System")]
    [Tooltip("Array defining where the player goes when hitting objects by tags")]
    public TeleportZone[] teleportZones;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Function called every time the player touches something
    public void HandlePlayerCollision(string hitTag, GameObject player)
    {
        if (teleportZones == null || teleportZones.Length == 0) return;

        // Iterate over the array to check if the hit tag is defined in the list
        foreach (TeleportZone zone in teleportZones)
        {
            if (zone.hazardTag == hitTag)
            {
                Debug.Log($"[GameManager] Match found for tag '{hitTag}'. Teleporting player.");
                if (zone.respawnPoint != null)
                {
                    TeleportPlayer(player, zone.respawnPoint);
                }
                else
                {
                    Debug.LogWarning($"[GameManager] Respawn point for tag '{hitTag}' is missing in the Inspector!");
                }
                return; // Found a match and handled it, stop searching
            }
        }
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