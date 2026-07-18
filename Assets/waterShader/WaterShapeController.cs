using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

// Builds and drives a 2D water surface on top of a SpriteShape. In the editor it
// regenerates evenly spaced wave points along the spline; at runtime it runs each
// point as a damped spring and spreads the motion sideways so splashes ripple out.
[ExecuteAlways]
public class WaterShapeController : MonoBehaviour
{
    private int cornersCount = 2;

    [SerializeField]
    private SpriteShapeController spriteShapeController;
    [SerializeField]
    private GameObject wavePointPref;
    [SerializeField]
    private GameObject wavePoints;

    [SerializeField]
    [Range(1, 100)]
    private int WavesCount;

    private List<WaterSpring> springs = new();

    // How stiff the spring is (pull back to rest height).
    public float springStiffness = 0.1f;
    // Slows the movement over time.
    public float dampening = 0.03f;
    // How much motion spreads to the neighbouring springs each pass.
    public float spread = 0.006f;

    void Start()
    {
        // Collect the wave points that were generated in the editor and baked into
        // the scene, so the simulation has its springs to drive at runtime.
        CollectSprings();
    }

    private void CollectSprings()
    {
        springs.Clear();
        if (wavePoints == null)
        {
            return;
        }

        foreach (Transform child in wavePoints.transform)
        {
            if (child.TryGetComponent(out WaterSpring spring))
            {
                spring.Init(spriteShapeController);
                springs.Add(spring);
            }
        }
    }

    void FixedUpdate()
    {
        // Only animate the water while playing; in the editor we just generate it.
        if (!Application.isPlaying || springs.Count == 0)
        {
            return;
        }

        foreach (WaterSpring spring in springs)
        {
            spring.WaveSpringUpdate(springStiffness, dampening);
        }

        UpdateSprings();

        foreach (WaterSpring spring in springs)
        {
            spring.WavePointUpdate();
        }
    }

    // Spreads each spring's motion into its neighbours (both velocity and height),
    // so a single splash travels along the surface as a wave.
    private void UpdateSprings()
    {
        int count = springs.Count;
        float[] leftDeltas = new float[count];
        float[] rightDeltas = new float[count];

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                leftDeltas[i] = spread * (springs[i].height - springs[i - 1].height);
                springs[i - 1].velocity += leftDeltas[i];
            }
            if (i < count - 1)
            {
                rightDeltas[i] = spread * (springs[i].height - springs[i + 1].height);
                springs[i + 1].velocity += rightDeltas[i];
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                springs[i - 1].height += leftDeltas[i];
            }
            if (i < count - 1)
            {
                springs[i + 1].height += rightDeltas[i];
            }
        }
    }

    // Kicks a single spring downwards - useful to trigger a splash from other systems.
    public void Splash(int index, float speed)
    {
        if (index >= 0 && index < springs.Count)
        {
            springs[index].velocity += speed;
        }
    }

    private void SetWaves()
    {
        Spline waterSpline = spriteShapeController.spline;
        int waterPointsCount = waterSpline.GetPointCount();

        // Remove the middle points, keeping only the corners. Removing index
        // 'cornersCount' repeatedly works because each removal shifts the rest down.
        for (int i = cornersCount; i < waterPointsCount - cornersCount; i++)
        {
            waterSpline.RemovePointAt(cornersCount);
        }

        Vector3 waterTopLeftCorner = waterSpline.GetPosition(1);
        Vector3 waterTopRightCorner = waterSpline.GetPosition(2);
        float waterWidth = waterTopRightCorner.x - waterTopLeftCorner.x;

        float spacingPerWave = waterWidth / (WavesCount + 1);

        // Insert the evenly spaced wave points between the two top corners.
        for (int i = WavesCount; i > 0; i--)
        {
            int index = cornersCount;

            float xPosition = waterTopLeftCorner.x + (spacingPerWave * i);
            Vector3 wavePoint = new Vector3(xPosition, waterTopLeftCorner.y, waterTopLeftCorner.z);
            waterSpline.InsertPointAt(index, wavePoint);
            waterSpline.SetHeight(index, 0.1f);
            waterSpline.SetCorner(index, false);
            waterSpline.SetTangentMode(index, ShapeTangentMode.Continuous);
        }

        // Loop through all the wave points plus the two top corners, smoothing each
        // and spawning a WaterSpring that owns it.
        springs = new();
        for (int i = 0; i <= WavesCount + 1; i++)
        {
            int index = i + 1;

            Smoothen(waterSpline, index);

            GameObject wavePoint = Instantiate(wavePointPref, wavePoints.transform, false);
            wavePoint.transform.localPosition = waterSpline.GetPosition(index);

            WaterSpring waterSpring = wavePoint.GetComponent<WaterSpring>();
            waterSpring.Init(spriteShapeController);
            springs.Add(waterSpring);
        }
    }

    private void Smoothen(Spline waterSpline, int index)
    {
        int pointCount = waterSpline.GetPointCount();

        Vector3 position = waterSpline.GetPosition(index);
        Vector3 positionPrev = index > 0 ? waterSpline.GetPosition(index - 1) : position;
        Vector3 positionNext = index < pointCount - 1 ? waterSpline.GetPosition(index + 1) : position;

        Vector3 forward = transform.forward;
        float scale = Mathf.Min(
            (positionNext - position).magnitude,
            (positionPrev - position).magnitude) * 0.33f;

        SplineUtility.CalculateTangents(position, positionPrev, positionNext, forward, scale,
            out Vector3 rightTangent, out Vector3 leftTangent);

        waterSpline.SetLeftTangent(index, leftTangent);
        waterSpline.SetRightTangent(index, rightTangent);
    }

    private void ClearWaves()
    {
        if (wavePoints == null)
        {
            return;
        }

        // Iterate backwards so destroying a child does not shift the ones left to remove.
        for (int i = wavePoints.transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(wavePoints.transform.GetChild(i).gameObject);
        }
    }

#if UNITY_EDITOR
    private bool rebuildQueued;

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }
        if (spriteShapeController == null || wavePoints == null || wavePointPref == null)
        {
            return;
        }
        if (rebuildQueued)
        {
            return;
        }

        // Editing the spline and destroying children is not allowed directly inside
        // OnValidate, so defer the rebuild to the next editor tick.
        rebuildQueued = true;
        UnityEditor.EditorApplication.delayCall += RebuildWaves;
    }

    private void RebuildWaves()
    {
        rebuildQueued = false;
        UnityEditor.EditorApplication.delayCall -= RebuildWaves;

        if (this == null)
        {
            return; // component was deleted before the tick fired
        }
        if (spriteShapeController == null || wavePoints == null || wavePointPref == null)
        {
            return;
        }

        ClearWaves();
        SetWaves();
    }
#endif
}
