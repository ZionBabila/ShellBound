using UnityEngine;
using UnityEngine.InputSystem;

// Simple physics object used to test the water surface. It falls under gravity
// and can be nudged around with the movement keys (arrows / WASD, so this control
// scheme can later be reused on the player). Uses the new Input System, matching
// the rest of the project (e.g. HeavyArmorShell reads Keyboard.current directly).
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
        Vector2 input = ReadMoveInput();

        // Only override the velocity while there is input; otherwise gravity pulls it down.
        if (input != Vector2.zero)
        {
            rigidbody2D.linearVelocity = input * forceAmount;
        }
    }

    // Read movement from the keyboard through the new Input System.
    private Vector2 ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;
        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) input.x -= 1f;
        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) input.y += 1f;
        return input;
    }
}
