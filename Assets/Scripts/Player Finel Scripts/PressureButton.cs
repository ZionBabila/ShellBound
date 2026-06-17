using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class PressureButton : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("The moving part of the button (the plunger).")]
    public Transform plunger;
    
    [Tooltip("Local Y position of the plunger when NOT pressed.")]
    public float unpressedY = 0f;
    
    [Tooltip("Local Y position of the plunger when pressed down.")]
    public float pressedY = -0.2f;
    
    [Tooltip("How fast the plunger moves down/up.")]
    public float animationSpeed = 10f;

    [Header("Detection Settings")]
    [Tooltip("Layers that are allowed to press this button (e.g., Player, Movables).")]
    public LayerMask acceptedLayers;
    
    [Tooltip("Minimum TOTAL mass required to press the button.")]
    public float requiredMass = 1f;

    [Header("Events")]
    [Tooltip("Fired when the button is fully pressed by a valid object.")]
    public UnityEvent OnPressed;
    
    [Tooltip("Fired when all objects leave the button.")]
    public UnityEvent OnReleased;

#if UNITY_EDITOR
    [Header("Editor Preview")]
    [Tooltip("Check this to preview the pressed state in the Editor.")]
    public bool previewPressed = false;
#endif

    [Header("Debug")]
    [Tooltip("Print messages to the console to help setup the button.")]
    public bool showDebugLogs = true;

    // Keeps track of valid colliders and the mass they contribute
    private Dictionary<Collider2D, float> objectsOnButton = new Dictionary<Collider2D, float>();
    private bool isPressed = false;

    private void Update()
    {
        List<Collider2D> keysToRemove = new List<Collider2D>();
        float currentMass = 0f;

        // Clean up the list and calculate current mass
        foreach (var kvp in objectsOnButton)
        {
            if (kvp.Key == null || !kvp.Key.gameObject.activeInHierarchy)
            {
                keysToRemove.Add(kvp.Key);
            }
            else
            {
                currentMass += kvp.Value;
            }
        }
        
        foreach (var key in keysToRemove) objectsOnButton.Remove(key);

        bool shouldBePressed = currentMass >= requiredMass;

        if (shouldBePressed && !isPressed)
        {
            if (showDebugLogs) Debug.Log($"<color=green>[PressureButton]</color> Pressed! Current Mass: {currentMass} >= Required: {requiredMass}");
            isPressed = true;
            OnPressed?.Invoke();
        }
        else if (!shouldBePressed && isPressed)
        {
            if (showDebugLogs) Debug.Log($"<color=orange>[PressureButton]</color> Released! Current Mass: {currentMass} < Required: {requiredMass}");
            isPressed = false;
            OnReleased?.Invoke();
        }

        // Smoothly animate the plunger moving up or down
        if (plunger != null)
        {
            float targetY = isPressed ? pressedY : unpressedY;
            Vector3 localPos = plunger.localPosition;
            localPos.y = Mathf.Lerp(localPos.y, targetY, Time.deltaTime * animationSpeed);
            plunger.localPosition = localPos;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object's layer is included in the acceptedLayers LayerMask
        if (((1 << other.gameObject.layer) & acceptedLayers) != 0)
        {
            float massToAdd = 1f;
            Rigidbody2D rb = other.attachedRigidbody;
            if (rb != null) massToAdd = rb.mass;

            if (!objectsOnButton.ContainsKey(other))
            {
                objectsOnButton.Add(other, massToAdd);
                if (showDebugLogs) Debug.Log($"[PressureButton] Object entered: {other.name} | Mass added: {massToAdd}");
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (objectsOnButton.ContainsKey(other))
        {
            objectsOnButton.Remove(other);
            if (showDebugLogs) Debug.Log($"[PressureButton] Object exited: {other.name}");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Instantly preview the animation in the Scene view when toggling the boolean
        if (plunger != null && !Application.isPlaying)
        {
            Vector3 localPos = plunger.localPosition;
            localPos.y = previewPressed ? pressedY : unpressedY;
            plunger.localPosition = localPos;
        }
    }
#endif

    private void OnDrawGizmos()
    {
        // Show a visual preview of where the plunger will be when pressed
        if (plunger != null && !Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            
            Vector3 targetLocalPos = plunger.localPosition;
            targetLocalPos.y = pressedY;
            
            Vector3 targetWorldPos = plunger.parent != null 
                ? plunger.parent.TransformPoint(targetLocalPos) 
                : plunger.position + new Vector3(0, pressedY - unpressedY, 0);

            Gizmos.DrawLine(plunger.position, targetWorldPos);

            SpriteRenderer sr = plunger.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Gizmos.matrix = Matrix4x4.TRS(targetWorldPos, plunger.rotation, plunger.lossyScale);
                Gizmos.color = new Color(1, 0.9f, 0, 0.3f);
                Vector3 spriteSize = sr.sprite != null ? sr.sprite.bounds.size : new Vector3(1, 0.5f, 1);
                Gizmos.DrawCube(Vector3.zero, spriteSize);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(Vector3.zero, spriteSize);
            }
            else
            {
                Gizmos.DrawWireSphere(targetWorldPos, 0.2f);
            }
        }
    }
}