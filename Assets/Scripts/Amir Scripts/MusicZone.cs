using UnityEngine;

public class MusicZone : MonoBehaviour
{
    [Header("Zone Music Settings")]
    [Tooltip("The music track to play in this zone.")]
    public AudioClip zoneMusic;

    [Tooltip("Check this to stop the music when entering this zone (End Zone)")]
    public bool stopMusicInThisZone = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the player entered the zone
        if (other.CompareTag("Player"))
        {
            if (stopMusicInThisZone)
            {
                if (zoneMusic != null)
                {
                    // TODO: Logic for stopping background music goes here
                    Debug.Log($"[MusicZone] Player entered STOP zone for {zoneMusic.name}.");
                }
            }
            else if (zoneMusic != null)
            {
                // TODO: Logic for playing background music goes here
                Debug.Log($"[MusicZone] Player entered PLAY zone for {zoneMusic.name}.");
            }
        }
    }
}
