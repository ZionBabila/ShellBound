using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class BaseShell : MonoBehaviour
{
    [Header("Feature Toggles")]
    public bool canBoost = false;
    public bool canRoll = false;
    public bool affectsMass = true;

    [Header("Shell Stats")]
    // Smart property: dynamically reads the mass directly from the Rigidbody2D component
    public float shellMass => GetComponent<Rigidbody2D>().mass; 
    public float speedMultiplier = 1f;

    [Header("Visual Settings")]
    public float collectionRadius = 2f; // Visual reference for the editor
    public Vector3 gizmoOffset; // Allows centering the visual circle

    protected Rigidbody2D rb;
    protected Collider2D coll;
    protected SpriteRenderer spriteRenderer;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Removed the redundant mass assignment here. 
        // The mass is now controlled entirely via the Inspector's Rigidbody2D component!
    }

    // Aligns the shell's pivot (defined in Sprite Editor) with the crab's mount point
    public virtual void Equip(Transform mountPoint)
    {
        // Turning off simulation means Unity physics COMPLETELY ignores this object.
        // This is exactly why the mass is "lost" when equipped! 
        // (The PlayerController must add this shell's mass to the Crab's Rigidbody)
        rb.simulated = false;
        coll.enabled = false;

        transform.SetParent(mountPoint);

        // Resetting local position to (0,0,0) aligns the sprite's pivot 
        // with the mountPoint's position automatically
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public virtual void Unequip(Vector2 throwForce, Collider2D playerCollider)
    {
        transform.SetParent(null); // Detach from mount point

        // Turn the physics engine and collisions back on for this shell
        rb.simulated = true;

        Collider2D shellCollider = GetComponent<Collider2D>();
        if (shellCollider != null)
        {
            shellCollider.enabled = true;
        }

        // Temporarily ignore collision between player and shell to prevent getting stuck
        if (playerCollider != null && shellCollider != null)
        {
            StartCoroutine(IgnoreCollisionRoutine(playerCollider, shellCollider));
        }

        // Apply the throwing force using the shell's actual physics mass
        rb.AddForce(throwForce, ForceMode2D.Impulse);
    }

    private System.Collections.IEnumerator IgnoreCollisionRoutine(Collider2D player, Collider2D shell)
    {
        // Disable collision momentarily
        Physics2D.IgnoreCollision(player, shell, true);

        // Wait for a fraction of a second until the shell is clear of the player's body
        yield return new WaitForSeconds(0.2f);

        // Re-enable collision so the shell can interact with the player again later
        Physics2D.IgnoreCollision(player, shell, false);
    }

    public virtual void UseAbility(Rigidbody2D playerRb)
    {
        // This will be overridden by specific shell types (like SprayShell)
        if (!canBoost && !canRoll)
        {
            Debug.Log("This shell has no active abilities.");
        }
    }

    private void OnDrawGizmos()
    {
        // Use TransformPoint so the offset rotates and flips perfectly with the shell
        Vector3 drawPosition = transform.TransformPoint(gizmoOffset);

        // Show the collection radius around the shell in the editor
        Gizmos.color = new Color(0, 1, 0, 0.3f); // Semi-transparent green
        Gizmos.DrawWireSphere(drawPosition, collectionRadius);
    }
}