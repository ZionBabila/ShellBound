using UnityEngine;

// =============================================================================
// RollingShell — shell with a "ride inside" ability (Space toggles enter/exit).
// -----------------------------------------------------------------------------
// Role:        While the player is inside, freezes the player Rigidbody, makes the
//              shell Dynamic, and torques it directly. PlayerController delegates
//              FixedUpdate locomotion to HandlePlayerMovement during this state.
// Depends on:  CircleCollider2D for the rolling collider; PlayerController to
//              freeze the player's Rigidbody and forward input.
// Used by:     PlayerController via the BaseShell virtual API.
// =============================================================================
[RequireComponent(typeof(CircleCollider2D))]
public class RollingShell : BaseShell
{
    [Header("Rolling Settings")]
    public float rollTorque = 25f;
    public float maxAngularVelocity = 600f;

    [Header("Rolling Visuals")]
    [Tooltip("Player offset relative to the rolling collider center (where the crab sits visually inside the ball).")]
    public Vector3 rollingOffset = new(0f, -0.5f, 0f);

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
        // Run the base equip first so we're parented to mountPoint.
        base.Equip(mountPoint);

        playerCtrl = mountPoint.GetComponentInParent<PlayerController>();
        playerRb = playerCtrl.GetComponent<Rigidbody2D>();
        playerCollider = playerCtrl.GetComponent<Collider2D>();

        isPlayerInside = false;
        rb.simulated = false;
        if (shellCollider != null) shellCollider.enabled = false;
    }

    public override void UseAbility(Rigidbody2D pRb)
    {
        if (!isPlayerInside) EnterShell();
        else ExitShell();
    }

    // Player movement is delegated to this shell only while the player is inside it.
    public override bool OverridesPlayerMovement => isPlayerInside;

    public override void HandlePlayerMovement(Vector2 moveInput, PlayerController player)
    {
        HandleRolling(moveInput.x);
        // Player is detached from the shell while rolling, so a normal flip works.
        player.UpdateFacing(moveInput.x);
    }

    // Crab's own velocity is zero while rolling — the shell carries the motion, so feed the shell speed.
    public override float GetAnimationSpeed(Rigidbody2D playerRb) => Mathf.Abs(rb.linearVelocity.x);

    private void EnterShell()
    {
        isPlayerInside = true;
        playerCtrl.SetDashing(true);

        // Detach the shell from the player so it can roll freely.
        transform.SetParent(null);

        // Freeze player physics — the shell carries the motion now.
        playerRb.simulated = false;

        // Activate shell physics.
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.simulated = true;
        shellCollider.enabled = true;

        // Prevent player/shell collision while inside.
        Physics2D.IgnoreCollision(playerCollider, shellCollider, true);

        // Carry the player's velocity into the shell so the entry feels continuous.
        rb.linearVelocity = playerRb.linearVelocity;
    }

    private void ExitShell()
    {
        isPlayerInside = false;
        playerCtrl.SetDashing(false);

        playerRb.simulated = true;
        playerRb.linearVelocity = rb.linearVelocity;

        // Re-mount on the player's back via shellMountPoint.
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
            // Glue the player to the rolling collider's physical center each visual frame.
            playerCtrl.transform.position = shellCollider.bounds.center + rollingOffset;
            playerCtrl.transform.rotation = Quaternion.identity;
        }
    }

    public override void Unequip(Vector2 throwForce, Collider2D pCol)
    {
        if (isPlayerInside)
        {
            isPlayerInside = false;
            playerCtrl.SetDashing(false);
            playerRb.simulated = true;
            playerRb.linearVelocity = rb.linearVelocity;
            Physics2D.IgnoreCollision(playerCollider, shellCollider, false);
        }
        base.Unequip(throwForce, pCol);
    }
}
