using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rumble style played while the player drags a Movable object.
/// </summary>
public enum DragRumbleMode
{
    Constant,    // Steady rumble for the whole drag
    Pulse,       // Short pulses synced to the drag sound's step cadence (Movable.dragSoundInterval)
    SpeedScaled  // Intensity follows how fast the object is actually moving
}

// =============================================================================
// PlayerInputHandler
// -----------------------------------------------------------------------------
// Role:        The single bridge between Unity's InputSystem and the player.
//              Reads MoveValue (vector) and fires OnInteract / OnAbility events.
//              Also owns all gamepad rumble/haptics (break + drag), since it is
//              already the script talking directly to the Input System.
// Depends on:  Three InputAction fields configured in the Inspector.
// Used by:     PlayerController subscribes to events in Awake, unsubscribes in OnDestroy.
// =============================================================================
[DisallowMultipleComponent]
public class PlayerInputHandler : MonoBehaviour
{
    // Static reference to the single player's input handler, so non-player scripts
    // (Breakable, Movable) can reach the rumble API without a scene lookup.
    public static PlayerInputHandler instance;

    [Header("Controller Rumble - Break")]
    [Tooltip("Low-frequency motor strength for the break rumble (0-1).")]
    [Range(0f, 1f)] public float breakRumbleLowFreq = 0.3f;
    [Tooltip("High-frequency motor strength for the break rumble (0-1).")]
    [Range(0f, 1f)] public float breakRumbleHighFreq = 0.6f;
    [Tooltip("How long the break rumble lasts, in seconds.")]
    public float breakRumbleDuration = 0.15f;

    [Header("Controller Rumble - Drag")]
    [Tooltip("Which rumble style plays while dragging a Movable object.")]
    public DragRumbleMode dragRumbleMode = DragRumbleMode.Constant;
    [Tooltip("Low-frequency motor strength while dragging (Constant/Pulse), or at max speed (SpeedScaled).")]
    [Range(0f, 1f)] public float dragRumbleLowFreq = 0.1f;
    [Tooltip("High-frequency motor strength while dragging (Constant/Pulse), or at max speed (SpeedScaled).")]
    [Range(0f, 1f)] public float dragRumbleHighFreq = 0.15f;
    [Tooltip("Pulse mode: how long each pulse lasts, in seconds. Pulses repeat every Movable.dragSoundInterval.")]
    public float dragRumblePulseDuration = 0.1f;
    [Tooltip("SpeedScaled mode: object speed that produces zero rumble.")]
    public float dragRumbleMinSpeed = 0.1f;
    [Tooltip("SpeedScaled mode: object speed that produces full rumble (dragRumbleLowFreq/HighFreq).")]
    public float dragRumbleMaxSpeed = 4f;

    private Coroutine rumbleRoutine;

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

    // Fired when the jump key is released. Needed for variable-height jumps.
    public event Action OnJumpReleased;

    // True while the jump button is currently held down.
    public bool IsJumpHeld => JumpAction.IsPressed();

    void Awake()
    {
        instance = this;

        InteractAction.performed += HandleInteractPerformed;
        AbilityAction.performed += HandleAbilityPerformed;
        GrabAction.performed += HandleGrabPerformed;
        GrabAction.canceled += HandleGrabCanceled;
        JumpAction.performed += HandleJumpPerformed;
        JumpAction.canceled += HandleJumpCanceled;
    }

    void OnDestroy()
    {
        InteractAction.performed -= HandleInteractPerformed;
        AbilityAction.performed -= HandleAbilityPerformed;
        GrabAction.performed -= HandleGrabPerformed;
        GrabAction.canceled -= HandleGrabCanceled;
        JumpAction.performed -= HandleJumpPerformed;
        JumpAction.canceled -= HandleJumpCanceled;
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
    private void HandleJumpCanceled(InputAction.CallbackContext _) => OnJumpReleased?.Invoke();

    // -------------------------------------------------------------------
    // Controller Rumble
    // -------------------------------------------------------------------

    // One-shot rumble played when a Breakable object is smashed. Called directly by Breakable.Smash().
    public void PlayBreakRumble()
    {
        Rumble(breakRumbleLowFreq, breakRumbleHighFreq, breakRumbleDuration);
    }

    // Called once by Movable on the "dragging started" edge. Only Constant mode needs to do
    // anything here - Pulse fires its own bursts per drag step (see PulseDragRumble), and
    // SpeedScaled is driven every frame from the object's actual speed (see UpdateDragRumble).
    public void StartDragRumble()
    {
        if (dragRumbleMode == DragRumbleMode.Constant)
            Gamepad.current?.SetMotorSpeeds(dragRumbleLowFreq, dragRumbleHighFreq);
    }

    // Called once by Movable on the "dragging stopped" edge (object stops moving or is released).
    // Cuts the motors regardless of mode, so a lingering Constant/SpeedScaled rumble (or a Pulse
    // burst still mid-flight) never survives past the drag.
    public void StopDragRumble()
    {
        Gamepad.current?.SetMotorSpeeds(0f, 0f);
    }

    // SpeedScaled mode only: called by Movable every FixedUpdate while actively dragging, with
    // the object's current horizontal speed. Scales the motors linearly between
    // dragRumbleMinSpeed (0 rumble) and dragRumbleMaxSpeed (full dragRumbleLowFreq/HighFreq).
    // No-op in the other two modes.
    public void UpdateDragRumble(float currentSpeed)
    {
        if (dragRumbleMode != DragRumbleMode.SpeedScaled) return;

        float t = Mathf.InverseLerp(dragRumbleMinSpeed, dragRumbleMaxSpeed, currentSpeed);
        Gamepad.current?.SetMotorSpeeds(dragRumbleLowFreq * t, dragRumbleHighFreq * t);
    }

    // Pulse mode only: called by Movable once per drag-sound "step" (its existing
    // dragSoundInterval cadence) to fire one short burst. Reuses the same timed Rumble() helper
    // as the break effect, so a new pulse cleanly cancels a still-running one. No-op in the
    // other two modes.
    public void PulseDragRumble()
    {
        if (dragRumbleMode != DragRumbleMode.Pulse) return;
        Rumble(dragRumbleLowFreq, dragRumbleHighFreq, dragRumblePulseDuration);
    }

    // Shared timed-rumble helper: starts the gamepad motors for 'duration' seconds, then resets
    // them. Restarting this while a previous rumble is still running cancels it first (no
    // overlapping/competing SetMotorSpeeds calls). Silently does nothing if there is no gamepad,
    // or on platforms that don't support it (e.g. WebGL).
    private void Rumble(float lowFreq, float highFreq, float duration)
    {
        if (Gamepad.current == null) return;

        if (rumbleRoutine != null) StopCoroutine(rumbleRoutine);
        rumbleRoutine = StartCoroutine(RumbleRoutine(lowFreq, highFreq, duration));
    }

    private IEnumerator RumbleRoutine(float lowFreq, float highFreq, float duration)
    {
        Gamepad.current?.SetMotorSpeeds(lowFreq, highFreq);
        yield return new WaitForSeconds(duration);
        Gamepad.current?.SetMotorSpeeds(0f, 0f);
        rumbleRoutine = null;
    }
}
