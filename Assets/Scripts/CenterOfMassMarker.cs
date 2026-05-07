using UnityEngine;

// =============================================================================
// CenterOfMassMarker — visual gizmo helper for center-of-mass tuning.
// -----------------------------------------------------------------------------
// Role:        Draws a green sphere at this Transform's position. Drag onto an
//              empty child of the player and assign that child to
//              PlayerController.centerOfMassPoint to define COM visually
//              instead of via numeric offset.
// =============================================================================
public class CenterOfMassMarker : MonoBehaviour
{
    [Tooltip("Radius of the green gizmo sphere marking the center of mass.")]
    public float gizmoRadius = 0.08f;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
        Gizmos.DrawWireSphere(transform.position, gizmoRadius * 2f);
    }
}
