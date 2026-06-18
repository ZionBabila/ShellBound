using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AcidDrop : MonoBehaviour
{
    [Header("Acid Settings")]
    [Tooltip("The tag sent to the GameManager when hitting the player (e.g. 'Acid' or 'Spikes').")]
    public string hazardTag = "Acid";
    
    [Tooltip("How many seconds before the drop destroys itself to prevent memory leaks.")]
    public float lifeTime = 5.0f;

    private void Start()
    {
        // Auto-destroy the drop after a few seconds if it falls endlessly into the void
        Destroy(gameObject, lifeTime);
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
        if (hitObject.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.HandlePlayerCollision(hazardTag, hitObject);
                
                // NOTE FOR THE FUTURE: 
                // If a Health System is added later, you can replace the above line with something like:
                // hitObject.GetComponent<PlayerHealth>().TakeDamage(1);
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