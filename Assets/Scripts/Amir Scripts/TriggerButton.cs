using UnityEngine;

public class TriggerButton : MonoBehaviour
{
    public enum MoveDirection { Down, Up, Left, Right }

    [Header("References")]
    [SerializeField] private Transform doorTransform;

    [Header("Movement Settings")]
    [SerializeField] private MoveDirection direction = MoveDirection.Down;
    [SerializeField] private float moveDistance = 3f;
    [SerializeField] private float moveSpeed = 4f;

    [Header("Button Behavior")]
    [Tooltip("If true, the button stays activated permanently after the first touch.")]
    [SerializeField] private bool stayPressed = false; // תסמן V ב-Inspector בשביל הכפתור הזה!

    private Vector3 initialPosition;
    private Vector3 targetPosition;
    private bool isPressed = false;

    void Start()
    {
        if (doorTransform != null)
        {
            initialPosition = doorTransform.position;
            Vector3 movementVector = Vector3.zero;

            switch (direction)
            {
                case MoveDirection.Down:
                    movementVector = new Vector3(0, -moveDistance, 0);
                    break;
                case MoveDirection.Up:
                    movementVector = new Vector3(0, moveDistance, 0);
                    break;
                case MoveDirection.Left:
                    movementVector = new Vector3(-moveDistance, 0, 0);
                    break;
                case MoveDirection.Right:
                    movementVector = new Vector3(moveDistance, 0, 0);
                    break;
            }

            targetPosition = initialPosition + movementVector;
        }
    }

    void Update()
    {
        if (doorTransform == null) return;

        Vector3 currentTarget = isPressed ? targetPosition : initialPosition;
        doorTransform.position = Vector3.MoveTowards(doorTransform.position, currentTarget, moveSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") || collision.GetComponent<Movable>() != null)
        {
            isPressed = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // הדלת תחזור למעלה רק אם הכפתור לא מוגדר כ-stayPressed
        if (!stayPressed)
        {
            if (collision.CompareTag("Player") || collision.GetComponent<Movable>() != null)
            {
                isPressed = false;
            }
        }
    }
}