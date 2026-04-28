using UnityEngine;
using System.Collections;

public class SprayShell : BaseShell
{
    [Header("Dash Settings")]
    public float dashSpeed = 25f;
    public float dashDuration = 0.2f;
    public float dashAngle = 15f; // זווית בסיס מהמישור
    
    [Header("Slope & Recoil")]
    public LayerMask groundLayer; // חשוב! הגדר את השכבה של הרצפה באינספקטור
    public float recoilForceMultiplier = 1.5f; // כמה חזק הפחית תעוף אחורה בזריקה
    
    public ParticleSystem sprayParticles;

    private bool isDashingInternal = false;

    public override void UseAbility(Rigidbody2D playerRb)
    {
        if (!canBoost) return;

        PlayerController player = playerRb.GetComponent<PlayerController>();
        if (player != null && !player.isDashing)
        {
            StartCoroutine(DashRoutine(player, playerRb));
        }
    }

    private IEnumerator DashRoutine(PlayerController player, Rigidbody2D rb)
    {
        isDashingInternal = true;
        player.isDashing = true;

        if (sprayParticles != null) sprayParticles.Play();

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        // --- חישוב כיוון ביחס למשטח ---
        float facingDirection = Mathf.Sign(player.transform.localScale.x);
        Vector2 dashDir = Vector2.right * facingDirection; // כיוון ברירת מחדל

        // ירידת לייזר קצרה למטה למציאת זווית הקרקע
        RaycastHit2D hit = Physics2D.Raycast(player.transform.position, Vector2.down, 1.5f, groundLayer);
        
        if (hit.collider != null)
        {
            // מציאת הוקטור המקביל למשטח (הניצב לנורמל שלו)
            Vector2 groundNormal = hit.normal;
            Vector2 slopeForward = new Vector2(groundNormal.y, -groundNormal.x); // וקטור מקביל לרצפה
            
            // תיקון כיוון לפי לאן שהשחקן מסתכל
            if (facingDirection < 0) slopeForward *= -1;

            // הוספת ה-Dash Angle המבוקשת מעל לזווית המשטח
            dashDir = Quaternion.Euler(0, 0, dashAngle * facingDirection) * slopeForward;
        }
        else
        {
            // אם אנחנו באוויר, נשתמש בזווית רגילה
            float angleInRad = dashAngle * Mathf.Deg2Rad;
            dashDir = new Vector2(Mathf.Cos(angleInRad) * facingDirection, Mathf.Sin(angleInRad));
        }

        rb.linearVelocity = dashDir.normalized * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravity;
        rb.linearVelocity *= 0.5f; 
        
        player.isDashing = false;
        isDashingInternal = false;
    }

    // --- טיפול בזריקה תוך כדי דש ---
    public override void Unequip(Vector2 throwForce, Collider2D playerCol)
    {
        if (isDashingInternal)
        {
            // אם אנחנו כאן, סימן שלחצנו E תוך כדי הסילון
            float facingDirection = Mathf.Sign(playerCol.transform.localScale.x);
            
            // אנחנו יוצרים וקטור הפוך לגמרי לכיוון המבט
            // אנחנו מעיפים את הפחית חזק אחורה ולמעלה מעט
            Vector2 recoilDirection = new Vector2(-facingDirection * 1.2f, 0.4f).normalized;
            
            // מחליפים את ה-throwForce המקורי ברתע חזק
            throwForce = recoilDirection * (dashSpeed * recoilForceMultiplier);
            
            Debug.Log("Recoil Triggered! Shell flying opposite direction.");
        }

        base.Unequip(throwForce, playerCol);
    }
}