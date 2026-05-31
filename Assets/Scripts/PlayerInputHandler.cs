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

    [Tooltip("Hold to grab a Movable item for push/pull (Ctrl).")]
    public InputAction GrabAction;

    [Tooltip("Jump action (e.g., Up Arrow / W / Gamepad A).")]
    public InputAction JumpAction;

    // Read on access so the timing matches the previous direct ReadValue call.
    public Vector2 MoveValue => MoveAction.ReadValue<Vector2>();

    // True while the grab button is currently held down.
    public bool IsGrabHeld => GrabAction.IsPressed();

    // Fired once when the interact key transitions to performed.
    public event Action OnInteract;

    // Fired once when the ability key transitions to performed.
    public event Action OnAbility;

    // Fired when the grab key is pressed down.
    public event Action OnGrabStart;

    // Fired when the grab key is released.
    public event Action OnGrabEnd;

    // Fired once when the jump key transitions to performed.
    public event Action OnJump;

    void Awake()
    {
        InteractAction.performed += HandleInteractPerformed;
        AbilityAction.performed += HandleAbilityPerformed;
        GrabAction.performed += HandleGrabPerformed;
        GrabAction.canceled += HandleGrabCanceled;
        JumpAction.performed += HandleJumpPerformed;
    }

    void OnDestroy()
    {
        InteractAction.performed -= HandleInteractPerformed;
        AbilityAction.performed -= HandleAbilityPerformed;
        GrabAction.performed -= HandleGrabPerformed;
        GrabAction.canceled -= HandleGrabCanceled;
        JumpAction.performed -= HandleJumpPerformed;
    }

    void OnEnable()
    {
        MoveAction.Enable();
        InteractAction.Enable();
        AbilityAction.Enable();
        GrabAction.Enable();
        JumpAction.Enable();
    }

    void OnDisable()
    {
        MoveAction.Disable();
        InteractAction.Disable();
        AbilityAction.Disable();
        GrabAction.Disable();
        JumpAction.Disable();
    }

    private void HandleInteractPerformed(InputAction.CallbackContext _) => OnInteract?.Invoke();
    private void HandleAbilityPerformed(InputAction.CallbackContext _) => OnAbility?.Invoke();
    private void HandleGrabPerformed(InputAction.CallbackContext _) => OnGrabStart?.Invoke();
    private void HandleGrabCanceled(InputAction.CallbackContext _) => OnGrabEnd?.Invoke();
    private void HandleJumpPerformed(InputAction.CallbackContext _) => OnJump?.Invoke();
}
