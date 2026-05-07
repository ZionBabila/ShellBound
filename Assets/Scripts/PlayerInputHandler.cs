using System;
using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================================
// PlayerInputHandler
// -----------------------------------------------------------------------------
// Role:        The single bridge between Unity's InputSystem and the player.
//              Reads MoveValue (vector) and fires OnInteract / OnAbility events.
// Depends on:  Three InputAction fields configured in the Inspector.
// Used by:     PlayerController subscribes to events in Awake, unsubscribes in OnDestroy.
// =============================================================================
[DisallowMultipleComponent]
public class PlayerInputHandler : MonoBehaviour
{
    [Header("Input Actions")]
    [Tooltip("Movement axes (WASD / left stick).")]
    public InputAction MoveAction;

    [Tooltip("Pickup / throw shell (E).")]
    public InputAction InteractAction;

    [Tooltip("Use shell ability (Space).")]
    public InputAction AbilityAction;

    // Read on access so the timing matches the previous direct ReadValue call.
    public Vector2 MoveValue => MoveAction.ReadValue<Vector2>();

    // Fired once when the interact key transitions to performed.
    public event Action OnInteract;

    // Fired once when the ability key transitions to performed.
    public event Action OnAbility;

    void Awake()
    {
        InteractAction.performed += HandleInteractPerformed;
        AbilityAction.performed += HandleAbilityPerformed;
    }

    void OnDestroy()
    {
        // Prevent leaks if the component is destroyed while the actions outlive it.
        InteractAction.performed -= HandleInteractPerformed;
        AbilityAction.performed -= HandleAbilityPerformed;
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

    private void HandleInteractPerformed(InputAction.CallbackContext _) => OnInteract?.Invoke();
    private void HandleAbilityPerformed(InputAction.CallbackContext _) => OnAbility?.Invoke();
}
