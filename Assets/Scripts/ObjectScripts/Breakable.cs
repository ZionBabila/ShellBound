using UnityEngine;

public class Breakable : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite unbrokenSprite;
    public Sprite brokenSprite;

    [Header("Break Effects")]
    [Tooltip("Visual effect or animation prefab to spawn when broken.")]
    public GameObject breakEffectPrefab;

    private bool isBroken = false;
    private SpriteRenderer spriteRenderer;
    private Collider2D col;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        // טעינת הספרייט השלם כבר בתחילת המשחק
        if (spriteRenderer != null && unbrokenSprite != null)
        {
            spriteRenderer.sprite = unbrokenSprite;
        }
    }

    // פונקציה זו נקראת על ידי ArmorShell או כל מנגנון אחר ששובר את האובייקט
    public void Smash()
    {
        // מוודאים שלא שוברים את האובייקט פעמיים באותו פריים
        if (isBroken) return;
        isBroken = true;

        // הפעלת סאונד השבירה דרך הטריגר שלנו ב-AudioManager
        AudioManager.breakPlatformSound = true;

        // החלפת הספרייט לגרסה השבורה
        if (spriteRenderer != null && brokenSprite != null)
        {
            spriteRenderer.sprite = brokenSprite;
        }

        // כיבוי הקוליידר כדי שהשחקן יוכל לעבור דרך השברים
        if (col != null)
        {
            col.enabled = false;
        }

        // יצירת אפקט החלקיקים / אנימציית השבירה במקום שבו האובייקט היה
        if (breakEffectPrefab != null)
        {
            Instantiate(breakEffectPrefab, transform.position, Quaternion.identity);
        }

        // הסרנו את הפקודה Destroy(gameObject) כדי שהספרייט השבור יישאר בסביבה
    }
}