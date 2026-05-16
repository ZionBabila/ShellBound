using UnityEngine;

// =============================================================================
// Movable
// -----------------------------------------------------------------------------
// Role:        Marker component for objects the player can push and pull.
//              The player passes through Movables by default (configured in the
//              Physics2D layer collision matrix: Player <-> Movable = ignore).
//              When the player holds Ctrl near a Movable, PlayerController
//              attaches a FixedJoint2D so the item moves rigidly with the crab.
// Requires:    Rigidbody2D (Dynamic) and a Collider2D on the same GameObject.
// =============================================================================
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Movable : MonoBehaviour
{
    [Tooltip("If true, the object's layer is forced to 'Movable' in the editor for safety.")]
    public bool autoAssignLayer = true; // משתנה שקובע האם להקצות אוטומטית את השכבה "Movable" לאובייקט בעורך של יוניטי

    private Rigidbody2D rb; // משתנה לשמירת הרכיב הפיזיקלי של האובייקט
    private RigidbodyConstraints2D baseConstraints; // שומר את ההגדרות המקוריות של נעילות הפיזיקה (למשל אם האובייקט לא אמור להסתובב)
    private PlayerController player; // רפרנס שמצביע על השחקן שלנו כדי שנוכל לקרוא ממנו נתונים

    private void Awake()
    {
        // פונקציית Awake נקראת אוטומטית כשהמשחק מתחיל, ומשמשת להגדרות ראשוניות
        rb = GetComponent<Rigidbody2D>(); // לוקח את הרכיב הפיזיקלי מהאובייקט הנוכחי ושומר אותו במשתנה
        baseConstraints = rb.constraints; // שומר את נעילות הפיזיקה שכבר הוגדרו באינספקטור (כדי לא לדרוס אותן בטעות אחר כך)
        player = FindAnyObjectByType<PlayerController>(); // סורק את הסצנה, מוצא את השחקן ושומר גישה אליו
    }

    private void FixedUpdate()
    {
        // פונקציית FixedUpdate רצה בסנכרון עם מנוע הפיזיקה (לרוב 50 פעם בשנייה) ומושלמת לחישובים פיזיקליים
        if (rb == null || player == null) return; // בודק שלא חסרים רכיבים קריטיים. אם חסר משהו (למשל השחקן מת) - הפונקציה עוצרת כאן

        // בדיקה: האם האובייקט כבד מדי עבור השחקן במצבו הנוכחי?
        if (rb.mass > player.currentMaxPushMass) // השוואה בין המסה של הקופסה ליכולת הדחיפה של השחקן
        {
            // נועלים את ציר ה-X כדי שהשחקן לא יוכל לדחוף את זה סתם בהליכה לתוכו
            rb.constraints = baseConstraints | RigidbodyConstraints2D.FreezePositionX; // מוסיף לנעילות המקוריות גם נעילה של ציר התנועה האופקי (X)
        }
        else
        {
            // השחקן חזק מספיק (למשל לקח את השריון) - מחזירים את ההגדרות המקוריות
            rb.constraints = baseConstraints; // מסיר את נעילת ה-X ומחזיר את הפיזיקה למצב הרגיל שלה
        }
    }

    private void Reset()
    {
        // פונקציית Reset נקראת רק בעורך של יוניטי כשאתה מוסיף את הסקריפט או לוחץ על Reset בהגדרות שלו
        // Default the Rigidbody2D to Dynamic so push/pull works out of the box.
        Rigidbody2D currentRb = GetComponent<Rigidbody2D>(); // מחפש את הרכיב הפיזיקלי
        if (currentRb != null) currentRb.bodyType = RigidbodyType2D.Dynamic; // מוודא שהאובייקט מוגדר כ-Dynamic (מושפע ממשקל וכבידה)

        TryAssignMovableLayer(); // קורא לפונקציית העזר כדי לשים את הקופסה בשכבה הנכונה
    }

    private void OnValidate()
    {
        // פונקציית OnValidate מופעלת בעורך של יוניטי בכל פעם שאתה משנה ערך באינספקטור
        if (autoAssignLayer) TryAssignMovableLayer(); // מפעילה את החלפת השכבה כל עוד ה-V מסומן בהגדרות
    }

    private void TryAssignMovableLayer()
    {
        // פונקציית עזר פרטית שמטפלת בשכבה (Layer) של האובייקט
        int movableLayer = LayerMask.NameToLayer("Movable"); // מחפש מה המספר (ID) של השכבה שנקראת Movable
        if (movableLayer < 0) return; // אם שכבה כזו עדיין לא קיימת בפרויקט - תעצור פה כדי לא לקרוס
        if (gameObject.layer != movableLayer) gameObject.layer = movableLayer; // אם האובייקט עוד לא בשכבה הזו, תשנה את השכבה שלו אליה
    }
}
