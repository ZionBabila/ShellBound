using UnityEngine;

public class MusicZone : MonoBehaviour
{
    [Header("Zone Music Settings")]
    public AudioClip zoneMusic;

    [SerializeField] private bool stopMusicInThisZone = false;

    private AudioManager audioManager;

    void Start()
    {
        audioManager = FindFirstObjectByType<AudioManager>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (audioManager != null)
            {
                audioManager.ChangeBackgroundMusic(zoneMusic, stopMusicInThisZone);
            }
            else
            {
                Debug.LogError("MusicZone: Could not find AudioManager in the scene!");
            }
        }
    }
}