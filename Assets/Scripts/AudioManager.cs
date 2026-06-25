using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// How the music/ambient channel moves from the current clip to a new one.
/// </summary>
public enum MusicTransition
{
    Instant,    // Cut immediately to the new clip
    CrossFade,  // Fade the new clip in while fading the old one out (overlapping)
    FadeOutIn,  // Fade the old clip out completely, then fade the new one in
    FadeIn      // Cut the old clip and fade the new one in
}

/// <summary>
/// One configurable music zone. The first entry plays on game start; the rest
/// are triggered when the player enters their activation collider.
/// </summary>
[System.Serializable]
public class MusicZoneEntry
{
    public string name = "Zone";

    [Header("Music")]
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 1f;

    [Header("Ambient (optional)")]
    [Tooltip("If enabled, entering this zone also changes the ambient channel.")]
    public bool changeAmbient = false;
    public AudioClip ambient;
    [Range(0f, 1f)] public float ambientVolume = 1f;

    [Header("Transition")]
    public MusicTransition transition = MusicTransition.CrossFade;
    [Tooltip("Length of the fade/crossfade in seconds.")]
    public float fadeDuration = 1.5f;

    [Header("Activation")]
    [Tooltip("Trigger collider that switches to this zone. Leave empty for the start zone (index 0).")]
    public Collider2D activationCollider;
    public string playerTag = "Player";
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance; // Singleton instance

    private AudioSource source; // Plays one-shot sound effects

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
    public AudioClip buttonPressSoundSource;
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

    [Header("Music & Ambient")]
    [Range(0f, 1f)] public float musicMasterVolume = 1f;
    [Range(0f, 1f)] public float ambientMasterVolume = 1f;
    [Tooltip("Fade length used by the legacy ChangeBackgroundMusic API.")]
    public float defaultFadeDuration = 1.5f;

    [Tooltip("First entry plays on start. Other entries switch when the player enters their collider.")]
    public List<MusicZoneEntry> musicZones = new List<MusicZoneEntry>();

    // Each channel keeps two AudioSources so it can crossfade between clips.
    private class Channel
    {
        public AudioSource primary;
        public AudioSource secondary;
        public float masterVolume = 1f;  // Settings-controlled (0..1)
        public float trackVolume = 1f;   // Volume requested by the current zone (0..1)
        public Coroutine routine;
    }

    private Channel musicChannel;
    private Channel ambientChannel;
    private int currentZone = -1;

    void Start()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        // Set up the source for sound effects
        source = GetComponent<AudioSource>();
        mainCamera = Camera.main;
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        // Build the two persistent channels (two sources each for crossfading)
        musicChannel = CreateChannel(musicMasterVolume);
        ambientChannel = CreateChannel(ambientMasterVolume);

        // Wire up each zone's activation collider so it can trigger a switch
        WireMusicZones();

        // Reset all sound flags on start
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

