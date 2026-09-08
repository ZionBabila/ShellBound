using UnityEngine;
using UnityEngine.Rendering.Universal; // Required for Light2D

// =============================================================================
// MushroomBrush
// -----------------------------------------------------------------------------
// Role:    Attach to each mushroom's TIP BONE - the bone that carries the
//          Rigidbody2D and collider, held by its Anchor through a HingeJoint2D
//          (bone_4 / bone_8 / bone_12 on the MashrumsPng rig).
//          The bush layer is set to NOT collide with the Player, so the player
//          walks straight through it. This script gives that pass-through its
//          reaction: brushing past sweeps the mushroom aside in the direction
//          the player is moving, and fires that mushroom's own particle burst.
// Notes:   - Detection is a self-contained distance check with NO collider.
//            This is deliberate: unchecking a pair in the Layer Collision Matrix
//            also disables OnTriggerEnter2D between those layers, so a trigger
//            would silently never fire. Same pattern as TutorialHint.
//          - One instance per mushroom, each with its own ParticleSystem.
//          - The impulse goes to the tip bone, exactly where the player's
//            physical push used to land, so the existing swing feel is kept.
// =============================================================================
[RequireComponent(typeof(Rigidbody2D))]
public class MushroomBrush : MonoBehaviour
{
    [Header("References")]
    [Tooltip("This mushroom's own particle burst. Parent it under this bone so it " +
             "rides along with the cap, and turn off its 'Play On Awake'.")]
    public ParticleSystem hitParticles;

    [Tooltip("Optional Light2D that glows when this mushroom is brushed. Parent it under the bone " +
             "so it rides along with the cap.")]
    public Light2D glowLight;

    [Tooltip("How long the glow stays on after a brush, in seconds.")]
    public float glowDuration = 0.3f;

    [Header("Detection")]
    [Tooltip("Moves the detection circle away from the bone, in world axes. The tip bone sits up in " +
             "the cap while the player walks past below it, so a negative Y drops the circle down to " +
             "where the player actually brushes through. Shown by the gizmo.")]
    public Vector2 detectionOffset = Vector2.zero;

    [Tooltip("How close the player must get for this mushroom to be swept aside.")]
    public float brushRadius = 1.2f;

    [Tooltip("The player must move back out past this distance before the mushroom " +
             "can be swept again. Keeps it from re-firing on the radius edge.")]
    public float rearmDistance = 1.6f;

    [Tooltip("Below this player speed nothing happens, so standing in the bush is quiet.")]
    public float minPlayerSpeed = 0.5f;

    [Header("Sway")]
    [Tooltip("Base sideways impulse. Bone rigidbodies are light, so start small and tune up.")]
    public float swayForce = 1f;

    [Tooltip("Scale the push by how fast the player is moving: a stroll barely stirs it, a run sweeps it.")]
    public bool scaleWithSpeed = true;

    [Tooltip("Player speed that maps to the full swayForce.")]
    public float speedForFullForce = 6f;

    [Header("Debug")]
    [Tooltip("Traces detection to the console: player found, distance vs radius, speed vs gate, and " +
             "what happens on an actual brush. Turn off once it works.")]
    public bool showDebug = false;

    // Where the detection circle actually sits. Deliberately a world-axis offset and not a local
    // one: the bone swings on its hinge, and a local offset would drag the detection circle along
    // with every sway, moving the trigger zone around while the mushroom is still settling.
    private Vector2 DetectionCenter => (Vector2)transform.position + detectionOffset;

