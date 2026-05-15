using System.Collections;
using UnityEngine;

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
    
    [Header("Visual Settings")]
    public Sprite shellSprite;
    public Sprite shellOnBackSprite;

    [Header("State")]
    [SerializeField] protected ShellState currentState = ShellState.OnGround;
    public ShellState CurrentState => currentState;

    protected Rigidbody2D rb;
    public Collider2D shellCollider;
    protected SpriteRenderer spriteRenderer;

    protected Vector3 originalScale;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (shellCollider == null) shellCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb.mass = shellWeight;

        originalScale = transform.localScale;

        OnDetach(); 
    }

   public virtual void OnCollect(Transform parentTransform, Vector2 playerMountOffset)
    {
        // התעלמות מהתנגשות באופן בטוח רק כשהקונכייה נאספת בפועל
        Collider2D playerCollider = parentTransform.GetComponentInParent<Collider2D>();
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
        transform.SetParent(parentTransform);
        UpdateAttachmentTransform(playerMountOffset);
        
        Debug.Log($"<color=white>🐚 SHELL COLLECTED:</color> Physics disabled, attached safely to {parentTransform.name}.");
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
    public void UpdateAttachmentTransform(Vector2 playerMountOffset)
    {
        // Rotate the anchor offset by the shell's rotation to get the correct local position
        Vector2 rotatedAnchorOffset = (Vector2)(Quaternion.Euler(0, 0, anchorRotation) * anchorOffset);
        transform.localPosition = (Vector3)(playerMountOffset - rotatedAnchorOffset);
        transform.localRotation = Quaternion.Euler(0, 0, anchorRotation);
    }

    public abstract void OnActivate();
    public abstract void OnDeactivate();

    public virtual void OnThrow(Vector2 throwVelocity)
    {
        currentState = ShellState.Thrown;
        transform.SetParent(null);
        transform.localScale = originalScale; 

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

        while (rb.linearVelocity.magnitude > 0.1f)
        {
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
        transform.SetParent(null);
        transform.localScale = originalScale;
        
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
        Vector3 anchorPos = transform.TransformPoint(anchorOffset);
        Gizmos.DrawWireSphere(anchorPos, 0.1f);
        
        // Draw the gizmo lines with the specific rotation so you can see the tilt in the editor!
        Quaternion rotation = Quaternion.Euler(0, 0, anchorRotation);
        Vector3 up = rotation * Vector2.up * 0.2f;
        Vector3 right = rotation * Vector2.right * 0.2f;
        
        Gizmos.DrawLine(anchorPos - right, anchorPos + right);
        Gizmos.DrawLine(anchorPos - up, anchorPos + up);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (currentState == ShellState.OnBack && transform.parent != null)
        {
            PlayerController player = transform.parent.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                UpdateAttachmentTransform(player.shellMountOffset);
            }
        }
    }
#endif
}