using UnityEngine;

public class RollingShell : BaseShell
{
    [Header("Rolling Settings")]
    public float rollTorque = 25f;
    public float maxAngularVelocity = 600f;

    [Header("Mounting")]
    [Tooltip("הזז את הערכים כאן (בעיקר Y) כדי למרכז את הסרטן")]
    public Vector3 playerVisualOffset = new Vector3(0f, -0.5f, 0f);

    private bool isPlayerInside = false;
    private Rigidbody2D playerRb;
    private PlayerController playerCtrl;
    private CircleCollider2D shellCollider;
    public override void Equip(Transform mountPoint)
    {
        playerCtrl = mountPoint.GetComponentInParent<PlayerController>();
        playerRb = playerCtrl.GetComponent<Rigidbody2D>();
        shellCollider = GetComponent<CircleCollider2D>();
        // 1. We completely UNPARENT the crab so it doesn't spin with the shell
        playerCtrl.transform.SetParent(null);

        // 2. Disable physics
        playerRb.simulated = false;

        // 3. Set flags
        playerCtrl.isDashing = true;
        isPlayerInside = true;

        // 4. Set shell to dynamic
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.simulated = true;
    }

    public void HandleRolling(float moveInput)
    {
        if (!isPlayerInside) return;

        // Apply rolling force
        rb.AddTorque(-moveInput * rollTorque);
        rb.angularVelocity = Mathf.Clamp(rb.angularVelocity, -maxAngularVelocity, maxAngularVelocity);
    }

    // LateUpdate runs AFTER physics, making it perfect for smooth following
void LateUpdate()
    {
        if (isPlayerInside && playerCtrl != null && shellCollider != null)
        {
            // shellCollider.bounds.center מחזיר את הנקודה המדויקת של מרכז העיגול הירוק בעולם.
            // הנקודה הזו תמיד נשארת יציבה ולא מושפעת מנקודת הציר (Pivot) של התמונה!
            Vector3 physicalCenter = shellCollider.bounds.center;
            
            // מחברים את מרכז הפיזיקה לאופסט החזותי שלך
            playerCtrl.transform.position = physicalCenter + playerVisualOffset;
            
            // שומרים על הסרטן זקוף
            playerCtrl.transform.rotation = Quaternion.identity;
        }
    }

    public override void Unequip(Vector2 throwForce, Collider2D playerCollider)
    {
        isPlayerInside = false;

        // Restore physics
        playerRb.simulated = true;
        playerCtrl.isDashing = false;

        base.Unequip(throwForce, playerCollider);
    }
}