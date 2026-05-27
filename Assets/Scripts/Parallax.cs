using UnityEngine;

[DefaultExecutionOrder(1000)] // מבטיח שהסקריפט ירוץ תמיד אחרי העדכון של Cinemachine
public class Parallax : MonoBehaviour
{
    public Camera cam;

    [Header("Parallax Settings")]
    [Tooltip("Controls the horizontal (X) parallax. 0 = Static, 1 = Moves with cam")]
    public float parallaxEffect = 0.5f;
    
    [Tooltip("Controls the vertical (Y) parallax. 0 = Static, 1 = Moves with cam")]
    public float parallaxEffectY = 0.5f;

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
        float distMovedY = (cam.transform.position.y - camStartPosition.y) * parallaxEffectY;

        // Apply the calculated distance to the object's starting position
        transform.position = new Vector3(startPosition.x + distMovedX, startPosition.y + distMovedY, transform.position.z);
    }
}
