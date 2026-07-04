using UnityEngine;
using UnityEngine.SceneManagement;

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
            settingsPanel.SetActive(true);
            AudioManager.clickSound = true;
        }
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
}
