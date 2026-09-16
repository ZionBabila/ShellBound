using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DynamicFluidBoundary : MonoBehaviour
{
    [Header("Boundary Settings")]
    public FluidBoundarySampler.BoundaryType boundaryType = FluidBoundarySampler.BoundaryType.Solid;
    public float velocityInfluence = 0.005f;

    private Vector3 lastPosition;
    public Vector2 Velocity { get; private set; }
    [HideInInspector] public Bounds ColliderBounds;
    public Collider2D BoundaryCollider { get; private set; }

    void Awake()
    {
        BoundaryCollider = GetComponent<Collider2D>();
        lastPosition = transform.position;
    }

    void OnEnable()
    {
        var sampler = FindFirstObjectByType<FluidBoundarySampler>();
        if (sampler) sampler.RegisterDynamicBoundary(this);
    }

    void FixedUpdate()
    {
        Velocity = (transform.position - lastPosition) / Time.fixedDeltaTime;
        lastPosition = transform.position;
        ColliderBounds = BoundaryCollider.bounds;
    }
}