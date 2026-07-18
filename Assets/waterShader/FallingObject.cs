using UnityEngine;

// Simple physics object used to test the water surface. It falls under gravity
// and can be nudged around with the movement keys (same axes as the player, so
// this control scheme can later be reused on the player itself).
public class FallingObject : MonoBehaviour
{
    public Rigidbody2D rigidbody2D;
    [SerializeField]
    private float forceAmount;

    void Start()
    {
        // Safety: grab the body from this object if it was not wired in the Inspector.
        if (rigidbody2D == null)
        {
            rigidbody2D = GetComponent<Rigidbody2D>();
        }

        // Initial downward plunge so the first splash has some punch.
        rigidbody2D.linearVelocity = Vector2.down * forceAmount;
    }

    void Update()
    {
        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        // Only override the velocity while there is input; otherwise gravity pulls it down.
        if (input != Vector2.zero)
        {
            rigidbody2D.linearVelocity = input * forceAmount;
        }
    }
}
