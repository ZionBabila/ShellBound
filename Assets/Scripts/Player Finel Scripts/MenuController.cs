using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the two menu types in the game:
///   1. Opening screen  -> New Game / Settings / Quit
///   2. Any other scene -> in-game pause panel: Continue / Settings / Quit
/// The same script is placed in every scene; each scene wires its own panels,
/// so the Settings button opens whatever settings panel belongs to that scene.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("Settings Panel (per scene)")]
    [Tooltip("The settings panel opened by the Settings button in this scene.")]
    public GameObject settingsPanel;

    [Tooltip("Optional. If assigned, these sliders sync to the saved volume when Settings opens.")]
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Opening Screen — New Game")]
    [Tooltip("Name of the first gameplay scene loaded by 'New Game'. Must be added to Build Settings.")]
    public string firstSceneName = "Tutorial";

    [Header("In-Game Pause Panel")]
    [Tooltip("The pause panel (Continue / Settings / Quit). Opening it freezes the game.")]
    public GameObject pausePanel;

    void Start()
    {
        // Panels start hidden; the scene always starts running.
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    // ------------------------------------------------------------------
    // Opening screen
    // ------------------------------------------------------------------

    // New Game — always loads the first gameplay scene.
    public void OnNewGameClick()
    {
        AudioManager.clickSound = true;
        Time.timeScale = 1f; // Safety: make sure we don't carry a paused state in.
        SceneManager.LoadScene(firstSceneName);
    }

    // ------------------------------------------------------------------
    // In-game pause panel
    // ------------------------------------------------------------------

    // Opens the pause panel and freezes the game. Hook this to the pause button.
    public void OnPauseClick()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
            Time.timeScale = 0f; // Put the game on hold while the panel is open.
            AudioManager.clickSound = true;
        }
    }

    // Continue — closes the pause panel and resumes the game.
    public void OnContinueClick()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        AudioManager.clickSound = true;
    }

    // Pause toggle — a single button that opens the panel if it's closed,
    // or resumes the game if it's already open.
    public void OnPauseToggle()
    {
        if (pausePanel == null) return;

        if (pausePanel.activeSelf)
            OnContinueClick(); // Already open -> resume
        else
            OnPauseClick();    // Closed -> open + freeze
    }

    // Restart — reloads the current scene from the start. Works in any scene
    // without hardcoding its name (unlike New Game, which loads firstSceneName).
    public void OnRestartClick()
    {
        AudioManager.clickSound = true;
        Time.timeScale = 1f; // In case restart is pressed while paused.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ------------------------------------------------------------------
    // Shared buttons (both menu types)
    // ------------------------------------------------------------------

    public void OnSettingsClick()
    {
        if (settingsPanel != null)
        {
            SyncSlidersToSavedVolumes();
            settingsPanel.SetActive(true);
            AudioManager.clickSound = true;
        }
    }

    // Sets each slider's handle to the current saved volume WITHOUT firing OnValueChanged
    // (so opening settings doesn't re-trigger a volume change).
    private void SyncSlidersToSavedVolumes()
    {
        if (AudioManager.instance == null) return;

        if (musicSlider != null) musicSlider.SetValueWithoutNotify(AudioManager.instance.musicMasterVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(AudioManager.instance.sfxMasterVolume);
    }

    public void OnCloseSettingsClick()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            AudioManager.clickSound = true;
        }
    }

    // Quit — closes the built application.
    public void OnQuitClick()
    {
        AudioManager.clickSound = true;
        Application.Quit();
#if UNITY_EDITOR
        // Application.Quit() does nothing in the editor, so stop play mode instead.
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ------------------------------------------------------------------
    // Settings sliders — wire each Slider's OnValueChanged (float) to these.
    // Both are 0..1 master volumes forwarded to the AudioManager.
    // ------------------------------------------------------------------

    // Music volume slider. Applies over the per-zone music volume, so it stays
    // in effect even when the collider-based MusicZone system switches tracks.
    public void SetMusicVolume(float value)
    {
        if (AudioManager.instance != null)
            AudioManager.instance.SetMusicVolume(value);
    }

    // Effects (SFX) volume slider — controls all one-shot sound effects.
    public void SetSfxVolume(float value)
    {
        if (AudioManager.instance != null)
            AudioManager.instance.SetSfxVolume(value);
    }
}
