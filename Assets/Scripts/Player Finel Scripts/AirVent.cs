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

    // Cached main camera, used to skip the sound when the vent is off-screen.
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;

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

            // Only play the sound when the vent is on-screen, so off-camera vents stay silent.
            if (IsVisibleToCamera()) AudioManager.airVentSound = true;

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

    // True if the vent's position is inside the main camera's view. Used to skip the
    // sound when the pipe is off-screen (the sound plays once at the start of each blast).
    private bool IsVisibleToCamera()
    {
        if (mainCamera == null) mainCamera = Camera.main; // Re-find if the camera changed (e.g. scene switch).
        if (mainCamera == null) return false;

        Vector3 vp = mainCamera.WorldToViewportPoint(transform.position);
        return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
    }
}
