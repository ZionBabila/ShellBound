using System.Collections;
using UnityEngine;

public class SprayCan : Shell
{
    [Header("Spray Dash Settings")]
    [Tooltip("The power of the dash burst.")]
    public float dashForce = 12.0f; //[cite: 1]

    [Tooltip("Angle relative to the surface. 0 = slide along surface, 90 = pop straight out.")]
    [Range(0f, 90f)]
    public float dashAngle = 45.0f; //[cite: 1]

    [Tooltip("How long the dash force is continuously applied (in seconds).")]
    public float dashDuration = 0.25f; //[cite: 1]

    [Tooltip("Time before the player can dash again.")]
    public float dashCooldown = 1.5f; //[cite: 1]

    private PlayerController playerInside;
    private bool isDashing = false;
    private float lastDashTime = -100f; // Ensure it's ready immediately
    private Coroutine dashCoroutine;
    private float originalGravity;

    public override void OnCollect(Transform parentTransform, Vector2 playerMountOffset)
    {
        base.OnCollect(parentTransform, playerMountOffset);
        playerInside = parentTransform.GetComponentInParent<PlayerController>();
    }

    public override void OnActivate()
    {
        // Cancel if not on the back, missing player, currently dashing, or on cooldown
        if (currentState != ShellState.OnBack || playerInside == null) return;
        if (isDashing || Time.time < lastDashTime + dashCooldown) return;

        // Requires the player to be touching a surface to launch off of it[cite: 1]
        if (!playerInside.IsGrounded) return;

        dashCoroutine = StartCoroutine(DashRoutine());
    }

    public override void OnDeactivate()
    {
        // Spray Can doesn't have a sustained InUse state to deactivate manually
    }

    public override void OnThrow(Vector2 throwVelocity)
    {
        ResetDashState();
        base.OnThrow(throwVelocity);
    }

    public override void OnDetach()
    {
        ResetDashState();
        base.OnDetach();
    }

    private void ResetDashState()
    {
        if (isDashing && playerInside != null)
        {
            if (dashCoroutine != null) StopCoroutine(dashCoroutine);
            Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
            if (playerRb != null) playerRb.gravityScale = originalGravity;
            isDashing = false;
        }
    }

private IEnumerator DashRoutine()
    {
        isDashing = true;
        currentState = ShellState.InUse;
        lastDashTime = Time.time;

        Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
        
        // 1. קבלת הנורמל (הזווית של המשטח) שעליו עומדים
        Vector2 surfaceNormal = playerInside.SurfaceNormal;

        // 2. חישוב כיוון ההתקדמות (המשיק) לפי הכיוון שהשחקן פונה אליו
        Vector2 forwardTangent = playerInside.facingRight ? 
            new Vector2(surfaceNormal.y, -surfaceNormal.x) : 
            new Vector2(-surfaceNormal.y, surfaceNormal.x);

        // 3. שילוב בין ההתקדמות לנורמל כדי לקבל את זווית הדאש המדויקת
        float angleRad = dashAngle * Mathf.Deg2Rad;
        Vector2 dashDirection = (forwardTangent * Mathf.Cos(angleRad) + surfaceNormal * Mathf.Sin(angleRad)).normalized;

        float timer = 0f;

        // 4. כיבוי כבידה זמני לדאש ישר וחלק (ללא נפילה)
        originalGravity = playerRb.gravityScale;
        playerRb.gravityScale = 0f;

        while (timer < dashDuration)
        {
            // דריסת המהירות לכיוון הדאש
            playerRb.linearVelocity = dashDirection * dashForce;
            
            timer += Time.fixedDeltaTime; // שימוש ב-fixed כי אנחנו מתעסקים בפיזיקה
            yield return new WaitForFixedUpdate(); 
        }

        // החזרת הכבידה ומצב הקונכייה
        playerRb.gravityScale = originalGravity;
        isDashing = false;
        currentState = ShellState.OnBack;
    }
}