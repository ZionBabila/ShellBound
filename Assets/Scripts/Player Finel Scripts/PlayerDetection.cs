using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerDetection : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Radius around the player to search for shells.")]
    public float interactRadius = 1.5f;
    
    [Tooltip("Offset for the interaction circle (X will flip based on facing direction).")]
    public Vector2 interactOffset = Vector2.zero;

    [Tooltip("Layer mask for shells.")]
    public LayerMask shellLayer;

    private PlayerInputHandler inputHandler;
    private PlayerShellSystem shellSystem;

    private void Awake()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
        shellSystem = GetComponent<PlayerShellSystem>();

        if (inputHandler != null)
        {
            inputHandler.OnInteract += HandleInteract;
        }
    }

    private void OnDestroy()
    {
        if (inputHandler != null)
        {
            inputHandler.OnInteract -= HandleInteract;
        }
    }

    private void HandleInteract()
    {
        if (shellSystem == null) return;

        // 1. If we have a shell, throw it
        if (shellSystem.HasShell)
        {
            shellSystem.ThrowCurrentShell();
            return;
        }

        // Calculate interaction center with facing direction
        float facingDir = shellSystem.VisualsRoot != null ? Mathf.Sign(shellSystem.VisualsRoot.localScale.x) : 1f;
        Vector2 checkPosition = (Vector2)transform.position + new Vector2(interactOffset.x * facingDir, interactOffset.y);

        // 2. Search for a shell on the ground
        Collider2D[] colliders = Physics2D.OverlapCircleAll(checkPosition, interactRadius, shellLayer);
        foreach (Collider2D col in colliders)
        {
            ShellPickup foundPickup = col.GetComponentInParent<ShellPickup>();
            if (foundPickup != null)
            {
                shellSystem.EquipShell(foundPickup);
                break; // Equip only one
            }
        }
    }

    // Detect trigger area collisions (like fall zones or water)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // --- CRITICAL FIX ---
        // If the colliding object has its own logic (like AcidDrop or HazardZone), let it handle itself.
        // This prevents PlayerDetection from hijacking the collision and using the generic tag system.
        if (collision.GetComponent<AcidDrop>() != null || 
            collision.GetComponent<HazardZone>() != null)
        {
            return; // Do nothing, let AcidDrop handle its own collision.
        }
        if (GameManager.Instance != null)
        {
            // Send the hit object's tag and the player itself to the GameManager
            GameManager.Instance.HandleHazardCollision(collision.gameObject.tag, gameObject, false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        
        Vector2 checkPos = (Vector2)transform.position + interactOffset;
        
        // Try to flip offset dynamically if the game is running
        if (Application.isPlaying && shellSystem != null && shellSystem.VisualsRoot != null)
        {
            checkPos.x = transform.position.x + (interactOffset.x * Mathf.Sign(shellSystem.VisualsRoot.localScale.x));
        }
        
        // Draw the interaction radius in the editor
        Gizmos.DrawWireSphere(checkPos, interactRadius);
    }
}