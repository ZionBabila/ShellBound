using UnityEngine;

public class RollingShell : BaseShell
{
    [Header("Rolling Settings")]
    public float rollTorque = 25f;
    public float maxAngularVelocity = 600f;

    [Header("Rolling Visuals")]
    [Tooltip("הסטה של הסרטן ביחס למרכז הקוליידר כשהוא מתגלגל")]
    public Vector3 rollingOffset = new Vector3(0f, -0.5f, 0f);

    private bool isPlayerInside = false;
    public bool IsPlayerInside => isPlayerInside;

    private Rigidbody2D playerRb;
    private PlayerController playerCtrl;
    private Collider2D playerCollider;
    private CircleCollider2D shellCollider;

    protected override void Awake()
    {
        base.Awake();
        shellCollider = GetComponent<CircleCollider2D>();
    }

public override void Equip(Transform mountPoint)
{
    // קודם כל עושים את ה-Equip הבסיסי שיצמיד אותנו ל-MountPoint
    base.Equip(mountPoint);

    playerCtrl = mountPoint.GetComponentInParent<PlayerController>();
    playerRb = playerCtrl.GetComponent<Rigidbody2D>();
    playerCollider = playerCtrl.GetComponent<Collider2D>();

    isPlayerInside = false;
    rb.simulated = false;
    if (shellCollider != null) shellCollider.enabled = false;
    
    // אם יש לך אופסט נוסף שאתה רוצה מעבר ל-Mount Point, אפשר להוסיף אותו כאן
    // transform.localPosition += carryOffset; 
}

    public override void UseAbility(Rigidbody2D pRb)
    {
        if (!isPlayerInside) EnterShell();
        else ExitShell();
    }

    private void EnterShell()
    {
        isPlayerInside = true;
        playerCtrl.isDashing = true;

        // ניתוק הקונכייה מהשחקן כדי שתתגלגל חופשי
        transform.SetParent(null);

        // הקפאת הפיזיקה של השחקן
        playerRb.simulated = false;

        // הפעלת הפיזיקה של הקונכייה
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.simulated = true;
        shellCollider.enabled = true;

        // מניעת התנגשות ביניהם
        Physics2D.IgnoreCollision(playerCollider, shellCollider, true);

        // העברת מהירות מהסרטן לקונכייה
        rb.linearVelocity = playerRb.linearVelocity;
    }

    private void ExitShell()
    {
        isPlayerInside = false;
        playerCtrl.isDashing = false;

        playerRb.simulated = true;
        playerRb.linearVelocity = rb.linearVelocity;

        // חזרה לגב (משתמש ב-shellMountPoint)
        Equip(playerCtrl.shellMountPoint);
        
        Physics2D.IgnoreCollision(playerCollider, shellCollider, false);
    }

    public void HandleRolling(float moveInput)
    {
        if (!isPlayerInside) return;
        rb.AddTorque(-moveInput * rollTorque);
        rb.angularVelocity = Mathf.Clamp(rb.angularVelocity, -maxAngularVelocity, maxAngularVelocity);
    }

    void LateUpdate()
    {
        if (isPlayerInside && playerCtrl != null)
        {
            // השחקן עוקב אחרי המרכז הפיזיקלי של הקוליידר העגול
            playerCtrl.transform.position = shellCollider.bounds.center + rollingOffset;
            playerCtrl.transform.rotation = Quaternion.identity;
        }
    }

    public override void Unequip(Vector2 throwForce, Collider2D pCol)
    {
        if (isPlayerInside)
        {
            isPlayerInside = false;
            playerCtrl.isDashing = false;
            playerRb.simulated = true;
            playerRb.linearVelocity = rb.linearVelocity;
            Physics2D.IgnoreCollision(playerCollider, shellCollider, false);
        }
        base.Unequip(throwForce, pCol);
    }
}