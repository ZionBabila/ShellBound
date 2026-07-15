using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Animator component, usually on the visualsRoot object.")]
    public Animator animator;
    
    [Tooltip("Reference to the player's core script.")]
    public SimplePlayer simplePlayer;

    [Tooltip("Multiplier for the 'Speed' parameter sent to the Animator. Tweaks the walk animation pace.")]
    public float animationSpeedMultiplier = 1.0f;

    // Animator parameter hashes for performance
    private static readonly int DeathTriggerHash = Animator.StringToHash("Die");
    private static readonly int HeavyAbilityTriggerHash = Animator.StringToHash("HeavyAbility");
    private static readonly int IsGrabbingHash = Animator.StringToHash("IsGrabbing");
    private static readonly int IsGroundPoundingHash = Animator.StringToHash("IsGroundPounding");
    private static readonly int RespawnTriggerHash = Animator.StringToHash("Respawn");

    // Tracks the previous grab state to fire the HeavyLift one-shot only on the grab start edge.
    private bool wasGrabbing = false;

    // Store respawn info temporarily while the animation is playing
    private GameObject playerToRespawn;
    private Transform respawnTarget;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        
        // If not assigned via Inspector, try to find the script on the same object or parent object
        if (simplePlayer == null) simplePlayer = GetComponentInParent<SimplePlayer>();
    }

    private void Update()
    {
        if (simplePlayer == null || animator == null) return;

        // While a curtain freeze is active, rb.linearVelocity is forced to zero every
        // FixedUpdate. Syncing that here would slam Speed to 0 and snap whatever animation
        // was playing (e.g. a fall loop) back to Idle. Skip the sync so it keeps playing as-is.
        if (simplePlayer.IsPhysicsFrozen) return;

        // 1. Read data from the player
        float currentSpeed = simplePlayer.CurrentSpeed;
        bool isGrounded = simplePlayer.IsGrounded;
        bool isGrabbing = simplePlayer.IsGrabbing;
        bool isGroundPounding = simplePlayer.IsGroundPounding;

        // 2. Pass data to Animator
        animator.SetFloat("Speed", currentSpeed * animationSpeedMultiplier);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool(IsGrabbingHash, isGrabbing);
        animator.SetBool(IsGroundPoundingHash, isGroundPounding);

        // Fire the HeavyLift one-shot only on the moment a grab starts (rising edge).
        // The HeavyLift state then plays once and exits back to Idle/Walk on its own.
        if (isGrabbing && !wasGrabbing)
        {
            animator.SetTrigger(HeavyAbilityTriggerHash);
        }
        wasGrabbing = isGrabbing;
    }

    /// <summary>
    /// Called by HeavyArmorShell to trigger a specific one-shot animation.
    /// </summary>
    public void TriggerHeavyAbility()
    {
        if (animator == null) return;
        animator.SetTrigger(HeavyAbilityTriggerHash);
    }

    /// <summary>
    /// Triggers the death animation. The animation clip itself must have an
    /// Animation Event at the end that calls 'OnDeathAnimationComplete'.
    /// </summary>
    public void PlayDeathAnimation(GameObject player, Transform respawnPoint)
    {
        if (animator == null) return;

        // Store the data needed for when the animation finishes
        playerToRespawn = player;
        respawnTarget = respawnPoint;

        // Fire the "Die" trigger in the Animator Controller
        animator.SetTrigger(DeathTriggerHash);
    }

    // THIS FUNCTION IS CALLED BY AN ANIMATION EVENT AT THE END OF THE DEATH CLIP
    public void OnDeathAnimationComplete()
    {
        if (GameManager.Instance != null && playerToRespawn != null && respawnTarget != null)
        {
            // First teleport the player to the respawn point and restore control...
            GameManager.Instance.FinishPlayerRespawn(playerToRespawn, respawnTarget);

            // ...then leave the Death state. Idle resumes exactly at the respawn point,
            // not when the death clip happens to end.
            animator.SetTrigger(RespawnTriggerHash);
        }
    }
}