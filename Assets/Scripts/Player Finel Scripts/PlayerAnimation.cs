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
}