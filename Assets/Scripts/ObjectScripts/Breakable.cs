using UnityEngine;

public class Breakable : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite brokenSprite;

    private bool isBroken = false;
    private SpriteRenderer spriteRenderer;
    private Collider2D col;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // If the SpriteRenderer was removed in the editor, add it dynamically
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        col = GetComponent<Collider2D>();
    }

    // This function is called by ArmorShell or any other mechanism that can break objects
    public void Smash()
    {
        // Ensure the object isn't smashed twice in the same frame
        if (isBroken) return;
        isBroken = true;

        // Trigger the global break sound via our AudioManager property
        AudioManager.PlayBreakPlatform();

        // Swap the sprite to the broken version
        if (spriteRenderer != null && brokenSprite != null)
        {
            spriteRenderer.sprite = brokenSprite;
        }

        // Disable the collider so the player can pass through the debris
        if (col != null)
        {
            col.enabled = false;
        }

        // We don't call Destroy(gameObject) so the broken sprite remains in the scene
    }
}