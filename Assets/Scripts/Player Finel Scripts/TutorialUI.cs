using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// TutorialUI
// -----------------------------------------------------------------------------
// Role:    A single on-screen tutorial display: one fixed canvas Image plus a
//          single TextMeshPro window shared by all hint sources.
//          - TutorialHint drives it from world proximity (e.g. 'Press E to wear').
//          - PlayerShellSystem drives it while a shell is worn (e.g. 'SPACE to roll').
//          Place ONE of these in the scene.
// Notes:   - The Image is visible while the text window is showing.
//          - A "source guard" makes sure one caller never clears another caller's
//            text: Hide only clears the window if the caller currently owns it.
// =============================================================================
public class TutorialUI : MonoBehaviour
{
    // Singleton for easy access from anywhere
    public static TutorialUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("The fixed UI Image in the canvas. Visible while the text window is showing.")]
    public Image hintImage;

    [Tooltip("The single tutorial text window.")]
    public TextMeshProUGUI hintText;

    // Who currently owns the window, so a different caller can't clear it.
    private Object currentSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DEBUG: which object won the singleton, and does it have the text wired?
            Debug.Log($"[TutorialUI] Instance set to '{name}'. hintText assigned: {hintText != null}", this);
        }
        else
        {
            // DEBUG: a duplicate TutorialUI is being destroyed - if this one had the text, that's the bug.
            Debug.LogWarning($"[TutorialUI] Duplicate on '{name}' destroyed (hintText assigned: {hintText != null}). " +
                             $"Active instance is '{Instance.name}'.", this);
            Destroy(gameObject);
            return;
        }

        if (hintText != null) hintText.gameObject.SetActive(false);
        UpdateImage();
    }

    // Show a message in the single window. The caller becomes the current owner.
    public void Show(string text, Object source)
    {
        // Nothing to show for an empty line
        if (string.IsNullOrEmpty(text)) return;

        // DEBUG: reports the incoming request and whether the text field is wired.
        Debug.Log($"[TutorialUI] Show requested: \"{text}\" | hintText assigned: {hintText != null}", this);

        currentSource = source;
        if (hintText != null)
        {
            hintText.text = text;
            hintText.gameObject.SetActive(true);

            // DEBUG: dump the visual state so we can see why an assigned text may not render.
            Debug.Log($"[TutorialUI] hintText state -> activeInHierarchy: {hintText.gameObject.activeInHierarchy}, " +
                      $"enabled: {hintText.enabled}, alpha: {hintText.color.a}, fontSize: {hintText.fontSize}, " +
                      $"font: {(hintText.font != null ? hintText.font.name : "NULL")}, " +
                      $"canvas: {(hintText.canvas != null ? hintText.canvas.name : "NULL")}", hintText);
        }
        UpdateImage();
    }

    // Hide the window, but only if this caller is the one that showed it.
    public void Hide(Object source)
    {
        if (source != currentSource) return;
        currentSource = null;
        if (hintText != null) hintText.gameObject.SetActive(false);
        UpdateImage();
    }

    // The image is visible whenever the text window is showing
    private void UpdateImage()
    {
        bool visible = hintText != null && hintText.gameObject.activeSelf;
        if (hintImage != null) hintImage.gameObject.SetActive(visible);
    }
}
