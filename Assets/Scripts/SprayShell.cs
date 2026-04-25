using UnityEngine;
using System.Collections; // Required for Coroutines

public class SprayShell : BaseShell
{
    [Header("Dash Settings")]
    public float dashSpeed = 25f; // How fast the crab shoots forward
    public float dashDuration = 0.2f; // How long the dash lasts (keep it very short!)
    public ParticleSystem sprayParticles;

    public override void UseAbility(Rigidbody2D playerRb)
    {
        if (!canBoost) return;

        // Get the PlayerController to tell it to stop moving normally
        PlayerController player = playerRb.GetComponent<PlayerController>();
        
        // Only trigger the dash if we aren't already dashing
        if (player != null && !player.isDashing)
        {
            StartCoroutine(DashRoutine(player, playerRb));
        }
    }

    private IEnumerator DashRoutine(PlayerController player, Rigidbody2D rb)
    {
        Debug.Log("Dash Started!");

        // 1. Lock the player's normal movement
        player.isDashing = true;

        // 2. Play visual effects
        if (sprayParticles != null) sprayParticles.Play();

        // 3. Save the original gravity and turn it off so we dash in a perfect straight line
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        // 4. Find out which way the crab is looking (1 for right, -1 for left)
        float facingDirection = Mathf.Sign(player.transform.localScale.x);

        // 5. OVERRIDE the velocity directly for an instant, aggressive burst of speed!
        rb.linearVelocity = new Vector2(facingDirection * dashSpeed, 0f);

        // 6. Wait for the dash duration to finish
        yield return new WaitForSeconds(dashDuration);

        // 7. RESTORE everything back to normal
        rb.gravityScale = originalGravity;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Stop the horizontal dash abruptly
        player.isDashing = false; // Unlock player movement

        Debug.Log("Dash Ended!");
    }
}