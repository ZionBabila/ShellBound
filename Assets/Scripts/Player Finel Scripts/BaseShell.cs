using UnityEngine;

public abstract class BaseShell : MonoBehaviour
{
    [Header("Shell Identity")]
    [Tooltip("Unique ID matching the ShellPickup on the ground (e.g. 'RollingCan').")]
    public string shellID;
    
    [Tooltip("The prefab to instantiate into the world when thrown.")]
    public GameObject worldPrefab;

    public ShellState CurrentState { get; protected set; } = ShellState.OnBack;
    protected PlayerShellSystem playerSystem;
    
    // Called by PlayerShellSystem when the player picks up the shell
    public virtual void Equip(PlayerShellSystem player)
    {
        playerSystem = player;
        CurrentState = ShellState.OnBack;
        gameObject.SetActive(true); // Turn on the rig sprite
    }

    // Called by PlayerShellSystem when the player throws the shell
    public virtual void Throw()
    {
        CurrentState = ShellState.OnBack; // Reset state for next equip
        playerSystem = null;
        gameObject.SetActive(false); // Turn off the rig sprite
    }

    // Abstract methods to be implemented by specific shells
    public abstract void ActivateAbility();
    public abstract void DeactivateAbility();

    // Allows shells to process physical collisions (e.g. breaking objects)
    public virtual void OnPlayerCollision(Collision2D collision)
    {
    }
}