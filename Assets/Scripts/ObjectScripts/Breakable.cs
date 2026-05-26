using UnityEngine;

public class Breakable : MonoBehaviour
{
    [Header("Break Effects")]
    [Tooltip("Sound to play when the object is broken.")]
    public AudioClip breakSound;

    [Tooltip("Visual effect or animation prefab to spawn when broken.")]
    public GameObject breakEffectPrefab;

    private bool isBroken = false;

    // פונקציה זו נקראת על ידי ArmorShell או כל מנגנון אחר ששובר את האובייקט
    public void Smash()
    {
        // מוודאים שלא שוברים את האובייקט פעמיים באותו פריים
        if (isBroken) return;
        isBroken = true;

        // הפעלת סאונד במיקום של האובייקט (מכיוון שהאובייקט עצמו יימחק מיד)
        if (breakSound != null)
        {
            AudioSource.PlayClipAtPoint(breakSound, transform.position);
        }

        // יצירת אפקט החלקיקים / אנימציית השבירה במקום שבו האובייקט היה
        if (breakEffectPrefab != null)
        {
            Instantiate(breakEffectPrefab, transform.position, Quaternion.identity);
        }

        // הריסת האובייקט השלם
        Destroy(gameObject);
    }
}