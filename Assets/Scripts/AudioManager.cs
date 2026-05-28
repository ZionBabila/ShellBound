using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    private AudioSource musicSource;
    private AudioSource ambientSource;

    [Header("Audio Clips")]
    public AudioClip breakPlatform;
    public AudioClip ouch;
    public AudioClip click;
    public AudioClip ghost;
    public AudioClip winLevel;
    public AudioClip loseLevel;
    public AudioClip hitFirst;
    public AudioClip hitSecond;
    public AudioClip hitLast;
    public AudioClip boo;
    public AudioClip bone1;
    public AudioClip bone2;
    public AudioClip bone3;
    public AudioClip bone4;

    private Coroutine fadeCoroutine;
    private float originalMusicVolume;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // ����� ��� ����� ������ ���� ��� ��������
        AudioSource[] sources = GetComponents<AudioSource>();

        if (sources.Length >= 2)
        {
            musicSource = sources[0];
            ambientSource = sources[1];
        }
        else
        {
            // ���� ����� ����� ������ ����� �-Inspector
            musicSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
            ambientSource = gameObject.AddComponent<AudioSource>();
        }

        originalMusicVolume = musicSource.volume;
    }

    // =================================================================
    // ����� ������� ��� (���� ���� ���� �� 4 �����)
    // =================================================================

    public static void PlayBackgroundMusic(AudioClip clip, bool loop = true)
    {
        if (instance == null || instance.musicSource == null || clip == null) return;

        // �� �� ���� ���� ��� ����, ����� ���� ������ �� �������
        if (instance.fadeCoroutine != null)
        {
            instance.StopCoroutine(instance.fadeCoroutine);
        }

        instance.musicSource.volume = instance.originalMusicVolume;

        if (instance.musicSource.clip == clip && instance.musicSource.isPlaying) return;

        instance.musicSource.clip = clip;
        instance.musicSource.loop = loop;
        instance.musicSource.Play();
    }

    /// <summary>
    /// ���� �� ������� ��������� (���� ����) ����� 4 �����
    /// </summary>
    public static void StopBackgroundMusic()
    {
        if (instance == null || instance.musicSource == null) return;

        if (instance.fadeCoroutine != null)
        {
            instance.StopCoroutine(instance.fadeCoroutine);
        }

        // ����� ����� ����� ���� �� 4 �����
        instance.fadeCoroutine = instance.StartCoroutine(instance.FadeOutMusic(4f));
    }
    /// <summary>
    /// ���� ��� ��� ������ ����� ���� ����� ������� �����
    /// </summary>
    public static bool IsPlaying(AudioClip clip)
    {
        if (instance == null || instance.musicSource == null) return false;
        return instance.musicSource.clip == clip && instance.musicSource.isPlaying;
    }
    private Coroutine ambientFadeCoroutine;

    // �������� ������� �� ������� ���� ����� ��� ������ �� ����
    private IEnumerator FadeOutMusic(float duration)
    {
        float startVolume = musicSource.volume;

        while (musicSource.volume > 0)
        {
            musicSource.volume -= startVolume * (Time.deltaTime / duration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = startVolume; // ���� �� ������� ���� ������ ����� ���� ����
    }

    // =================================================================
    // ����� ������ �������� (Ambient) ������ �������
    // =================================================================

    public static void PlayAmbient(AudioClip clip, bool loop = true)
    {
        if (instance == null || instance.ambientSource == null || clip == null) return;
        if (instance.ambientSource.clip == clip && instance.ambientSource.isPlaying) return;

        instance.ambientSource.clip = clip;
        instance.ambientSource.loop = loop;
        instance.ambientSource.Play();
    }

    public static void StopAmbient()
    {
        if (instance != null && instance.ambientSource != null)
        {
            instance.ambientSource.Stop();
        }
    }

    // =================================================================
    // ������ ������ (SFX) - ������� ������� ���� ����� �-PlayOneShot
    // =================================================================
    public static void PlayOuch() => PlaySFX(instance?.ouch);
    public static void PlayClick() => PlaySFX(instance?.click);
    public static void PlayGhost() => PlaySFX(instance?.ghost);
    public static void PlayWinLevel() => PlaySFX(instance?.winLevel);
    public static void PlayLoseLevel() => PlaySFX(instance?.loseLevel);
    public static void PlayHitFirst() => PlaySFX(instance?.hitFirst);
    public static void PlayHitSecond() => PlaySFX(instance?.hitSecond);
    public static void PlayHitLast() => PlaySFX(instance?.hitLast);
    public static void PlayBoo() => PlaySFX(instance?.boo);
    public static void PlayBone1() => PlaySFX(instance?.bone1);
    public static void PlayBone2() => PlaySFX(instance?.bone2);
    public static void PlayBone3() => PlaySFX(instance?.bone3);
    public static void PlayBone4() => PlaySFX(instance?.bone4);

    private static void PlaySFX(AudioClip clip)
    {
        if (instance != null && instance.musicSource != null && clip != null)
        {
            instance.musicSource.PlayOneShot(clip);
        }
    }
}