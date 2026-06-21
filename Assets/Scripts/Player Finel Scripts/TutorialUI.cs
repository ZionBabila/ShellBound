using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// TutorialUI
// -----------------------------------------------------------------------------
// Role:    A single on-screen tutorial display: a fixed canvas Image plus two
//          independent TextMeshPro windows.
//          - PRIMARY  is driven by TutorialHint (proximity to a world element).
//          - SECONDARY is driven by the shell system (only while a shell is worn).
//          Place ONE of these in the scene.
// Notes:   - The Image is fixed in the canvas; it is visible while any window is.
//          - Each window has its own "source guard" so one caller never clears
//            another caller's text.
// =============================================================================
public class TutorialUI : MonoBehaviour
{
    // Singleton for easy access from anywhere
    public static TutorialUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("The fixed UI Image in the canvas. Visible while any window is showing.")]
    public Image hintImage;

    [Tooltip("Primary text window. Driven by proximity (e.g. 'Press E to wear').")]
    public TextMeshProUGUI primaryText;

    [Tooltip("Secondary text window. Driven by the worn shell (e.g. 'SPACE to roll').")]
    public TextMeshProUGUI secondaryText;

    private Object primarySource;
    private Object secondarySource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (primaryText != null) primaryText.gameObject.SetActive(false);
        if (secondaryText != null) secondaryText.gameObject.SetActive(false);
        UpdateImage();
    }

    // --- Primary window (proximity hints) ---
    public void ShowPrimary(string text, Object source)
    {
        primarySource = source;
        if (primaryText != null)
        {
            primaryText.text = text;
            primaryText.gameObject.SetActive(true);
        }
        UpdateImage();
    }

    public void HidePrimary(Object source)
    {
        if (source != primarySource) return;
        primarySource = null;
        if (primaryText != null) primaryText.gameObject.SetActive(false);
        UpdateImage();
    }

    // --- Secondary window (worn shell) ---
    public void ShowSecondary(string text, Object source)
    {
        // Nothing to show if this element has no second line
        if (string.IsNullOrEmpty(text)) return;

        secondarySource = source;
        if (secondaryText != null)
        {
            secondaryText.text = text;
            secondaryText.gameObject.SetActive(true);
        }
        UpdateImage();
    }

    public void HideSecondary(Object source)
    {
        if (source != secondarySource) return;
        secondarySource = null;
        if (secondaryText != null) secondaryText.gameObject.SetActive(false);
        UpdateImage();
    }

    // The image is visible whenever at least one text window is showing
    private void UpdateImage()
    {
        bool anyVisible =
            (primaryText != null && primaryText.gameObject.activeSelf) ||
            (secondaryText != null && secondaryText.gameObject.activeSelf);

        if (hintImage != null) hintImage.gameObject.SetActive(anyVisible);
    }
}
