using UnityEngine;

// =============================================================================
// ClimbShell — auto-cling shell for walls and ceilings.
// -----------------------------------------------------------------------------
// Role:        While equipped, monitors player contact normals each FixedUpdate.
//              When the steepest contact tilts more than ~45° from horizontal,
//              gravity is suspended and the player is rotated to align with the
//              surface. Arrow input is projected onto the tangent so up-arrow
//              always walks "up" the surface in screen space.
// Depends on:  PlayerController.TryGetSteepestContactNormal for live surface lookup;
//              BaseShell.Equip caches playerCtrl/playerRb for direct manipulation.
// Used by:     PlayerController via the BaseShell virtual API
//              (OverridesPlayerMovement / HandlePlayerMovement / GetAnimationSpeed).
// =============================================================================
public class ClimbShell : BaseShell
{
    [Header("Climb")]
    [Tooltip("Speed along the surface tangent while clinging.")]
    public float climbSpeed = 4f;

    [Range(0f, 1f)]
    [Tooltip("Body rotation smoothing per FixedUpdate (higher = snappier alignment to surface).")]
    public float rotationLerp = 0.3f;

    [Tooltip("Inward velocity component pressing the player into the surface to maintain contact.")]
    public float adhesionForce = 30f;

    [Range(-1f, 1f)]
    [Tooltip("Cling activates when contact normal's dot with world up is BELOW this. 0.7 ≈ ignore floors but cling to anything 45°+ from horizontal.")]
    public float maxFloorDot = 0.7f;

    private bool isClinging;
    private float originalGravityScale;
    private Rigidbody2D playerRb;
    private PlayerController playerCtrl;

    public override void Equip(Transform mountPoint)
    {
        base.Equip(mountPoint);
        playerCtrl = mountPoint.GetComponentInParent<PlayerController>();
        playerRb = playerCtrl != null ? playerCtrl.GetComponent<Rigidbody2D>() : null;
    }

    public override void Unequip(Vector2 throwForce, Collider2D playerCollider)
    {
        StopCling();
        playerCtrl = null;
        playerRb = null;
        base.Unequip(throwForce, playerCollider);
    }

    void FixedUpdate()
    {
        // Only relevant while equipped (Equip caches refs; Unequip clears them).
        if (playerCtrl == null || playerRb == null) return;

        bool shouldCling = playerCtrl.TryGetSteepestContactNormal(out _, out float upDot)
                           && upDot < maxFloorDot;

        if (shouldCling && !isClinging) StartCling();
        else if (!shouldCling && isClinging) StopCling();
    }

    private void StartCling()
    {
        originalGravityScale = playerRb.gravityScale;
        playerRb.gravityScale = 0f;
        isClinging = true;
    }

    private void StopCling()
    {
        if (!isClinging) return;
        if (playerRb != null) playerRb.gravityScale = originalGravityScale;
        isClinging = false;
    }

    public override bool OverridesPlayerMovement => isClinging;

    public override void HandlePlayerMovement(Vector2 moveInput, PlayerController player)
    {
        if (!player.TryGetSteepestContactNormal(out Vector2 normal, out float upDot)
            || upDot >= maxFloorDot)
        {
            StopCling();
            return;
        }

        // Align body's up axis with the surface normal.
        float targetZ = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;
        playerRb.MoveRotation(Mathf.LerpAngle(playerRb.rotation, targetZ, rotationLerp));
        playerRb.angularVelocity = 0f;

        // Tangent in screen space (perpendicular to normal).
        Vector2 tangent = new Vector2(normal.y, -normal.x);

        // Screen-space input projected onto the tangent — up arrow always walks "up" the wall.
        float tangentInput = Vector2.Dot(moveInput, tangent);
        Vector2 tangentVel = tangent * (tangentInput * climbSpeed);

        // Adhesion pulls the player into the surface so it doesn't drift off.
        Vector2 adhesionVel = -normal * (adhesionForce * Time.fixedDeltaTime);

        playerRb.linearVelocity = tangentVel + adhesionVel;

        // Body-frame flip — sprite faces forward along the tangent direction.
        player.UpdateFacing(tangentInput);
    }

    public override float GetAnimationSpeed(Rigidbody2D playerRb) => playerRb.linearVelocity.magnitude;
}
