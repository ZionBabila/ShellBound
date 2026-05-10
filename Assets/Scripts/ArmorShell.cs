using UnityEngine;

public class ArmorShell : Shell
{
    [Header("Armor Physics Settings")]
    [Tooltip("How much mass to add to the player when equipped.")]
    public float extraMass = 2.0f; //
    
    [Tooltip("Multiplier for player speed while using armor. 0.5 = half speed.")]
    public float weightPenalty = 0.5f; //

    [Header("Armor Visuals")]
    [Tooltip("Sprite used when the crab is actively hiding inside the armor.")]
    public Sprite armorSpriteActive; //
    
    [Header("Crush Settings")]
    [Tooltip("If true, the armor can destroy objects when falling on them.")]
    public bool crushObjects = true; //
    
    [Tooltip("The Layer of objects that can be crushed.")]
    public LayerMask crushLayer; //

    private PlayerController playerInside;
    private float originalPlayerMass;

  public override void OnCollect(Transform parentTransform, Vector2 playerMountOffset)
    {
        base.OnCollect(parentTransform, playerMountOffset);
        
        // מציאת השחקן ביררכיה
        playerInside = parentTransform.GetComponentInParent<PlayerController>();
        
        if (playerInside != null)
        {
            Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
            
            // שמירת המסה המקורית כדי שנוכל להחזיר אותה אחר כך
            originalPlayerMass = playerRb.mass;
            
            // 1. הוספת משקל פיזי (מאפשר לדחוף חפצים כבדים יותר בעולם)
            playerRb.mass = originalPlayerMass + extraMass;
            
            // 2. הפעלת עונש המהירות! זה מה שישפיע על פונקציית התנועה של השחקן
            playerInside.speedMultiplier = weightPenalty;
            
            Debug.Log($"<color=blue>🛡 ARMOR EQUIPPED:</color> Mass changed to {playerRb.mass}, Speed multiplier is now {playerInside.speedMultiplier}");
        }
        else
        {
            Debug.LogError("ArmorShell could not find the PlayerController on the parent!");
        }
    }

    public override void OnActivate()
    {
        // Occurs when Space is pressed[cite: 1]
        if (currentState != ShellState.OnBack || playerInside == null) return;

        currentState = ShellState.InUse;
        
        // Change to the "Active/Hidden" sprite from the GDD[cite: 1]
        if (spriteRenderer != null && armorSpriteActive != null)
        {
            spriteRenderer.sprite = armorSpriteActive;
        }
        
        Debug.Log("<color=blue>🛡 ARMOR ACTIVE:</color> Crab is now protecting itself.");
    }

    public override void OnDeactivate()
    {
        // Exit protected mode[cite: 1]
        if (currentState != ShellState.InUse || playerInside == null) return;

        currentState = ShellState.OnBack;

        // Return to the standard 'OnBack' sprite[cite: 1]
        if (spriteRenderer != null && shellOnBackSprite != null)
        {
            spriteRenderer.sprite = shellOnBackSprite;
        }
    }
    
    public override void OnThrow(Vector2 throwVelocity)
    {
        // Reset player physics BEFORE the shell is detached[cite: 1]
        ResetPlayerPhysics();
        base.OnThrow(throwVelocity);
    }

    public override void OnDetach()
    {
        // Reset player physics if dropped[cite: 1]
        ResetPlayerPhysics();
        base.OnDetach();
    }

    private void ResetPlayerPhysics()
    {
        if (playerInside != null)
        {
            Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
            
            // Restore original mass and full speed multiplier[cite: 1]
            playerRb.mass = originalPlayerMass;
            playerInside.speedMultiplier = 1.0f;
            
            Debug.Log("<color=white>🛡 ARMOR REMOVED:</color> Player restored to normal weight and speed.");
        }
    }

    // Logic for crushing objects when falling (based on Design Doc)[cite: 1]
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState == ShellState.InUse && crushObjects)
        {
            // Check if object is in the designated crush layer[cite: 1]
            if (((1 << collision.gameObject.layer) & crushLayer) != 0)
            {
                foreach (ContactPoint2D contact in collision.contacts)
                {
                    // If the impact is from below (normal pointing up), crush it
                    if (contact.normal.y > 0.5f)
                    {
                        Destroy(collision.gameObject);
                        Debug.Log($"<color=red>💥 CRUSHED:</color> {collision.gameObject.name} was destroyed!");
                        break;
                    }
                }
            }
        }
    }
}