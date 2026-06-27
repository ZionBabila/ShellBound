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

        // 1. Read data from the player
        float currentSpeed = simplePlayer.CurrentSpeed;
        bool isGrounded = simplePlayer.IsGrounded;

        // 2. Pass data to Animator
        animator.SetFloat("Speed", currentSpeed * animationSpeedMultiplier);
        animator.SetBool("IsGrounded", isGrounded);
        
        // In the future, we will also add reading data from the shell system (PlayerShellSystem)
        // For example: animator.SetBool("IsRolling", shellSystem.IsRolling);
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
            GameManager.Instance.FinishPlayerRespawn(playerToRespawn, respawnTarget);
        }
    }
}