using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float baseMoveSpeed = 5f;
    public float jumpForce = 10f;
    [Header("Physics Movement")]
    // The force the crab's legs apply to the ground. 
    // You will need to play with this number in the Inspector! (Try 20-40)
    public float legPower = 25f; 
    
    // How much rotational power the crab has to flip itself over
    public float flipTorque = 15f;
    [Header("Throw Settings")]
    public Vector2 throwAngle = new Vector2(1f, 1f); // X = Forward, Y = Up
    public float throwPower = 8f;
    [Header("Shell System")]
    public Transform shellMountPoint; // Empty GameObject above the crab
    public float interactRadius = 2f;
    
    // IMPORTANT: Changed to Vector3 so it works with TransformPoint.
    // Use this in the inspector to move the yellow circle exactly where you want it.
    public Vector3 interactOffset; 
    public LayerMask shellLayer; // Layer containing only shells
    
    [Header("Inputs")]
    public InputAction MoveAction;
    public InputAction InteractAction; // E key press
    public InputAction AbilityAction; // Space/Shift key press

    [Header("Animation")]
    public Animator animator; 

    private Rigidbody2D rb;
    private BaseShell currentShell;
    private float originalMass;
    // Tells the controller to ignore keyboard input while a shell is ability is pushing the player 
    [HideInInspector] public bool isDashing = false;
    // The exact boolean used in the video to track facing direction
    private bool facingRight = true; 

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Automatically fetch the Animator if not assigned
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        originalMass = rb.mass; // Store the naked crab's weight
        
        // Subscribe to input system events
        InteractAction.performed += ctx => TryInteractShell();
        AbilityAction.performed += ctx => UseShellAbility();
    }

    void OnEnable()
    {
        MoveAction.Enable();
        InteractAction.Enable();
        AbilityAction.Enable();
    }

    void OnDisable()
    {
        MoveAction.Disable();
        InteractAction.Disable();
        AbilityAction.Disable();
    }

void FixedUpdate() 
    {
        // 1. Read input
        Vector2 moveInput = MoveAction.ReadValue<Vector2>();

        // 2. Rolling Shell takes complete control if inside one
        if (currentShell is RollingShell rollingShell)
        {
            if (animator != null) animator.SetFloat("speed", 0f);
            rollingShell.HandleRolling(moveInput.x);
            return;
        }

        // 3. Skip walking during dash
        if (isDashing) 
        {
            if (animator != null) animator.SetFloat("speed", 0f);
            return;
        }

        // --- 4. PHYSICS-BASED HORIZONTAL MOVEMENT ---
        // Calculate the TARGET speed we want to reach
        float currentTargetSpeed = baseMoveSpeed;
        if (currentShell != null)
        {
            currentTargetSpeed *= currentShell.speedMultiplier;
        }

        // Calculate the difference between our current speed and our target speed
        float speedDifference = (moveInput.x * currentTargetSpeed) - rb.linearVelocity.x;

        // Apply force based on the difference. 
        // Because we use AddForce, Unity respects the crab's Mass!
        // A heavy crab will accelerate slower, and heavy boxes will resist pushing.
        rb.AddForce(Vector2.right * speedDifference * legPower);

        // --- 5. UNLOCKED ROTATION & FLIPPING (Z-AXIS) ---
        // Using W/S or Up/Down arrows to apply rotation torque
        if (Mathf.Abs(moveInput.y) > 0.1f)
        {
            // Apply rotational force. Up arrow/W rotates one way, Down/S rotates the other.
            rb.AddTorque(moveInput.y * flipTorque); 
        }

        // --- 6. ANIMATION & FLIPPING ---
        // Update animation parameters using the actual physics velocity
        if (animator != null)
        {
            animator.SetFloat("speed", Mathf.Abs(rb.linearVelocity.x));
        }

        // Handle visual flipping
        if (moveInput.x > 0 && !facingRight) Flip();
        else if (moveInput.x < 0 && facingRight) Flip();
    }

    // This is the exact flip function from the YouTube video
    private void Flip()
    {
        // Switch the boolean to its opposite
        facingRight = !facingRight;

        // Multiply the player's x local scale by -1
        Vector3 currentScale = transform.localScale;
        currentScale.x *= -1;
        transform.localScale = currentScale;
    }

private void TryInteractShell()
    {
        Debug.Log("Interact Action (E) Triggered!");

        // --- UNEQUIP (Throwing the shell) ---
        // If the player is already wearing a shell, pressing E will throw it
        if (currentShell != null)
        {
            // 1. Determine throw direction (1 for right, -1 for left)
            float direction = facingRight ? 1f : -1f;

            // 2. Calculate the final force vector based on angle and power
            Vector2 finalThrowForce = new Vector2(throwAngle.x * direction, throwAngle.y).normalized * throwPower;

            // 3. Get the player's collider to pass it to the ignore-collision logic
            Collider2D playerCol = GetComponent<Collider2D>();

            // 4. Detach the shell and throw it using physics
            currentShell.Unequip(finalThrowForce, playerCol);

            // 5. Reset the shell reference and restore the crab's original lightweight mass
            currentShell = null;
            rb.mass = originalMass;
            
            return; // Exit the function early so we don't pick up another shell immediately
        }

        // --- EQUIP (Picking up a shell) ---
        // Convert the local interactOffset into a world position for the overlap circle
        Vector3 searchCenter = transform.TransformPoint(interactOffset);
        
        // Find all colliders within range that belong specifically to the shellLayer
        Collider2D[] colliders = Physics2D.OverlapCircleAll(searchCenter, interactRadius, shellLayer);
        
        Debug.Log("Physics check found " + colliders.Length + " colliders in range.");

        if (colliders.Length > 0)
        {
            // Try to extract the BaseShell script from the first valid collider found
            BaseShell foundShell = colliders[0].GetComponent<BaseShell>();
            
            if (foundShell != null)
            {
                // CRITICAL FIX: Read the shell's mass BEFORE calling Equip(). 
                // This prevents bugs if special shells (like RollingShell) alter their physics during Equip.
                float addedWeight = foundShell.shellMass;

                // Assign the new shell and mount it to the crab's back
                currentShell = foundShell;
                currentShell.Equip(shellMountPoint);
                
                // Update the crab's physics body to feel heavier
                rb.mass = originalMass + addedWeight; 
                
                Debug.Log("Successfully equipped: " + foundShell.gameObject.name + " | New crab mass: " + rb.mass);
            }
            else
            {
                // Triggered if the object is in the Shell layer but lacks the required script
                Debug.LogWarning("Found an object in range, but it is missing a BaseShell script!");
            }
        }
    }

    private void UseShellAbility()
    {
        Debug.Log("Ability Action (Space) Triggered!");

        if (currentShell != null)
        {
            currentShell.UseAbility(rb); // Activate shell ability
        }
    }

    private void OnDrawGizmos()
    {
        // Ensure the Gizmo draws in the correct flipped position in the editor too!
        Vector3 gizmoCenter = transform.TransformPoint(interactOffset);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(gizmoCenter, interactRadius);

        // Draw the shell mounting point pivot
        if (shellMountPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(shellMountPoint.position, 0.1f);
            Gizmos.DrawWireSphere(shellMountPoint.position, 0.15f);
        }
    }
}