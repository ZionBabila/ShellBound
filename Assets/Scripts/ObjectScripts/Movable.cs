using UnityEngine;

// =============================================================================
// Movable
// -----------------------------------------------------------------------------
// Role:        Marker component for objects the player can push and pull.
//              The player passes through Movables by default (configured in the
//              Physics2D layer collision matrix: Player <-> Movable = ignore).
//              When the player holds Ctrl near a Movable, PlayerController
//              attaches a FixedJoint2D so the item moves rigidly with the crab.
// Requires:    Rigidbody2D (Dynamic) and a Collider2D on the same GameObject.
// =============================================================================
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Movable : MonoBehaviour
{
    [Tooltip("If true, the object's layer is forced to 'Movable' in the editor for safety.")]
    public bool autoAssignLayer = true; // Determines whether to automatically assign the "Movable" layer to the object in the Unity Editor

    private Rigidbody2D rb; // Reference to the object's Rigidbody component
    private RigidbodyConstraints2D baseConstraints; // Stores original physics constraints (e.g. if object shouldn't rotate)
    private PlayerController player; // Reference pointing to our player to read state data

    private void Awake()
    {
        // Awake is called automatically when the game starts, used for initialization
        rb = GetComponent<Rigidbody2D>(); // Gets the physics component from current object and stores it
        baseConstraints = rb.constraints; // Stores constraints already defined in the inspector
        player = FindAnyObjectByType<PlayerController>(); // Scans the scene, finds player and stores reference
    }

    private void FixedUpdate()
    {
        // FixedUpdate runs in sync with physics engine (usually 50 FPS), perfect for physics math
        if (rb == null || player == null) return; // Safety check for missing components

        bool isHeavy = rb.mass >= player.heavyObjectMassThreshold;

        if (isHeavy && !player.canPushHeavyObjects) 
        {
            // Lock all axes so the player can't move the box, and it won't bounce
            rb.constraints = baseConstraints | RigidbodyConstraints2D.FreezeAll; 
        }
        else
        {
            rb.constraints = baseConstraints; 
        }
    }

    private void Reset()
    {
        // Default the Rigidbody2D to Dynamic so push/pull works out of the box.
        Rigidbody2D currentRb = GetComponent<Rigidbody2D>(); 
        if (currentRb != null) currentRb.bodyType = RigidbodyType2D.Dynamic; 

        TryAssignMovableLayer(); 
    }

    private void OnValidate()
    {
        // OnValidate runs in Unity Editor when a value is changed in the Inspector
        if (autoAssignLayer) TryAssignMovableLayer(); 
    }

    private void TryAssignMovableLayer()
    {
        // Helper function to handle the object's layer assignment
        int movableLayer = LayerMask.NameToLayer("Movable"); 
        if (movableLayer < 0) return; // Prevent crash if layer doesn't exist
        if (gameObject.layer != movableLayer) gameObject.layer = movableLayer; 
    }
}
