using UnityEngine;
using UnityEngine.U2D;

// A single point on the water surface. It behaves like a damped spring that
// returns to its rest height. WaterShapeController drives every spring and
// spreads the wave between neighbours; this component owns the physics state
// (height + velocity) and writes the result to the transform and the spline.
public class WaterSpring : MonoBehaviour
{
    // Simulation state. Public so WaterShapeController can spread the wave.
    public float velocity = 0f;
    public float height = 0f;

    // How hard an incoming object pushes the surface down. Higher = smaller splash.
    [SerializeField]
    private float impactResistance = 40f;

    private float targetHeight = 0f;   // rest height (local Y)
    private float baseX;               // fixed local X
    private float baseZ;               // fixed local Z
    private int waveIndex = 0;         // matching point on the spline
    private SpriteShapeController spriteShapeController;

    public void Init(SpriteShapeController ssc)
    {
        spriteShapeController = ssc;
        waveIndex = transform.GetSiblingIndex() + 1;

        Vector3 local = transform.localPosition;
        baseX = local.x;
        baseZ = local.z;
        height = local.y;
        targetHeight = local.y;
        velocity = 0f;
    }

    // Damped-spring step towards the rest height. Frame-based (no Time.deltaTime
    // term) to keep the original stiffness/dampening tuning.
    public void WaveSpringUpdate(float springStiffness, float dampening)
    {
        float x = height - targetHeight;
        float loss = -dampening * velocity;
        float force = -springStiffness * x + loss;

        velocity += force;
        height += velocity;
    }

    // Write the current height onto both the collider transform and the spline
    // point, so the visual surface and the physics collider stay together.
    public void WavePointUpdate()
    {
        transform.localPosition = new Vector3(baseX, height, baseZ);

        if (spriteShapeController != null)
        {
            Spline spline = spriteShapeController.spline;
            Vector3 point = spline.GetPosition(waveIndex);
            spline.SetPosition(waveIndex, new Vector3(point.x, height, point.z));
        }
    }

    // Something crossed the surface: convert its vertical speed into a ripple.
    // Trigger (not collision) so bodies pass through the surface into the water
    // instead of resting on top of it.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody != null)
        {
            velocity += other.attachedRigidbody.linearVelocity.y / impactResistance;
        }
    }
}
