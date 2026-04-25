using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float baseMoveSpeed = 5f;
    public float jumpForce = 10f;
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
        //if shell is currently dashing, completely stop reading keyboard movement 
        if (isDashing)
        {
            return;
        }
        // Physics-based movement must be done in FixedUpdate
        Vector2 moveInput = MoveAction.ReadValue<Vector2>();
        
        // Calculate speed (affected by the shell if equipped)
        float currentSpeed = baseMoveSpeed;
        if (currentShell != null)
        {
            currentSpeed *= currentShell.speedMultiplier;
        }

        // Apply horizontal movement
        rb.linearVelocity = new Vector2(moveInput.x * currentSpeed, rb.linearVelocity.y);

        // --- Animation Logic ---
        bool isWalking = Mathf.Abs(moveInput.x) > 0.01f;
        if (animator != null)
        {
            animator.SetFloat("speed", Mathf.Abs(rb.linearVelocity.x));
        }

        // --- Flip Logic (Exactly like the video) ---
        // If moving right but not facing right
        if (moveInput.x > 0 && !facingRight)
        {
            Flip();
        }
        // If moving left but currently facing right
        else if (moveInput.x < 0 && facingRight)
        {
            Flip();
        }
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

        // If wearing a shell, pressing will throw it
   if (currentShell != null)
        {
            // 1. Determine direction (1 for right, -1 for left)
            float direction = facingRight ? 1f : -1f;

            // 2. Calculate the final force vector
            Vector2 finalThrowForce = new Vector2(throwAngle.x * direction, throwAngle.y).normalized * throwPower;

            // 3. Get player's collider to pass it for the ignore logic
            Collider2D playerCol = GetComponent<Collider2D>();

            // 4. Call unequip with physics parameters
            currentShell.Unequip(finalThrowForce, playerCol);

            currentShell = null;
            rb.mass = originalMass;
            return;
        }

        // TransformPoint converts the interactOffset into a world position
        Vector3 searchCenter = transform.TransformPoint(interactOffset);
        
        // Check for colliders in the designated layer
        Collider2D[] colliders = Physics2D.OverlapCircleAll(searchCenter, interactRadius, shellLayer);
        
        // --- NEW DEBUG LINE TO FIND THE ISSUE ---
        Debug.Log("Physics check found " + colliders.Length + " colliders in range.");

        if (colliders.Length > 0)
        {
            // Try to get the BaseShell script from the first collider found
            BaseShell foundShell = colliders[0].GetComponent<BaseShell>();
            
            if (foundShell != null)
            {
                currentShell = foundShell;
                currentShell.Equip(shellMountPoint);
                rb.mass = originalMass + currentShell.shellMass; // The crab becomes heavier
                Debug.Log("Successfully equipped: " + foundShell.gameObject.name);
            }
            else
            {
                // This will trigger if the object is in the Shells layer, but has no script attached
                Debug.LogWarning("Found an object, but it doesn't have a BaseShell/SprayShell script on it!");
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