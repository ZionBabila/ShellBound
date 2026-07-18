using UnityEngine;

// Lets the player dive/rise while inside a BuoyancyEffector2D water volume. The
// player stays INSIDE the effector, so buoyancy and drag (the nice water feel)
// come from the effector itself. This component only adds the player-driven swim
// force from the existing Move input (its Y axis, which SimplePlayer ignores on
// land). A strong Swim Down Force lets the player dive against the buoyancy that
// keeps objects afloat, so only the player - not passive objects - can sink.
// Horizontal movement stays with SimplePlayer. It does not modify SimplePlayer.
[RequireComponent(typeof(Rigidbody2D))]
public class WaterSwim : MonoBehaviour
{
    [Tooltip("Upward force when swimming up. Can be low - the effector's buoyancy already lifts.")]
    [SerializeField] private float swimUpForce = 20f;

    [Tooltip("Downward force when diving. Must beat the effector's buoyancy to sink.")]
    [SerializeField] private float swimDownForce = 40f;

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

        // Buoyancy and drag are handled by the water's BuoyancyEffector2D.
        // Here we only add the player-driven swim force, separate for up vs down.
        float verticalInput = inputHandler != null ? inputHandler.MoveValue.y : 0f;
        float force = verticalInput > 0f ? swimUpForce : swimDownForce;
        rb.AddForce(Vector2.up * (verticalInput * force), ForceMode2D.Force);
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
