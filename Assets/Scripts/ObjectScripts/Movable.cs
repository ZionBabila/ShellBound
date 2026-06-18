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
    private PlayerController oldPlayer; // Reference pointing to the old player script
    private SimplePlayer simplePlayer; // Reference pointing to our new player script
    
    private void Awake()
    {
        // Awake is called automatically when the game starts, used for initialization
        rb = GetComponent<Rigidbody2D>(); // Gets the physics component from current object and stores it
        
        // Z rotation released for testing (explicitly removing FreezeRotation)
        rb.constraints &= ~RigidbodyConstraints2D.FreezeRotation;
        
        baseConstraints = rb.constraints; // Stores constraints already defined in the inspector
        oldPlayer = FindFirstObjectByType<PlayerController>(); // Scans the scene, finds old player
        simplePlayer = FindFirstObjectByType<SimplePlayer>(); // Scans the scene, finds new player
    }

    private void FixedUpdate()
    {
        // FixedUpdate runs in sync with physics engine (usually 50 FPS), perfect for physics math
        if (rb == null) return; // Safety check for missing components
        
        bool isHeavy = false;
        bool canPush = false;
        bool playerFound = false;
        bool isGrabbed = false;

        if (simplePlayer != null)
        {
            isHeavy = rb.mass > simplePlayer.heavyMassThreshold;
            canPush = simplePlayer.CanPushHeavyObjects;
            playerFound = true;
            
            // Check if SimplePlayer is currently grabbing THIS specific object
            FixedJoint2D joint = simplePlayer.GetComponent<FixedJoint2D>();
            if (joint != null && joint.connectedBody == rb) isGrabbed = true;
        }
        else if (oldPlayer != null)
        {
            isHeavy = rb.mass >= oldPlayer.heavyObjectMassThreshold;
            canPush = oldPlayer.canPushHeavyObjects;
            playerFound = true;
            
            // Check if old PlayerController is currently grabbing THIS specific object
            FixedJoint2D joint = oldPlayer.GetComponent<FixedJoint2D>();
            if (joint != null && joint.connectedBody == rb) isGrabbed = true;
        }

        if (!playerFound) return;

        // Freeze position if the player isn't grabbing it with Ctrl, OR if it's too heavy
        if (!isGrabbed || (isHeavy && !canPush)) 
        {
            // Lock ONLY the X axis. Y remains open for gravity. Z rotation is free for testing.
            rb.constraints = baseConstraints | RigidbodyConstraints2D.FreezePositionX; 
        }
        else
        {
            // Unlock X when actively grabbed, Z remains free, Y is always free
            rb.constraints = baseConstraints; 
        }

        // Handle dragging sound if the object is actually moving horizontally
        if (Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            AudioManager.moveSound = true;
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
