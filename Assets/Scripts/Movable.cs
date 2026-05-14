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
    public bool autoAssignLayer = true;

    private void Reset()
    {
        // Default the Rigidbody2D to Dynamic so push/pull works out of the box.
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;

        TryAssignMovableLayer();
    }

    private void OnValidate()
    {
        if (autoAssignLayer) TryAssignMovableLayer();
    }

    private void TryAssignMovableLayer()
    {
        int movableLayer = LayerMask.NameToLayer("Movable");
        if (movableLayer < 0) return; // Layer not defined yet — silently skip.
        if (gameObject.layer != movableLayer) gameObject.layer = movableLayer;
    }
}
