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

    [Tooltip("Delay (seconds) after release before the X axis locks. Lets the object keep sliding briefly with its momentum. Set to 0 to lock instantly.")]
    public float lockDelay = 0.5f;

    [Header("Drag Sound")]
    [Tooltip("Time in seconds between drag-sound 'steps' while the object is being pushed/pulled.")]
    public float dragSoundInterval = 0.3f;

    [Tooltip("Minimum horizontal speed the object must have for the drag sound to play.")]
    public float dragMinSpeed = 0.1f;

    [Tooltip("How far below the object to look for the surface it's scraping (picks metal vs normal drag sound).")]
    public float dragGroundCheckDistance = 0.4f;

    private Collider2D bodyCollider;   // Physical (non-trigger) collider, used to find the surface under the object.
    private float dragTimer = 0f;      // Counts down to the next drag-sound step.
    private bool wasDragging = false;  // Tracks drag state so we cut the sound only on the stop edge.

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Allow rotation so the crate rests flush on slopes (gravity + contact align it naturally).
        // Safe now that push/pull is driven manually (no joint), so nothing artificially tilts it.
        rb.constraints &= ~RigidbodyConstraints2D.FreezeRotation;
        baseConstraints = rb.constraints;
        
        // If no specific grab handle is assigned, default to the object's main collider for backward compatibility.
        if (grabHandleTrigger == null)
            grabHandleTrigger = GetComponent<Collider2D>();

        // Pick a non-trigger collider as the physical body (used to raycast the surface underneath).
        foreach (Collider2D c in GetComponents<Collider2D>())
        {
            if (!c.isTrigger) { bodyCollider = c; break; }
        }
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();

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
            // Only start the lock sequence once (when not already locking and X isn't frozen yet).
            if (lockCoroutine == null && (rb.constraints & RigidbodyConstraints2D.FreezePositionX) == 0)
            {
                // Freeze rotation immediately so the player can't spin the object by walking into it.
                // X stays free for now so the object can keep sliding with its momentum; the coroutine
                // freezes X after the delay.
                rb.constraints = baseConstraints | RigidbodyConstraints2D.FreezeRotation;
                lockCoroutine = StartCoroutine(LockAfterDelay(lockDelay));
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

            // Actively grabbed: free both X and rotation (rotation free lets it rest flush on slopes).
            rb.constraints = baseConstraints;
        }

        HandleDragSound(isGrabbed);
    }

    // Plays a drag sound at a steady cadence while the player is actively pushing/pulling this
    // object and it's moving. Uses a dedicated interruptible source (no cacophony) and cuts the
    // sound the instant the object stops. Picks the metal clip based on the surface underneath.
    private void HandleDragSound(bool isGrabbed)
    {
        // Require BOTH the object and the player to be moving. The player check stops a "fake" drag
        // sound while pulling, where the spring correction can nudge the object while the crab stands still.
        bool playerMoving = simplePlayer != null && Mathf.Abs(simplePlayer.Rb.linearVelocity.x) > dragMinSpeed;
        bool dragging = isGrabbed && playerMoving && Mathf.Abs(rb.linearVelocity.x) > dragMinSpeed;

        if (!dragging)
        {
            // Cut the drag sound only on the transition to not-dragging, not every frame.
            if (wasDragging && AudioManager.instance != null) AudioManager.instance.StopDrag();
            wasDragging = false;
            dragTimer = 0f; // Reset so the first drag step fires immediately when dragging resumes.
            return;
        }
        wasDragging = true;

        dragTimer -= Time.fixedDeltaTime;
        if (dragTimer <= 0f)
        {
            if (AudioManager.instance != null) AudioManager.instance.PlayDrag(IsDraggingOnMetal());
            dragTimer = dragSoundInterval;
        }
    }

    // Raycasts down from the object to see whether the surface it's scraping is metal-tagged.
    private bool IsDraggingOnMetal()
    {
        if (simplePlayer == null || bodyCollider == null) return false;

        Vector2 origin = bodyCollider.bounds.center;
        float distance = bodyCollider.bounds.extents.y + dragGroundCheckDistance;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, distance, simplePlayer.groundLayer);
        foreach (RaycastHit2D hit in hits)
        {
            // Skip triggers and this object's own colliders.
            if (hit.collider.isTrigger || hit.collider.attachedRigidbody == rb) continue;

            // Trim so a stray space in the tag name (e.g. "Metal ") still matches.
            return hit.collider.tag.Trim() == simplePlayer.metalGroundTag.Trim();
        }
        return false;
    }

    private IEnumerator LockAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // After the delay, lock both the X position and rotation so the object stays put and
        // can't be pushed or spun by the player walking into it.
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