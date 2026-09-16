using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;

[RequireComponent(typeof(JosStamSmokeEmitterJobs))]
public class FluidBoundarySampler : MonoBehaviour
{
    //static boundary sources
    [SerializeField] private CompositeCollider2D[] boundaryColliders;
    [SerializeField] private LayerMask boundaryLayerMask = -1;
    [SerializeField] private bool autoFindColliders = true;

    //sampling settings
    [SerializeField] private bool showBoundaryDebug = false;
    [SerializeField] private Color debugBoundaryColor = Color.red;

    public BoundaryType boundaryType = BoundaryType.Solid;
    public float dampingFactor = 0.5f;
    [HideInInspector] public List<DynamicFluidBoundary> DynamicBoundaries { get; private set; } = new List<DynamicFluidBoundary>();

    private NativeArray<bool> staticBoundaryMask;
    public NativeArray<bool> StaticBoundaryMask => staticBoundaryMask;

    private JosStamSmokeEmitterJobs smokeEmitter;

    public enum BoundaryType
    {
        Solid,
        Damping,
        Absorbing,
        Reflective
    }

    void Awake()
    {
        smokeEmitter = GetComponent<JosStamSmokeEmitterJobs>();

        if (autoFindColliders)
        {
            FindBoundaryColliders();
        }

        FindExistingDynamicBoundaries();
    }

    void OnDestroy()
    {
        if (staticBoundaryMask.IsCreated)
        {
            staticBoundaryMask.Dispose();
        }
    }

    void FindBoundaryColliders()
    {
        CompositeCollider2D[] allColliders = FindObjectsByType<CompositeCollider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<CompositeCollider2D> validColliders = new List<CompositeCollider2D>();
        foreach (var collider in allColliders)
        {
            if (((1 << collider.gameObject.layer) & boundaryLayerMask) != 0)
            {
                validColliders.Add(collider);
            }
        }
        boundaryColliders = validColliders.ToArray();
    }