    private Rigidbody2D rb;
    private SimplePlayer player;
    private bool wasInside = false; // Edge detection: true while the player counts as inside.
    private float nextDebugTime = 0f; // Throttles the debug print so it does not flood the console.
    private float glowOffTime = 0f;   // When the glow should switch back off.

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        player = FindPlayer();
    }

    private void Update()
    {
        UpdateGlow();

        if (player == null)
        {
            player = FindPlayer(); // Re-find if lost (e.g. respawn/scene switch).
            if (player == null)
            {
                if (DebugDue()) Debug.LogWarning($"[MushroomBrush] {name}: no SimplePlayer found in the scene.");
                return;
            }
        }

        float distance = Vector2.Distance(DetectionCenter, player.transform.position);

        if (DebugDue())
        {
            Debug.Log($"[MushroomBrush] {name} | distance {distance:F2} (needs <= {brushRadius}) | " +
                      $"speed {player.CurrentSpeed:F2} (needs >= {minPlayerSpeed}) | wasInside {wasInside}");
        }

        if (!wasInside)
        {
            // Crossing in: sweep once, but only if the player is actually moving.
            if (distance <= brushRadius && player.CurrentSpeed >= minPlayerSpeed)
            {
                Brush();
                wasInside = true;
            }
        }
        else if (distance > rearmDistance)
        {
            wasInside = false; // Re-armed: the player left, it can be swept again.
        }
    }

    // Pushes the mushroom sideways and plays its burst.
    private void Brush()
    {
        // Direction comes from where the player is actually moving, so walking right
        // sweeps the mushroom right. Falls back to the player->mushroom direction if
        // the player is nearly still (e.g. drifting in on a slope).
        float dir = Mathf.Sign(player.Rb.linearVelocity.x);
        if (Mathf.Approximately(dir, 0f))
            dir = Mathf.Sign(DetectionCenter.x - player.transform.position.x);

        float force = swayForce;
        if (scaleWithSpeed)
            force *= Mathf.Clamp01(player.CurrentSpeed / speedForFullForce);

        rb.AddForce(Vector2.right * dir * force, ForceMode2D.Impulse);
        PlayImpact();
        StartGlow();

        if (showDebug)
        {
            // bodyType and simulated matter: AddForce silently does nothing on a Kinematic or
            // non-simulated body, and an Animator driving this bone would undo it every frame.
            Debug.Log($"[MushroomBrush] {name} BRUSHED | dir {dir} | force {force:F2} | " +
                      $"bodyType {rb.bodyType} | simulated {rb.simulated} | mass {rb.mass} | " +
                      $"velocity after {rb.linearVelocity} | particles " +
                      (hitParticles != null ? hitParticles.name : "NOT ASSIGNED"));
        }
    }

    // Anything that still collides physically (thrown shells, Movable boxes) also
    // sparks the burst. The physics engine already handles the push for those.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        PlayImpact();
    }

    // Lights the mushroom up. Brushing again while it is already lit just extends the glow.
    private void StartGlow()
    {
        if (glowLight == null) return;

        glowLight.enabled = true;
        glowOffTime = Time.time + glowDuration;
    }

    // Switches the glow back off once its time is up. Also covers a light left enabled in the
    // editor, which simply goes dark on the first frame.
    private void UpdateGlow()
    {
        if (glowLight == null || !glowLight.enabled) return;

        if (Time.time >= glowOffTime) glowLight.enabled = false;
    }

    private void PlayImpact()
    {
        if (hitParticles == null) return;

        // Play() on an already-playing system does not re-fire its Burst, so a second brush
        // inside the same Duration would show nothing. Stopping first restarts the burst;
        // the default Stop only halts emission, so particles already in the air still fade.
        hitParticles.Stop();
        hitParticles.Play();
    }

    // True at most twice a second, so the per-frame trace stays readable.
    private bool DebugDue()
    {
        if (!showDebug || Time.time < nextDebugTime) return false;
        nextDebugTime = Time.time + 0.5f;
        return true;
    }

    // Finds the active player in the scene (no tag needed), same as AirVent.
    private SimplePlayer FindPlayer()
    {
        return FindFirstObjectByType<SimplePlayer>();
    }

    // Shows the brush and re-arm radii in the Scene view for easy tuning.
    private void OnDrawGizmosSelected()
    {
        Vector3 center = DetectionCenter;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, brushRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, rearmDistance);

        // Thin line back to the bone, so it stays obvious how far the circle was moved.
        Gizmos.color = Color.gray;
        Gizmos.DrawLine(transform.position, center);
    }
}
