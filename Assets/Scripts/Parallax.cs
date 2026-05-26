using UnityEngine;

public class Parallax : MonoBehaviour
{
    public Camera cam;
    [Tooltip("0 = Completely static (normal gameplay), 1 = Moves exactly with the camera (very distant background)")]
    public float parallaxEffect;

    private Vector2 startPosition;
    private Vector2 camStartPosition;

    void Start()
    {
        // Automatically find the main camera if none is assigned
        if (cam == null) cam = Camera.main;

        // Store the initial positions of the object and the camera
        startPosition = transform.position;
        camStartPosition = cam.transform.position;
    }

    void LateUpdate()
    {
        // Calculate how much the camera has moved since the start
        float distMovedX = (cam.transform.position.x - camStartPosition.x) * parallaxEffect;
        float distMovedY = (cam.transform.position.y - camStartPosition.y) * parallaxEffect; // Allows slight up/down movement if the camera moves vertically

        // Apply the calculated distance to the object's starting position
        transform.position = new Vector3(startPosition.x + distMovedX, startPosition.y + distMovedY, transform.position.z);
    }
}
