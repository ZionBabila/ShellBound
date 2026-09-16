using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GlobalSmokeEmitter : MonoBehaviour
{
    public static List<GlobalSmokeEmitter> AllEmitters = new List<GlobalSmokeEmitter>();

    public enum EmissionShape { Circle = 0, Ring = 1, Line = 2 }
    public enum VelocityMode { Directional = 0, Radial = 1 }
    public enum StopAction { None, Disable, Destroy, Callback }

    [Header("Smoke Properties")]
    public EmissionShape shape = EmissionShape.Circle;
    public Color emitColor = Color.white;
    [Range(0.1f, 100f)] public float density = 10f;

    [Header("Shape Dimensions")]
    public float radius = 3f;
    [Tooltip("Used for Ring and Line thickness")]
    public float thickness = 0.5f;
    [Tooltip("Local offset for the end of the line segment")]
    public Vector2 lineEndPoint = new Vector2(5f, 0f);

    [Header("Velocity")]
    public VelocityMode velocityMode = VelocityMode.Directional;
    [Range(0f, 300f)] public float velocityStrength = 20f;
    [Tooltip("Used only if Mode is Directional")]
    public Vector2 velocityDirection = Vector2.up;
    [Range(0f, 100f)] public float velocityRandomness = 5f;
    [Tooltip("How much volume is created (Required for Radial Explosions)")]
    [Range(0f, 5000f)] public float expansionRate = 150f;

    [Header("Local Turbulence")]
    [Tooltip("Injects curl noise exclusively inside this emitter's shape.")]
    [Range(0f, 500f)] public float turbulenceStrength = 0f;
    [Range(0.01f, 10f)] public float turbulenceScale = 0.5f;

    [Header("Timing")]
    public bool continuous = true;
    public float burstDuration = 0.35f;
    public float burstCooldown = 2f;
    [Min(1)] public int burstCount = 1;

    [Header("Stop Behavior")]
    public StopAction stopAction = StopAction.None;
    [Tooltip("Delay in seconds after emission finishes before the stop action occurs.")]
    public float stopActionDelay = 0f;
    public UnityEvent onStopped;

    public bool IsEmitting { get; private set; }
    public Vector2 CurrentVelocity { get; private set; }

    private float _burstTimer = 0f;
    private int _burstsEmitted = 0;
    private bool _isPlaying = true;
    private bool _isPendingStop = false;
    private float _stopDelayTimer = 0f;

    private void OnEnable() { AllEmitters.Add(this); }
    private void OnDisable() { AllEmitters.Remove(this); }

    public void Play()
    {
        _isPlaying = true;
        _isPendingStop = false;
        _stopDelayTimer = 0f;
        _burstsEmitted = 0;
        _burstTimer = 0f;
    }

    public void Stop()
    {
        _isPlaying = false;
        IsEmitting = false;
        _isPendingStop = true;
    }

    public void Tick(float dt)
    {
        if (!_isPlaying)
        {
            IsEmitting = false;
            if (_isPendingStop)
            {
                _stopDelayTimer += dt;
                if (_stopDelayTimer >= stopActionDelay)
                {
                    ExecuteStopAction();
                }
            }
            return;
        }

        UpdateTiming(dt);

        if (IsEmitting && velocityMode == VelocityMode.Directional)
        {
            CalculateDirectionalVelocity();
        }
    }

    private void UpdateTiming(float dt)
    {
        if (continuous)
        {
            IsEmitting = true;
            return;
        }

        _burstTimer += dt;

        if (_burstsEmitted < burstCount)
        {
            // 1. Emit continuously for the entire duration
            if (_burstTimer <= burstDuration)
            {
                IsEmitting = true;
            }
            else
            {
                // 2. Wait for the cooldown, then reset for the next burst
                if (_burstTimer >= burstDuration + burstCooldown)
                {
                    _burstsEmitted++;
                    _burstTimer = 0f;
                }
                IsEmitting = false;
            }
        }
        else
        {
            // 3. All bursts finished
            _isPlaying = false;
            IsEmitting = false;
            _isPendingStop = true;
        }
    }

    private void CalculateDirectionalVelocity()
    {
        Vector3 worldDir = transform.TransformDirection(new Vector3(velocityDirection.x, velocityDirection.y, 0f));
        Vector2 dir = new Vector2(worldDir.x, worldDir.y).normalized;

        if (velocityRandomness > 0)
        {
            float angle = Random.Range(-velocityRandomness, velocityRandomness) * Mathf.Deg2Rad;
            dir = new Vector2(
                dir.x * Mathf.Cos(angle) - dir.y * Mathf.Sin(angle),
                dir.x * Mathf.Sin(angle) + dir.y * Mathf.Cos(angle)
            );
        }
        CurrentVelocity = dir * velocityStrength;
    }

    private void ExecuteStopAction()
    {
        _isPendingStop = false;

        switch (stopAction)
        {
            case StopAction.Disable:

                gameObject.SetActive(false);
                break;

            case StopAction.Destroy:

                Destroy(gameObject);
                break;

            case StopAction.Callback:

                onStopped?.Invoke();
                break;

            case StopAction.None:
                break;

            default:
                break;
        }
    }
}