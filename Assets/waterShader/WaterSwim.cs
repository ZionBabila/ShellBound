using UnityEngine;

// Lets the player free-swim while inside a water volume. The player is kept OUT
// of the BuoyancyEffector2D (exclude the Player layer from the effector's Collider
// Mask), so this component fully owns the crab's vertical motion: it cancels
// gravity so the crab hovers, then drives up/down from the existing Move input
// (its Y axis, which SimplePlayer ignores on land). This decouples the crab from
// the water Density, which can then be tuned purely to float other objects.
// Horizontal movement stays with SimplePlayer. It does not modify SimplePlayer.
[RequireComponent(typeof(Rigidbody2D))]
public class WaterSwim : MonoBehaviour
{
    [Tooltip("Upward force applied when swimming up (rising).")]
    [SerializeField] private float swimUpForce = 20f;

    [Tooltip("Downward force applied when swimming down (diving).")]
    [SerializeField] private float swimDownForce = 20f;

    [Tooltip("How strongly the water damps vertical motion (higher = thicker water).")]
    [SerializeField] private float verticalDrag = 3f;

    [Tooltip("Reads MoveValue.y for the swim direction. Auto-found on this object if empty.")]
    [SerializeField] private PlayerInputHandler inputHandler;

    private Rigidbody2D rb;

    // Counts overlapping water triggers so multi-collider bodies (or several water
    // volumes) enter/exit cleanly. In water only while this is above zero.
    private int waterOverlaps = 0;

    private bool InWater => waterOverlaps > 0;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (inputHandler == null)
        {
            inputHandler = GetComponent<PlayerInputHandler>();
        }
    }

    private void FixedUpdate()
    {
        if (!InWater)
        {
            return;
        }

        // Cancel gravity so the crab hovers instead of sinking (free-swim feel).
        // Matches whatever gravityScale SimplePlayer set this step.
        rb.AddForce(-Physics2D.gravity * (rb.mass * rb.gravityScale), ForceMode2D.Force);

        // Player-driven vertical swim, with a separate strength for rising vs diving.
        float verticalInput = inputHandler != null ? inputHandler.MoveValue.y : 0f;
        float force = verticalInput > 0f ? swimUpForce : swimDownForce;
        rb.AddForce(Vector2.up * (verticalInput * force), ForceMode2D.Force);

        // Water resistance on the vertical axis so motion settles smoothly.
        rb.AddForce(Vector2.down * (rb.linearVelocity.y * verticalDrag), ForceMode2D.Force);
    }

    // Identify water by the BuoyancyEffector2D on (or above) the volume the body enters - no tag needed.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<BuoyancyEffector2D>() != null)
        {
            waterOverlaps++;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<BuoyancyEffector2D>() != null)
        {
            waterOverlaps = Mathf.Max(0, waterOverlaps - 1);
        }
    }
}
