using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public abstract class BaseShell : MonoBehaviour
{
    [Header("Base Settings")]
    [Tooltip("Offset relative to the player's visual root when equipped")]
    public Vector3 equippedOffset = new Vector3(0, 0.5f, 0);
    
    public ShellState CurrentState { get; protected set; } = ShellState.OnGround;
    protected PlayerShellSystem playerSystem;
    
    protected Rigidbody2D rb;
    protected Collider2D col;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    // Called by PlayerShellSystem when the player picks up the shell
    public virtual void Equip(PlayerShellSystem player)
    {
        playerSystem = player;
        CurrentState = ShellState.OnBack;

        // Disable physics while carried
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        col.enabled = false;

        // Attach to player visuals
        transform.SetParent(player.VisualsRoot);
        transform.localPosition = equippedOffset;
        transform.localRotation = Quaternion.identity;
    }

    // Called by PlayerShellSystem when the player throws the shell
    public virtual void Throw(Vector2 throwVelocity)
    {
        CurrentState = ShellState.Thrown;
        playerSystem = null;

        // Re-enable physics
        transform.SetParent(null);
        rb.bodyType = RigidbodyType2D.Dynamic;
        col.enabled = true;

        rb.linearVelocity = throwVelocity;
        
        // Wait a brief moment before it can be collected again
        Invoke(nameof(ResetToGround), 0.5f);
    }

    protected virtual void ResetToGround()
    {
        CurrentState = ShellState.OnGround;
    }

    // Abstract methods to be implemented by specific shells
    public abstract void ActivateAbility();
    public abstract void DeactivateAbility();
}