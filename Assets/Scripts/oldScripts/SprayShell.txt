using UnityEngine;
using System.Collections;

// =============================================================================
// SprayShell — shell with a slope-aware dash (Space launches a short burst).
// -----------------------------------------------------------------------------
// Role:        Raycasts down to find the ground normal, builds a slope-tangent
//              dash direction with optional upward angle, drives the player
//              Rigidbody for dashDuration, then restores gravity.
//              Refuses to dash unless player.IsGrounded (no air-dash).
//              If thrown mid-dash, replaces throw force with a backward recoil.
// Depends on:  groundLayer LayerMask, PlayerController.IsGrounded / IsDashing.
// Used by:     PlayerController via BaseShell.UseAbility.
// =============================================================================
public class SprayShell : BaseShell
{
    [Header("Dash Settings")]
    public float dashSpeed = 25f;
    public float dashDuration = 0.2f;
    [Tooltip("Base launch angle above the surface tangent.")]
    public float dashAngle = 15f;

    [Header("Slope & Recoil")]
    [Tooltip("Layer mask for the surface raycast — set this in the Inspector.")]
    public LayerMask groundLayer;
    [Tooltip("Multiplier on dashSpeed when the shell is thrown mid-dash and recoils backward.")]
    public float recoilForceMultiplier = 1.5f;

    public ParticleSystem sprayParticles;

    private bool isDashingInternal = false;

    public override void UseAbility(Rigidbody2D playerRb)
    {
        if (!canBoost) return;

        PlayerController player = playerRb.GetComponent<PlayerController>();
        if (player == null) return;
        if (player.IsDashing) return;
        // Block air-dashes — player must be touching the ground layer to launch.
        if (!player.IsGrounded) return;

        StartCoroutine(DashRoutine(player, playerRb));
    }

    private IEnumerator DashRoutine(PlayerController player, Rigidbody2D rb)
    {
        isDashingInternal = true;
        player.SetDashing(true);

        if (sprayParticles != null) sprayParticles.Play();

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        // Compute dash direction relative to the surface beneath the player.
        float facingDirection = Mathf.Sign(player.transform.localScale.x);
        Vector2 dashDir = Vector2.right * facingDirection;

        // Short downward raycast to read the ground angle.
        RaycastHit2D hit = Physics2D.Raycast(player.transform.position, Vector2.down, 1.5f, groundLayer);

        if (hit.collider != null)
        {
            // Tangent along the surface (perpendicular to the normal).
            Vector2 groundNormal = hit.normal;
            Vector2 slopeForward = new(groundNormal.y, -groundNormal.x);

            // Flip tangent based on facing.
            if (facingDirection < 0) slopeForward *= -1;

            // Pitch up by dashAngle relative to the surface tangent.
            dashDir = Quaternion.Euler(0, 0, dashAngle * facingDirection) * slopeForward;
        }
        else
        {
            // Airborne fallback — use a fixed pitch.
            float angleInRad = dashAngle * Mathf.Deg2Rad;
            dashDir = new Vector2(Mathf.Cos(angleInRad) * facingDirection, Mathf.Sin(angleInRad));
        }

        rb.linearVelocity = dashDir.normalized * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravity;
        rb.linearVelocity *= 0.5f;

        player.SetDashing(false);
        isDashingInternal = false;
    }

    // Throwing the shell mid-dash replaces the normal throw force with a strong backward recoil.
    public override void Unequip(Vector2 throwForce, Collider2D playerCol)
    {
        if (isDashingInternal)
        {
            float facingDirection = Mathf.Sign(playerCol.transform.localScale.x);

            // Recoil opposite the player's facing, with a small upward bias.
            Vector2 recoilDirection = new Vector2(-facingDirection * 1.2f, 0.4f).normalized;
            throwForce = recoilDirection * (dashSpeed * recoilForceMultiplier);
        }

        base.Unequip(throwForce, playerCol);
    }
}
