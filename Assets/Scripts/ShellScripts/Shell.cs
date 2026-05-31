using System.Collections;
using UnityEngine;
using TMPro;

public enum ShellState
{
    OnGround, 
    OnBack,   
    InUse,    
    Thrown    
}

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public abstract class Shell : MonoBehaviour
{
    [Header("Base Shell Parameters")]
    [Tooltip("The 'Plug' point on this specific shell that snaps to the player's socket.")]
    public Vector2 anchorOffset = new Vector2(0, -0.2f);
    
    [Tooltip("The specific rotation (in degrees) for THIS shell when sitting on the crab's back.")]
    public float anchorRotation = 0f; // NEW: שליטה בזווית הקונכייה
    
    public float shellWeight = 1.0f;

    // מאפשר לקונכיות לשנות את נקודת העגינה בזמן אמת (למשל כשהסרטן מתחבא) בלי לדרוס את המשתנה המקורי
    public virtual Vector2 ActiveAnchorOffset => anchorOffset;

    // מאפשר לקונכיות להשפיע באופן דינמי על מהירות השחקן ללא קוד מסובך (1 = רגיל)
    public virtual float MovementSpeedMultiplier => 1.0f;
    
    [Header("Visual Settings")]
    public Sprite shellSprite;
    public Sprite shellOnBackSprite;

    [Header("Tutorial UI - Near Shell")]
    [Tooltip("Reference to the TextMeshPro UI element for the 'Near' message.")]
    public TMP_Text nearTextUI;

    [Header("Tutorial UI - Equip Shell")]
    [Tooltip("Reference to the TextMeshPro UI element for the 'Equip' message.")]
    public TMP_Text equipTextUI;

    [Header("Tutorial UI - General")]
    [Tooltip("How many times the ENTIRE sequence (Near -> Equip) should be shown. (-1 for infinite)")]
    public int maxSequenceShows = 1;

    [Tooltip("How long the message will stay on screen.")]
    public float tutorialDisplayTime = 4f;

    [Header("State")]
    [SerializeField] protected ShellState currentState = ShellState.OnGround;
    public ShellState CurrentState => currentState;

    protected Rigidbody2D rb;
    public Collider2D shellCollider;
    protected SpriteRenderer spriteRenderer;

    protected Vector3 originalScale;
    protected int originalLayer; // שומר את שכבת הפיזיקה המקורית
    protected PlayerController playerInside; // נשמר ברמת הבסיס כדי למנוע כפילויות בכל הקונכיות
    protected int sequenceCompletedCount = 0;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (shellCollider == null) shellCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb.mass = shellWeight;

        originalScale = transform.localScale;
        originalLayer = gameObject.layer;

        OnDetach(); 
    }

    public virtual void OnCollect(PlayerController player)
    {
        if (player == null) return;
        
        playerInside = player;

        // התעלמות מהתנגשות באופן בטוח רק כשהקונכייה נאספת בפועל
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider != null && shellCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, shellCollider, true);
        }

        currentState = ShellState.OnBack;

        // 1. כיבוי הפיזיקה לחלוטין! זה מה שימנע מהשחקן ליפול דרך הרצפה
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // 2. כיבוי הקוליידר של הקונכייה
        if (shellCollider != null)
        {
            shellCollider.enabled = false;
        }

        // 3. החלפת שכבה כדי שיוניטי יתעלם מהתנגשויות
        gameObject.layer = LayerMask.NameToLayer("ShellOnPlayer");

        // 4. חיבור פיזי לשחקן
        Transform attachParent = player.visualsRoot != null ? player.visualsRoot : player.transform;
        transform.SetParent(attachParent);
        UpdateAttachmentTransform(player);
        
        Debug.Log($"<color=white>🐚 SHELL COLLECTED:</color> Physics disabled, attached safely to {attachParent.name}.");

        if (maxSequenceShows == -1 || sequenceCompletedCount < maxSequenceShows)
        {
            ShowEquipTutorialText();
            sequenceCompletedCount++; // סופר את השלמת הסיקוונס רק אחרי שהקונכייה נאספה
        }
    }
    public void ApplyCompensatedScale(Transform newParent)
    {
        if (newParent == null) return;
        
        Vector3 parentScale = newParent.lossyScale;
        
        float scaleX = parentScale.x != 0 ? originalScale.x / Mathf.Abs(parentScale.x) : originalScale.x;
        float scaleY = parentScale.y != 0 ? originalScale.y / Mathf.Abs(parentScale.y) : originalScale.y;
        float scaleZ = parentScale.z != 0 ? originalScale.z / Mathf.Abs(parentScale.z) : originalScale.z;

        transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
    }

    // RENAMED AND UPDATED: Now handles both position and rotation
    public void UpdateAttachmentTransform(PlayerController player)
    {
        if (player == null) return;

        // מוצאים את המיקום המדויק של נקודת העגינה בעולם, בהתייחס לשחקן הראשי
        Vector3 worldMountPos = player.transform.TransformPoint(player.shellMountOffset);
        
        // ממירים את המיקום העולמי הזה למיקום הלוקאלי של האובייקט שאליו אנחנו מחוברים בפועל (למשל visualsRoot)
        Vector3 targetLocalPos = transform.parent != null ? transform.parent.InverseTransformPoint(worldMountPos) : worldMountPos;

        // Rotate the anchor offset by the shell's rotation to get the correct local position
        Vector2 rotatedAnchorOffset = (Vector2)(Quaternion.Euler(0, 0, anchorRotation) * ActiveAnchorOffset);
        transform.localPosition = targetLocalPos - (Vector3)rotatedAnchorOffset;
        transform.localRotation = Quaternion.Euler(0, 0, anchorRotation);
    }

    public abstract void OnActivate();
    public abstract void OnDeactivate();

    // פונקציה זו נקראת על ידי השחקן כאשר הוא מתנגש במשהו בעודו לובש את הקונכייה
    public virtual void OnPlayerCollisionEnter(Collision2D collision)
    {
    }

    public virtual void OnThrow(Vector2 throwVelocity)
    {
        currentState = ShellState.Thrown;
        playerInside = null;
        transform.SetParent(null);
        transform.localScale = originalScale; 
        gameObject.layer = originalLayer; // החזרת השכבה כדי שנוכל לאסוף אותה שוב

        rb.bodyType = RigidbodyType2D.Dynamic;
        shellCollider.enabled = true; 
        shellCollider.isTrigger = false; 

        if (spriteRenderer != null && shellSprite != null)
            spriteRenderer.sprite = shellSprite;

        rb.linearVelocity = throwVelocity;
        StartCoroutine(AutoLandRoutine());
    }

    private IEnumerator AutoLandRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        // זמן השהייה התחלתי כדי לתת לקונכייה לעוף מהשחקן
        yield return new WaitForSeconds(0.2f);

        float timeout = 2.0f; // מקסימום שתי שניות  המתנה לפני נחיתה מאולצת
        
        // מוודא שהקונכייה נוחתת גם אם הפיזיקה "רועדת" (נפוץ בחפצים כבדים כמו השריון)
        while (rb.linearVelocity.magnitude > 0.1f && !rb.IsSleeping() && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (currentState == ShellState.Thrown)
        {
            OnDetach();
        }
    }

    public virtual void OnDetach()
    {
        currentState = ShellState.OnGround;
        playerInside = null;
        transform.SetParent(null);
        transform.localScale = originalScale;
        gameObject.layer = originalLayer; // החזרת השכבה בעת ניתוק
        
        shellCollider.sharedMaterial = null;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic; 
        
        shellCollider.enabled = true;
        shellCollider.isTrigger = true; 

        if (spriteRenderer != null && shellSprite != null)
            spriteRenderer.sprite = shellSprite;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Vector3 anchorPos = transform.TransformPoint(ActiveAnchorOffset);
        Gizmos.DrawWireSphere(anchorPos, 0.1f);
        
        // Draw the gizmo lines with the specific rotation so you can see the tilt in the editor!
        Quaternion rotation = Quaternion.Euler(0, 0, anchorRotation);
        Vector3 up = rotation * Vector2.up * 0.2f;
        Vector3 right = rotation * Vector2.right * 0.2f;
        
        Gizmos.DrawLine(anchorPos - right, anchorPos + right);
        Gizmos.DrawLine(anchorPos - up, anchorPos + up);
    }

    // סנכרון תצוגה מדויק לכל הקונכיות - מונע רעידות ומבטיח שהקונכייה ממוקמת נכון תמיד
    protected virtual void LateUpdate()
    {
        if (currentState == ShellState.OnBack && playerInside != null)
        {
            UpdateAttachmentTransform(playerInside);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // מפעיל את הטקסט רק אם הקונכייה על הרצפה, השחקן נוגע בה, ועוד לא חרגנו מכמות הפעמים המותרת
        if (currentState == ShellState.OnGround && collision.CompareTag("Player"))
        {
            Debug.Log($"<color=yellow>TUTORIAL:</color> Player entered {gameObject.name} trigger.");
            if (maxSequenceShows == -1 || sequenceCompletedCount < maxSequenceShows)
            {
                ShowNearTutorialText();
            }
        }
    }

    public void ShowNearTutorialText()
    {
        // מכבה מיד את הודעת הלבישה (Equip) למקרה שהיא דלוקה
        HideEquipTutorialText();
        CancelInvoke(nameof(HideEquipTutorialText));

        if (nearTextUI != null)
        {
            Debug.Log($"<color=yellow>TUTORIAL:</color> Showing Near message for {gameObject.name}");
            nearTextUI.gameObject.SetActive(true); // מדליק את האובייקט גם אם הוא כבוי בקנבס
            
            CancelInvoke(nameof(HideNearTutorialText)); // מאפס את הטיימר אם נכנסנו שוב
            Invoke(nameof(HideNearTutorialText), tutorialDisplayTime);
        }
        else
        {
            Debug.LogWarning($"<color=red>TUTORIAL ERROR:</color> Cannot show 'Near' text on {gameObject.name}. Is nearTextUI assigned?");
        }
    }

    private void HideNearTutorialText()
    {
        if (nearTextUI != null)
        {
            nearTextUI.gameObject.SetActive(false);
        }
    }

    public void ShowEquipTutorialText()
    {
        // מכבה מיד את הודעת ההתקרבות (Near) כדי שלא יעלו אחת על השנייה
        HideNearTutorialText();
        CancelInvoke(nameof(HideNearTutorialText));

        if (equipTextUI != null)
        {
            Debug.Log($"<color=yellow>TUTORIAL:</color> Showing Equip message for {gameObject.name}");
            equipTextUI.gameObject.SetActive(true); // מדליק את האובייקט גם אם הוא כבוי בקנבס
            
            CancelInvoke(nameof(HideEquipTutorialText));
            Invoke(nameof(HideEquipTutorialText), tutorialDisplayTime);
        }
        else
        {
            Debug.LogWarning($"<color=red>TUTORIAL ERROR:</color> Cannot show 'Equip' text on {gameObject.name}. Is equipTextUI assigned?");
        }
    }

    private void HideEquipTutorialText()
    {
        if (equipTextUI != null)
        {
            equipTextUI.gameObject.SetActive(false);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (currentState == ShellState.OnBack && transform.parent != null)
        {
            PlayerController player = transform.parent.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                UpdateAttachmentTransform(player);
            }
        }
    }
#endif
}