        // Play the first zone automatically when the game starts
        if (musicZones.Count > 0)
            PlayZone(0);
    }

    void Update()
    {
        BreakObj();
        MoveObj();
        EquipShell();
        ThrowShell();
        CrabHurt();
        PlayerHazardHit();
        UiClick();
        WinLevel();
        LoseLevel();
        ButtonPress();
        ButtonRelease();
        AcidDropDestroy();
    }

    // ---------------------------------------------------------------------
    // Music & ambient channels
    // ---------------------------------------------------------------------

    private Channel CreateChannel(float masterVolume)
    {
        Channel ch = new Channel { masterVolume = Mathf.Clamp01(masterVolume) };
        ch.primary = NewLoopingSource();
        ch.secondary = NewLoopingSource();
        return ch;
    }

    private AudioSource NewLoopingSource()
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.loop = true;
        s.playOnAwake = false;
        s.volume = 0f;
        return s;
    }

    private void WireMusicZones()
    {
        for (int i = 0; i < musicZones.Count; i++)
        {
            MusicZoneEntry zone = musicZones[i];
            if (zone.activationCollider == null) continue;

            // Music zones rely on trigger callbacks
            if (!zone.activationCollider.isTrigger)
                zone.activationCollider.isTrigger = true;

            MusicZoneTrigger trigger = zone.activationCollider.gameObject.AddComponent<MusicZoneTrigger>();
            trigger.Setup(this, i, zone.playerTag);
        }
    }

    /// <summary>Switches both channels to the given zone (called by colliders and on start).</summary>
    public void PlayZone(int index)
    {
        if (index < 0 || index >= musicZones.Count) return;
        if (index == currentZone) return; // Already playing this zone
        currentZone = index;

        MusicZoneEntry zone = musicZones[index];
        RunChannel(musicChannel, zone.music, zone.musicVolume, zone.transition, zone.fadeDuration);
        if (zone.changeAmbient)
            RunChannel(ambientChannel, zone.ambient, zone.ambientVolume, zone.transition, zone.fadeDuration);
    }

    /// <summary>Plays a music clip directly from code.</summary>
    public void PlayMusic(AudioClip clip, MusicTransition transition, float fadeDuration, float volume = 1f)
    {
        RunChannel(musicChannel, clip, volume, transition, fadeDuration);
    }

    /// <summary>Plays an ambient clip directly from code.</summary>
    public void PlayAmbient(AudioClip clip, MusicTransition transition, float fadeDuration, float volume = 1f)
    {
        RunChannel(ambientChannel, clip, volume, transition, fadeDuration);
    }

    public void StopMusic(float fadeDuration)
    {
        RunChannel(musicChannel, null, 0f, MusicTransition.FadeOutIn, fadeDuration);
    }

    public void StopAmbient(float fadeDuration)
    {
        RunChannel(ambientChannel, null, 0f, MusicTransition.FadeOutIn, fadeDuration);
    }

    /// <summary>Sets the music master volume (e.g. from a settings menu) and applies it live.</summary>
    public void SetMusicVolume(float volume)
    {
        musicMasterVolume = Mathf.Clamp01(volume);
        ApplyMasterVolume(musicChannel, musicMasterVolume);
    }

    /// <summary>Sets the ambient master volume (e.g. from a settings menu) and applies it live.</summary>
    public void SetAmbientVolume(float volume)
    {
        ambientMasterVolume = Mathf.Clamp01(volume);
        ApplyMasterVolume(ambientChannel, ambientMasterVolume);
    }

    private void ApplyMasterVolume(Channel ch, float master)
    {
        ch.masterVolume = master;
        if (ch.primary.isPlaying && ch.routine == null)
            ch.primary.volume = ch.trackVolume * master;
    }

    // Legacy API kept for the old MusicZone.cs component
    public void ChangeBackgroundMusic(AudioClip newMusic, bool stopMusic)
    {
        if (stopMusic)
            StopMusic(defaultFadeDuration);
        else
            RunChannel(musicChannel, newMusic, 1f, MusicTransition.CrossFade, defaultFadeDuration);
    }

    private void RunChannel(Channel ch, AudioClip clip, float volume, MusicTransition transition, float duration)
    {
        if (ch.routine != null) StopCoroutine(ch.routine);
        ch.routine = StartCoroutine(TransitionChannel(ch, clip, volume, transition, duration));
    }

    private IEnumerator TransitionChannel(Channel ch, AudioClip clip, float volume, MusicTransition transition, float duration)
    {
        ch.trackVolume = Mathf.Clamp01(volume);
        float target = ch.trackVolume * ch.masterVolume;

        // If this clip is already playing, just adjust its volume instead of restarting it
        if (clip != null && ch.primary.clip == clip && ch.primary.isPlaying)
        {
            yield return FadeVolume(ch.primary, ch.primary.volume, target, duration);
            ch.routine = null;
            yield break;
        }

        switch (transition)
        {
            case MusicTransition.Instant:
                ch.secondary.Stop();
                ch.primary.Stop();
                ch.primary.clip = clip;
                ch.primary.volume = target;
                if (clip != null) ch.primary.Play();
                break;

            case MusicTransition.CrossFade:
            {
                AudioSource incoming = ch.secondary;
                AudioSource outgoing = ch.primary;

                incoming.clip = clip;
                incoming.volume = 0f;
                if (clip != null) incoming.Play();

                float t = 0f;
                float startOut = outgoing.volume;
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    float k = duration > 0f ? t / duration : 1f;
                    incoming.volume = Mathf.Lerp(0f, target, k);
                    outgoing.volume = Mathf.Lerp(startOut, 0f, k);
                    yield return null;
                }
                incoming.volume = target;
                outgoing.Stop();
                outgoing.volume = 0f;

                // Swap so 'primary' is always the live source
                ch.primary = incoming;
                ch.secondary = outgoing;
                break;
            }

            case MusicTransition.FadeOutIn:
                yield return FadeVolume(ch.primary, ch.primary.volume, 0f, duration);
                ch.primary.Stop();
                ch.primary.clip = clip;
                ch.primary.volume = 0f;
                if (clip != null) ch.primary.Play();
                yield return FadeVolume(ch.primary, 0f, target, duration);
                break;

            case MusicTransition.FadeIn:
                ch.secondary.Stop();
                ch.primary.Stop();
                ch.primary.clip = clip;
                ch.primary.volume = 0f;
                if (clip != null) ch.primary.Play();
                yield return FadeVolume(ch.primary, 0f, target, duration);
                break;
        }

        ch.routine = null;
    }

    private IEnumerator FadeVolume(AudioSource src, float from, float to, float duration)
    {
        if (src == null) yield break;
        if (duration <= 0f) { src.volume = to; yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        src.volume = to;
    }

    // ---------------------------------------------------------------------
    // Sound effects
    // ---------------------------------------------------------------------

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

    // Public static helper for acid drops to call
    public static void PlayAcidDropDestroySound(Vector3 position)
    {
        if (instance != null)
            instance.PlaySoundAtPosition(instance.acidDropDestroy, position);
    }

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
            source.PlayOneShot(buttonPressSoundSource);
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
