using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private AudioSource source;       // ישמש לאפקטים (SFX) כפי שהיה לך
    private AudioSource musicSource;  // ערוץ חדש ונפרד למוזיקת רקע בלופ

    //Sounds and Variables
    public AudioClip breakObject;
    public AudioClip moveObject;
    public AudioClip equipShell;
    public AudioClip throwShell;
    public AudioClip crabHurt;
    public AudioClip uiClick;
    public AudioClip winLevel;
    public AudioClip loseLevel;
    public AudioClip buttonPressSoundSorce;
    public AudioClip buttonReleaseSoundSource;

    public static bool breakSound = false;
    public static bool moveSound = false;
    public static bool equipShellSound = false;
    public static bool throwShellSound = false;
    public static bool crabHurtSound = false;
    public static bool uiClickSound = false;
    public static bool winLevelSound = false;
    public static bool loseLevelSound = false;
    public static bool buttonPressSound = false;
    public static bool buttonReleaseSound = false;

    void Start()
    {
        // הגדרת הערוץ לאפקטים
        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        // הגדרת הערוץ החדש למוזיקה
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true; // מוזיקת רקע תמיד בלופ

        // איפוס משתנים כפי שהיה לך
        breakSound = false;
        moveSound = false;
        equipShellSound = false;
        throwShellSound = false;
        crabHurtSound = false;
        uiClickSound = false;
        winLevelSound = false;
        loseLevelSound = false;
        buttonPressSound = false;
        buttonReleaseSound = false;
    }

    void Update()
    {
        BreakObj();
        MoveObj();
        EquipShell();
        ThrowShell();
        CrabHurt();
        UiClick();
        WinLevel();
        LoseLevel();
        ButtonPress();
        ButtonRelease();
    }

    // פונקציה פשוטה חדשה: החלפת מוזיקה או עצירה
    public void ChangeBackgroundMusic(AudioClip newMusic, bool stopMusic)
    {
        if (stopMusic)
        {
            musicSource.Stop();
            return;
        }

        // אם זה כבר השיר שמנגן עכשיו, אל תעשה כלום
        if (musicSource.clip == newMusic && musicSource.isPlaying)
        {
            return;
        }

        // החלפת השיר ונגינה
        musicSource.clip = newMusic;
        if (newMusic != null)
        {
            musicSource.Play();
        }
    }

    // שאר הפונקציות המקוריות שלך ללא שינוי:
    public void ButtonRelease()
    {
        if (buttonReleaseSound == true)
        {
            source.PlayOneShot(buttonReleaseSoundSource);
            buttonReleaseSound = false;
        }
    }
    public void ButtonPress()
    {
        if (buttonPressSound == true)
        {
            source.PlayOneShot(buttonPressSoundSorce);
            buttonPressSound = false;
        }
    }
    private void BreakObj()
    {
        if (breakSound == true)
        {
            source.PlayOneShot(breakObject);
            breakSound = false;
        }
    }
    private void MoveObj()
    {
        if (moveSound == true)
        {
            source.PlayOneShot(moveObject);
            moveSound = false;
        }
    }
    private void EquipShell()
    {
        if (equipShellSound == true)
        {
            source.PlayOneShot(equipShell);
            equipShellSound = false;
        }
    }
    private void ThrowShell()
    {
        if (throwShellSound == true)
        {
            source.PlayOneShot(throwShell);
            throwShellSound = false;
        }
    }
    private void CrabHurt()
    {
        if (crabHurtSound == true)
        {
            source.PlayOneShot(crabHurt);
            crabHurtSound = false;
        }
    }
    private void UiClick()
    {
        if (uiClickSound == true)
        {
            source.PlayOneShot(uiClick);
            uiClickSound = false;
        }
    }
    private void WinLevel()
    {
        if (winLevelSound == true)
        {
            source.PlayOneShot(winLevel);
            winLevelSound = false;
        }
    }
    private void LoseLevel()
    {
        if (loseLevelSound == true)
        {
            source.PlayOneShot(loseLevel);
            loseLevelSound = false;
        }
    }
}