    void FindExistingDynamicBoundaries()
    {
        DynamicBoundaries.Clear();
        DynamicFluidBoundary[] existing = FindObjectsByType<DynamicFluidBoundary>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var boundary in existing)
        {
            RegisterDynamicBoundary(boundary);
        }
    }

    public void RegisterDynamicBoundary(DynamicFluidBoundary boundary)
    {
        if (!DynamicBoundaries.Contains(boundary))
        {
            DynamicBoundaries.Add(boundary);
        }
    }

    public void InitializeBoundarySystem()
    {
        if (staticBoundaryMask.IsCreated)
        {
            staticBoundaryMask.Dispose();
        }

        int gridSize = smokeEmitter.GridSize;
        float effectiveCellSize = smokeEmitter.CellSize * smokeEmitter.transform.localScale.x;

        Vector3 gridOrigin = smokeEmitter.transform.position;

        int arraySize = (gridSize + 2) * (gridSize + 2);
        staticBoundaryMask = new NativeArray<bool>(arraySize, Allocator.Persistent);

        if (boundaryColliders == null || boundaryColliders.Length == 0)
            return;

        for (int i = 0; i < gridSize + 2; i++)
        {
            for (int j = 0; j < gridSize + 2; j++)
            {
                Vector3 worldPos = gridOrigin + new Vector3(
                    (i - (gridSize / 2f) - 0.5f) * effectiveCellSize,
                    (j - (gridSize / 2f) - 0.5f) * effectiveCellSize,
                    0);

                int index = i + j * (gridSize + 2);
                staticBoundaryMask[index] = IsInsideStaticBoundary(worldPos);
            }
        }
    }

    bool IsInsideStaticBoundary(Vector3 worldPos)
    {
        foreach (var collider in boundaryColliders)
        {
            if (collider != null && collider.OverlapPoint(worldPos))
            {
                return true;
            }
        }
        return false;
    }
    void OnDrawGizmos()
    {
        if (!showBoundaryDebug || !staticBoundaryMask.IsCreated || smokeEmitter == null) return;

        Gizmos.color = debugBoundaryColor;
        Vector3 scale = smokeEmitter.transform.lossyScale;
        float effectiveCellSize = smokeEmitter.CellSize * scale.x;
        Vector3 gridCenter = smokeEmitter.transform.position;
        Gizmos.matrix = Matrix4x4.TRS(gridCenter, smokeEmitter.transform.rotation, Vector3.one);

        for (int i = 1; i <= smokeEmitter.GridSize; i++)
        {
            for (int j = 1; j <= smokeEmitter.GridSize; j++)
            {
                int index = i + j * (smokeEmitter.GridSize + 2);
                if (staticBoundaryMask[index])
                {
                    Vector3 localCellCenter = new Vector3(
                        (i - smokeEmitter.GridSize * 0.5f - 0.5f) * effectiveCellSize,
                        (j - smokeEmitter.GridSize * 0.5f - 0.5f) * effectiveCellSize,
                        0);

                    Gizmos.DrawCube(localCellCenter, Vector3.one * effectiveCellSize * 0.8f);
                }
            }
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
}

#region Editor Script
#if UNITY_EDITOR

[CustomEditor(typeof(FluidBoundarySampler))]
public class FluidBoundarySamplerEditor : Editor
{
    // Foldout states
    private static bool showStaticBoundarySettings = true;
    private static bool showSamplingSettings = true;
    private static bool showBoundaryBehavior = true;

    // Style variables
    private GUIStyle boxStyle;
    private GUIStyle foldoutStyle;
    private bool stylesInitialized = false;

    private void InitializeStyles()
    {
        if (stylesInitialized) return;

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 5, 5);
        boxStyle.margin = new RectOffset(5, 5, 2, 2);

        foldoutStyle = new GUIStyle(EditorStyles.foldout);
        foldoutStyle.fontStyle = FontStyle.Bold;
        foldoutStyle.fontSize = 12;

        stylesInitialized = true;
    }

    public override void OnInspectorGUI()
    {
        InitializeStyles();
        serializedObject.Update();

        FluidBoundarySampler sampler = (FluidBoundarySampler)target;

        GUILayout.Space(5);

        // Static Boundary Settings Section
        DrawSection("Static Boundary Sources", ref showStaticBoundarySettings, () =>
        {
            var autoFindCollidersProp = serializedObject.FindProperty("autoFindColliders");
            EditorGUILayout.PropertyField(autoFindCollidersProp);

            // Only show boundaryColliders if autoFindColliders is false
            if (!autoFindCollidersProp.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("boundaryColliders"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("boundaryLayerMask"));
        });

        // Sampling Settings Section
        DrawSection("Sampling Settings", ref showSamplingSettings, () =>
        {
            var showBoundaryDebugProp = serializedObject.FindProperty("showBoundaryDebug");
            EditorGUILayout.PropertyField(showBoundaryDebugProp);

            // Only show debugBoundaryColor if showBoundaryDebug is true
            if (showBoundaryDebugProp.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("debugBoundaryColor"));
            }
        });

        // Static Boundary Behavior Section
        DrawSection("Static Boundary Behavior", ref showBoundaryBehavior, () =>
        {
            var boundaryTypeProp = serializedObject.FindProperty("boundaryType");
            EditorGUILayout.PropertyField(boundaryTypeProp);

            // Only show dampingFactor if boundaryType is set to Damping
            var boundaryTypeValue = (FluidBoundarySampler.BoundaryType)boundaryTypeProp.enumValueIndex;
            if (boundaryTypeValue == FluidBoundarySampler.BoundaryType.Damping)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dampingFactor"));
            }
        });

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSection(string title, ref bool foldout, System.Action content)
    {
        EditorGUILayout.BeginVertical(boxStyle);

        foldout = EditorGUILayout.Foldout(foldout, title, true, foldoutStyle);

        if (foldout)
        {
            EditorGUI.indentLevel++;
            GUILayout.Space(5);
            content?.Invoke();
            GUILayout.Space(5);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(3);
    }
}
#endif
#endregion