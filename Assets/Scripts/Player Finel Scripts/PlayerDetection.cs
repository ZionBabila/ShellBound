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
            BaseShell foundShell = col.GetComponentInParent<BaseShell>();
            if (foundShell != null && foundShell.CurrentState == ShellState.OnGround)
            {
                shellSystem.EquipShell(foundShell);
                break; // Equip only one
            }
        }
    }

    // Detect trigger area collisions (like fall zones or water)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (GameManager.Instance != null)
        {
            // Send the hit object's tag and the player itself to the GameManager
            GameManager.Instance.HandlePlayerCollision(collision.gameObject.tag, gameObject);
        }
    }

    // Detect physical collider collisions (like spikes)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandlePlayerCollision(collision.gameObject.tag, gameObject);
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