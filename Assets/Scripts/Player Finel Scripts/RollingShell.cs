using UnityEngine;

public class RollingShell : BaseShell
{
    [Header("Visuals")]
    [Tooltip("The GameObject (sprite) shown when the shell is on the crab's back.")]
    public GameObject onBackVisuals;
    
    [Tooltip("The GameObject (sprite) shown when the crab is actively rolling.")]
    public GameObject rollingVisuals;

    [Header("Roll Particles")]
    [Tooltip("Plays only while rolling above minRollSpeed. Parent it under this shell object (NOT " +
             "under rollingVisuals, which gets switched off), set Looping on, Play On Awake off, and " +
             "Simulation Space = World so the trail stays behind instead of spinning with the shell.")]
    public ParticleSystem rollParticles;

    [Tooltip("Rolling speed the particles start at. Rate over Distance has no speed threshold of " +
             "its own, so this gate is what gives it one.")]
    public float minRollSpeed = 4f;

    [Tooltip("Where the dust comes off, measured from the player's center. Y is down to the ground " +
             "contact; X flips with the roll direction, so a negative X always trails behind.")]
    public Vector2 groundOffset = new Vector2(-0.2f, -0.5f);

    // Edge detection: only call Play/Stop when the answer actually changes, not every frame.
    private bool particlesOn = false;

    public override void Equip(PlayerShellSystem player)
    {
        base.Equip(player);
        
        // Set default visual state when equipped
        if (onBackVisuals != null) onBackVisuals.SetActive(true);
        if (rollingVisuals != null) rollingVisuals.SetActive(false);

        // Defensive reset: Ensure this shell does not inherit a slowdown from a previously equipped shell.
        // Each shell should be responsible for defining the player's speed.
        playerSystem.Player.currentSpeedMultiplier = 1f;
    }

    private void Update()
    {
        if (rollParticles == null) return;

        bool rolling = CurrentState == ShellState.InUse && playerSystem != null;

        // The shell spins while rolling, so as a child the emitter would orbit with it and throw
        // dust from every angle. Pin it to the ground contact instead, upright and flipped by
        // the direction of travel, so it always sheds from where the shell meets the floor.
        if (rolling) PinParticlesToGround();

        // Emit only while actually rolling, and only once the roll is fast enough to be worth showing.
        bool shouldEmit = rolling && playerSystem.Player.CurrentSpeed >= minRollSpeed;

        if (shouldEmit == particlesOn) return;

        if (shouldEmit) rollParticles.Play();
        else rollParticles.Stop(); // Default Stop only halts emission, live particles still fade out.

        particlesOn = shouldEmit;
    }

    // Holds the emitter at the shell's ground contact point, cancelling the spin it would
    // otherwise inherit from its rotating parent.
    private void PinParticlesToGround()
    {
        SimplePlayer player = playerSystem.Player;

        // Roll direction, not the visual flip: while rolling the shell moves purely by physics.
        float dir = Mathf.Sign(player.Rb.linearVelocity.x);
        if (Mathf.Approximately(dir, 0f)) dir = 1f;

        rollParticles.transform.position =
            player.transform.position + new Vector3(groundOffset.x * dir, groundOffset.y, 0f);
        rollParticles.transform.rotation = Quaternion.identity;
    }

    public override void ActivateAbility()
    {
        if (CurrentState != ShellState.OnBack) return;
        
        CurrentState = ShellState.InUse;
        
        // Swap visual objects
        if (onBackVisuals != null) onBackVisuals.SetActive(false);
        if (rollingVisuals != null) rollingVisuals.SetActive(true);
        
        // 1. Swap colliders
        if (playerSystem.Player.withShellCollider != null) 
            playerSystem.Player.withShellCollider.enabled = false;
            
        if (playerSystem.Player.rollingCollider != null) 
            playerSystem.Player.rollingCollider.enabled = true;
            
        // 2. Hide the main crab visuals
        playerSystem.SetCrabVisualsActive(false);

        // 3. Disable manual input so the shell rolls purely by physics (gravity/momentum)
        playerSystem.Player.isMovementDisabled = true;

        // 4. Unlock Z rotation for physical rolling
        playerSystem.Player.Rb.freezeRotation = false;

        Debug.Log("[RollingShell] Activated! Player is in roll mode.");
    }

    public override void DeactivateAbility()
    {
        if (CurrentState != ShellState.InUse) return;
        
        CurrentState = ShellState.OnBack;
        
        // Swap visual objects back
        if (onBackVisuals != null) onBackVisuals.SetActive(true);
        if (rollingVisuals != null) rollingVisuals.SetActive(false);
            
        // 1. Restore colliders
        if (playerSystem.Player.withShellCollider != null) 
            playerSystem.Player.withShellCollider.enabled = true;
            
        if (playerSystem.Player.rollingCollider != null) 
            playerSystem.Player.rollingCollider.enabled = false;
            
        // 2. Show the main crab visuals
        playerSystem.SetCrabVisualsActive(true);

        // 3. Re-enable manual input
        playerSystem.Player.isMovementDisabled = false;

        // 4. Lock Z rotation and snap upright
        playerSystem.Player.Rb.freezeRotation = true;
        playerSystem.Player.transform.rotation = Quaternion.identity;

        // Request momentum preservation for 1 second to allow a natural slowdown
        playerSystem.Player.PreserveMomentumFor(1f);

        Debug.Log("[RollingShell] Deactivated! Back to walking.");
    }
}