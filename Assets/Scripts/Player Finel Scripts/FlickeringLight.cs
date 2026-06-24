using UnityEngine;
using UnityEngine.Rendering.Universal; // Required for Light2D

// =============================================================================
// FlickeringLight
// -----------------------------------------------------------------------------
// Role:    Creates a natural, lava-like flickering effect for a Light2D.
// Usage:   Attach this script to a GameObject that has a Light2D component.
//          It uses Perlin noise for a smooth, organic flicker.
// =============================================================================
[RequireComponent(typeof(Light2D))]
public class FlickeringLight : MonoBehaviour
{
    // Enum to define the different flicker behaviors
    public enum FlickerMode { Perlin, Blink }

    [Header("Light Component")]
    [Tooltip("The Light2D component to control. Will be found automatically if left empty.")]
    private Light2D lightSource;

    [Header("Control")]
    [Tooltip("The behavior of the flicker effect.")]
    public FlickerMode mode = FlickerMode.Perlin;
    [Tooltip("Is the flickering effect currently active?")]
    public bool isFlickering = true;

    [Header("Flicker Settings")]
    [Tooltip("The minimum light intensity during the flicker.")]
    [Range(0f, 5f)]
    public float minIntensity = 0.8f;
    [Tooltip("The maximum light intensity during the flicker.")]
    [Range(0f, 5f)]
    public float maxIntensity = 1.2f;

    [Tooltip("The minimum light radius during the flicker.")]
    [Range(0f, 10f)]
    public float minRadius = 3.0f;
    [Tooltip("The maximum light radius during the flicker.")]
    [Range(0f, 10f)]
    public float maxRadius = 3.5f;

    [Tooltip("How fast the light flickers. Higher values are faster.")]
    public float flickerSpeed = 5.0f;

    [Header("Blink Settings")]
    [Tooltip("How many times the light blinks on and off per second (only in Blink mode).")]
    public float blinkRate = 2.0f;

    // A random offset for the noise to make multiple lights flicker differently
    private float noiseOffset;

    // Store the original light settings to restore them when flicker is off
    private float originalIntensity;
    private float originalRadius;

    private void Awake()
    {
        lightSource = GetComponent<Light2D>();
        
        // Store the initial state of the light
        originalIntensity = lightSource.intensity;
        originalRadius = lightSource.pointLightOuterRadius;

        // Give each light a unique starting point in the noise pattern
        noiseOffset = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        if (isFlickering)
        {
            switch (mode)
            {
                case FlickerMode.Perlin:
                    // Use Perlin noise to create a smooth, natural-looking flicker (value between 0.0 and 1.0)
                    float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, noiseOffset);

                    // Map the noise value (0-1) to our desired intensity and radius ranges
                    lightSource.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
                    lightSource.pointLightOuterRadius = Mathf.Lerp(minRadius, maxRadius, noise);
                    break;

                case FlickerMode.Blink:
                    // Simple ON/OFF blinking based on a timer
                    // The expression creates a value that goes from 0 to 1 repeatedly.
                    bool lightIsOn = (Time.time * blinkRate) % 1.0f < 0.5f;
                    lightSource.intensity = lightIsOn ? maxIntensity : 0f;
                    lightSource.pointLightOuterRadius = lightIsOn ? maxRadius : 0f;
                    break;
            }
        }
        else
        {
            // When not flickering, smoothly return to the original steady state
            lightSource.intensity = Mathf.Lerp(lightSource.intensity, originalIntensity, Time.deltaTime * flickerSpeed);
            lightSource.pointLightOuterRadius = Mathf.Lerp(lightSource.pointLightOuterRadius, originalRadius, Time.deltaTime * flickerSpeed);
        }
    }

    // Public functions to be called from other scripts or Unity Events
    public void EnableFlicker()
    {
        isFlickering = true;
    }

    public void DisableFlicker()
    {
        isFlickering = false;
    }
}