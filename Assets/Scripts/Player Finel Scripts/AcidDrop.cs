using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AcidDrop : MonoBehaviour
{
    [Header("Acid Settings")]
    [Tooltip("The tag sent to the GameManager when hitting the player (e.g. 'Acid' or 'Spikes').")]
    public string hazardTag = "Acid";
    
    [Tooltip("How many seconds before the drop destroys itself to prevent memory leaks.")]
    public float lifeTime = 5.0f;

    // The ID of the barrel that spawned this drop. Set by AcidBarrel.cs
    public string sourceBarrelID { get; set; }

    private void Start()
    {
        // Auto-destroy the drop after a few seconds if it falls endlessly into the void
        Destroy(gameObject, lifeTime);
    }

    private void OnDestroy()
    {
        // This is called right before the object is destroyed, for any reason.
        // It's a safe way to play a sound without worrying about race conditions.
        // We call the new method that checks if the drop is on-screen.
        if (AudioManager.instance != null)
            AudioManager.PlayAcidDropDestroySound(transform.position);
    }

    // Support for both Trigger colliders and regular physical colliders
    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void HandleCollision(GameObject hitObject)
    {
        // Use a more robust check for the player
        SimplePlayer player = hitObject.GetComponentInParent<SimplePlayer>();
        if (player != null)
        {
            // Pass the collision to the GameManager. It will handle all logic,
            // including checking for shells and finding the right respawn point.
            if (GameManager.Instance != null)
            {
                if (!string.IsNullOrEmpty(sourceBarrelID))
                    GameManager.Instance.HandleHazardCollision(sourceBarrelID, player.gameObject, true); // AcidDrop respects shell protection
                else
                    GameManager.Instance.HandleHazardCollision(hazardTag, player.gameObject, true); // AcidDrop respects shell protection
            }
            Destroy(gameObject);
        }
        // Destroy only if hitting the ground (by Ground layer or tag)
        else if (hitObject.layer == LayerMask.NameToLayer("Ground") || hitObject.tag == "Ground")
        {
            Destroy(gameObject);
        }
        // Destroy if hitting movable boxes
        else if (hitObject.layer == LayerMask.NameToLayer("Movable") || hitObject.tag == "Movable")
        {
            Destroy(gameObject);
        }
    }
}