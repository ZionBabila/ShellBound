using UnityEngine;

public class MusicZone : MonoBehaviour
{
    [Header("הגדרות מוזיקה לאזור זה")]
    [Tooltip("אם זה אזור התחלה: גרור לכאן את השיר שצריך להתחיל. אם זה אזור סיום: גרור לכאן את השיר שצריך להפסיק.")]
    public AudioClip zoneMusic;

    [Tooltip("סמן ב-V אך ורק אם מדובר באזור שאמור להפסיק מוזיקה ספציפית (End Zone)")]
    public bool stopMusicInThisZone = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // בדיקה שהאובייקט שנכנס הוא השחקן
        if (other.CompareTag("Player"))
        {
            if (stopMusicInThisZone)
            {
                // הגנה: מוודאים שהגדרת איזה שיר להפסיק במשבצת zoneMusic
                if (zoneMusic != null)
                {
                    // אנחנו שואלים את ה-AudioManager: האם השיר שרץ עכשיו הוא השיר של האזור הזה?
                    if (AudioManager.IsPlaying(zoneMusic))
                    {
                        // רק אם כן - מפעילים את הפייד-אאוט של ה-4 שניות
                        AudioManager.StopBackgroundMusic();
                        Debug.Log($"[MusicZone] המוזיקה {zoneMusic.name} הופסקה בהצלחה בפייד.");
                    }
                    else
                    {
                        // אם מתנגן שיר אחר, מתעלמים ולא הורסים את החוויה
                        Debug.Log($"[MusicZone] התעלמתי מהעצירה כי המוזיקה הנוכחית היא לא {zoneMusic.name}");
                    }
                }
            }
            else if (zoneMusic != null)
            {
                // אזור התחלה רגיל - מנגן את השיר בלופ
                AudioManager.PlayBackgroundMusic(zoneMusic, true);
            }
        }
    }
}