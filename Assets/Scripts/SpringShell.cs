using UnityEngine;

public class SpringShell : Shell
{
    [Header("Spring Settings")]
    [Tooltip("The vertical force applied when the spring is used.")]
    public float jumpForce = 15.0f;

    [Tooltip("Time before the spring can be used again.")]
    public float cooldown = 1.0f;

    [Tooltip("Can the spring be used in mid-air? (Double jump)")]
    public bool allowMidAirJump = false;

    private float lastUseTime = -100f;

    public override void OnActivate()
    {
        // מוודאים שהשחקן לובש את הקונכייה
        if (currentState != ShellState.OnBack || playerInside == null) return;

        // בדיקת קולדאון כדי שהשחקן לא יספים את הקפיצה
        if (Time.time < lastUseTime + cooldown)
        {
            Debug.Log("<color=orange>Spring is on cooldown!</color>");
            return;
        }

        // בדיקה האם מותר לקפוץ (אם חובה רצפה והשחקן באוויר - נחסום את הקפיצה)
        if (!allowMidAirJump && !playerInside.IsGrounded)
        {
            Debug.Log("<color=red>Spring Blocked:</color> Must be on the ground to use.");
            return;
        }

        Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            // מאפסים את המהירות האנכית כדי שהקפיצה תהיה עקבית גם אם השחקן בדיוק נופל באותו רגע
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, 0f);
            
            // מפעילים כוח אנכי כלפי מעלה בבת אחת (Impulse)
            playerRb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            
            lastUseTime = Time.time;
            Debug.Log("<color=green>BOING!</color> Spring Shell activated!");
        }
    }

    public override void OnDeactivate()
    {
        // הקפיץ הוא יכולת חד-פעמית (One-shot), ולכן אין לו מצב מתמשך שצריך לכבות.
    }
}