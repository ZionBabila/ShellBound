using UnityEngine;

public class CenterOfMassMarker : MonoBehaviour
{
    [Tooltip("גודל הגיזמו הירוק שמציין את מרכז המסה")]
    public float gizmoRadius = 0.08f;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
        Gizmos.DrawWireSphere(transform.position, gizmoRadius * 2f);
    }
}
