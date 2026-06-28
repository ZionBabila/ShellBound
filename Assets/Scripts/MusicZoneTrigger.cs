using UnityEngine;

/// <summary>
/// Lightweight trigger helper that the AudioManager attaches at runtime to each
/// music-zone collider. When the player enters, it tells the AudioManager to
/// switch to the matching zone. You don't add this component manually — configure
/// the zones in the AudioManager's "Music Zones" list instead.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MusicZoneTrigger : MonoBehaviour
{
    private AudioManager manager;
    private int zoneIndex;
    private string playerTag = "Player";

    public void Setup(AudioManager manager, int zoneIndex, string playerTag)
    {
        this.manager = manager;
        this.zoneIndex = zoneIndex;
        this.playerTag = playerTag;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (manager != null && other.CompareTag(playerTag))
            manager.PlayZone(zoneIndex);
    }
}
