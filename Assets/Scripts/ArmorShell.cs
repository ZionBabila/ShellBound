using UnityEngine;

public class ArmorShell : Shell
{
    [Header("Armor Physics Settings")]
    [Tooltip("Speed multiplier: values under 1 slow the player down, over 1 speed them up.")]
    [Range(0.1f, 3.0f)]
    public float speedMultiplier = 0.5f; 

    [Header("Armor Visuals")]
    [Tooltip("Sprite used when the crab is actively hiding inside the armor.")]
    public Sprite armorSpriteActive; 
    
    [Header("Crush Settings")]
    [Tooltip("If true, the armor can destroy objects when falling on them.")]
    public bool crushObjects = true; 
    
    [Tooltip("The Layer of objects that can be crushed.")]
    public LayerMask crushLayer; 

    [Header("Hide Settings")]
    [Tooltip("The anchor offset when the crab is hiding inside. (Centers the shell on the crab)")]
    public Vector2 hidingAnchorOffset = new Vector2(0, 0f);

    // דורס את המהירות הבסיסית ומדווח על הקנס הנוכחי של השריון בזמן אמת
    public override float MovementSpeedMultiplier => speedMultiplier;

    // דורס את נקודת העגינה כדי להשתמש במיקום ההתחבאות כשלוחצים רווח
    public override Vector2 ActiveAnchorOffset => currentState == ShellState.InUse ? hidingAnchorOffset : anchorOffset;

    public override void OnCollect(PlayerController player)
    {
        base.OnCollect(player);
        
        if (playerInside != null)
        {
            // נותן לשחקן כוח דחיפה אינסופי לקוביות כבדות (במקום לחשב מסות מורכבות)
            playerInside.currentMaxPushMass = Mathf.Infinity;

            Debug.Log($"<color=blue>🛡 ARMOR EQUIPPED:</color> Can push heavy objects!");
        }
    }

    public override void OnActivate()
    {
        // Occurs when the player uses the ability button (e.g., Space)
        if (currentState != ShellState.OnBack || playerInside == null) return;

        currentState = ShellState.InUse;
        
        UpdateAttachmentTransform(playerInside);

        // Change to the "Active/Hidden" sprite as defined in the GDD
        if (spriteRenderer != null && armorSpriteActive != null)
        {
            spriteRenderer.sprite = armorSpriteActive;
        }
        
        Debug.Log("<color=blue>🛡 ARMOR ACTIVE:</color> Crab is now protecting itself and can crush objects.");
    }

    public override void OnDeactivate()
    {
        // Exit protected mode and return to carrying the shell
        if (currentState != ShellState.InUse || playerInside == null) return;

        currentState = ShellState.OnBack;

        UpdateAttachmentTransform(playerInside);

        // Return to the standard 'OnBack' sprite
        if (spriteRenderer != null && shellOnBackSprite != null)
        {
            spriteRenderer.sprite = shellOnBackSprite;
        }
    }
    
    public override void OnThrow(Vector2 throwVelocity)
    {
        // Reset player physics BEFORE the shell is physically detached and thrown
        ResetPlayerPhysics();
        base.OnThrow(throwVelocity);
    }

    public override void OnDetach()
    {
        // Reset player physics if the shell is dropped or forcefully removed
        ResetPlayerPhysics();
        base.OnDetach();
    }

    private void ResetPlayerPhysics()
    {
        if (playerInside != null)
        {
            // Restore push limit
            playerInside.currentMaxPushMass = playerInside.baseMaxPushMass;

            Debug.Log("<color=white>🛡 ARMOR REMOVED:</color> Player restored to normal weight and speed.");
            
        }
    }

    // Logic for crushing objects when falling (based on the Design Document)
    public override void OnPlayerCollisionEnter(Collision2D collision)
    {
        // Only crush if the crab is hiding inside the heavy armor
        if (currentState == ShellState.InUse && crushObjects)
        {
            // Check if the collided object is in the designated crush layer
            if (((1 << collision.gameObject.layer) & crushLayer) != 0)
            {
                foreach (ContactPoint2D contact in collision.contacts)
                {
                    // If the impact is from below (normal pointing up), it means we fell on it
                    if (contact.normal.y > 0.5f)
                    {
                        // מחפשים את סקריפט השבירה
                        Breakable breakableObj = collision.gameObject.GetComponent<Breakable>();
                        if (breakableObj != null)
                        {
                            breakableObj.Smash(); // הפעלת האנימציה והסאונד לפני ההריסה
                        }
                        else
                        {
                            Destroy(collision.gameObject); // גיבוי: הריסה רגילה
                        }
                        Debug.Log($"<color=red>💥 CRUSHED:</color> {collision.gameObject.name} was destroyed!");
                        break;
                    }
                }
            }
        }
    }
}