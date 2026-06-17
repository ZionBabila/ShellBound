using UnityEngine;

public class DoorController : MonoBehaviour
{
    public enum DoorMode { Slide, Rotate }

    [Header("General Settings")]
    public DoorMode mode = DoorMode.Slide;
    [Tooltip("How fast the door transitions between open and closed.")]
    public float transitionSpeed = 5f;

    [Header("Slide Settings")]
    [Tooltip("The local offset to ADD to the door's starting position.")]
    public Vector3 slideOffset = new Vector3(0, 2f, 0);

    [Header("Rotate Settings")]
    [Tooltip("The rotation offset to ADD to the door's starting rotation.")]
    public Vector3 rotationOffset = new Vector3(0, 0, 90f);

    private bool isOpen = false;

    private Vector3 closedLocalPosition;
    private Vector3 openLocalPosition;
    
    private Quaternion closedRotation;
    private Quaternion openRotation;

    private void Awake()
    {
        // Calculate open positions based on the initial transform in the scene
        closedLocalPosition = transform.localPosition;
        openLocalPosition = closedLocalPosition + slideOffset;

        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(rotationOffset);
    }

    private void Update()
    {
        // Smoothly transition between states based on the chosen mode
        if (mode == DoorMode.Slide)
        {
            Vector3 targetPos = isOpen ? openLocalPosition : closedLocalPosition;
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * transitionSpeed);
        }
        else if (mode == DoorMode.Rotate)
        {
            Quaternion targetRot = isOpen ? openRotation : closedRotation;
            transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRot, Time.deltaTime * transitionSpeed);
        }
    }

    // These functions can be called by UnityEvents (like from the PressureButton)
    public void OpenDoor()
    {
        isOpen = true;
    }

    public void CloseDoor()
    {
        isOpen = false;
    }

    private void OnDrawGizmos()
    {
        // Show a visual preview of where the door will slide to in the Unity Editor
        if (Application.isPlaying) return;

        Gizmos.color = Color.cyan;

        if (mode == DoorMode.Slide)
        {
            Vector3 targetPos = transform.parent != null 
                ? transform.parent.TransformPoint(transform.localPosition + slideOffset) 
                : transform.position + slideOffset;

            Gizmos.DrawLine(transform.position, targetPos);

            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                Gizmos.matrix = Matrix4x4.TRS(targetPos, transform.rotation, transform.lossyScale);
                Gizmos.color = new Color(0, 1, 1, 0.3f);
                Gizmos.DrawCube(col.offset, col.size);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(col.offset, col.size);
            }
            else
            {
                Gizmos.DrawWireSphere(targetPos, 0.5f);
            }
        }
        else if (mode == DoorMode.Rotate)
        {
            Quaternion targetLocalRot = transform.localRotation * Quaternion.Euler(rotationOffset);
            Quaternion targetWorldRot = transform.parent != null 
                ? transform.parent.rotation * targetLocalRot 
                : targetLocalRot;

            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, targetWorldRot, transform.lossyScale);
                Gizmos.color = new Color(0, 1, 1, 0.3f);
                Gizmos.DrawCube(col.offset, col.size);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(col.offset, col.size);
            }
            else
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, targetWorldRot, transform.lossyScale);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            }
        }
    }
}