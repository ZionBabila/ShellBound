using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SimplePlayer : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("The force applied to move the player.")]
    public float speed = 50f;
    
    [Tooltip("The maximum movement speed.")]
    public float maxSpeed = 8f;
    
    [Tooltip("How fast the player slows down when there is no input.")]
    public float deceleration = 10f;

    [Tooltip("Extra braking force applied when turning around, to prevent 'ice skating'. Higher values give a snappier feel.")]
    [Range(0f, 1f)]
    public float turnAroundBrake = 0.5f;

    [Header("Visuals & Slopes")]
    [Tooltip("The main empty GameObject containing the rig. Used for slope rotation and flipping.")]
    public Transform visualsRoot;

    [Tooltip("How fast the visuals rotate to align with the slope.")]
    public float rotationSpeed = 10f;

    [Header("Slope Handling")]
    [Tooltip("Slopes steeper than this angle (degrees) make the player slide down instead of gripping.")]
    public float maxSlopeAngle = 45f;

    [Tooltip("Downhill force applied while sliding on a steep slope.")]
    public float slideForce = 30f;

    [Tooltip("Maximum speed reached while sliding down a steep slope.")]
    public float maxSlideSpeed = 12f;

    [Header("Push / Pull (Movable)")]
    [Tooltip("Items on this layer can be grabbed with Ctrl for push/pull.")]
    public LayerMask movableLayer;

    [Tooltip("Local-space offset from the player toward the facing direction where the grab probe is placed.")]
    public Vector2 grabCheckOffset = new Vector2(0.5f, 0.0f);

    [Tooltip("Radius of the grab probe used to find a Movable in front of the crab.")]
    public float grabCheckRadius = 0.4f;

    [Tooltip("How tightly a grabbed object keeps its hold offset (horizontal drift correction). Higher = stiffer.")]
    public float grabFollowStiffness = 12f;

    [Tooltip("Maximum extra speed the drift correction can add, to avoid snapping when the object is blocked.")]
    public float grabMaxCorrectionSpeed = 6f;

    [Tooltip("Caps the player's max speed while grabbing a Movable, so dragging feels heavy instead of weightless. 1 = no penalty, 0.5 = half speed.")]
    [Range(0.1f, 1f)]
    public float grabSpeedMultiplier = 0.5f;

    [Tooltip("Objects with mass greater than this are considered 'heavy' and require a special shell.")]
    public float heavyMassThreshold = 3.0f;


    [Header("Components")]
    [Tooltip("The regular collider used for walking without a shell.")]
    public Collider2D standingCollider;

    [Tooltip("The collider used when carrying a shell on the back. Taller/Wider so it gets stuck in narrow paths.")]
    public Collider2D withShellCollider;

    [Tooltip("The round collider used for rolling. MUST be on this main GameObject.")]
    public Collider2D rollingCollider;

    [Header("Footsteps")]
    [Tooltip("Time in seconds between footstep sounds while walking.")]
    public float footstepInterval = 0.35f;

    [Tooltip("Minimum horizontal speed required to play footstep sounds.")]
    public float footstepMinSpeed = 0.5f;

    [Tooltip("Ground with this tag plays the metal footstep sound instead of the default one.")]
    public string metalGroundTag = "Metal";

    [Tooltip("TEMP: logs the surface under the player on each step (name/tag/layer) to diagnose footstep issues.")]
    public bool footstepDebug = false;

    [Header("Debug")]
    [Tooltip("Print movement forces and speeds to the console.")]
    public bool showSpeedDebug = false;

    [Header("Ground Detection")]
    [Tooltip("Offset from the player's center to start the ground check raycast.")]
    public Vector2 groundCheckOffset = new Vector2(0, 0f); // Start from player center to avoid getting stuck in slopes
    
    [Tooltip("Distance between the outermost ground rays, centred on groundCheckOffset. Hand-tuned by feel - the collider's bounding box is NOT a good substitute here.")]
    public float groundCheckWidth = 0.5f;

    [Tooltip("Probe width while carrying a shell (withShellCollider active). Leave at 0 to just use groundCheckWidth.")]
    public float withShellCheckWidth = 0f;

    [Tooltip("Probe width while rolling (rollingCollider active). Leave at 0 to just use groundCheckWidth.")]
    public float rollingCheckWidth = 0f;

    [Tooltip("How many parallel ground rays to cast across the probe width. 3 matches the old left/center/right behaviour.")]
    [Range(2, 10)]
    public int groundRayCount = 3;

    public float groundCheckDistance = 1.0f; // Lengthen raycast to reach the ground safely
    public LayerMask groundLayer;

    [Tooltip("TUNING: run ground detection BEFORE movement so movement uses this frame's normal instead of last frame's. Off = the original (one frame stale) order. Flip in play mode to A/B the feel.")]
    public bool groundCheckBeforeMovement = true;

    
    private Rigidbody2D rb;
    private PlayerInputHandler inputHandler;
    private float moveInputX;
    private Vector2 surfaceNormal = Vector2.up;

    // Reused across FixedUpdate so the ground probes never allocate. Sized generously: a single
    // ray rarely stacks more than a couple of ground colliders.
    private readonly RaycastHit2D[] groundHitBuffer = new RaycastHit2D[8];
    private ContactFilter2D groundContactFilter;

    // Cached gravity so the grip logic can toggle it to 0 and restore it safely.
    private float defaultGravityScale = 1f;

    // Timer to temporarily disable aggressive braking for momentum preservation.
    private float preserveMomentumUntil = 0f;

    // The Rigidbody being dragged, or null when not grabbing. No joint is used: we drive it manually
    // on the horizontal axis and leave the vertical to gravity/collision so it rides slopes naturally.
    private Rigidbody2D grabbedBody;

    // Horizontal offset from the player captured at grab time, defining the hold distance.
    private float grabHoldOffsetX;
    private float grabDirection = 0f; // 0 = not grabbing, 1 = grabbed right, -1 = grabbed left

    // Authoritative facing sign (+1 = right, -1 = left), kept in sync with the visual flip.
    // Other systems (e.g. shell throwing) read this instead of guessing from visualsRoot.localScale.
    public float FacingDirection { get; private set; } = 1f;

    // Counts down to the next footstep sound; reset whenever the player isn't walking.
    private float footstepTimer = 0f;
    private bool wasWalking = false; // Tracks the walking state so we cut the sound only on the stop edge.
    private bool onMetalGround = false; // True when the closest ground under the player is tagged metal.
    private Collider2D groundCollider;  // The closest ground collider under the player (for footstep debug).

    [HideInInspector] public float currentSpeedMultiplier = 1f;

    [SerializeField, HideInInspector] 
    private bool _isMovementDisabled = false;

    public bool isMovementDisabled
    {
        get => _isMovementDisabled;
        set
        {
            _isMovementDisabled = value;
            if (value) ReleaseGrab(); // Safety check (Code Review 1.2)
        }
    }

    // True while a cinematic curtain (GameManager.TriggerGameStopCurtain) is holding the
    // player in place. Unlike isMovementDisabled, this zeroes gravity too (so a mid-air
    // player stays frozen instead of continuing to fall) but leaves Time.timeScale alone,
    // so Update()-driven animations keep playing normally during the freeze.
    private bool isPhysicsFrozen = false;
    public bool IsPhysicsFrozen => isPhysicsFrozen;

    public void SetPhysicsFrozen(bool frozen)
    {
        isPhysicsFrozen = frozen;
        rb.gravityScale = frozen ? 0f : defaultGravityScale;
        if (frozen) rb.linearVelocity = Vector2.zero;
    }

    // Public read-only properties (for animation system and other scripts)
    // Ignoring micro-movements so the animator gets a "clean" 0 when the player barely moves
    public float CurrentSpeed => Mathf.Abs(rb.linearVelocity.x) < 0.15f ? 0f : Mathf.Abs(rb.linearVelocity.x);
    public bool IsGrounded { get; private set; }

    // Previous frame's grounded state. Nothing reads it yet; it is the single field that coyote
    // time, landing detection and jump buffering all need once vertical mechanics arrive.
    public bool WasGroundedLastFrame { get; private set; }

    public bool IsGrabbing => grabbedBody != null;

    // The Rigidbody currently held, or null when not grabbing. Movable uses this to identify the grabbed object.
    public Rigidbody2D GrabbedBody => grabbedBody;
    
    // Property to be controlled by shells
    public bool CanPushHeavyObjects { get; set; } = false;

    // True while the heavy shell is performing a ground pound (heavy shell + airborne + Space held).
    // Read by PlayerAnimation to play the SkullSpin animation. Set by HeavyArmorShell.
    public bool IsGroundPounding { get; set; } = false;

    public Rigidbody2D Rb => rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravityScale = rb.gravityScale;
        inputHandler = GetComponent<PlayerInputHandler>();

        // Lock physics rotation to prevent flipping bugs.
        // All rotation is handled purely visually via visualsRoot.
        rb.freezeRotation = true;

        // useTriggers = false makes the cast itself skip triggers, so we don't pay for hits we
        // would only discard afterwards.
        groundContactFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = groundLayer
        };
    }

    private void OnDestroy()
    {
        // Input event subscriptions removed as grab is now handled by the shell.
    }

    private void FixedUpdate()
    {
        // Probing first means HandleMovement consumes THIS frame's surfaceNormal/IsGrounded.
        // The original order probed last, so movement ran on values that were one physics step
        // (20ms at 50Hz) old - visible when crossing from flat ground onto a slope.
        if (groundCheckBeforeMovement)
        {
            HandleGroundDetection();
        }

        if (isPhysicsFrozen)
        {
            // Re-zero every step: gravity/collisions could otherwise nudge velocity away from zero.
            rb.linearVelocity = Vector2.zero;
        }
        else if (!isMovementDisabled)
        {
            HandleMovement();
        }
        else
        {
            // While physics-controlled (rolling/hiding), never leave gravity zeroed by the grip logic.
            rb.gravityScale = defaultGravityScale;
        }

        if (IsGrabbing)
        {
            DriveGrabbedObject();
        }

        if (!groundCheckBeforeMovement)
        {
            HandleGroundDetection();
        }
    }

    private void Update()
    {
        // Pull movement value automatically from the existing input system
        if (inputHandler != null)
        {
            moveInputX = isMovementDisabled ? 0f : inputHandler.MoveValue.x;
        }

        if (!isMovementDisabled)
        {
            HandleVisualRotation();
        }

        HandleFootsteps();
    }

    // Plays a footstep sound at a steady cadence while the player walks on the ground.
    // Skipped while airborne, standing still, or physics-overridden (rolling/hiding).
    private void HandleFootsteps()
    {
        bool walking = IsGrounded && !isMovementDisabled && CurrentSpeed > footstepMinSpeed;

        if (!walking)
        {
            // Cut the step sound the instant we stop walking (only on the transition, not every frame).
            if (wasWalking && AudioManager.instance != null) AudioManager.instance.StopFootstep();
            wasWalking = false;
            footstepTimer = 0f; // Reset so the first step fires immediately when walking resumes.
            return;
        }
        wasWalking = true;

        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0f)
        {
            if (AudioManager.instance != null) AudioManager.instance.PlayFootstep(onMetalGround);
            footstepTimer = footstepInterval;

            // TEMP debug: shows exactly which surface drives the footstep choice.
            if (footstepDebug && groundCollider != null)
            {
                Debug.Log($"[Footstep] name='{groundCollider.name}' | tag='{groundCollider.tag}' | layer='{LayerMask.LayerToName(groundCollider.gameObject.layer)}' | metal={onMetalGround} (looking for tag '{metalGroundTag}')");
            }
        }
    }

    private void HandleMovement()
    {
        // Measure how steep the current ground is (0 = flat, 90 = vertical wall).
        // Rounded to whole degrees to match the walkable test in HandleGroundDetection, so a
        // surface can never be "walkable" there and "too steep" here on the same frame.
        float slopeAngle = Mathf.Round(Vector2.Angle(surfaceNormal, Vector2.up));
        bool onSteepSlope = IsGrounded && slopeAngle > maxSlopeAngle;

        // CASE A: Steep slope -> lose grip and slide downhill, ignoring player input
        if (onSteepSlope)
        {
            rb.gravityScale = defaultGravityScale;

            // Tangent of the slope, forced to point downhill (negative Y)
            Vector2 downSlope = new Vector2(surfaceNormal.y, -surfaceNormal.x).normalized;
            if (downSlope.y > 0) downSlope = -downSlope;

            rb.AddForce(downSlope * slideForce, ForceMode2D.Force);

            // Cap the slide so it doesn't accelerate forever
            if (rb.linearVelocity.magnitude > maxSlideSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSlideSpeed;
            }

            if (showSpeedDebug)
            {
                Debug.Log($"<color=red>SLIDING</color> | Angle: {slopeAngle:F1} > Max: {maxSlopeAngle} | Speed: {rb.linearVelocity.magnitude:F2}");
            }
            return;
        }

        float actualMaxSpeed = maxSpeed * currentSpeedMultiplier;

        // Dragging a heavy object should feel weighty: cap the player's top speed while grabbing.
        if (IsGrabbing) actualMaxSpeed *= grabSpeedMultiplier;

        // CASE B: Player is actively moving on walkable ground
        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            // Normal gravity while moving so walking off ledges feels natural
            rb.gravityScale = defaultGravityScale;

            // Calculate movement direction (parallel to slope if grounded)
            Vector2 moveDirection = Vector2.right * Mathf.Sign(moveInputX);

            if (IsGrounded && surfaceNormal != Vector2.up)
            {
                // Calculate the tangent to the slope to push along the surface
                moveDirection = new Vector2(surfaceNormal.y, -surfaceNormal.x).normalized * Mathf.Sign(moveInputX);

                // Ensure the vector points in the intended horizontal direction
                if (moveInputX < 0 && moveDirection.x > 0) moveDirection *= -1;
                if (moveInputX > 0 && moveDirection.x < 0) moveDirection *= -1;
            }

            // --- Counter-Movement Force (Anti-Skating) ---
            // If the player is trying to move in the opposite direction of their current velocity,
            // apply a braking force to make the turn feel snappier.
            float currentVelocityX = rb.linearVelocity.x;
            if (Mathf.Sign(moveInputX) != Mathf.Sign(currentVelocityX) && Mathf.Abs(currentVelocityX) > 0.1f)
            {
                rb.AddForce(Vector2.right * -currentVelocityX * (speed * turnAroundBrake), ForceMode2D.Force);
            }

            // Apply physics force - this automatically handles the weight of pushed objects natively!
            rb.AddForce(moveDirection * speed, ForceMode2D.Force);


            // Cap the maximum horizontal speed so the player doesn't accelerate infinitely
            Vector2 vel = rb.linearVelocity;
            if (Mathf.Abs(vel.x) > actualMaxSpeed)
            {
                vel.x = Mathf.Sign(vel.x) * actualMaxSpeed;
                rb.linearVelocity = vel;
            }

            if (showSpeedDebug)
            {
                Debug.Log($"<color=green>Velocity X:</color> {rb.linearVelocity.x:F2} / <color=yellow>Max:</color> {actualMaxSpeed:F2} | <color=cyan>Force Applied:</color> {speed} | <color=orange>Shell Multiplier:</color> {currentSpeedMultiplier:F2}");
            }
        }
        // CASE C: No input while grounded on a walkable slope -> GRIP (no drift)
        else if (IsGrounded)
        {
            // If an external system requested to preserve momentum, skip the aggressive brake.
            if (Time.time < preserveMomentumUntil)
            {
                rb.gravityScale = defaultGravityScale; // Let natural friction and gravity take over
                return;
            }

            // Zero gravity removes the force that causes downhill sliding entirely
            rb.gravityScale = 0f;

            // Smoothly bleed off any leftover momentum so stopping still feels natural
            Vector2 vel = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * deceleration);
            if (vel.magnitude < 0.05f) vel = Vector2.zero;
            rb.linearVelocity = vel;
        }
        // CASE D: No input in the air -> normal gravity, just damp horizontal drift
        else
        {
            rb.gravityScale = defaultGravityScale;

            Vector2 vel = rb.linearVelocity;
            vel.x = Mathf.Lerp(vel.x, 0, Time.fixedDeltaTime * deceleration);
            if (Mathf.Abs(vel.x) < 0.05f) vel.x = 0;
            rb.linearVelocity = vel;
        }
    }

    private void HandleGroundDetection()
    {
        GetGroundProbeSpan(out float minX, out float maxX, out float originY);
        
        int rayCount = Mathf.Max(2, groundRayCount);
        float span = maxX - minX;

        bool foundGround = false;
        bool foundWalkableGround = false;
        float smallestHitDistance = float.MaxValue;

        RaycastHit2D closestHit = new RaycastHit2D(); // Store the best valid hit

        for (int i = 0; i < rayCount; i++)
        {
            // Evenly spaced across the footprint. With rayCount = 3 this lands on
            // left / center / right, matching the original three-ray layout.
            float t = (float)i / (rayCount - 1);
            Vector2 origin = new Vector2(minX + span * t, originY);

            // Non-allocating cast into a reused buffer. The old RaycastAll built a fresh array
            // per ray, per FixedUpdate - roughly 150 throwaway arrays a second.
            int hitCount = Physics2D.Raycast(origin, Vector2.down, groundContactFilter, groundHitBuffer, groundCheckDistance);

            for (int h = 0; h < hitCount; h++)
            {
                RaycastHit2D hit = groundHitBuffer[h];

                // The filter already drops triggers; this still guards the player's own collider,
                // and any stray trigger if queriesHitTriggers was left on by another system.
                if (hit.collider == null) continue;
                if (hit.collider.isTrigger || hit.collider.gameObject == gameObject) continue;

                // Rounded for the same reason as in HandleMovement: raw normals jitter by
                // fractions of a degree at collider seams and would flip this test frame to frame.
                float hitAngle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.up));
                bool isHitWalkable = hitAngle < maxSlopeAngle;

                if (!foundGround)
                {
                    smallestHitDistance = hit.distance;
                    closestHit = hit;
                    foundGround = true;
                    foundWalkableGround = isHitWalkable;
                }
                else if (!foundWalkableGround && isHitWalkable)
                {
                    // Walkable ground beats non-walkable even when it is further away. Without
                    // this the crab slides while standing on flat floor whenever a steep ramp
                    // happens to sit marginally closer to one of the rays.
                    smallestHitDistance = hit.distance;
                    closestHit = hit;
                    foundWalkableGround = true;
                }
                else if (foundWalkableGround == isHitWalkable && hit.distance < smallestHitDistance)
                {
                    // Same category - nearest wins.
                    smallestHitDistance = hit.distance;
                    closestHit = hit;
                }
            }
        }

        WasGroundedLastFrame = IsGrounded;
        IsGrounded = foundGround;

        if (foundGround)
        {
            // Use the normal from ONLY the closest hit point for maximum stability.
            // This prevents averaging weird normals from collider seams.
            surfaceNormal = closestHit.normal;

            // Pick the footstep flavor from the surface we're actually standing on.
            // Use a plain string compare, not CompareTag: CompareTag throws if the tag isn't
            // defined in the project, which would spam every FixedUpdate and freeze the game.
            // Trim so a stray space in the tag name (e.g. "Metal ") still matches.
            onMetalGround = closestHit.collider.tag.Trim() == metalGroundTag.Trim();
            groundCollider = closestHit.collider;
        }
        else
        {
            surfaceNormal = Vector2.up;
            onMetalGround = false;
            groundCollider = null;
        }
    }

    /// <summary>
    /// The collider currently defining the player's footprint. PlayerShellSystem and RollingShell
    /// toggle these via .enabled, keeping exactly one active at a time.
    /// </summary>
    private Collider2D GetActiveCollider()
    {
        if (rollingCollider != null && rollingCollider.enabled) return rollingCollider;
        if (withShellCollider != null && withShellCollider.enabled) return withShellCollider;
        if (standingCollider != null && standingCollider.enabled) return standingCollider;
        return null;
    }

    /// <summary>
    /// Horizontal span and vertical origin for the ground probes. Shared by the detection pass and
    /// the gizmo so the drawing can never drift from what is actually being cast.
    /// </summary>
    private void GetGroundProbeSpan(out float minX, out float maxX, out float originY)
    {
        Vector2 center = (Vector2)transform.position + groundCheckOffset;
        originY = center.y;

        float width = GetActiveGroundCheckWidth();

        minX = center.x - width * 0.5f;
        maxX = center.x + width * 0.5f;
    }

    /// <summary>
    /// Probe width for whichever collider is active. Deliberately NOT derived from the collider's
    /// bounds. The tuned width is intentionally WIDER than any of the colliders (2.28 against a
    /// 1.29 capsule / 1.86 polygon), and that overhang is what smooths the slope normal: the rays
    /// sample ahead of and behind the footing instead of only under it. Deriving the span from
    /// bounds narrows it - asymmetrically so for the polygon - and slope reads got noisy.
    /// These stay hand-tuned; 0 means "no separate value, use groundCheckWidth".
    /// </summary>
    private float GetActiveGroundCheckWidth()
    {
        Collider2D active = GetActiveCollider();

        if (active == rollingCollider && rollingCheckWidth > 0f) return rollingCheckWidth;
        if (active == withShellCollider && withShellCheckWidth > 0f) return withShellCheckWidth;

        return groundCheckWidth;
    }

    private void HandleVisualRotation()
    {
        if (visualsRoot == null) return;

        Quaternion targetRotation;

        if (IsGrabbing)
        {
            // When grabbing, force the player to be upright to prevent the joint from breaking.
            targetRotation = Quaternion.identity;
        }
        else
        {
            // When not grabbing, align to the slope as usual.
            targetRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
        }
        
        // Smoothly rotate the sprite to prevent sharp "jumps" at floor seams
        visualsRoot.localRotation = Quaternion.Lerp(visualsRoot.localRotation, targetRotation, Time.deltaTime * rotationSpeed);
        
        // Flip visuals left or right
        if (Mathf.Abs(moveInputX) > 0.01f && visualsRoot != null)
        {
            Vector3 scale = visualsRoot.localScale;
            float targetFacingDirection;

            if (IsGrabbing && grabbedBody != null)
            {
                // When grabbing, logic is more complex: PUSH vs PULL.
                float objectDirection = Mathf.Sign(grabbedBody.transform.position.x - transform.position.x);
                float moveDirection = Mathf.Sign(moveInputX);

                // If moving TOWARDS the object (pushing), face the object.
                // If moving AWAY from the object (pulling), face away from it.
                if (moveDirection == objectDirection) // Pushing
                {
                    targetFacingDirection = objectDirection;
                }
                else // Pulling
                {
                    targetFacingDirection = moveDirection;
                }
            }
            else
            {
                targetFacingDirection = Mathf.Sign(moveInputX);
            }
            scale.x = Mathf.Abs(scale.x) * targetFacingDirection;
            visualsRoot.localScale = scale;

            // Keep the authoritative facing in sync with the visual flip.
            FacingDirection = targetFacingDirection;
        }
    }

    public void PreserveMomentumFor(float duration)
    {
        preserveMomentumUntil = Time.time + duration;
    }

    public void ToggleGrab()
    {
        if (grabbedBody != null) ReleaseGrab();
        else TryStartGrab();
    }

    public void TryStartGrab()
    {
        if (isMovementDisabled || grabbedBody != null) return;

        Movable targetMovable = FindGrabbableObject();
        if (targetMovable == null) return;

        Rigidbody2D targetRb = targetMovable.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        // Face the object at the moment of grabbing (flip via scale.x).
        float objectDirection = Mathf.Sign(targetMovable.transform.position.x - transform.position.x);
        grabDirection = objectDirection;
        FacingDirection = objectDirection; // Keep the authoritative facing in sync.
        if (visualsRoot != null)
        {
            Vector3 scale = visualsRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * objectDirection;
            visualsRoot.localScale = scale;
        }

        // No joint: we drive the object ourselves. Capture the current horizontal offset as the hold
        // distance. The vertical axis is never touched, so gravity/collision keep it on the slope.
        grabbedBody = targetRb;
        grabHoldOffsetX = targetRb.position.x - transform.position.x;

        Debug.Log($"[SimplePlayer] Grabbed {targetMovable.name}");
    }

    public void ReleaseGrab()
    {
        if (grabbedBody == null) return;

        Debug.Log($"[SimplePlayer] Released {grabbedBody.name}");

        grabbedBody = null;
        grabDirection = 0f; // Reset grab direction
    }

    /// <summary>
    /// Drives the grabbed object horizontally to follow the player while leaving its vertical velocity
    /// untouched, so gravity and collisions keep it resting on the ground/slope (no float, no bounce).
    /// </summary>
    private void DriveGrabbedObject()
    {
        if (grabbedBody == null) return;

        // Feed-forward the player's horizontal velocity, plus a spring that corrects drift back to the
        // hold offset. errorX is ~0 at grab time, so there is no yank/jump on grab.
        float desiredX = transform.position.x + grabHoldOffsetX;
        float errorX = desiredX - grabbedBody.position.x;
        float correction = Mathf.Clamp(errorX * grabFollowStiffness, -grabMaxCorrectionSpeed, grabMaxCorrectionSpeed);

        Vector2 v = grabbedBody.linearVelocity;
        v.x = rb.linearVelocity.x + correction; // horizontal only; vertical stays physics-driven
        grabbedBody.linearVelocity = v;
    }

    /// <summary>
    /// Checks if a movable object is in range to be grabbed, without actually grabbing it.
    /// </summary>
    /// <returns>True if a valid, non-static movable object is in front of the player.</returns>
    public bool CanGrabObject()
    {
        return FindGrabbableObject() != null && grabbedBody == null && !isMovementDisabled;
    }

    /// <summary>
    /// A simple, direct check for a grabbable object in front of the player.
    /// </summary>
    /// <returns>The Movable component if found, otherwise null.</returns>
    private Movable FindGrabbableObject()
    {
        // Temporarily enable trigger detection for the check
        bool originalQueriesHitTriggers = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;

        // float facingMul = (visualsRoot != null && visualsRoot.localScale.x < 0) ? -1f : 1f; // OLD: Always used current visual direction
        float facingMul;
        if (IsGrabbing)
        {
            // If we are already grabbing, the check MUST stay locked to the original grab direction.
            facingMul = grabDirection;
        }
        else
        {
            // If we are not grabbing, the check depends on the current visual facing direction.
            facingMul = (visualsRoot != null && visualsRoot.localScale.x < 0) ? -1f : 1f;
        }
        Vector2 checkCenter = (Vector2)transform.position + new Vector2(grabCheckOffset.x * facingMul, grabCheckOffset.y);

        // Find ALL colliders in a small circle in front of the player
        Collider2D[] hits = Physics2D.OverlapCircleAll(checkCenter, grabCheckRadius, movableLayer);

        // Restore original setting immediately
        Physics2D.queriesHitTriggers = originalQueriesHitTriggers;

        foreach (var hit in hits)
        {
            // Check if the found collider is a grab handle of a Movable object
            Movable movable = hit.GetComponentInParent<Movable>();
            if (movable != null && hit == movable.grabHandleTrigger)
            {
                return movable; // Found it!
            }
        }

        return null; // Nothing found
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        PlayerShellSystem shellSystem = GetComponent<PlayerShellSystem>();
        if (shellSystem != null && shellSystem.CurrentShell != null)
        {
            shellSystem.CurrentShell.OnPlayerCollision(collision);
        }
    }

    private void OnDrawGizmos()
    {
        // Draw the raycasts in the editor to easily adjust the offset, distance, and width
        Gizmos.color = Color.red;

        GetGroundProbeSpan(out float minX, out float maxX, out float originY);
        int rayCount = Mathf.Max(2, groundRayCount);
        float span = maxX - minX;

        for (int i = 0; i < rayCount; i++)
        {
            float t = (float)i / (rayCount - 1);
            Vector2 origin = new Vector2(minX + span * t, originY);
            Gizmos.DrawLine(origin, origin + Vector2.down * groundCheckDistance);
        }

        // Draw Grab Probe (Yellow = Free, Magenta = Grabbing)
        Gizmos.color = grabbedBody != null ? Color.magenta : Color.yellow;
        // float facingMul = (visualsRoot != null && visualsRoot.localScale.x < 0) ? -1f : 1f; // OLD: Gizmo always followed visuals
        float facingMul;
        if (IsGrabbing)
        {
            // If grabbing, the gizmo MUST show the locked check direction.
            facingMul = grabDirection;
        }
        else
        {
            // If not grabbing, the gizmo follows the current visual direction.
            facingMul = (visualsRoot != null && visualsRoot.localScale.x < 0) ? -1f : 1f;
        }
        Vector2 grabOrigin = (Vector2)transform.position + new Vector2(0, grabCheckOffset.y);
        Vector2 grabDir = Vector2.right * facingMul;
        Vector2 grabPos = grabOrigin + grabDir * grabCheckOffset.x;

        Gizmos.DrawWireSphere(grabPos, grabCheckRadius);
        Gizmos.DrawLine(grabOrigin, grabPos);
    }
}