using UnityEngine;
using System.Collections;

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

    // Coroutine for handling the lock delay
    private Coroutine lockCoroutine;
    
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
        bool isPlayerGrounded = false;

        if (simplePlayer != null)
        {
            isHeavy = rb.mass > simplePlayer.heavyMassThreshold;
            canPush = simplePlayer.CanPushHeavyObjects;
            playerFound = true;
            isPlayerGrounded = simplePlayer.IsGrounded;
            
            // Check if SimplePlayer is currently grabbing THIS specific object
            FixedJoint2D joint = simplePlayer.GetComponent<FixedJoint2D>();
            if (joint != null && joint.connectedBody == rb) isGrabbed = true;
        }
        else if (oldPlayer != null) // Fallback for the old player controller
        {
            isHeavy = rb.mass >= oldPlayer.heavyObjectMassThreshold;
            canPush = oldPlayer.canPushHeavyObjects;
            playerFound = true;
            isPlayerGrounded = oldPlayer.IsGrounded;
            
            // Check if old PlayerController is currently grabbing THIS specific object
            FixedJoint2D joint = oldPlayer.GetComponent<FixedJoint2D>();
            if (joint != null && joint.connectedBody == rb) isGrabbed = true;
        }

        if (!playerFound) return;

        // --- 1. הדפסה לקונסול ---
        Debug.Log($"[Movable] Player Grounded: {isPlayerGrounded}");

        // Freeze position if:
        // 1. The player isn't grabbing it.
        // 2. The object is too heavy for the player.
        // 3. The player is grabbing it but is in the air.
        bool shouldLock = !isGrabbed || (isHeavy && !canPush) || !isPlayerGrounded;

        if (shouldLock)
        {
            // If we need to lock and no locking coroutine is running, start one.
            // This check prevents starting multiple coroutines.
            if (lockCoroutine == null)
            {
                lockCoroutine = StartCoroutine(LockAfterDelay(0.5f)); // 0.5 second delay
            }
        }
        else
        {
            // If a locking coroutine was in progress, stop it because the player grabbed the object again.
            if (lockCoroutine != null)
            {
                StopCoroutine(lockCoroutine);
                lockCoroutine = null;
            }

            // Unlock X when actively grabbed, Z remains free, Y is always free
            rb.constraints = baseConstraints;
        }

        // Handle dragging sound if the object is actually moving horizontally
        if (Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            AudioManager.moveSound = true;
        }
    }

    // --- 2. קורוטינה להשהיית הנעילה ---
    private IEnumerator LockAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // After the delay, apply the locked constraints
        rb.constraints = baseConstraints | RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        
        lockCoroutine = null; // Signal that the coroutine has finished
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
