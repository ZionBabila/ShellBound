using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// PlayerController — central player script for ShellBound
// =============================================================================
// Architecture overview:
//
//   PlayerInputHandler  ── events ─▶  PlayerController
//                                          │
//                                          │ Equip / Unequip / UseAbility
//                                          ▼
//                                      BaseShell
//                                     /    │    \
//                          RollingShell  SprayShell  ClimbShell
//                                          │
//                                  ShellAttachment (per-shell config)
//
// PlayerController:
//   - Rigidbody2D-driven crab body. Walks via leg force, rights itself via torque.
//   - Tracks ground contacts (OnCollisionEnter/Exit2D + HashSet) → IsGrounded.
//   - Exposes TryGetSteepestContactNormal for shells that need surface alignment.
//   - Delegates locomotion to currentShell when shell.OverridesPlayerMovement is true.
//   - Computes COM each FixedUpdate (centerOfMassPoint or offset, plus shell carryComShift).
//
// Shells (inherit BaseShell):
//   - Override OverridesPlayerMovement / HandlePlayerMovement / GetAnimationSpeed
//     to plug custom locomotion (e.g. ClimbShell projecting input onto a wall tangent).
//   - Override UseAbility for the Space-key ability.
//   - Call player.SetDashing(true) to suppress player input during ability sequences.
//
// Input:
//   - PlayerInputHandler is the only place that talks to Unity's InputSystem.
//     PlayerController subscribes to OnInteract (E) and OnAbility (Space).
// =============================================================================

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float baseMoveSpeed = 5f;
    [Tooltip("Force the crab's legs apply to the ground each FixedUpdate. Tune in Inspector (try 20-40).")]
    public float legPower = 25f;
    [Tooltip("Rotational force the crab uses to flip itself over.")]
    public float flipTorque = 15f;

    [Header("Throw")]
    [Tooltip("Throw direction relative to facing. X = forward, Y = up.")]
    public Vector2 throwAngle = new Vector2(1f, 1f);
    public float throwPower = 8f;

    [Header("Balance & Center of Mass")]
    [Tooltip("Optional child Transform that defines center of mass. If null, falls back to centerOfMassOffset.")]
    public Transform centerOfMassPoint;
    [Tooltip("Center of mass offset relative to the player pivot (used only when centerOfMassPoint is null).")]
    public Vector2 centerOfMassOffset = new Vector2(0f, -0.3f);

    [Range(0f, 1f)]
    [Tooltip("Below this dot(transform.up, world up), the crab counts as 'on its back' and cannot walk — only torque to right itself.")]
    public float uprightThreshold = 0.3f;

    [Header("Shell System")]
    public Transform shellMountPoint;
    public float interactRadius = 2f;
    [Tooltip("Center of the pickup search circle, in player-local space (flips with facing).")]
    public Vector3 interactOffset;
    public LayerMask shellLayer;

    [Header("Ground Detection")]
    [Tooltip("Layers that count as 'ground' for IsGrounded — driven by collision callbacks.")]
    public LayerMask groundLayer;

    [Header("Animation")]
    public Animator animator;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private PlayerInputHandler input;
    private BaseShell currentShell;
    private float originalMass;
    private bool facingRight = true;

    private static readonly int AnimSpeedHash = Animator.StringToHash("speed");

    public BaseShell CurrentShell => currentShell;
    public bool FacingRight => facingRight;

    // Tracks every collider on groundLayer the player is currently touching.
    // HashSet guards against duplicate Enter calls (multiple contact points per collider).
    private readonly HashSet<Collider2D> groundContacts = new();
    public bool IsGrounded => groundContacts.Count > 0;

    // Reused buffer for on-demand contact queries.
    private static readonly ContactPoint2D[] contactsBuffer = new ContactPoint2D[16];

    // Returns the steepest current contact normal (the one most "wall-like" / least "floor-like").
    // upDot = Dot(normal, world up). 1 = perfect floor, 0 = vertical wall, -1 = perfect ceiling.
    // Returns false if no ground-layer contacts.
    public bool TryGetSteepestContactNormal(out Vector2 normal, out float upDot)
    {
        int count = playerCollider.GetContacts(contactsBuffer);
        normal = Vector2.up;
        upDot = 1f;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            var c = contactsBuffer[i];
            if (c.collider == null) continue;
            if (!IsOnGroundLayer(c.collider)) continue;
            float d = Vector2.Dot(c.normal, Vector2.up);
            if (d < upDot)
            {
                upDot = d;
                normal = c.normal;
                found = true;
            }
        }
        return found;
    }

    // Set true while a shell ability is driving the player (dash, rolling, etc.).
    // Setter is restricted to BaseShell subclasses via SetDashing().
    public bool IsDashing { get; private set; }
    public void SetDashing(bool value) => IsDashing = value;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        input = GetComponent<PlayerInputHandler>();

        if (animator == null) animator = GetComponent<Animator>();

        originalMass = rb.mass;
        ApplyCenterOfMass();

        input.OnInteract += TryInteractShell;
        input.OnAbility += UseShellAbility;
    }

    void OnDestroy()
    {
        if (input != null)
        {
            input.OnInteract -= TryInteractShell;
            input.OnAbility -= UseShellAbility;
        }
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (IsOnGroundLayer(c.collider)) groundContacts.Add(c.collider);
    }

    void OnCollisionExit2D(Collision2D c)
    {
        groundContacts.Remove(c.collider);
    }

    private bool IsOnGroundLayer(Collider2D col)
    {
        return (groundLayer.value & (1 << col.gameObject.layer)) != 0;
    }

    void FixedUpdate()
    {
        // Re-apply COM each tick so live-tuning the marker in the editor is reflected immediately.
        ApplyCenterOfMass();

        Vector2 moveInput = input.MoveValue;

        // Shell-driven movement (e.g. RollingShell while inside) takes precedence over walking physics.
        if (currentShell != null && currentShell.OverridesPlayerMovement)
        {
            currentShell.HandlePlayerMovement(moveInput, this);
            if (animator != null) animator.SetFloat(AnimSpeedHash, currentShell.GetAnimationSpeed(rb));
            return;
        }

        // Shell ability is currently pushing the player — suppress input-driven motion.
        if (IsDashing)
        {
            if (animator != null) animator.SetFloat(AnimSpeedHash, 0f);
            return;
        }

        // Upright check: 1 = standing perfectly, -1 = fully upside-down.
        float upright = Vector2.Dot(transform.up, Vector2.up);
        bool canWalk = upright > uprightThreshold;

        if (canWalk)
        {
            float targetSpeed = baseMoveSpeed;
            if (currentShell != null) targetSpeed *= currentShell.speedMultiplier;

            float speedDifference = (moveInput.x * targetSpeed) - rb.linearVelocity.x;
            rb.AddForce(Vector2.right * (speedDifference * legPower));
        }

        // Self-righting torque is always available — even on its back — so the crab can flip back upright.
        // Multiplied by facing so up/down feel consistent from the player's perspective regardless of orientation.
        if (Mathf.Abs(moveInput.y) > 0.1f)
        {
            float facingMultiplier = facingRight ? 1f : -1f;
            rb.AddTorque(moveInput.y * flipTorque * facingMultiplier);
        }

        if (animator != null)
        {
            // Animation speed zeros out while on its back so the crab doesn't appear to "walk" mid-air.
            float animSpeed = canWalk ? Mathf.Abs(rb.linearVelocity.x) : 0f;
            animator.SetFloat(AnimSpeedHash, animSpeed);
        }

        if (canWalk) UpdateFacing(moveInput.x);
    }

    private void ApplyCenterOfMass()
    {
        Vector2 baseCom = centerOfMassPoint != null
            ? (Vector2)transform.InverseTransformPoint(centerOfMassPoint.position)
            : centerOfMassOffset;

        // Carried shell may shift the COM (carryComShift) — top-heavy when worn, neutral when bare.
        Vector2 shellShift = (currentShell != null && currentShell.attachment != null)
            ? currentShell.attachment.carryComShift
            : Vector2.zero;

        rb.centerOfMass = baseCom + shellShift;
    }

    // Public so shells that override movement can drive facing without exposing Flip().
    public void UpdateFacing(float horizontalInput)
    {
        if (horizontalInput > 0f && !facingRight) Flip();
        else if (horizontalInput < 0f && facingRight) Flip();
    }

    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    private void TryInteractShell()
    {
        if (currentShell != null) TryThrowShell();
        else TryPickupShell();
    }

    private void TryThrowShell()
    {
        float direction = facingRight ? 1f : -1f;
        Vector2 throwForce = new Vector2(throwAngle.x * direction, throwAngle.y).normalized * throwPower;

        BaseShell thrown = currentShell;
        currentShell = null;

        if (thrown.affectsMass) rb.mass = originalMass;

        thrown.Unequip(throwForce, playerCollider);
    }

    private void TryPickupShell()
    {
        Vector3 searchCenter = transform.TransformPoint(interactOffset);
        Collider2D[] colliders = Physics2D.OverlapCircleAll(searchCenter, interactRadius, shellLayer);
        if (colliders.Length == 0) return;

        BaseShell foundShell = colliders[0].GetComponent<BaseShell>();
        if (foundShell == null)
        {
            Debug.LogWarning("Object on shellLayer is missing a BaseShell script: " + colliders[0].name);
            return;
        }

        // Read mass BEFORE Equip — special shells (e.g. RollingShell) can mutate their physics on equip.
        float addedWeight = foundShell.affectsMass ? foundShell.shellMass : 0f;

        currentShell = foundShell;
        currentShell.Equip(shellMountPoint);

        rb.mass = originalMass + addedWeight;
    }

    private void UseShellAbility()
    {
        if (currentShell != null) currentShell.UseAbility(rb);
    }

    private void OnDrawGizmos()
    {
        Vector3 gizmoCenter = transform.TransformPoint(interactOffset);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(gizmoCenter, interactRadius);

        if (shellMountPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(shellMountPoint.position, 0.1f);
            Gizmos.DrawWireSphere(shellMountPoint.position, 0.15f);
        }

        // COM gizmo only drawn when no marker is assigned — the marker has its own gizmo script.
        if (centerOfMassPoint == null)
        {
            Vector3 comWorld = transform.TransformPoint(centerOfMassOffset);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(comWorld, 0.08f);
            Gizmos.DrawWireSphere(comWorld, 0.16f);
        }
    }
}
