using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class WallWalker : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float customGravityForce = 10f; // מחליף את הגרביטציה הרגילה

    [Header("Raycast Settings")]
    public float rayLength = 1.5f;
    public float rayOffset = 0.5f; // המרחק מהמרכז שממנו יורים את הקרניים (ימינה ושמאלה)
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private Vector2 currentNormal = Vector2.up; // מתחילים כשהרצפה רגילה

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // חובה לכבות את הגרביטציה הרגילה כדי שהשחקן לא ייפול מהתקרה!
        rb.gravityScale = 0f; 
    }

    void Update()
    {
        AlignToSurface();
    }

    void FixedUpdate()
    {
        MovePlayer();
        ApplyCustomGravity();
    }

    private void AlignToSurface()
    {
        // נקודות ההתחלה של הקרניים (ימין ושמאל ביחס לדמות)
        Vector2 leftOrigin = transform.position - transform.right * rayOffset;
        Vector2 rightOrigin = transform.position + transform.right * rayOffset;

        // כיוון הירי הוא הלמטה היחסי של הדמות
        Vector2 rayDirection = -transform.up;

        // ביצוע ה-Raycasts
        RaycastHit2D hitLeft = Physics2D.Raycast(leftOrigin, rayDirection, rayLength, groundLayer);
        RaycastHit2D hitRight = Physics2D.Raycast(rightOrigin, rayDirection, rayLength, groundLayer);

        // ציור הקרניים בדיבאג כדי שנוכל לראות אותן בעורך (אדום וצהוב כמו בתיאור שלך)
        Debug.DrawRay(leftOrigin, rayDirection * rayLength, Color.red);
        Debug.DrawRay(rightOrigin, rayDirection * rayLength, Color.yellow);

        Vector2 averageNormal = Vector2.zero;
        int hitCount = 0;

        if (hitLeft.collider != null)
        {
            averageNormal += hitLeft.normal;
            hitCount++;
        }
        if (hitRight.collider != null)
        {
            averageNormal += hitRight.normal;
            hitCount++;
        }

        if (hitCount > 0)
        {
            // ממוצע של הנורמלים
            averageNormal = (averageNormal / hitCount).normalized;
            currentNormal = averageNormal;
        }

        // חישוב הסיבוב הרצוי כדי שהראש תמיד יצביע בכיוון הנורמל הממוצע
        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, currentNormal);

        // סלרפ (Slerp) מחליק את התנועה כדי למנוע רעידות ולגרום למעבר טבעי בפינות
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    private void MovePlayer()
    {
        // קבלת קלט ימינה ושמאלה מהשחקן
        float horizontalInput = Input.GetAxisRaw("Horizontal");

        // תנועה ימינה ושמאלה יחסית לזווית הנוכחית של השחקן
        Vector2 targetVelocity = transform.right * horizontalInput * moveSpeed;
        
        // אנחנו רוצים לשמור על מהירות הנפילה/ההדבקות לקיר, ולכן נדרוס רק את ציר התנועה ה"אופקי" היחסי
        rb.linearVelocity = targetVelocity; 
    }

    private void ApplyCustomGravity()
    {
        // הפעלת כוח רציף הדוחף את השחקן אל עבר המשטח (בכיוון ההפוך מהנורמל)
        // זה מה שמדביק אותו לקיר או לתקרה
        rb.AddForce(-currentNormal * customGravityForce);
    }
}