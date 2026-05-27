using UnityEngine;

[DefaultExecutionOrder(1000)] // מבטיח שהסקריפט ירוץ תמיד אחרי העדכון של Cinemachine
public class Parallax : MonoBehaviour
{
    public Camera cam;
    [Tooltip("0 = Completely static (normal gameplay), 1 = Moves exactly with the camera (very distant background)")]
    public float parallaxEffect = 0.0f;

    private Vector2 startPosition;
    private Vector2 camStartPosition;

    void Start()
    {
        if (cam == null) cam = Camera.main;

        // הגנה מפני קריסה אם לא נמצאה מצלמה (כמו שהוזכר בסקירת הקוד)
        if (cam == null)
        {
            Debug.LogError("Parallax script on " + gameObject.name + " could not find a camera! Disabling script.");
            enabled = false;
            return;
        }

        startPosition = transform.position;
        camStartPosition = cam.transform.position;
    }

    void LateUpdate()
    {
        if (cam == null) return; // רשת ביטחון נוספת

        // Calculate how much the camera has moved since the start
        float distMovedX = (cam.transform.position.x - camStartPosition.x) * parallaxEffect;
        float distMovedY = (cam.transform.position.y - camStartPosition.y) * parallaxEffect;

        // Apply the calculated distance to the object's starting position
        transform.position = new Vector3(startPosition.x + distMovedX, startPosition.y + distMovedY, transform.position.z);
    }
}
