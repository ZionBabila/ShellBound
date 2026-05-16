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

    private bool isDashing = false;
    private float lastDashTime = -100f; // Ensure it's ready immediately
    private Coroutine dashCoroutine;

    // משתנים לסנכרון הפיזיקה מול מנוע הפיזיקה של יוניטי
    private bool pendingDash = false;
    private Vector2 pendingDashVelocity;

    public override void OnActivate()
    {
        // Cancel if not on the back, missing player, currently dashing, or on cooldown
        if (currentState != ShellState.OnBack || playerInside == null) return;
        
        if (isDashing) return;

        if (Time.time < lastDashTime + dashCooldown)
        {
            Debug.Log("<color=orange>Dash is on cooldown!</color>");
            return;
        }

        // בדיקת אדמה
        if (!playerInside.IsGrounded)
        {
            Debug.Log("<color=red>Dash Blocked:</color> Player must be touching the ground.");
            return;
        }

        dashCoroutine = StartCoroutine(DashRoutine());
    }

    private void FixedUpdate()
    {
        // ביצוע הדחיפה הפיזיקלית בדיוק בתחילת פריים הפיזיקה, כדי למנוע התנגשויות שווא עם במות
        if (pendingDash && playerInside != null)
        {
            Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = pendingDashVelocity;
            }
            pendingDash = false;
        }
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

        // מכינים את המהירות, ומעבירים ל-FixedUpdate את האחריות לבצע את זה בסנכרון מושלם
        pendingDashVelocity = dashDirection * dashForce;
        pendingDash = true;

        // ממתינים את זמן ההשתהות (dashDuration). השחקן עדיין באוויר ובקשת, פשוט אין לו שליטת הליכה כרגע
        yield return new WaitForSeconds(dashDuration);

        isDashing = false;
        currentState = ShellState.OnBack;
    }
}