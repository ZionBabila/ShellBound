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
    public float anchorRotation = 0f; // NEW: Control the shell's rotation
    
    public float shellWeight = 1.0f;

    // Allows shells to dynamically change the anchor point (e.g. when the crab hides) without overriding the original variable
    public virtual Vector2 ActiveAnchorOffset => anchorOffset;

    // Allows shells to dynamically affect player speed without complex code (1 = normal)
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
    protected int originalLayer; // Saves the original physics layer
    protected PlayerController playerInside; // Kept at base level to prevent duplication in all shells
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

        // Safely ignore collisions only when the shell is actually collected
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider != null && shellCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, shellCollider, true);
        }

        currentState = ShellState.OnBack;

        // 1. Completely disable physics! This prevents the player from falling through the floor
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // 2. Disable the shell's collider
        if (shellCollider != null)
        {
            shellCollider.enabled = false;
        }

        // 3. Change layer so Unity ignores collisions
        gameObject.layer = LayerMask.NameToLayer("ShellOnPlayer");

        // 4. Physically attach to the player
        Transform attachParent = player.visualsRoot != null ? player.visualsRoot : player.transform;
        transform.SetParent(attachParent);
        UpdateAttachmentTransform(player);
        
        Debug.Log($"<color=white>🐚 SHELL COLLECTED:</color> Physics disabled, attached safely to {attachParent.name}.");

        if (maxSequenceShows == -1 || sequenceCompletedCount < maxSequenceShows)
        {
            ShowEquipTutorialText();
            sequenceCompletedCount++; // Count sequence completion only after the shell is collected
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

        // Find the exact world position of the anchor point, relative to the main player
        Vector3 worldMountPos = player.transform.TransformPoint(player.shellMountOffset);
        
        // Convert this world position to the local position of the object we are actually attached to (e.g. visualsRoot)
        Vector3 targetLocalPos = transform.parent != null ? transform.parent.InverseTransformPoint(worldMountPos) : worldMountPos;

        // Rotate the anchor offset by the shell's rotation to get the correct local position
        Vector2 rotatedAnchorOffset = (Vector2)(Quaternion.Euler(0, 0, anchorRotation) * ActiveAnchorOffset);
        transform.localPosition = targetLocalPos - (Vector3)rotatedAnchorOffset;
        transform.localRotation = Quaternion.Euler(0, 0, anchorRotation);
    }

    public abstract void OnActivate();
    public abstract void OnDeactivate();

    // This function is called by the player when they collide with something while wearing the shell
    public virtual void OnPlayerCollisionEnter(Collision2D collision)
    {
    }

    public virtual void OnThrow(Vector2 throwVelocity)
    {
        currentState = ShellState.Thrown;
        playerInside = null;
        transform.SetParent(null);
        transform.localScale = originalScale; 
        gameObject.layer = originalLayer; // Restore layer so we can collect it again

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
        // Initial delay to let the shell fly away from the player
        yield return new WaitForSeconds(0.2f);

        float timeout = 2.0f; // Maximum two seconds wait before forced landing
        
        // Ensure the shell lands even if physics "jitters" (common with heavy objects like armor)
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
        gameObject.layer = originalLayer; // Restore layer upon detachment
        
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

    // Exact visual synchronization for all shells - prevents jitter and ensures correct positioning always
    protected virtual void LateUpdate()
    {
        if (currentState == ShellState.OnBack && playerInside != null)
        {
            UpdateAttachmentTransform(playerInside);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Activate text only if the shell is on the ground, player touches it, and we haven't exceeded allowed times
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
        // Immediately turn off the Equip message in case it's on
        HideEquipTutorialText();
        CancelInvoke(nameof(HideEquipTutorialText));

        if (nearTextUI != null)
        {
            Debug.Log($"<color=yellow>TUTORIAL:</color> Showing Near message for {gameObject.name}");
            nearTextUI.gameObject.SetActive(true); // Turn on object even if it's disabled in canvas
            
            CancelInvoke(nameof(HideNearTutorialText)); // Reset timer if we entered again
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
        // Immediately turn off the Near message so they don't overlap
        HideNearTutorialText();
        CancelInvoke(nameof(HideNearTutorialText));

        if (equipTextUI != null)
        {
            Debug.Log($"<color=yellow>TUTORIAL:</color> Showing Equip message for {gameObject.name}");
            equipTextUI.gameObject.SetActive(true); // Turn on object even if it's disabled in canvas
            
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