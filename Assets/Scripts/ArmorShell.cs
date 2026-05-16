using UnityEngine;

public class ArmorShell : Shell
{
    [Header("Armor Physics Settings")]
    [Tooltip("How much mass to add to the player when equipped.")]
    public float extraMass = 2.0f; 
    
    [Tooltip("Multiplier for player speed while using armor. 0.5 = half speed.")]
    public float weightPenalty = 0.5f; 

    [Tooltip("How much extra mass the player can push/pull when equipped.")]
    public float extraPushMass = 10.0f;

    [Header("Armor Visuals")]
    [Tooltip("Sprite used when the crab is actively hiding inside the armor.")]
    public Sprite armorSpriteActive; 
    
    [Header("Crush Settings")]
    [Tooltip("If true, the armor can destroy objects when falling on them.")]
    public bool crushObjects = true; 
    
    [Tooltip("The Layer of objects that can be crushed.")]
    public LayerMask crushLayer; 

    private float originalPlayerMass;

    public override void OnCollect(PlayerController player)
    {
        base.OnCollect(player);
        
        if (playerInside != null)
        {
            Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                // Store original mass to restore it later when detached
                originalPlayerMass = playerRb.mass;
                
                // 1. Apply physical mass increase for pushing heavy objects
                playerRb.mass = originalPlayerMass + extraMass;
            }

            // 2. Increase the mass limit the player can push/pull
            playerInside.currentMaxPushMass = playerInside.baseMaxPushMass + extraPushMass;

            // 3. Apply movement speed penalty immediately on collect
            playerInside.speedMultiplier = weightPenalty;
            
            Debug.Log($"<color=blue>🛡 ARMOR EQUIPPED:</color> Mass increased, Speed reduced to {weightPenalty * 100}%");
        }
    }

    public override void OnActivate()
    {
        // Occurs when the player uses the ability button (e.g., Space)
        if (currentState != ShellState.OnBack || playerInside == null) return;

        currentState = ShellState.InUse;
        
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
            Rigidbody2D playerRb = playerInside.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                // Restore original mass
                playerRb.mass = originalPlayerMass;
            }
            
            // Restore push limit
            playerInside.currentMaxPushMass = playerInside.baseMaxPushMass;

            // Restore full speed multiplier
            playerInside.speedMultiplier = 1.0f;
            
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