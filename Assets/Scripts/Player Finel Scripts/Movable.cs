using UnityEngine;
using System.Collections;

/// <summary>
/// Defines an object that can be pushed and pulled by the player.
/// This script handles locking the object in place if the player is not strong enough.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Movable : MonoBehaviour
{
    private Rigidbody2D rb;
    private RigidbodyConstraints2D baseConstraints;
    private SimplePlayer simplePlayer;
    private Coroutine lockCoroutine;
    
    [Header("Grab Settings")]
    [Tooltip("Optional. A specific trigger collider that the player must hit to grab this object. If null, the main collider will be used.")]
    public Collider2D grabHandleTrigger;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Keep the crate upright: a pushable object should slide, never tip or rotate.
        // Without this, the FixedJoint2D would fix the relative angle and tilt the object on grab.
        rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        baseConstraints = rb.constraints;
        
        // If no specific grab handle is assigned, default to the object's main collider for backward compatibility.
        if (grabHandleTrigger == null)
            grabHandleTrigger = GetComponent<Collider2D>();

        simplePlayer = FindFirstObjectByType<SimplePlayer>();
    }

    private void FixedUpdate()
    {
        if (rb == null || simplePlayer == null) return;

        bool isHeavy = rb.mass > simplePlayer.heavyMassThreshold;
        bool canPush = simplePlayer.CanPushHeavyObjects;
        bool isPlayerGrounded = simplePlayer.IsGrounded;

        // An object is "grabbed" when the player's grab joint (TargetJoint2D, on the object)
        // is connected to THIS object's Rigidbody. SimplePlayer exposes it via GrabbedBody.
        bool isGrabbed = simplePlayer.GrabbedBody == rb;

        // Freeze position if:
        // 1. The player isn't grabbing it.
        // 2. The object is too heavy for the player.
        // 3. The player is grabbing it but is in the air.
        bool shouldLock = !isGrabbed || (isGrabbed && !isPlayerGrounded); // Lock if not grabbed, or if grabbed but in the air.

        // The lock for heavy objects should only apply IF the object is actually heavy.
        if (isHeavy && !canPush) shouldLock = true;

        if (shouldLock)
        {
            // If we need to lock and no locking coroutine is running, start one.
            if (lockCoroutine == null)
            {
                lockCoroutine = StartCoroutine(LockAfterDelay(0.5f));
            }
        }
        else
        {
            // If a locking coroutine was in progress, stop it because the player can now move the object.
            if (lockCoroutine != null)
            {
                StopCoroutine(lockCoroutine);
                lockCoroutine = null;
            }

            // Unlock X when actively grabbed
            rb.constraints = baseConstraints;
        }

        // Handle dragging sound if the object is actually moving horizontally
        if (Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            if (AudioManager.instance != null)
                AudioManager.moveSound = true;
        }
    }

    private IEnumerator LockAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // After the delay, apply the locked constraints
        rb.constraints = baseConstraints | RigidbodyConstraints2D.FreezePositionX;
        
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
        TryAssignMovableLayer(); 
    }

    private void TryAssignMovableLayer()
    {
        // Helper function to handle the object's layer assignment
        int movableLayer = LayerMask.NameToLayer("Movable"); 
        if (movableLayer < 0) return; // Prevent crash if layer doesn't exist
        if (gameObject.layer != movableLayer) gameObject.layer = movableLayer; 
    }
}