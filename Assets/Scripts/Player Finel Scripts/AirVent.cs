using System.Collections;
using UnityEngine;

// =============================================================================
// AirVent — a pipe that periodically blasts air. On a repeating on/off cycle it
// pushes the player (via an AreaEffector2D collider), plays a particle burst,
// and a looping air sound. WIP: built step by step.
// =============================================================================
public class AirVent : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The particle system that shows the air blast.")]
    public ParticleSystem airParticles;

    [Tooltip("The trigger collider (Used By Effector) that pushes the player. Enabled only while blowing.")]
    public Collider2D pushZone;

   

    [Header("Timing")]
    [Tooltip("How long the air blows each cycle, in seconds.")]
    public float activeDuration = 2f;

    [Tooltip("Minimum pause between blasts, in seconds.")]
    public float inactiveDurationMin = 2f;

    [Tooltip("Maximum pause between blasts, in seconds. Each cycle picks a random pause in this range.")]
    public float inactiveDurationMax = 4f;

    [Tooltip("Initial delay before the first blast, so multiple vents can be offset from each other.")]
    public float startDelay = 0f;

    [Tooltip("Delay before the particles appear each blast, to line them up with the sound (which has a tiny lag). 0 = together.")]
    public float particleDelay = 0f;

    [Header("Sound Distance Fade")]
    [Tooltip("Peak volume of this vent's blast when the player is close (0 = silent, 1 = full).")]
    [Range(0f, 1f)]
    public float soundMaxVolume = 1f;

    [Tooltip("Within this distance from the player the blast is at full volume.")]
    [Range(0f, 50f)]
    public float soundFullVolumeDistance = 6f;

    [Tooltip("Beyond this distance the player no longer hears the blast. Between the two it fades gradually.")]
    [Range(0f, 100f)]
    public float soundSilenceDistance = 20f;

    // Cached player transform, used to fade the sound by distance.
    private Transform player;

    private void Start()
    {
        player = FindPlayer();

        // Start idle: no push, no particles, no sound. The cycle turns it on.
        StopBlow();
        StartCoroutine(VentCycle());
    }

    // The repeating blow/pause loop. Runs forever while the object is alive.
    private IEnumerator VentCycle()
    {
        // Optional offset so several vents don't all blow on the same beat.
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        while (true)
        {
            // Push + sound start now; the particles trail by particleDelay so they line up
            // with the sound (which lags slightly through the AudioManager flag).
            if (pushZone != null) pushZone.enabled = true;

            // Fade the blast sound by the player's distance: full volume up close,
            // fading out with distance, silent only once the player is really far.
            float volume = ComputeSoundVolume();
            if (volume > 0f)
            {
                AudioManager.airVentVolume = volume;
                AudioManager.airVentSound = true;
            }

            if (particleDelay > 0f) yield return new WaitForSeconds(particleDelay);
            if (airParticles != null) airParticles.Play();

            yield return new WaitForSeconds(activeDuration);

            StopBlow();

            // Wait a random pause before the next blast.
            float pause = Random.Range(inactiveDurationMin, inactiveDurationMax);
            yield return new WaitForSeconds(pause);
        }
    }

    // Turns the vent off: no push, and stop the particles (live ones fade out naturally).
    private void StopBlow()
    {
        if (pushZone != null) pushZone.enabled = false;
        if (airParticles != null) airParticles.Stop();
    }

    // Blast volume based on the player's distance: 1 within soundFullVolumeDistance,
    // fading to 0 at soundSilenceDistance (silent only when the player is really far).
    private float ComputeSoundVolume()
    {
        if (player == null) player = FindPlayer(); // Re-find if lost (e.g. respawn/scene switch).
        if (player == null) return 1f;             // No player found - don't silence the vent.

        float distance = Vector2.Distance(transform.position, player.position);
        float distanceFactor = Mathf.InverseLerp(soundSilenceDistance, soundFullVolumeDistance, distance);
        return distanceFactor * soundMaxVolume;
    }

    // Finds the active player in the scene (no tag needed).
    private Transform FindPlayer()
    {
        SimplePlayer simplePlayer = FindFirstObjectByType<SimplePlayer>();
        return simplePlayer != null ? simplePlayer.transform : null;
    }
}
