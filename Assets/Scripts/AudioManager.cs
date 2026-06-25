using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance; // Singleton instance

    private AudioSource source;

    [Header("Sound Clips")]
    public AudioClip breakObject;
    public AudioClip moveObject;
    public AudioClip equipShell;
    public AudioClip throwShell;
    public AudioClip crabHurt;
    public AudioClip playerHazardHit;
    public AudioClip acidDropDestroy;
    public AudioClip uiClick;
    public AudioClip winLevel;
    public AudioClip loseLevel;
    public AudioClip buttonPressSoundSorce;
    public AudioClip buttonReleaseSoundSource;

    [Header("Sound Flags (Legacy)")]
    public static bool breakSound = false;
    public static bool moveSound = false;
    public static bool equipShellSound = false;
    public static bool throwShellSound = false;
    public static bool crabHurtSound = false;
    public static bool playerHazardHitSound = false;
    public static bool acidDropDestroySound = false;
    public static bool uiClickSound = false;
    public static bool winLevelSound = false;
    public static bool loseLevelSound = false;
    public static bool buttonPressSound = false;
    public static bool buttonReleaseSound = false;
    
    private Camera mainCamera;

    void Start()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        source = GetComponent<AudioSource>();
        mainCamera = Camera.main;
        
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        breakSound = false;
        moveSound = false;
        equipShellSound = false;
        throwShellSound = false;
        crabHurtSound = false;
        playerHazardHitSound = false;
        acidDropDestroySound = false;
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
        CrabHurt(); // This and below are still using flags
        PlayerHazardHit(); 
        UiClick();
        WinLevel();
        LoseLevel();
        ButtonPress();
        ButtonRelease();
        AcidDropDestroy(); // Added the missing call
    }

    /// <summary>
    /// Plays a sound only if the given position is visible to the main camera.
    /// </summary>
    /// <param name="clip">The AudioClip to play.</param>
    /// <param name="position">The world position of the sound event.</param>
    public void PlaySoundAtPosition(AudioClip clip, Vector3 position)
    {
        if (clip == null || mainCamera == null) return;

        // Check if the sound's origin is within the camera's view
        Vector3 viewportPoint = mainCamera.WorldToViewportPoint(position);
        bool isVisible = viewportPoint.x >= 0 && viewportPoint.x <= 1 && viewportPoint.y >= 0 && viewportPoint.y <= 1 && viewportPoint.z > 0;

        if (isVisible)
        {
            source.PlayOneShot(clip);
        }
    }

    // New public static method for acid drops to call
    public static void PlayAcidDropDestroySound(Vector3 position)
    {
        if (instance != null)
            instance.PlaySoundAtPosition(instance.acidDropDestroy, position);
    }
    public  void ButtonRelease()
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

    private void PlayerHazardHit()
    {
        if (playerHazardHitSound == true)
        {
            source.PlayOneShot(playerHazardHit);
            playerHazardHitSound = false;
        }
    }

    private void AcidDropDestroy()
    {
        if (acidDropDestroySound)
        {
            source.PlayOneShot(acidDropDestroy);
            acidDropDestroySound = false;
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
