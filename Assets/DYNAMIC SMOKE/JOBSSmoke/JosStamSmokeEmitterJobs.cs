using System.Collections.Generic;
using System.Diagnostics; //remove after testing
using System.Linq; //remove after testing
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Unity.Collections.LowLevel.Unsafe;
using static JosStamSmokeEmitterJobs;
using Unity.VisualScripting;

#region Emission Settings

[System.Serializable]
public class SmokeEmissionSettingsJobs
{
    //runtime controls
    public bool PlayOnAwake = true;
    [Range(0.5f, 4f)] public float SimulationSpeed = 1f;
    [Range(1, 4)] public int Substeps = 1;
    [Range(0f, 1f), Tooltip("use this to adjust how much emission strength is scaled when we increase the simulation speed")]
    public float SpeedEmissionFactor = 0.5f;

    //emission position
    public Vector2 Position = new Vector2(32, 5);

    //emission shape
    public EmissionShape Shape = EmissionShape.Circle;
    [Range(0f, 1f)] public float EdgeFalloff = 0.5f;
    public float Radius = 3f;
    public Vector2 BoxSize = new Vector2(6f, 2f);
    public Vector2 LineStart = new Vector2(30, 5);
    public Vector2 LineEnd = new Vector2(34, 5);
    public float LineWidth = 1f;
    [Range(0f, 1f)] public float RingSize = 0.25f;
    public Texture2D CustomShapeTexture;
    public bool UseTextureAlpha = true;

    //emission properties
    [Range(0.1f, 1000f)] public float Strength = 100f;
    [Range(0f, 100f)] public float VelocityStrength = 20f;
    public Vector2 VelocityDirection = Vector2.up;
    [Range(0f, 100f)] public float VelocityRandomness = 5f;
    public bool UseRandomDirection = false;
    public bool ScaleVelocityWithStrength = false;

    //radial controls
    public bool RadialOutward = false;
    [Range(0.1f, 0.5f)] public float TangentialStrength = 0.25f;
    [Range(0.1f, 5f)] public float RadialExpansionSpeed = 1f;

    //temporal control
    public bool Continuous = true;
    public float BurstDuration = 0.35f;
    public float BurstCooldown = 2f;
    [Min(1)] public int BurstCount = 1;
    [Min(0.01f)] public float BurstInterval = 0.01f;
    [Range(1f, 10f)] public float BurstStrengthMultiplier = 3f;
    [Tooltip("How many simulation steps to emit per burst frame")]
    [Range(1, 5)] public int BurstEmissionSteps = 2;

    public enum EmissionShape
    {
        Circle,
        Square,
        Line,
        Ring,
        Custom
    }
}

#endregion

public class JosStamSmokeEmitterJobs : MonoBehaviour
{
    public enum StopAction
    {
        None,
        Disable,
        Destroy,
        Callback
    }

    //emission settings
    [SerializeField] private SmokeEmissionSettingsJobs _emissionSettings = new SmokeEmissionSettingsJobs();

    public StopAction stopAction = StopAction.Disable;
    [Tooltip("Density threshold to consider smoke vanished")]
    [Range(0f, 5f)] public float DensityThreshold = 2.5f;
    public UnityEvent OnStopped;

    //grid settings
    public int GridSize = 64;
    public float CellSize = 0.1f;

    //smoke properties
    public Gradient SmokeGradient = new Gradient()
    {
        alphaKeys = new GradientAlphaKey[]
        {
            new GradientAlphaKey(0.0f, 0.0f), // Alpha is 0 (transparent) at the start (time = 0)
            new GradientAlphaKey(1.0f, 0.3f), // Alpha is 1 (opaque) at the 30% mark (time = 0.3)
            new GradientAlphaKey(1.0f, 1.0f)  // Alpha stays 1 until the end (time = 1)
        },

        colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 0.0f),
            new GradientColorKey(Color.white, 1.0f)
        }
    };

    [SerializeField] private float _viscosity = 0.0001f;
    [SerializeField] private float _diffusion = 0.0001f;
    [SerializeField] private float _timeStep = 0.0167f;
    [SerializeField] private float _smokeDecay = 0.995f;
    [Tooltip("Adds a small linear decay to ensure smoke fully disappears.")]
    [SerializeField] private float _linearSmokeDecay = 0.01f; 

    //vorticity
    [SerializeField] private bool _useVorticity = true;
    [Tooltip("How strongly the fluid tries to preserve its rotational energy.")]
    [SerializeField] private float _vorticityStrength = 0.8f;

    //boundary fade
    [SerializeField] private bool _enableBoundaryFade = true;
    [Range(0.05f, 0.5f)] public float FadeWidth = 0.15f;
    public AnimationCurve FadeCurve = AnimationCurve.Linear(0, 1, 1, 0);
    [Range(0.01f, 1f)] public float VelocityDamping = 0.01f;

    //automatic grid positioning
    [SerializeField] private bool _useAutomaticPositioning = true;
    [Tooltip("How much of the grid to keep as buffer around the effect")]
    [Range(0.1f, 0.5f)]
    [SerializeField] private float _gridBuffer = 0.3f;

    //adaptive grid settings
    [SerializeField] private bool _useAdaptiveGrid = true;
    [SerializeField] private int _gridPadding = 3;
    [SerializeField] private float _densityThresholdForGrid = 0.2f;
    [SerializeField] private int _minGridSize = 16;
    [SerializeField] private float _inactiveRegionDamping = 0.98f;

    //references
    [SerializeField] private SpriteRenderer _spritePrefab;

    //Advanced performance optimization:
    [SerializeField] private bool _showPerformanceReadout = false;
    [SerializeField] private bool _changeJobsBatchSize;
    [SerializeField] private int _batchSize = 128;
    [SerializeField] private SmootherType _smootherType = SmootherType.Jacobi;
    [SerializeField, Range(1, 20)] private int _diffuseIterations = 4;
    [SerializeField, Range(1, 20)] private int _mgPreSmoothIterations = 2;
    [SerializeField, Range(1, 20)] private int _mgPostSmoothIterations = 2;
    [SerializeField, Range(1, 20)] private int _mgBottomSolveIterations = 20;
    public bool PauseWhenOffscreen = true;
    public enum SmootherType
    {
        Jacobi,
        RedBlackGaussSeidel
    }

    //debug visualizations
    [SerializeField] private bool _showVelocityField = false;
    [SerializeField] private float _velocityLineWidth = 0.02f;
    [SerializeField] private float _velocityLineLength = 0.1f;
    [SerializeField] private bool _showBoundaries = false;
    [SerializeField] private bool _showAdaptiveGridBounds = false;
    [SerializeField] private Color _boundaryColor = Color.green;

    // Emission timing control
    private float _burstTimer = 0f;
    private float _burstIntervalTimer = 0f;
    private int _burstsEmitted = 0;

    private bool _isEmitting = false;
    private bool _isPlaying = false;
    private bool _shouldAutoStart = false;
    private bool _isEmissionHalted = false;
    private bool _isFadingOut = false;
    private bool _isSimulating = false;

    private NativeArray<float> _density;
    private NativeArray<float> _prevDensity;
    private NativeArray<float> _velocityX;
    private NativeArray<float> _prevVelocityX;
    private NativeArray<float> _velocityY;
    private NativeArray<float> _prevVelocityY;

    private NativeArray<float> _tempArray1;
    private NativeArray<float> _tempArray2;
    private NativeArray<Color32> _pixelBuffer;
    private NativeArray<Color32> _gradientLookupTable;
    private NativeArray<float> _vorticity;
    private NativeArray<float> _fadeCurveLookupTable;

    private JobHandle _currentJobHandle;
    private JobHandle _boundsJobHandle;

    // Visualization
    private Texture2D _smokeTexture;
    private SpriteRenderer _smokeSprite;
    private LineRenderer[] _velocityLines;
    private LineRenderer[] _boundaryLines;
    private LineRenderer[] _adaptiveGridLines;

    private Vector3 _effectCenter;
    private bool _hasSetInitialPosition = false;

    private bool _hasEmitted = false;
    private bool _stopActionTriggered = false;
    private float _lastEmissionTime = 0f;
    private const float CHECK_INTERVAL = 0.5f;
    private float _checkTimer = 0f;

    private int _smokeTextureProperty = Shader.PropertyToID("_SmokeTexture");

    private GridBounds _currentBounds;
    private int _framesSinceLastBoundsUpdate = 0;
    private const int _boundsUpdateInterval = 3; // Update bounds every N frames

    private FluidBoundarySampler _boundarySampler;

    private NativeArray<bool> _staticBoundaryMask;
    private NativeArray<DynamicBoundaryData> _dynamicBoundaryData;

    private const int BOUNDS_REDUCTION_GROUPS = 128;
    private NativeArray<int4> _boundsReductionIntermediate; //x=minX, y=maxX, z=minY, w=maxY
    private bool _isCurrentlyVisible = true;

    //editor stuff
    [SerializeField, HideInInspector]
    private EditorFoldoutStates _foldoutStates = new EditorFoldoutStates();

    #region performance benchmark variables
    //// --- Performance Counter Variables --- (remove after testing)
    private const int BATCH_SAMPLE_SIZE = 100; // How many frames to average in one batch.
    private readonly Stopwatch simulationStopwatch = new Stopwatch();

    private readonly List<double> frameTimeBatch = new List<double>(BATCH_SAMPLE_SIZE);
    private readonly List<double> batchAverages = new List<double>();
    private string currentStatusText = "Calculating...";
    private string longTermAverageText = "Last Avg: N/A";
    ////------
    #endregion

    private CGSolver CGSolver;

    public struct DynamicBoundaryData
    {
        public Bounds bounds;
        public Vector2 velocity;
        public float velocityInfluence;
        public FluidBoundarySampler.BoundaryType boundaryType;
    }

    #region Unity Callbacks

    private void Awake()
    {
        CGSolver = new CGSolver(GridSize);
        _boundsReductionIntermediate = new NativeArray<int4>(BOUNDS_REDUCTION_GROUPS, Allocator.Persistent);
        _shouldAutoStart = _emissionSettings.PlayOnAwake;

        InitializeNativeArrays();
        UpdateGradientLookupTable();
        UpdateFadeCurveLookupTable();

        if (!_hasSetInitialPosition)
        {
            if (_useAutomaticPositioning)
            {
                Vector3 localEmissionPos = new Vector3(
                    _emissionSettings.Position.x * CellSize,
                    _emissionSettings.Position.y * CellSize,
                    0
                );
                _effectCenter = transform.TransformPoint(localEmissionPos);
                PositionGridOptimally();
            }
            else
            {
                _effectCenter = transform.position + new Vector3(
                    _emissionSettings.Position.x * CellSize,
                    _emissionSettings.Position.y * CellSize,
                    0
                );
            }

            _hasSetInitialPosition = true;
        }

        if (_useAdaptiveGrid)
        {
            int emissionX = Mathf.RoundToInt(_emissionSettings.Position.x);
            int emissionY = Mathf.RoundToInt(_emissionSettings.Position.y);
            int halfMin = _minGridSize / 2;

            _currentBounds = new GridBounds(
                Mathf.Max(1, emissionX - halfMin),
                Mathf.Min(GridSize, emissionX + halfMin),
                Mathf.Max(1, emissionY - halfMin),
                Mathf.Min(GridSize, emissionY + halfMin)
            );

        }
        else
        {
            _currentBounds = new GridBounds(1, GridSize, 1, GridSize);
        }

        CreateVisualization();
    }

    private void Start()
    {
        _boundarySampler = GetComponent<FluidBoundarySampler>();
        if (_boundarySampler != null)
        {
            _boundarySampler.InitializeBoundarySystem();
            _staticBoundaryMask = _boundarySampler.StaticBoundaryMask;
        }
        else
        {
            _staticBoundaryMask = new NativeArray<bool>((GridSize + 2) * (GridSize + 2), Allocator.Persistent);
        }

        _dynamicBoundaryData = new NativeArray<DynamicBoundaryData>(10, Allocator.Persistent);
    }

    private void OnDisable()
    {
        Stop();
        _hasSetInitialPosition = false;
        _hasEmitted = false;
        _stopActionTriggered = false;
    }

    private void OnEnable()
    {
        if (_shouldAutoStart)
        {
            Play();
        }
    }

    void Update()
    {
        // Wait for all jobs scheduled in the last FixedUpdate to complete.
        // This is the sync point that ensures the data we're about to read for visualization is ready.
        JobHandle previousJobs = JobHandle.CombineDependencies(_currentJobHandle, _boundsJobHandle);
        previousJobs.Complete();

        // Now that the data is safe, schedule and complete the job to create the texture.
        var colorJob = new DensityToColorJob
        {
            density = this._density,
            pixels = this._pixelBuffer,
            gradientLookup = this._gradientLookupTable,
            N = this.GridSize
        };

        colorJob.Schedule(GridSize * GridSize, _batchSize).Complete();

        UpdateTextureAndApply();
        if (_showVelocityField) UpdateVelocityVisualization();
        if (_showBoundaries) UpdateBoundaryVisualization();
        if (_showAdaptiveGridBounds) UpdateAdaptiveGridVisualization();

        if (_hasEmitted && !_stopActionTriggered && ShouldCheckStopCondition())
        {
            CheckStopCondition();
        }
    }

    void FixedUpdate()
    {
        // --- Step 1: Early exit and visibility checks ---
        if (_showPerformanceReadout)
            simulationStopwatch.Restart();

        _isFadingOut = _hasEmitted && !_stopActionTriggered;
        _isSimulating = _isPlaying || _isFadingOut;

        if (!_isSimulating)
        {
            if (_showPerformanceReadout) simulationStopwatch.Stop();
            return;
        }

        if (PauseWhenOffscreen && !_isCurrentlyVisible && !_isFadingOut)
        {
            if (_showPerformanceReadout) simulationStopwatch.Stop();
            return;
        }

        // --- Step 2: Synchronize & Prepare Main Thread Data ---
        JobHandle previousFrameJobs = JobHandle.CombineDependencies(_currentJobHandle, _boundsJobHandle);
        previousFrameJobs.Complete();

        // Now that jobs are complete, it's safe to do main-thread work that depends on job results.
        if (_useAdaptiveGrid)
        {
            ProcessBoundsResults();
        }
        UpdateBoundaryData();

        // --- Step 3: Simulation Logic ---
        float dt = Time.fixedDeltaTime * _emissionSettings.SimulationSpeed;
        UpdateEmissionTiming(dt);

        if (_isEmitting)
        {
            int emissionCount = (!_emissionSettings.Continuous && _isEmitting) ? _emissionSettings.BurstEmissionSteps : 1;
            for (int i = 0; i < emissionCount; i++)
            {
                AddSmokeSource();
            }
            _lastEmissionTime = Time.time;
        }

        // --- Step 4: Schedule All Jobs for this Tick ---
        float adjustedDt = dt / _emissionSettings.Substeps;
        JobHandle simulationHandle = new JobHandle();

        for (int step = 0; step < _emissionSettings.Substeps; step++)
        {
            simulationHandle = VelocityStepJobs(adjustedDt, simulationHandle);
            simulationHandle = DensityStepJobs(adjustedDt, simulationHandle);
        }

        _currentJobHandle = simulationHandle;

        // Schedule the bounds job to run after this tick's simulation.
        if (_useAdaptiveGrid)
        {
            _framesSinceLastBoundsUpdate++;
            if (_framesSinceLastBoundsUpdate >= _boundsUpdateInterval)
            {
                _framesSinceLastBoundsUpdate = 0;
                var findBoundsJob = new FindBoundsJob
                {
                    density = _density,
                    results = _boundsReductionIntermediate,
                    N = GridSize,
                    densityThreshold = _densityThresholdForGrid,
                    itemsPerJob = ((GridSize + 2) * (GridSize + 2)) / BOUNDS_REDUCTION_GROUPS
                };
                _boundsJobHandle = findBoundsJob.Schedule(BOUNDS_REDUCTION_GROUPS, 1, _currentJobHandle);
            }
            else
            {
                // If not scheduling a new job, chain the handle so the dependency is maintained.
                _boundsJobHandle = _currentJobHandle;
            }
        }
        else
        {
            _boundsJobHandle = new JobHandle();
        }

        JobHandle.ScheduleBatchedJobs();

        if (_showPerformanceReadout)
        {
            simulationStopwatch.Stop();
            UpdatePerformanceReadout(simulationStopwatch.Elapsed.TotalMilliseconds);
        }
    }


    void OnValidate()
    {
        if (Application.isPlaying)
        {
            _currentJobHandle.Complete();
            UpdateGradientLookupTable();
            UpdateFadeCurveLookupTable();
        }
    }

    #region Performance Benchmark methods

    private void UpdatePerformanceReadout(double frameTime)
    {
        frameTimeBatch.Add(frameTime);
        currentStatusText = $"Calculating... ({frameTimeBatch.Count}/{BATCH_SAMPLE_SIZE})";

        if (frameTimeBatch.Count >= BATCH_SAMPLE_SIZE)
        {
            double currentBatchAverage = frameTimeBatch.Average();
            batchAverages.Add(currentBatchAverage);
            double longTermAverage = batchAverages.Average();
            longTermAverageText = $"Last Avg: {longTermAverage:F3} ms ({batchAverages.Count} batches)";
            frameTimeBatch.Clear();
        }
    }

    //remove when done testing
    void OnGUI()
    {
        if (_showPerformanceReadout)
        {
            GUI.backgroundColor = Color.black;

            GUI.Label(new Rect(10, 10, 400, 20), currentStatusText);

            GUI.Label(new Rect(10, 30, 400, 20), longTermAverageText);
        }
    }

    #endregion

    void OnDestroy()
    {
        JobHandle finalHandle = JobHandle.CombineDependencies(_currentJobHandle, _boundsJobHandle);
        finalHandle.Complete();

        CGSolver.Dispose();
        if (_boundsReductionIntermediate.IsCreated) _boundsReductionIntermediate.Dispose();
        if (_density.IsCreated) _density.Dispose();
        if (_prevDensity.IsCreated) _prevDensity.Dispose();
        if (_velocityX.IsCreated) _velocityX.Dispose();
        if (_prevVelocityX.IsCreated) _prevVelocityX.Dispose();
        if (_velocityY.IsCreated) _velocityY.Dispose();
        if (_prevVelocityY.IsCreated) _prevVelocityY.Dispose();
        if (_tempArray1.IsCreated) _tempArray1.Dispose();
        if (_tempArray2.IsCreated) _tempArray2.Dispose();
        if (_pixelBuffer.IsCreated) _pixelBuffer.Dispose();
        if (_vorticity.IsCreated) _vorticity.Dispose();
        if (_fadeCurveLookupTable.IsCreated) _fadeCurveLookupTable.Dispose();
        if (_gradientLookupTable.IsCreated) _gradientLookupTable.Dispose();
        if (_dynamicBoundaryData.IsCreated) _dynamicBoundaryData.Dispose();
        if (_boundarySampler == null && _staticBoundaryMask.IsCreated)
        {
            _staticBoundaryMask.Dispose();
        }

        if (_smokeTexture != null)
        {
            Destroy(_smokeTexture);
        }
    }


    #endregion

    #region Dynamic/Static Boundaries (Obstacles)

    void UpdateBoundaryData()
    {
        if (_boundarySampler == null || _boundarySampler.DynamicBoundaries.Count == 0) return;

        int boundaryCount = _boundarySampler.DynamicBoundaries.Count;

        if (_dynamicBoundaryData.Length < boundaryCount)
        {
            _dynamicBoundaryData.Dispose();
            _dynamicBoundaryData = new NativeArray<DynamicBoundaryData>(boundaryCount, Allocator.Persistent);
        }

        for (int i = 0; i < boundaryCount; i++)
        {
            var source = _boundarySampler.DynamicBoundaries[i];
            _dynamicBoundaryData[i] = new DynamicBoundaryData
            {
                bounds = source.ColliderBounds,
                velocity = source.Velocity,
                velocityInfluence = source.velocityInfluence,
                boundaryType = source.boundaryType
            };
        }
    }

    #endregion

    #region JOB-Based-Simulation


    private void InitializeNativeArrays()
    {
        int size = (GridSize + 2) * (GridSize + 2);
        int textureSize = GridSize * GridSize;

        _density = new NativeArray<float>(size, Allocator.Persistent);
        _prevDensity = new NativeArray<float>(size, Allocator.Persistent);
        _velocityX = new NativeArray<float>(size, Allocator.Persistent);
        _prevVelocityX = new NativeArray<float>(size, Allocator.Persistent);
        _velocityY = new NativeArray<float>(size, Allocator.Persistent);
        _prevVelocityY = new NativeArray<float>(size, Allocator.Persistent);

        _tempArray1 = new NativeArray<float>(size, Allocator.Persistent);
        _tempArray2 = new NativeArray<float>(size, Allocator.Persistent);
        _pixelBuffer = new NativeArray<Color32>(textureSize, Allocator.Persistent);
        _vorticity = new NativeArray<float>(size, Allocator.Persistent);

        // Initialize to zero
        for (int i = 0; i < size; i++)
        {
            _density[i] = 0;
            _prevDensity[i] = 0;
            _velocityX[i] = 0;
            _prevVelocityX[i] = 0;
            _velocityY[i] = 0;
            _prevVelocityY[i] = 0;
            _tempArray1[i] = 0;
            _tempArray2[i] = 0;
            _vorticity[i] = 0;
        }
    }

    private JobHandle VelocityStepJobs(float deltaTime, JobHandle dependency)
    {
        JobHandle jobHandle = dependency;
        int size = (GridSize + 2) * (GridSize + 2);

        // Define the bounds and schedule count based on whether the adaptive grid is active.
        GridBounds activeBounds;
        int scheduleCount;

        if (_useAdaptiveGrid)
        {
            activeBounds = _currentBounds;
            scheduleCount = activeBounds.width * activeBounds.height;
            if (scheduleCount <= 0) return jobHandle; // Safety check if bounds are invalid
        }
        else
        {
            // When adaptive grid is off, the "bounds" are the entire grid, including the 1-cell border.
            activeBounds = new GridBounds(0, GridSize + 1, 0, GridSize + 1);
            scheduleCount = size;
        }

        if (_useVorticity)
        {
            var calcVorticityJob = new CalculateVorticityJob
            {
                velocityX = this._velocityX,
                velocityY = this._velocityY,
                vorticity = this._vorticity,
                N = this.GridSize,
                useAdaptiveGrid = _useAdaptiveGrid,
                bounds = activeBounds
            };
            jobHandle = calcVorticityJob.Schedule(scheduleCount, _batchSize, jobHandle);

            var applyVorticityJob = new ApplyVorticityJob
            {
                vorticity = this._vorticity,
                velocityX = this._velocityX,
                velocityY = this._velocityY,
                vorticityStrength = this._vorticityStrength,
                dt = deltaTime,
                N = this.GridSize,
                useAdaptiveGrid = _useAdaptiveGrid,
                bounds = activeBounds
            };
            jobHandle = applyVorticityJob.Schedule(scheduleCount, _batchSize, jobHandle);
        }

        jobHandle = new CopyArrayJob { source = _velocityX, destination = _prevVelocityX }.Schedule(size, _batchSize, jobHandle);
        jobHandle = new CopyArrayJob { source = _velocityY, destination = _prevVelocityY }.Schedule(size, _batchSize, jobHandle);

        jobHandle = DiffuseJobsRedBlack(_velocityX, _prevVelocityX, _viscosity, deltaTime, 1, jobHandle);
        jobHandle = SetBoundaryJobs(1, _velocityX, jobHandle);
        jobHandle = DiffuseJobsRedBlack(_velocityY, _prevVelocityY, _viscosity, deltaTime, 2, jobHandle);
        jobHandle = SetBoundaryJobs(2, _velocityY, jobHandle);

        jobHandle = ProjectJobsMG(_velocityX, _velocityY, jobHandle);

        jobHandle = new CopyArrayJob { source = _velocityX, destination = _prevVelocityX }.Schedule(size, _batchSize, jobHandle);
        jobHandle = new CopyArrayJob { source = _velocityY, destination = _prevVelocityY }.Schedule(size, _batchSize, jobHandle);

        var advectJobX = new AdvectInBoundsJob
        {
            d0 = _prevVelocityX,
            u = _prevVelocityX,
            v = _prevVelocityY,
            d = _velocityX,
            dt = deltaTime * GridSize,
            N = GridSize,
            bounds = activeBounds
        };
        jobHandle = advectJobX.Schedule(scheduleCount, _batchSize, jobHandle);

        var advectJobY = new AdvectInBoundsJob
        {
            d0 = _prevVelocityY,
            u = _prevVelocityX,
            v = _prevVelocityY,
            d = _velocityY,
            dt = deltaTime * GridSize,
            N = GridSize,
            bounds = activeBounds
        };
        jobHandle = advectJobY.Schedule(scheduleCount, _batchSize, jobHandle);

        jobHandle = SetBoundaryJobs(1, _velocityX, jobHandle);
        jobHandle = SetBoundaryJobs(2, _velocityY, jobHandle);

        if (_enableBoundaryFade)
        {
            var spongeJob = new VelocitySpongeJob
            {
                velocityX = this._velocityX,
                velocityY = this._velocityY,
                N = this.GridSize,
                fadeWidth = this.FadeWidth,
                damping = this.VelocityDamping,
                //useAdaptiveGrid = _useAdaptiveGrid,
                //bounds = activeBounds
            };
            jobHandle = spongeJob.Schedule(size, _batchSize, jobHandle);
        }

        if (_useAdaptiveGrid)
        {
            var dampenJob = new DampenInactiveVelocityJob
            {
                velocityX = this._velocityX,
                velocityY = this._velocityY,
                N = this.GridSize,
                bounds = activeBounds,
                dampingFactor = this._inactiveRegionDamping
            };
            // This job also runs over the ENTIRE grid but has internal logic to only affect inactive cells.
            jobHandle = dampenJob.Schedule(size, _batchSize, jobHandle);
        }

        return jobHandle;
    }

    private JobHandle DensityStepJobs(float deltaTime, JobHandle dependency)
    {
        JobHandle jobHandle = dependency;
        int size = (GridSize + 2) * (GridSize + 2);

        GridBounds activeBounds;
        int scheduleCount;

        if (_useAdaptiveGrid)
        {
            activeBounds = _currentBounds;
            scheduleCount = activeBounds.width * activeBounds.height;
            if (scheduleCount <= 0) return jobHandle;
        }
        else
        {
            activeBounds = new GridBounds(0, GridSize + 1, 0, GridSize + 1);
            scheduleCount = size;
        }

        jobHandle = new CopyArrayJob { source = _density, destination = _prevDensity }.Schedule(size, _batchSize, jobHandle);
        jobHandle = DiffuseJobsRedBlack(_density, _prevDensity, _diffusion, deltaTime, 0, jobHandle);
        jobHandle = new CopyArrayJob { source = _density, destination = _prevDensity }.Schedule(size, _batchSize, jobHandle);

        var advectJob = new AdvectInBoundsJob
        {
            d0 = _prevDensity,
            u = _velocityX,
            v = _velocityY,
            d = _density,
            dt = deltaTime * GridSize,
            N = GridSize,
            bounds = activeBounds
        };
        jobHandle = advectJob.Schedule(scheduleCount, _batchSize, jobHandle);

        float decayFactor = Mathf.Pow(_smokeDecay, deltaTime / this._timeStep);
        var decayJob = new DecayInBoundsJob
        {
            density = _density,
            decayFactor = decayFactor,
            linearDecay = _linearSmokeDecay * deltaTime,
            N = GridSize,
            bounds = activeBounds
        };
        jobHandle = decayJob.Schedule(scheduleCount, _batchSize, jobHandle);

        jobHandle = SetBoundaryJobs(0, _density, jobHandle);

        if (_enableBoundaryFade)
        {
            var falloffJob = new DensityFalloffJob
            {
                density = this._density,
                curveLookup = this._fadeCurveLookupTable,
                N = this.GridSize,
                fadeWidth = this.FadeWidth,
                useAdaptiveGrid = _useAdaptiveGrid,
                bounds = activeBounds
            };
            jobHandle = falloffJob.Schedule(scheduleCount, _batchSize, jobHandle);
        }
        if (_boundarySampler != null)
        {
            var staticBoundaryJob = new StaticBoundaryJob
            {
                velocityX = this._velocityX,
                velocityY = this._velocityY,
                density = this._density,
                mask = this._staticBoundaryMask,
                boundaryType = _boundarySampler.boundaryType,
                dampingFactor = _boundarySampler.dampingFactor,
                N = this.GridSize,
                useAdaptiveGrid = _useAdaptiveGrid,
                bounds = activeBounds
            };
            jobHandle = staticBoundaryJob.Schedule(scheduleCount, _batchSize, jobHandle);
        }
        if (_boundarySampler != null && _dynamicBoundaryData.Length > 0 && _boundarySampler.DynamicBoundaries.Count > 0)
        {
            var dynamicBoundaryJob = new DynamicBoundaryJob
            {
                velocityX = this._velocityX,
                velocityY = this._velocityY,
                dynamicBoundaries = this._dynamicBoundaryData.GetSubArray(0, _boundarySampler.DynamicBoundaries.Count),
                gridOrigin = transform.position,
                cellSize = this.CellSize * transform.localScale.x,
                N = this.GridSize,
                useAdaptiveGrid = _useAdaptiveGrid,
                bounds = activeBounds
            };
            jobHandle = dynamicBoundaryJob.Schedule(scheduleCount, _batchSize, jobHandle);
        }

        return jobHandle;
    }

    private JobHandle DiffuseJobsRedBlack(NativeArray<float> x, NativeArray<float> x0, float diff, float dt, int boundaryType, JobHandle dependency)
    {
        float a = dt * diff * GridSize * GridSize;
        float c = 1 + 4 * a;
        JobHandle handle = dependency;

        // The area to schedule over is correctly based on the rectangular bounds, not the sparse list.
        int scheduleCount = _useAdaptiveGrid ? (_currentBounds.width * _currentBounds.height) : ((GridSize + 2) * (GridSize + 2));

        // A critical safety check to prevent scheduling jobs with a size of zero if the bounds are invalid.
        if (_useAdaptiveGrid && scheduleCount <= 0) return handle;

        for (int k = 0; k < _diffuseIterations; k++)
        {
            var redJob = new RedBlackLinearSolveJob
            {
                x0 = x0,
                x = x,
                a = a,
                c = c,
                N = GridSize,
                isRedPass = true,
                useAdaptiveGrid = _useAdaptiveGrid,
                bounds = _currentBounds
            };
            handle = redJob.Schedule(scheduleCount, _batchSize, handle);
            handle = SetBoundaryJobs(boundaryType, x, handle);

            var blackJob = new RedBlackLinearSolveJob
            {
                x0 = x0,
                x = x,
                a = a,
                c = c,
                N = GridSize,
                isRedPass = false,
                useAdaptiveGrid = _useAdaptiveGrid,
                bounds = _currentBounds
            };
            handle = blackJob.Schedule(scheduleCount, _batchSize, handle);
            handle = SetBoundaryJobs(boundaryType, x, handle);
        }
        return handle;
    }

    private void ProcessBoundsResults()
    {
        if (_framesSinceLastBoundsUpdate != 0) return;

        int minX = GridSize + 1, maxX = 0, minY = GridSize + 1, maxY = 0;
        bool foundActiveArea = false;

        for (int i = 0; i < BOUNDS_REDUCTION_GROUPS; i++)
        {
            int4 result = _boundsReductionIntermediate[i];
            if (result.y != -1)
            {
                foundActiveArea = true;
                minX = math.min(minX, result.x);
                maxX = math.max(maxX, result.y);
                minY = math.min(minY, result.z);
                maxY = math.max(maxY, result.w);
            }
        }

        if (foundActiveArea)
        {
            minX = Mathf.Max(1, minX - _gridPadding);
            maxX = Mathf.Min(GridSize, maxX + _gridPadding);
            minY = Mathf.Max(1, minY - _gridPadding);
            maxY = Mathf.Min(GridSize, maxY + _gridPadding);

            if (maxX - minX + 1 < _minGridSize)
            {
                int diff = _minGridSize - (maxX - minX + 1);
                minX = Mathf.Max(1, minX - diff / 2);
                maxX = Mathf.Min(GridSize, maxX + (diff + 1) / 2);
            }
            if (maxY - minY + 1 < _minGridSize)
            {
                int diff = _minGridSize - (maxY - minY + 1);
                minY = Mathf.Max(1, minY - diff / 2);
                maxY = Mathf.Min(GridSize, maxY + (diff + 1) / 2);
            }
            _currentBounds = new GridBounds(minX, maxX, minY, maxY);
        }
        else
        {
            int emissionX = Mathf.RoundToInt(_emissionSettings.Position.x);
            int emissionY = Mathf.RoundToInt(_emissionSettings.Position.y);
            int halfMin = _minGridSize / 2;

            _currentBounds = new GridBounds(
                Mathf.Max(1, emissionX - halfMin),
                Mathf.Min(GridSize, emissionX + halfMin),
                Mathf.Max(1, emissionY - halfMin),
                Mathf.Min(GridSize, emissionY + halfMin)
            );
        }
    }


    private JobHandle ProjectJobsMG(NativeArray<float> u, NativeArray<float> v, JobHandle dependency)
    {
        JobHandle handle = dependency;

        NativeArray<float> full_rhs = CGSolver.r;
        NativeArray<float> full_solution = _tempArray2;

        GridBounds activeBounds;
        int scheduleCount;

        if (_useAdaptiveGrid)
        {
            activeBounds = _currentBounds;
            scheduleCount = activeBounds.width * activeBounds.height;
            if (scheduleCount <= 0) return handle;
        }
        else
        {
            // Note: For these jobs, we only want to process the *inner* grid cells, not the boundary.
            // So the bounds are 1 to N, not 0 to N+1.
            activeBounds = new GridBounds(1, GridSize, 1, GridSize);
            scheduleCount = GridSize * GridSize;
        }

        var divJob = new DivergenceInBoundsJob
        {
            u = u,
            v = v,
            div = full_rhs,
            N = GridSize,
            h = GridSize,
            bounds = activeBounds
        };
        handle = divJob.Schedule(scheduleCount, _batchSize, handle);
        handle = SetBoundaryJobs(0, full_rhs, handle);

        int adaptiveGridSize = _useAdaptiveGrid ? math.max(_currentBounds.width, _currentBounds.height) : GridSize;
        adaptiveGridSize = Mathf.NextPowerOfTwo(adaptiveGridSize);
        adaptiveGridSize = math.clamp(adaptiveGridSize, 16, GridSize);
        var tempData = new FlatMultigridData(adaptiveGridSize, 5, Allocator.TempJob);

        var copyInJob = new CopyRegionJob
        {
            source = full_rhs,
            destination = tempData.residuals,
            sourceBounds = _useAdaptiveGrid ? _currentBounds : new GridBounds(1, GridSize, 1, GridSize),
            sourceGridSize = GridSize,
            destGridSize = adaptiveGridSize,
            destOffsetX = 0,
            destOffsetY = 0
        };
        handle = copyInJob.Schedule(handle);
        handle = new ClearArrayJob { array = tempData.solutions }.Schedule((adaptiveGridSize + 2) * (adaptiveGridSize + 2), _batchSize, handle);
        handle = MultigridSolver.VCycle_Jobs(handle, ref tempData, _mgPreSmoothIterations, _mgPostSmoothIterations, _mgBottomSolveIterations, _smootherType);
        handle = new ClearArrayJob { array = full_solution }.Schedule((GridSize + 2) * (GridSize + 2), _batchSize, handle);

        var copyOutJob = new CopyRegionJob
        {
            source = tempData.solutions,
            destination = full_solution,
            sourceBounds = new GridBounds(1, adaptiveGridSize, 1, adaptiveGridSize),
            sourceGridSize = adaptiveGridSize,
            destGridSize = GridSize,
            destOffsetX = _useAdaptiveGrid ? _currentBounds.minX - 1 : 0,
            destOffsetY = _useAdaptiveGrid ? _currentBounds.minY - 1 : 0
        };
        handle = copyOutJob.Schedule(handle);

        var gradJob = new PressureGradInBoundsJob
        {
            p = full_solution,
            u = u,
            v = v,
            N = GridSize,
            h = GridSize,
            bounds = activeBounds
        };
        handle = gradJob.Schedule(scheduleCount, _batchSize, handle);

        handle = SetBoundaryJobs(1, u, handle);
        handle = SetBoundaryJobs(2, v, handle);

        handle = tempData.Dispose(handle);

        return handle;
    }

    private JobHandle SetBoundaryJobs(int b, NativeArray<float> field, JobHandle dependency)
    {
        var boundaryJob = new BoundaryJob
        {
            field = field,
            N = GridSize,
            boundaryType = b,
            offset = 0
        };

        return boundaryJob.Schedule(dependency);
    }

    private void UpdateFadeCurveLookupTable()
    {
        if (_fadeCurveLookupTable.IsCreated)
        {
            _fadeCurveLookupTable.Dispose();
        }
        _fadeCurveLookupTable = new NativeArray<float>(256, Allocator.Persistent);
        for (int i = 0; i < 256; i++)
        {
            // Evaluate the curve at 256 points and store the result
            _fadeCurveLookupTable[i] = FadeCurve.Evaluate(i / 255f);
        }
    }

    #endregion

    #region Stop Actions

    private bool ShouldCheckStopCondition()
    {
        if (_isEmitting || !_hasEmitted) return false;

        // Only check periodically for performance
        _checkTimer += Time.deltaTime;
        if (_checkTimer < CHECK_INTERVAL) return false;

        _checkTimer = 0f;
        return true;
    }

    private void CheckStopCondition()
    {
        if (Time.time - _lastEmissionTime < 1f) return;

        float maxDensity = 0f;
        for (int i = 1; i <= GridSize; i++)
        {
            for (int j = 1; j <= GridSize; j++)
            {
                int index = i + j * (GridSize + 2);
                if (_density[index] > maxDensity)
                {
                    maxDensity = _density[index];
                }
            }
        }

        if (maxDensity <= DensityThreshold)
        {
            _stopActionTriggered = true;
            HandleStopAction();
        }
    }

    private void HandleStopAction()
    {
        switch (stopAction)
        {
            case StopAction.Disable:
                gameObject.SetActive(false);
                break;

            case StopAction.Destroy:
                Destroy(gameObject);
                break;

            case StopAction.Callback:
                OnStopped.Invoke();
                break;

            case StopAction.None:
                break;
        }
    }

    #endregion

    #region Grid Positioning

    private void PositionGridOptimally()
    {
        Vector3 localEmissionOffset = new Vector3(
            (_emissionSettings.Position.x - GridSize * 0.5f) * CellSize,
            (_emissionSettings.Position.y - GridSize * 0.5f) * CellSize,
            0
        );
        Vector3 effectCenter = transform.TransformPoint(localEmissionOffset);

        float bufferSize = GridSize * CellSize * _gridBuffer * transform.lossyScale.x;

        Vector3 optimalGridCenter = effectCenter;
        if (!_emissionSettings.RadialOutward && _emissionSettings.VelocityDirection != Vector2.zero && !_emissionSettings.UseRandomDirection)
        {
            Vector2 dir = _emissionSettings.VelocityDirection.normalized;
            optimalGridCenter += transform.TransformDirection(new Vector3(dir.x, dir.y, 0f)) * bufferSize;
        }

        transform.position = optimalGridCenter;

        Vector3 newLocalEmissionPos = transform.InverseTransformPoint(effectCenter);
        _emissionSettings.Position = new Vector2(
            (newLocalEmissionPos.x / CellSize) + (GridSize * 0.5f),
            (newLocalEmissionPos.y / CellSize) + (GridSize * 0.5f)
        );
    }

    private void PositionGridOptimallyForEffectCenter()
    {
        float bufferSize = GridSize * CellSize * _gridBuffer * transform.lossyScale.x;

        Vector3 optimalGridCenter = _effectCenter;
        if (!_emissionSettings.RadialOutward && _emissionSettings.VelocityDirection != Vector2.zero && !_emissionSettings.UseRandomDirection)
        {
            Vector2 dir = _emissionSettings.VelocityDirection.normalized;
            optimalGridCenter += transform.TransformDirection(new Vector3(dir.x, dir.y, 0f)) * bufferSize;
        }

        // Position the transform so the grid is optimally placed
        transform.position = optimalGridCenter;

        // Calculate the emission position in grid coordinates based on where we want the effect center to be
        Vector3 newLocalEmissionPos = transform.InverseTransformPoint(_effectCenter);
        _emissionSettings.Position = new Vector2(
            (newLocalEmissionPos.x / CellSize) + (GridSize * 0.5f),
            (newLocalEmissionPos.y / CellSize) + (GridSize * 0.5f)
        );
    }

    #endregion

    #region Texture Visualization

    private void UpdateGradientLookupTable()
    {
        if (_gradientLookupTable.IsCreated)
        {
            _gradientLookupTable.Dispose();
        }
        _gradientLookupTable = new NativeArray<Color32>(256, Allocator.Persistent);
        for (int i = 0; i < 256; i++)
        {
            _gradientLookupTable[i] = SmokeGradient.Evaluate(i / 255f);
        }
    }

    private void CreateVisualization()
    {
        CreateCPUVisualization();

        if (_showVelocityField)
            CreateVelocityVisualization();
        if (_showBoundaries)
            CreateBoundaryVisualization();
        if (_showAdaptiveGridBounds)
            CreateAdaptiveGridVisualization();
    }

    private void CreateCPUVisualization()
    {
        _smokeTexture = new Texture2D(GridSize, GridSize, TextureFormat.RGBA32, false, true);
        _smokeTexture.filterMode = FilterMode.Bilinear;

        Color[] transparentPixels = new Color[GridSize * GridSize];
        for (int i = 0; i < transparentPixels.Length; i++)
        {
            transparentPixels[i] = Color.clear;
        }
        _smokeTexture.SetPixels(transparentPixels);
        _smokeTexture.Apply();

        _smokeSprite = Instantiate(_spritePrefab, transform);
        _smokeSprite.transform.localPosition = Vector3.zero;
        _smokeSprite.transform.localScale = new Vector3(GridSize * CellSize, GridSize * CellSize, 1f);
        _smokeSprite.material.SetTexture(_smokeTextureProperty, _smokeTexture);

        var proxy = _smokeSprite.gameObject.AddComponent<VisibilityProxy>();
        proxy.Initialize(this);
    }

    private void UpdateTextureAndApply()
    {
        _smokeTexture.SetPixelData(_pixelBuffer, 0);
        _smokeTexture.Apply(false);
    }


    #endregion

    #region Smoke Emission Methods

    void UpdateEmissionTiming(float dt)
    {
        if (_isEmissionHalted)
        {
            _isEmitting = false;
            return;
        }
        if (!_isPlaying)
        {
            _isEmitting = false;
            return;
        }

        // Continuous mode - always emit
        if (_emissionSettings.Continuous)
        {
            _isEmitting = true;
            return;
        }

        // Burst emission timing
        // MODIFIED: Use the passed-in 'dt' instead of Time.deltaTime
        _burstTimer += dt;

        if (_burstsEmitted < _emissionSettings.BurstCount)
        {
            if (_burstTimer <= _emissionSettings.BurstDuration)
            {
                // MODIFIED: Use the passed-in 'dt' instead of Time.deltaTime
                _burstIntervalTimer += dt;
                if (_burstIntervalTimer >= _emissionSettings.BurstInterval)
                {
                    _isEmitting = true;
                    _burstIntervalTimer = 0f;
                }
                else
                {
                    _isEmitting = false;
                }
            }
            else
            {
                // Cooldown between bursts
                if (_burstTimer >= _emissionSettings.BurstDuration + _emissionSettings.BurstCooldown)
                {
                    _burstsEmitted++;
                    _burstTimer = 0f;
                    _burstIntervalTimer = 0f;
                }
                _isEmitting = false;
            }
        }
        else
        {
            _isPlaying = false;
            _isEmitting = false;
        }
    }


    void AddSmokeSource()
    {
        switch (_emissionSettings.Shape)
        {
            case SmokeEmissionSettingsJobs.EmissionShape.Circle:
                AddCircleEmission();
                break;
            case SmokeEmissionSettingsJobs.EmissionShape.Square:
                AddBoxEmission();
                break;
            case SmokeEmissionSettingsJobs.EmissionShape.Line:
                AddLineEmission();
                break;
            case SmokeEmissionSettingsJobs.EmissionShape.Ring:
                AddRingEmission();
                break;
            case SmokeEmissionSettingsJobs.EmissionShape.Custom:
                AddCustomShapeEmission();
                break;
        }
    }


    void AddCircleEmission()
    {
        int centerX = Mathf.RoundToInt(_emissionSettings.Position.x);
        int centerY = Mathf.RoundToInt(_emissionSettings.Position.y);
        int emissionRadius = Mathf.RoundToInt(_emissionSettings.Radius);

        for (int i = -emissionRadius; i <= emissionRadius; i++)
        {
            for (int j = -emissionRadius; j <= emissionRadius; j++)
            {
                int x = centerX + i;
                int y = centerY + j;

                if (x >= 1 && x <= GridSize && y >= 1 && y <= GridSize)
                {
                    float distance = Mathf.Sqrt(i * i + j * j);
                    if (distance <= _emissionSettings.Radius)
                    {
                        float falloff = CalculateFalloff(distance / _emissionSettings.Radius);
                        AddEmissionAtCell(x, y, falloff);
                    }
                }
            }
        }
    }


    void AddBoxEmission()
    {
        int centerX = Mathf.RoundToInt(_emissionSettings.Position.x);
        int centerY = Mathf.RoundToInt(_emissionSettings.Position.y);
        int halfWidth = Mathf.RoundToInt(_emissionSettings.BoxSize.x * 0.5f);
        int halfHeight = Mathf.RoundToInt(_emissionSettings.BoxSize.y * 0.5f);

        for (int i = -halfWidth; i <= halfWidth; i++)
        {
            for (int j = -halfHeight; j <= halfHeight; j++)
            {
                int x = centerX + i;
                int y = centerY + j;

                if (x >= 1 && x <= GridSize && y >= 1 && y <= GridSize)
                {
                    float xNorm = Mathf.Abs(i) / (float)halfWidth;
                    float yNorm = Mathf.Abs(j) / (float)halfHeight;
                    float edgeDist = Mathf.Max(xNorm, yNorm);
                    float falloff = CalculateFalloff(edgeDist);
                    AddEmissionAtCell(x, y, falloff);
                }
            }
        }
    }

    void AddLineEmission()
    {
        Vector2 start = _emissionSettings.LineStart;
        Vector2 end = _emissionSettings.LineEnd;
        float lineWidth = _emissionSettings.LineWidth;

        Vector2 direction = (end - start).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float length = Vector2.Distance(start, end);

        int steps = Mathf.CeilToInt(length);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 linePoint = Vector2.Lerp(start, end, i / (float)steps);
            int halfWidth = Mathf.CeilToInt(lineWidth * 0.5f);

            for (int j = -halfWidth; j <= halfWidth; j++)
            {
                Vector2 emissionPoint = linePoint + perpendicular * j;
                int x = Mathf.RoundToInt(emissionPoint.x);
                int y = Mathf.RoundToInt(emissionPoint.y);

                if (x >= 1 && x <= GridSize && y >= 1 && y <= GridSize)
                {
                    float normDist = Mathf.Abs(j) / (float)halfWidth;
                    float falloff = CalculateFalloff(normDist);
                    AddEmissionAtCell(x, y, falloff);
                }
            }
        }
    }


    void AddRingEmission()
    {
        int centerX = Mathf.RoundToInt(_emissionSettings.Position.x);
        int centerY = Mathf.RoundToInt(_emissionSettings.Position.y);
        int emissionRadius = Mathf.RoundToInt(_emissionSettings.Radius);
        float ringWidth = emissionRadius * _emissionSettings.RingSize;

        for (int i = -emissionRadius; i <= emissionRadius; i++)
        {
            for (int j = -emissionRadius; j <= emissionRadius; j++)
            {
                int x = centerX + i;
                int y = centerY + j;

                if (x >= 1 && x <= GridSize && y >= 1 && y <= GridSize)
                {
                    float distance = Mathf.Sqrt(i * i + j * j);
                    if (distance > emissionRadius - ringWidth && distance <= emissionRadius)
                    {
                        float ringPos = (distance - (emissionRadius - ringWidth)) / ringWidth;
                        float falloff = CalculateFalloff(Mathf.Abs(ringPos - 0.5f) * 2f);
                        AddEmissionAtCell(x, y, falloff);
                    }
                }
            }
        }
    }

    void AddCustomShapeEmission()
    {
        if (_emissionSettings.CustomShapeTexture == null) return;

        int centerX = Mathf.RoundToInt(_emissionSettings.Position.x);
        int centerY = Mathf.RoundToInt(_emissionSettings.Position.y);
        int texWidth = _emissionSettings.CustomShapeTexture.width;
        int texHeight = _emissionSettings.CustomShapeTexture.height;

        Color[] pixels = _emissionSettings.CustomShapeTexture.GetPixels();

        for (int i = 0; i < texWidth; i++)
        {
            for (int j = 0; j < texHeight; j++)
            {
                int x = centerX + i - texWidth / 2;
                int y = centerY + j - texHeight / 2;

                if (x >= 1 && x <= GridSize && y >= 1 && y <= GridSize)
                {
                    Color pixel = pixels[j * texWidth + i];
                    float alpha = _emissionSettings.UseTextureAlpha ? pixel.a : (pixel.r + pixel.g + pixel.b) / 3f;

                    if (alpha > 0.1f)
                    {
                        float falloff = alpha * CalculateFalloff(0);
                        AddEmissionAtCell(x, y, falloff);
                    }
                }
            }
        }
    }


    float CalculateFalloff(float normalizedDistance)
    {
        if (normalizedDistance > 1f) return 0f;

        if (normalizedDistance <= 1f - _emissionSettings.EdgeFalloff)
            return 1f;

        return 1f - ((normalizedDistance - (1f - _emissionSettings.EdgeFalloff)) / _emissionSettings.EdgeFalloff);
    }

    void AddEmissionAtCell(int x, int y, float falloff)
    {
        int index = x + y * (GridSize + 2);

        if (_boundarySampler != null && _staticBoundaryMask[index])
        {
            return;
        }

        float strengthMultiplier = 1f;
        if (!_emissionSettings.Continuous && _isEmitting)
        {
            strengthMultiplier = _emissionSettings.BurstStrengthMultiplier;
        }

        // Use the scaled fixed delta time for emission calculations.
        // The SpeedEmissionFactor is no longer used, as we directly scale the delta time.
        float dt = Time.fixedDeltaTime * _emissionSettings.SimulationSpeed;

        float densityAdded = _emissionSettings.Strength * falloff * dt * strengthMultiplier;
        _density[index] += densityAdded;

        // Mark as having emitted if we added any significant density
        if (densityAdded > 0.01f && !_hasEmitted)
        {
            _hasEmitted = true;
        }

        // Calculate velocity direction
        Vector2 velocityDir;
        if (_emissionSettings.RadialOutward)
        {
            Vector2 center = _emissionSettings.Position;
            Vector2 cellPos = new Vector2(x, y);
            Vector2 direction = cellPos - center;

            if (direction.magnitude > 0.1f)
            {
                velocityDir = direction.normalized;

                // Add slight tangential component to create more natural swirl
                Vector2 tangent = new Vector2(-direction.y, direction.x).normalized;
                velocityDir = (velocityDir + tangent * _emissionSettings.TangentialStrength).normalized;
            }
            else
            {
                velocityDir = UnityEngine.Random.insideUnitCircle.normalized;
            }
        }
        else if (_emissionSettings.UseRandomDirection)
        {
            velocityDir = UnityEngine.Random.insideUnitCircle.normalized;
        }
        else
        {
            velocityDir = _emissionSettings.VelocityDirection.normalized;
        }

        // Apply randomness to direction
        if (_emissionSettings.VelocityRandomness > 0)
        {
            float angle = UnityEngine.Random.Range(-_emissionSettings.VelocityRandomness, _emissionSettings.VelocityRandomness) * Mathf.Deg2Rad;
            velocityDir = new Vector2(
                velocityDir.x * Mathf.Cos(angle) - velocityDir.y * Mathf.Sin(angle),
                velocityDir.x * Mathf.Sin(angle) + velocityDir.y * Mathf.Cos(angle)
            );
        }

        float velocityStrength = _emissionSettings.ScaleVelocityWithStrength ?
            _emissionSettings.VelocityStrength * (_emissionSettings.Strength / 100f) :
            _emissionSettings.VelocityStrength;

        // Apply radial expansion speed multiplier
        if (_emissionSettings.RadialOutward)
        {
            velocityStrength *= _emissionSettings.RadialExpansionSpeed;
        }

        // Also use the scaled fixed delta time for adding velocity.
        _velocityX[index] += velocityDir.x * velocityStrength * falloff * dt;
        _velocityY[index] += velocityDir.y * velocityStrength * falloff * dt;
    }


    #endregion

    #region Helper methods

    public void Play()
    {
        _isEmissionHalted = false;
        _isPlaying = true;
        _burstsEmitted = 0;
        _burstTimer = 0f;
        _burstIntervalTimer = 0f;
        _stopActionTriggered = false;
        _hasEmitted = false;

        _isCurrentlyVisible = true;

        if (_emissionSettings.Continuous)
        {
            _isEmitting = true;
        }
    }

    public void Stop()
    {
        _isEmissionHalted = true;
    }

    private void ResetSimulation()
    {
        _currentJobHandle.Complete();

        JobHandle clearHandle = new JobHandle();
        clearHandle = new ClearArrayJob { array = _density }.Schedule((GridSize + 2) * (GridSize + 2), _batchSize, clearHandle);
        clearHandle = new ClearArrayJob { array = _prevDensity }.Schedule((GridSize + 2) * (GridSize + 2), _batchSize, clearHandle);
        clearHandle = new ClearArrayJob { array = _velocityX }.Schedule((GridSize + 2) * (GridSize + 2), _batchSize, clearHandle);
        clearHandle = new ClearArrayJob { array = _prevVelocityX }.Schedule((GridSize + 2) * (GridSize + 2), _batchSize, clearHandle);
        clearHandle = new ClearArrayJob { array = _velocityY }.Schedule((GridSize + 2) * (GridSize + 2), _batchSize, clearHandle);
        clearHandle = new ClearArrayJob { array = _prevVelocityY }.Schedule((GridSize + 2) * (GridSize + 2), _batchSize, clearHandle);
        clearHandle = new ClearArrayJob { array = _vorticity }.Schedule((GridSize + 2) * (GridSize + 2), _batchSize, clearHandle);

        var colorJob = new DensityToColorJob
        {
            density = this._density,
            pixels = this._pixelBuffer,
            gradientLookup = this._gradientLookupTable,
            N = this.GridSize
        };

        JobHandle finalHandle = colorJob.Schedule(GridSize * GridSize, _batchSize, clearHandle);

        _currentJobHandle = finalHandle;
        _currentJobHandle.Complete();
    }

    public void SpawnAndPlay(Vector2 worldPosition, Vector2 initialVelocityDirection)
    {
        // 1. Reset all simulation data to zero.
        ResetSimulation();

        // 2. Set the effect's new position and properties.
        _effectCenter = worldPosition;
        _emissionSettings.VelocityDirection = initialVelocityDirection;

        // 3. Run positioning logic to place the grid correctly.
        if (_useAutomaticPositioning)
        {
            PositionGridOptimallyForEffectCenter();
        }

        // 4. Re-bake the static boundary map for the new position.
        if (_boundarySampler != null)
        {
            _boundarySampler.InitializeBoundarySystem();
            _staticBoundaryMask = _boundarySampler.StaticBoundaryMask;
        }

        // 5. Update the gradient texture and other visuals.
        UpdateGradientLookupTable();
        UpdateFadeCurveLookupTable();
    }

    #endregion

    #region Events

    public void ReportVisibilityChange(bool isVisible)
    {
        if (PauseWhenOffscreen)
        {
            _isCurrentlyVisible = isVisible;
        }
    }

    #endregion

    #region Debug Visualizations

    void UpdateVelocityVisualization()
    {
        if (!_showVelocityField || _velocityLines == null) return;

        int skipStep = 4;
        int lineIndex = 0;
        float dynamicWidth = _velocityLineWidth * transform.lossyScale.x;

        for (int i = skipStep; i < GridSize; i += skipStep)
        {
            for (int j = skipStep; j < GridSize; j += skipStep)
            {
                if (lineIndex < _velocityLines.Length)
                {
                    LineRenderer currentLine = _velocityLines[lineIndex];
                    int dataIndex = (i + 1) + (j + 1) * (GridSize + 2);

                    float velX = _velocityX[dataIndex];
                    float velY = _velocityY[dataIndex];

                    Vector3 localPos = new Vector3(
                        (i - GridSize * 0.5f) * CellSize,
                        (j - GridSize * 0.5f) * CellSize,
                        0
                    );

                    Vector3 worldPos = transform.TransformPoint(localPos);

                    Vector3 vel = new Vector3(velX * _velocityLineLength, velY * _velocityLineLength, 0);

                    Vector3 worldVel = transform.TransformDirection(vel);

                    currentLine.SetPosition(0, worldPos);
                    currentLine.SetPosition(1, worldPos + worldVel);

                    currentLine.startWidth = dynamicWidth;
                    currentLine.endWidth = dynamicWidth;

                    lineIndex++;
                }
            }
        }
    }


    void CreateVelocityVisualization()
    {
        int skipStep = 4;
        int lineCount = (GridSize / skipStep) * (GridSize / skipStep);
        _velocityLines = new LineRenderer[lineCount];

        int index = 0;
        for (int i = skipStep; i < GridSize; i += skipStep)
        {
            for (int j = skipStep; j < GridSize; j += skipStep)
            {
                GameObject lineObj = new GameObject($"VelocityLine_{i}_{j}");
                lineObj.transform.parent = transform;

                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.useWorldSpace = true;

                _velocityLines[index++] = lr;
            }
        }
    }

    void CreateAdaptiveGridVisualization()
    {
        _adaptiveGridLines = new LineRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject lineObj = new GameObject($"AdaptiveGridLine_{i}");
            lineObj.transform.parent = transform;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = Color.blue;
            lr.endColor = Color.blue;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            _adaptiveGridLines[i] = lr;
        }
    }

    void CreateBoundaryVisualization()
    {
        // Create 8 lines: 4 for outer boundary, 4 for inner fade boundary
        _boundaryLines = new LineRenderer[8];

        for (int i = 0; i < 8; i++)
        {
            GameObject lineObj = new GameObject($"BoundaryLine_{i}");
            lineObj.transform.parent = transform;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = _boundaryColor;
            lr.endColor = _boundaryColor;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.positionCount = 2;
            lr.useWorldSpace = true;

            if (i >= 4)
            {
                lr.startColor = Color.yellow;
                lr.endColor = Color.yellow;
            }

            _boundaryLines[i] = lr;
        }

        UpdateBoundaryVisualization();
    }

    void UpdateBoundaryVisualization()
    {
        if (_boundaryLines == null || _boundaryLines.Length < 8) return;

        float halfWidth = GridSize * CellSize * 0.5f;
        float halfHeight = GridSize * CellSize * 0.5f;

        Vector3[] corners = new Vector3[4]
        {
        new Vector3(-halfWidth, -halfHeight, 0), // Bottom-left
        new Vector3(halfWidth, -halfHeight, 0),  // Bottom-right
        new Vector3(halfWidth, halfHeight, 0),   // Top-right
        new Vector3(-halfWidth, halfHeight, 0)   // Top-left
        };

        // Convert to world space
        for (int i = 0; i < 4; i++)
        {
            corners[i] = transform.TransformPoint(corners[i]);
        }

        // Set outer boundary lines
        for (int i = 0; i < 4; i++)
        {
            _boundaryLines[i].SetPosition(0, corners[i]);
            _boundaryLines[i].SetPosition(1, corners[(i + 1) % 4]);
        }

        // Draw inner fade boundary if enabled
        if (_enableBoundaryFade)
        {
            float fadeWidthWorld = FadeWidth * GridSize * CellSize;
            float innerHalfWidth = (GridSize * CellSize - fadeWidthWorld * 2) * 0.5f;
            float innerHalfHeight = (GridSize * CellSize - fadeWidthWorld * 2) * 0.5f;

            // Define inner corners in consistent clockwise order
            Vector3[] innerCorners = new Vector3[4]
            {
            // Consistent clockwise order starting from bottom-left
            transform.TransformPoint(new Vector3(-innerHalfWidth, -innerHalfHeight, 0)), // Bottom-left
            transform.TransformPoint(new Vector3(innerHalfWidth, -innerHalfHeight, 0)),  // Bottom-right
            transform.TransformPoint(new Vector3(innerHalfWidth, innerHalfHeight, 0)),   // Top-right
            transform.TransformPoint(new Vector3(-innerHalfWidth, innerHalfHeight, 0))   // Top-left
            };

            // Set inner boundary lines
            // Bottom line 
            _boundaryLines[4].SetPosition(0, innerCorners[0]);
            _boundaryLines[4].SetPosition(1, innerCorners[1]);

            // Right line 
            _boundaryLines[5].SetPosition(0, innerCorners[1]);
            _boundaryLines[5].SetPosition(1, innerCorners[2]);

            // Top line 
            _boundaryLines[6].SetPosition(0, innerCorners[2]);
            _boundaryLines[6].SetPosition(1, innerCorners[3]);

            // Left line 
            _boundaryLines[7].SetPosition(0, innerCorners[3]);
            _boundaryLines[7].SetPosition(1, innerCorners[0]);

            for (int i = 4; i < 8; i++)
            {
                _boundaryLines[i].enabled = true;
            }
        }
        else
        {
            for (int i = 4; i < 8; i++)
            {
                _boundaryLines[i].enabled = false;
            }
        }
    }

    void UpdateAdaptiveGridVisualization()
    {
        if (!_showAdaptiveGridBounds || _adaptiveGridLines == null || _adaptiveGridLines.Length < 4 || !_currentBounds.IsValid())
        {
            if (_adaptiveGridLines != null)
            {
                for (int i = 0; i < _adaptiveGridLines.Length; i++)
                {
                    if (_adaptiveGridLines[i] != null) _adaptiveGridLines[i].enabled = false;
                }
            }
            return;
        }

        for (int i = 0; i < 4; i++) { _adaptiveGridLines[i].enabled = true; }

        float minX = _currentBounds.minX - 1;
        float maxX = _currentBounds.maxX;
        float minY = _currentBounds.minY - 1;
        float maxY = _currentBounds.maxY;

        Vector2[] gridCorners = new Vector2[4]
        {
        new Vector2(minX, minY), // Bottom-left
        new Vector2(maxX, minY), // Bottom-right
        new Vector2(maxX, maxY), // Top-right
        new Vector2(minX, maxY)  // Top-left
        };

        // Convert corners from grid space to world space
        Vector3[] worldCorners = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            worldCorners[i] = transform.TransformPoint(GridToLocal(gridCorners[i]));
        }

        _adaptiveGridLines[0].SetPositions(new Vector3[] { worldCorners[0], worldCorners[1] });
        _adaptiveGridLines[1].SetPositions(new Vector3[] { worldCorners[1], worldCorners[2] });
        _adaptiveGridLines[2].SetPositions(new Vector3[] { worldCorners[2], worldCorners[3] });
        _adaptiveGridLines[3].SetPositions(new Vector3[] { worldCorners[3], worldCorners[0] });
    }

    // A helper function to convert grid coordinates to the simulation's local space
    Vector3 GridToLocal(Vector2 gridPos)
    {
        return new Vector3(
            (gridPos.x - GridSize * 0.5f) * CellSize,
            (gridPos.y - GridSize * 0.5f) * CellSize,
            0);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 scale = transform.lossyScale;
        Vector3 gridSize3D = new Vector3(GridSize * CellSize * scale.x, GridSize * CellSize * scale.y, 0);

        // --- Step 1: Determine the correct centers for the grid and the emission shapes ---
        Vector3 gridCenter;
        Vector3 emissionWorldPos;

        if (_useAutomaticPositioning && !Application.isPlaying)
        {
            Vector3 localEmissionPosUnscaled = new Vector3(
                (_emissionSettings.Position.x - GridSize * 0.5f) * CellSize,
                (_emissionSettings.Position.y - GridSize * 0.5f) * CellSize,
                0);

            Vector3 previewEffectCenter = transform.TransformPoint(localEmissionPosUnscaled);
            float bufferSize = GridSize * CellSize * _gridBuffer * scale.x;

            gridCenter = previewEffectCenter; 
            if (!_emissionSettings.RadialOutward && _emissionSettings.VelocityDirection != Vector2.zero && !_emissionSettings.UseRandomDirection)
            {
                Vector2 dir = _emissionSettings.VelocityDirection.normalized;
                gridCenter += transform.TransformDirection(new Vector3(dir.x, dir.y, 0f)) * bufferSize;
            }
            emissionWorldPos = previewEffectCenter;
        }
        else
        {
            gridCenter = transform.position;
            Vector3 localEmissionPos = new Vector3(
                (_emissionSettings.Position.x - GridSize * 0.5f) * CellSize,
                (_emissionSettings.Position.y - GridSize * 0.5f) * CellSize,
                0);
            emissionWorldPos = transform.TransformPoint(localEmissionPos);
        }

        // --- Step 2: Draw the Grid and its Fade Boundary ---
        // We set the Gizmos matrix to draw everything related to the grid at its correct center.
        Gizmos.matrix = Matrix4x4.TRS(gridCenter, transform.rotation, Vector3.one);

        if (_enableBoundaryFade)
        {
            // --- Case 1: Boundary Fade is ON ---
            // The outer fade area will be yellow, and the inner safe area will be cyan.

            Gizmos.color = new Color(1, 1, 0, 0.15f); 
            Gizmos.DrawCube(Vector3.zero, gridSize3D);

            float fadeAmountX = gridSize3D.x * FadeWidth;
            float fadeAmountY = gridSize3D.y * FadeWidth;
            Vector3 innerAreaSize = new Vector3(gridSize3D.x - (fadeAmountX * 2), gridSize3D.y - (fadeAmountY * 2), 0);

            Gizmos.color = new Color(0, 1, 1, 0.15f); 
            Gizmos.DrawCube(Vector3.zero, innerAreaSize);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, innerAreaSize);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, gridSize3D);
        }
        else
        {
            // --- Case 2: Boundary Fade is OFF ---
            // The entire grid is a single semi-transparent cyan color.

            // Draw the solid cyan fill.
            Gizmos.color = new Color(0, 1, 1, 0.15f);
            Gizmos.DrawCube(Vector3.zero, gridSize3D);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, gridSize3D);
        }

        Gizmos.matrix = Matrix4x4.identity;

        // --- Step 3: Draw the Emission Shapes and Marker ---
        Gizmos.color = Color.yellow;
        Handles.color = Color.yellow;

        switch (_emissionSettings.Shape)
        {
            case SmokeEmissionSettingsJobs.EmissionShape.Circle:
                Handles.matrix = Matrix4x4.TRS(emissionWorldPos, transform.rotation, scale);
                Handles.DrawWireDisc(Vector3.zero, Vector3.forward, _emissionSettings.Radius * CellSize);
                Handles.matrix = Matrix4x4.identity;
                break;

            case SmokeEmissionSettingsJobs.EmissionShape.Square:
                Gizmos.matrix = Matrix4x4.TRS(emissionWorldPos, transform.rotation, scale);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(_emissionSettings.BoxSize.x * CellSize, _emissionSettings.BoxSize.y * CellSize, 0));
                Gizmos.matrix = Matrix4x4.identity;
                break;

            case SmokeEmissionSettingsJobs.EmissionShape.Line:
                Vector3 localStart = new Vector3((_emissionSettings.LineStart.x - GridSize * 0.5f) * CellSize, (_emissionSettings.LineStart.y - GridSize * 0.5f) * CellSize, 0);
                Vector3 localEnd = new Vector3((_emissionSettings.LineEnd.x - GridSize * 0.5f) * CellSize, (_emissionSettings.LineEnd.y - GridSize * 0.5f) * CellSize, 0);
                Vector3 worldStart = transform.TransformPoint(localStart);
                Vector3 worldEnd = transform.TransformPoint(localEnd);
                Gizmos.DrawLine(worldStart, worldEnd);
                break;

            case SmokeEmissionSettingsJobs.EmissionShape.Ring:
                Handles.matrix = Matrix4x4.TRS(emissionWorldPos, transform.rotation, scale);
                Handles.DrawWireDisc(Vector3.zero, Vector3.forward, _emissionSettings.Radius * CellSize);

                float innerRadius = _emissionSettings.Radius * (1.0f - _emissionSettings.RingSize);
                Handles.DrawWireDisc(Vector3.zero, Vector3.forward, innerRadius * CellSize);

                Handles.matrix = Matrix4x4.identity;
                break;

            case SmokeEmissionSettingsJobs.EmissionShape.Custom:
                if (_emissionSettings.CustomShapeTexture != null)
                {
                    Vector3 texSize = new Vector3(_emissionSettings.CustomShapeTexture.width * CellSize, _emissionSettings.CustomShapeTexture.height * CellSize, 0);
                    Gizmos.matrix = Matrix4x4.TRS(emissionWorldPos, transform.rotation, scale);
                    Gizmos.DrawWireCube(Vector3.zero, texSize);
                    Gizmos.matrix = Matrix4x4.identity;
                }
                break;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(emissionWorldPos, 0.1f * Mathf.Max(scale.x, scale.y));
    }

#endif


    #endregion

}

#region Job Structs

public struct GridBounds
{
    public int minX, maxX, minY, maxY;
    public int width, height;

    public GridBounds(int minX, int maxX, int minY, int maxY)
    {
        this.minX = minX;
        this.maxX = maxX;
        this.minY = minY;
        this.maxY = maxY;
        this.width = maxX - minX + 1;
        this.height = maxY - minY + 1;
    }

    public bool Contains(int x, int y)
    {
        return x >= minX && x <= maxX && y >= minY && y <= maxY;
    }

    public bool IsValid()
    {
        return minX <= maxX && minY <= maxY;
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct RedBlackLinearSolveJob : IJobParallelFor
{
    [ReadOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> x0;
    [NativeDisableParallelForRestriction] public NativeArray<float> x;

    public float a, c;
    public int N;
    public bool isRedPass;
    public bool useAdaptiveGrid;
    public GridBounds bounds;

    public void Execute(int index)
    {
        int x_coord;
        int y_coord;

        if (useAdaptiveGrid)
        {
            // The job is scheduled over a rectangle of size bounds.width * bounds.height
            // We decode the 1D index into local coordinates within that rectangle.
            int local_i = index % bounds.width;
            int local_j = index / bounds.width;

            // Then convert to global grid coordinates.
            x_coord = bounds.minX + local_i;
            y_coord = bounds.minY + local_j;
        }
        else
        {
            x_coord = index % (N + 2);
            y_coord = index / (N + 2);
        }

        if (x_coord == 0 || x_coord > N || y_coord == 0 || y_coord > N) return;

        bool cellIsRed = (x_coord + y_coord) % 2 == 0;
        if (cellIsRed != isRedPass) return;

        int global_idx = x_coord + y_coord * (N + 2);
        x[global_idx] = (x0[global_idx] + a * (
            x[global_idx - 1] +
            x[global_idx + 1] +
            x[global_idx - (N + 2)] +
            x[global_idx + (N + 2)]
        )) / c;
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct BoundaryJob : IJob
{
    public NativeArray<float> field;
    public int N;
    public int boundaryType;
    public int offset;

    public void Execute()
    {
        // Set X-Walls
        for (int i = 1; i <= N; i++)
        {
            int y_index = i * (N + 2);
            if (boundaryType == 1)
            {
                field[offset + 0 + y_index] = -field[offset + 1 + y_index];
                field[offset + (N + 1) + y_index] = -field[offset + N + y_index];
            }
            else
            {
                field[offset + 0 + y_index] = field[offset + 1 + y_index];
                field[offset + (N + 1) + y_index] = field[offset + N + y_index];
            }
        }

        // Set Y-Walls
        for (int i = 1; i <= N; i++)
        {
            if (boundaryType == 2)
            {
                field[offset + i + 0 * (N + 2)] = -field[offset + i + 1 * (N + 2)];
                field[offset + i + (N + 1) * (N + 2)] = -field[offset + i + N * (N + 2)];
            }
            else
            {
                field[offset + i + 0 * (N + 2)] = field[offset + i + 1 * (N + 2)];
                field[offset + i + (N + 1) * (N + 2)] = field[offset + i + N * (N + 2)];
            }
        }

        // Set Corners
        field[offset + 0 + 0 * (N + 2)] = 0.5f * (field[offset + 1] + field[offset + 0 + (N + 2)]);
        field[offset + 0 + (N + 1) * (N + 2)] = 0.5f * (field[offset + 1 + (N + 1) * (N + 2)] + field[offset + 0 + N * (N + 2)]);
        field[offset + (N + 1) + 0 * (N + 2)] = 0.5f * (field[offset + N] + field[offset + (N + 1) + (N + 2)]);
        field[offset + (N + 1) + (N + 1) * (N + 2)] = 0.5f * (field[offset + N + (N + 1) * (N + 2)] + field[offset + (N + 1) + N * (N + 2)]);
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct CopyArrayJob : IJobParallelFor
{
    [ReadOnly, NoAlias] public NativeArray<float> source;
    [WriteOnly, NoAlias] public NativeArray<float> destination;

    public void Execute(int index)
    {
        destination[index] = source[index];
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct VelocitySpongeJob : IJobParallelFor
{
    [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> velocityX;
    [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> velocityY;
    public int N;
    public float fadeWidth;
    public float damping;

    public void Execute(int index)
    {
        int x = index % (N + 2);
        int y = index / (N + 2);

        if (x > 0 && x <= N && y > 0 && y <= N)
        {
            int fadeZone = (int)(N * fadeWidth);
            if (fadeZone <= 0) return;

            int distFromEdge = math.min(math.min(x - 1, N - x), math.min(y - 1, N - y));

            if (distFromEdge < fadeZone)
            {
                float t = 1.0f - ((float)distFromEdge / fadeZone);
                t = math.saturate(t);

                float spongeStrength = t * t * damping;
                float multiplier = math.max(0, 1.0f - spongeStrength);

                velocityX[index] *= multiplier;
                velocityY[index] *= multiplier;
            }
        }
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct DensityFalloffJob : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float> density;
    [ReadOnly, NoAlias] public NativeArray<float> curveLookup;
    public int N;
    public float fadeWidth;
    public bool useAdaptiveGrid;
    public GridBounds bounds;

    public void Execute(int index)
    {
        int x, y;
        if (useAdaptiveGrid)
        {
            int local_i = index % bounds.width;
            int local_j = index / bounds.width;
            x = bounds.minX + local_i;
            y = bounds.minY + local_j;
        }
        else
        {
            x = index % (N + 2);
            y = index / (N + 2);
        }

        int global_idx = x + y * (N + 2);

        if (x > 0 && x <= N && y > 0 && y <= N)
        {
            int fadeZone = (int)(N * fadeWidth);
            if (fadeZone <= 1) return;

            int distFromEdge = math.min(math.min(x - 1, N - x), math.min(y - 1, N - y));

            if (distFromEdge < fadeZone)
            {
                // 1. Calculate our progress 't' through the fade zone.
                // This now correctly goes from 0.0 (at the inner edge) to 1.0 (at the absolute border).
                float t = 1.0f - ((float)distFromEdge / (fadeZone - 1));
                t = math.saturate(t);

                // 2. Sample the curve directly with 't' to get the fade amount.
                // No more inverting the lookup! This is now simple and intuitive.
                int lookupIndex = (int)(t * 255);
                lookupIndex = math.clamp(lookupIndex, 0, 255);
                float fadeAmount = curveLookup[lookupIndex];

                // 3. Apply the fade to the density.
                density[global_idx] *= fadeAmount;
            }
        }
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct ClearArrayJob : IJobParallelFor
{
    [WriteOnly] public NativeArray<float> array;

    public void Execute(int index)
    {
        array[index] = 0f;
    }
}


[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct StaticBoundaryJob : IJobParallelFor
{
    [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> velocityX;
    [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> velocityY;
    [NativeDisableParallelForRestriction] public NativeArray<float> density;
    [ReadOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<bool> mask;

    public FluidBoundarySampler.BoundaryType boundaryType;
    public float dampingFactor;
    public int N;
    public bool useAdaptiveGrid;
    public GridBounds bounds;

    public void Execute(int index)
    {
        int x, y;
        if (useAdaptiveGrid)
        {
            int local_i = index % bounds.width;
            int local_j = index / bounds.width;
            x = bounds.minX + local_i;
            y = bounds.minY + local_j;
        }
        else
        {
            x = index % (N + 2);
            y = index / (N + 2);
        }

        int global_idx = x + y * (N + 2);

        // Make sure we don't process outside the valid grid area
        if (x > N || y > N || x < 1 || y < 1) return;

        // --- Logic for cells that ARE boundaries ---
        if (mask[global_idx])
        {
            switch (boundaryType)
            {
                case FluidBoundarySampler.BoundaryType.Solid:
                    velocityX[global_idx] = 0;
                    velocityY[global_idx] = 0;
                    density[global_idx] = 0;
                    break;

                case FluidBoundarySampler.BoundaryType.Damping:
                    velocityX[global_idx] *= dampingFactor;
                    velocityY[global_idx] *= dampingFactor;
                    break;

                case FluidBoundarySampler.BoundaryType.Absorbing:
                    velocityX[global_idx] *= 0.1f;
                    velocityY[global_idx] *= 0.1f;
                    density[global_idx] *= 0.8f;
                    break;

                case FluidBoundarySampler.BoundaryType.Reflective:
                    velocityX[global_idx] = 0;
                    velocityY[global_idx] = 0;
                    density[global_idx] = 0;
                    break;
            }
        }
        else if (boundaryType == FluidBoundarySampler.BoundaryType.Reflective)
        {
            if (mask[global_idx + 1])
            {
                velocityX[global_idx] = -math.abs(velocityX[global_idx]);
            }
            if (mask[global_idx - 1])
            {
                velocityX[global_idx] = math.abs(velocityX[global_idx]);
            }
            if (mask[global_idx + (N + 2)])
            {
                velocityY[global_idx] = -math.abs(velocityY[global_idx]);
            }
            if (mask[global_idx - (N + 2)])
            {
                velocityY[global_idx] = math.abs(velocityY[global_idx]);
            }
        }
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct DynamicBoundaryJob : IJobParallelFor
{
    [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> velocityX;
    [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> velocityY;
    [ReadOnly, NoAlias] public NativeArray<JosStamSmokeEmitterJobs.DynamicBoundaryData> dynamicBoundaries;
    public float3 gridOrigin;
    public float cellSize;
    public int N;
    public bool useAdaptiveGrid;
    public GridBounds bounds;

    public void Execute(int index)
    {
        int x, y;
        if (useAdaptiveGrid)
        {
            int local_i = index % bounds.width;
            int local_j = index / bounds.width;
            x = bounds.minX + local_i;
            y = bounds.minY + local_j;
        }
        else
        {
            x = index % (N + 2);
            y = index / (N + 2);
        }

        int global_idx = x + y * (N + 2);
        if (x == 0 || x > N || y == 0 || y > N) return;

        float3 cellWorldPos = gridOrigin + new float3(
        (x - (N / 2f) - 0.5f) * cellSize,
        (y - (N / 2f) - 0.5f) * cellSize,
        0);

        for (int i = 0; i < dynamicBoundaries.Length; i++)
        {
            DynamicBoundaryData boundary = dynamicBoundaries[i];

            if (cellWorldPos.x >= boundary.bounds.min.x && cellWorldPos.x <= boundary.bounds.max.x &&
                cellWorldPos.y >= boundary.bounds.min.y && cellWorldPos.y <= boundary.bounds.max.y)
            {
                velocityX[global_idx] += boundary.velocity.x * boundary.velocityInfluence;
                velocityY[global_idx] += boundary.velocity.y * boundary.velocityInfluence;
            }
        }
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct DensityToColorJob : IJobParallelFor
{
    [ReadOnly, NoAlias] public NativeArray<float> density;
    [ReadOnly, NoAlias] public NativeArray<Color32> gradientLookup;
    [WriteOnly, NoAlias] public NativeArray<Color32> pixels;
    public int N;

    public void Execute(int index)
    {
        int x = index % N + 1;
        int y = index / N + 1;
        int densityIndex = x + y * (N + 2);

        float densityValue = math.saturate(density[densityIndex] / 10f);
        int gradientIndex = (int)(densityValue * 255);

        pixels[index] = gradientLookup[gradientIndex];
    }
}


[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct CalculateVorticityJob : IJobParallelFor
{
    [ReadOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> velocityX;
    [ReadOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> velocityY;
    [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> vorticity;
    public int N;
    public bool useAdaptiveGrid;
    public GridBounds bounds;

    public void Execute(int index)
    {
        int x, y;
        if (useAdaptiveGrid)
        {
            int local_i = index % bounds.width;
            int local_j = index / bounds.width;
            x = bounds.minX + local_i;
            y = bounds.minY + local_j;
        }
        else
        {
            x = index % (N + 2);
            y = index / (N + 2);
        }

        int global_idx = x + y * (N + 2);

        if (x > 0 && x < N + 1 && y > 0 && y < N + 1)
        {
            float v_x_top = velocityX[global_idx + (N + 2)];
            float v_x_bottom = velocityX[global_idx - (N + 2)];
            float v_y_right = velocityY[global_idx + 1];
            float v_y_left = velocityY[global_idx - 1];

            vorticity[global_idx] = (v_y_right - v_y_left) - (v_x_top - v_x_bottom);
        }
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct ApplyVorticityJob : IJobParallelFor
{
    [ReadOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> vorticity;
    [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> velocityX;
    [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> velocityY;
    public float vorticityStrength;
    public float dt;
    public int N;
    public bool useAdaptiveGrid;
    public GridBounds bounds;

    public void Execute(int index)
    {
        int x, y;
        if (useAdaptiveGrid)
        {
            int local_i = index % bounds.width;
            int local_j = index / bounds.width;
            x = bounds.minX + local_i;
            y = bounds.minY + local_j;
        }
        else
        {
            x = index % (N + 2);
            y = index / (N + 2);
        }

        int global_idx = x + y * (N + 2);

        if (x > 0 && x < N + 1 && y > 0 && y < N + 1)
        {
            // Calculate the gradient of the vorticity magnitude
            float v_mag_right = math.abs(vorticity[global_idx + 1]);
            float v_mag_left = math.abs(vorticity[global_idx - 1]);
            float v_mag_top = math.abs(vorticity[global_idx + (N + 2)]);
            float v_mag_bottom = math.abs(vorticity[global_idx - (N + 2)]);

            // 1. Calculate the gradient components as simple scalars.
            float grad_x = (v_mag_right - v_mag_left) * 0.5f;
            float grad_y = (v_mag_top - v_mag_bottom) * 0.5f;

            // 2. Calculate the squared length directly from the scalar components.
            //    This avoids creating an intermediate 'gradient' vector.
            float lenSq = grad_x * grad_x + grad_y * grad_y;

            // 3. Perform the check on the scalar value.
            if (lenSq > 0.00001f) // Use a small epsilon to avoid division by zero
            {
                // 4. Normalize the vector using scalar operations.
                //    math.rsqrt() is a fast inverse square root.
                float invLen = math.rsqrt(lenSq);
                float N_vec_x = grad_x * invLen;
                float N_vec_y = grad_y * invLen;

                float vort = vorticity[global_idx];

                // 5. Calculate the final force scale.
                float scale = vort * vorticityStrength * dt;

                // 6. Apply the perpendicular force directly to the velocity fields.
                //    This avoids creating an intermediate 'force' vector.
                velocityX[global_idx] += N_vec_y * scale;
                velocityY[global_idx] += -N_vec_x * scale;
            }
        }
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct CopyRegionJob : IJob
{
    [ReadOnly] public NativeArray<float> source;
    public NativeArray<float> destination;

    public GridBounds sourceBounds;

    public int sourceGridSize;
    public int destGridSize;

    public int destOffsetX;
    public int destOffsetY;

    public void Execute()
    {
        for (int j = 0; j < sourceBounds.height; j++)
        {
            for (int i = 0; i < sourceBounds.width; i++)
            {
                // Calculate source index from the region's min corner
                int sourceX = sourceBounds.minX + i;
                int sourceY = sourceBounds.minY + j;
                int sourceIndex = sourceX + sourceY * (sourceGridSize + 2);

                // Calculate destination index, including the copy-back offset
                int destX = destOffsetX + i + 1; // +1 for border
                int destY = destOffsetY + j + 1; // +1 for border
                int destIndex = destX + destY * (destGridSize + 2);

                if (sourceIndex < source.Length && destIndex < destination.Length)
                {
                    destination[destIndex] = source[sourceIndex];
                }
            }
        }
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct DivergenceInBoundsJob : IJobParallelFor
{
    [NativeDisableParallelForRestriction][ReadOnly, NoAlias] public NativeArray<float> u;
    [NativeDisableParallelForRestriction][ReadOnly, NoAlias] public NativeArray<float> v;
    [NativeDisableParallelForRestriction][WriteOnly, NoAlias] public NativeArray<float> div;
    public int N;
    public float h;
    [ReadOnly] public GridBounds bounds;

    public void Execute(int index)
    {
        int local_i = index % bounds.width;
        int local_j = index / bounds.width;

        int x = bounds.minX + local_i;
        int y = bounds.minY + local_j;
        int global_idx = x + y * (N + 2);

        div[global_idx] = -0.5f * (u[global_idx + 1] - u[global_idx - 1] + v[global_idx + (N + 2)] - v[global_idx - (N + 2)]) / h;
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct PressureGradInBoundsJob : IJobParallelFor
{
    [NativeDisableParallelForRestriction][ReadOnly, NoAlias] public NativeArray<float> p;
    [NativeDisableParallelForRestriction][NoAlias] public NativeArray<float> u;
    [NativeDisableParallelForRestriction][NoAlias] public NativeArray<float> v;
    public int N;
    public float h;
    [ReadOnly] public GridBounds bounds;

    public void Execute(int index)
    {
        int local_i = index % bounds.width;
        int local_j = index / bounds.width;

        int x = bounds.minX + local_i;
        int y = bounds.minY + local_j;
        int global_idx = x + y * (N + 2);

        u[global_idx] -= 0.5f * (p[global_idx + 1] - p[global_idx - 1]) * h;
        v[global_idx] -= 0.5f * (p[global_idx + (N + 2)] - p[global_idx - (N + 2)]) * h;
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct AdvectInBoundsJob : IJobParallelFor
{
    [ReadOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> d0;
    [ReadOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> u;
    [ReadOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> v;
    [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> d;
    public float dt;
    public int N;
    [ReadOnly] public GridBounds bounds;

    public void Execute(int index)
    {
        // Decode the 1D index into local coordinates within the bounds
        int local_i = index % bounds.width;
        int local_j = index / bounds.width;

        // Convert to global grid coordinates
        int i = bounds.minX + local_i;
        int j = bounds.minY + local_j;
        int global_idx = i + j * (N + 2);

        // Standard advection logic
        float x = i - dt * u[global_idx];
        float y = j - dt * v[global_idx];

        x = math.clamp(x, 0.5f, N + 0.5f);
        y = math.clamp(y, 0.5f, N + 0.5f);

        int i0 = (int)math.floor(x);
        int i1 = i0 + 1;
        int j0 = (int)math.floor(y);
        int j1 = j0 + 1;

        float s1 = x - i0;
        float s0 = 1 - s1;
        float t1 = y - j0;
        float t0 = 1 - t1;

        float d00 = d0[i0 + j0 * (N + 2)];
        float d01 = d0[i0 + j1 * (N + 2)];
        float d10 = d0[i1 + j0 * (N + 2)];
        float d11 = d0[i1 + j1 * (N + 2)];

        d[global_idx] = s0 * (t0 * d00 + t1 * d01) + s1 * (t0 * d10 + t1 * d11);
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
struct DecayInBoundsJob : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float> density;
    public float decayFactor;
    public float linearDecay;
    public int N;
    [ReadOnly] public GridBounds bounds;

    public void Execute(int index)
    {
        int local_i = index % bounds.width;
        int local_j = index / bounds.width;

        int i = bounds.minX + local_i;
        int j = bounds.minY + local_j;
        int global_idx = i + j * (N + 2); 

        // Apply multiplicative decay first, then subtractive
        float newDensity = density[global_idx] * decayFactor;
        newDensity -= linearDecay;

        // Ensure density doesn't go below zero
        density[global_idx] = math.max(0f, newDensity);
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct FindBoundsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> density;
    [WriteOnly] public NativeArray<int4> results;

    public int N;
    public float densityThreshold;
    public int itemsPerJob;

    public void Execute(int index)
    {
        // 1. Establish the start and end points for this specific job in the main density array
        int start = index * itemsPerJob;
        int end = math.min(start + itemsPerJob, (N + 2) * (N + 2));

        // 2. Initialize local min/max values. Min starts high, max starts low.
        int minX = N + 2, maxX = -1, minY = N + 2, maxY = -1;

        // 3. Iterate ONLY through this job's assigned chunk
        for (int i = start; i < end; i++)
        {
            if (density[i] > densityThreshold)
            {
                int x = i % (N + 2);
                int y = i / (N + 2);

                minX = math.min(minX, x);
                maxX = math.max(maxX, x);
                minY = math.min(minY, y);
                maxY = math.max(maxY, y);
            }
        }

        // 4. Write the result for this chunk into its unique spot in the results array
        results[index] = new int4(minX, maxX, minY, maxY);
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct DampenInactiveVelocityJob : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float> velocityX;
    [NativeDisableParallelForRestriction] public NativeArray<float> velocityY;

    public int N;
    [ReadOnly] public GridBounds bounds;
    public float dampingFactor;

    public void Execute(int index)
    {
        int x = index % (N + 2);
        int y = index / (N + 2);

        if (x > 0 && x <= N && y > 0 && y <= N)
        {
            // The core logic: if the cell is OUTSIDE the active bounds, dampen its velocity.
            if (x < bounds.minX || x > bounds.maxX || y < bounds.minY || y > bounds.maxY)
            {
                velocityX[index] *= dampingFactor;
                velocityY[index] *= dampingFactor;
            }
        }
    }
}

#endregion


#region Editor Script
#if UNITY_EDITOR

[CustomEditor(typeof(JosStamSmokeEmitterJobs))]
public class JosStamSmokeEmitterJobsEditor : Editor
{
    #region Serialized Properties
    private SerializedProperty showControls;
    private SerializedProperty showEmissionSettings;
    private SerializedProperty showEmissionPosition;
    private SerializedProperty showEmissionShape;
    private SerializedProperty showEmissionProperties;
    private SerializedProperty showRadialControls;
    private SerializedProperty showTemporalControl;
    private SerializedProperty showStopBehavior;
    private SerializedProperty showGridSettings;
    private SerializedProperty showSmokeProperties;
    private SerializedProperty showBoundaryFade;
    private SerializedProperty showVorticityFade;
    private SerializedProperty showGridPositioning;
    private SerializedProperty showAdaptiveGrid;
    private SerializedProperty showReferences;
    private SerializedProperty showPerformanceOptimization;
    private SerializedProperty showDebug;
    #endregion

    // Style variables
    private GUIStyle boxStyle;
    private GUIStyle foldoutStyle;
    private bool stylesInitialized = false;

    private void OnEnable()
    {
        var foldoutStatesProp = serializedObject.FindProperty("_foldoutStates");

        // Link each SerializedProperty to its corresponding field
        showControls = foldoutStatesProp.FindPropertyRelative("showControls");
        showEmissionSettings = foldoutStatesProp.FindPropertyRelative("showEmissionSettings");
        showEmissionPosition = foldoutStatesProp.FindPropertyRelative("showEmissionPosition");
        showEmissionShape = foldoutStatesProp.FindPropertyRelative("showEmissionShape");
        showEmissionProperties = foldoutStatesProp.FindPropertyRelative("showEmissionProperties");
        showRadialControls = foldoutStatesProp.FindPropertyRelative("showRadialControls");
        showTemporalControl = foldoutStatesProp.FindPropertyRelative("showTemporalControl");
        showStopBehavior = foldoutStatesProp.FindPropertyRelative("showStopBehavior");
        showGridSettings = foldoutStatesProp.FindPropertyRelative("showGridSettings");
        showSmokeProperties = foldoutStatesProp.FindPropertyRelative("showSmokeProperties");
        showBoundaryFade = foldoutStatesProp.FindPropertyRelative("showBoundaryFade");
        showVorticityFade = foldoutStatesProp.FindPropertyRelative("showVorticityFade");
        showGridPositioning = foldoutStatesProp.FindPropertyRelative("showGridPositioning");
        showAdaptiveGrid = foldoutStatesProp.FindPropertyRelative("showAdaptiveGrid");
        showReferences = foldoutStatesProp.FindPropertyRelative("showReferences");
        showPerformanceOptimization = foldoutStatesProp.FindPropertyRelative("showPerformanceOptimization");
        showDebug = foldoutStatesProp.FindPropertyRelative("showDebug");
    }

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

        JosStamSmokeEmitterJobs emitter = (JosStamSmokeEmitterJobs)target;

        GUILayout.Space(5);

        // Controls Section
        DrawSection("Controls", showControls, () =>
        {
            GUILayout.Space(5);
            if (GUILayout.Button("Play", GUILayout.Height(30)))
            {
                emitter.Play();
            }
            GUILayout.Space(5);
            if (GUILayout.Button("Stop", GUILayout.Height(30)))
            {
                emitter.Stop();
            }
            GUILayout.Space(5);
        });

        // Emission Settings Section (contains sub-sections)
        DrawSection("Emission Settings", showEmissionSettings, () =>
        {
            var emissionSettingsProp = serializedObject.FindProperty("_emissionSettings");

            // Emission Position Sub-section
            DrawSubSection("Emission Position", showEmissionPosition, () =>
            {
                EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("Position"));
            });

            // Emission Shape Sub-section
            DrawSubSection("Emission Shape", showEmissionShape, () =>
            {
                var shapeProp = emissionSettingsProp.FindPropertyRelative("Shape");
                EditorGUILayout.PropertyField(shapeProp);

                var shapeValue = (SmokeEmissionSettingsJobs.EmissionShape)shapeProp.enumValueIndex;

                EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("EdgeFalloff"));

                // Conditional fields based on shape
                switch (shapeValue)
                {
                    case SmokeEmissionSettingsJobs.EmissionShape.Circle:
                        EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("Radius"));
                        break;
                    case SmokeEmissionSettingsJobs.EmissionShape.Ring:
                        EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("Radius"));
                        EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("RingSize"));
                        break;
                    case SmokeEmissionSettingsJobs.EmissionShape.Square:
                        EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("BoxSize"));
                        break;
                    case SmokeEmissionSettingsJobs.EmissionShape.Line:
                        EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("LineStart"));
                        EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("LineEnd"));
                        EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("LineWidth"));
                        break;
                    case SmokeEmissionSettingsJobs.EmissionShape.Custom:
                        EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("CustomShapeTexture"));
                        EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("UseTextureAlpha"));
                        break;
                }
            });

            // Emission Properties Sub-section
            DrawSubSection("Emission Properties", showEmissionProperties, () =>
            {
                EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("PlayOnAwake"));
                EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("SimulationSpeed"));
                EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("SpeedEmissionFactor"));
                EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("Substeps"));
                EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("Strength"));
                EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("VelocityStrength"));

                var useRandomDirProp = emissionSettingsProp.FindPropertyRelative("UseRandomDirection");
                EditorGUILayout.PropertyField(useRandomDirProp);

                if (!useRandomDirProp.boolValue)
                {
                    EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("VelocityDirection"));
                    EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("VelocityRandomness"));
                }

                EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("ScaleVelocityWithStrength"));
            });

            // Radial Controls Sub-section
            DrawSubSection("Radial Controls", showRadialControls, () =>
            {
                var radialOutwardProp = emissionSettingsProp.FindPropertyRelative("RadialOutward");
                EditorGUILayout.PropertyField(radialOutwardProp);

                if (radialOutwardProp.boolValue)
                {
                    EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("TangentialStrength"));
                    EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("RadialExpansionSpeed"));
                }
            });

            // Temporal Control Sub-section
            DrawSubSection("Temporal Control", showTemporalControl, () =>
            {
                var continuousProp = emissionSettingsProp.FindPropertyRelative("Continuous");
                EditorGUILayout.PropertyField(continuousProp);

                if (!continuousProp.boolValue)
                {
                    EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("BurstDuration"));
                    EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("BurstCooldown"));
                    EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("BurstCount"));
                    EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("BurstInterval"));
                    EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("BurstStrengthMultiplier"));
                    EditorGUILayout.PropertyField(emissionSettingsProp.FindPropertyRelative("BurstEmissionSteps"));
                }
            });
        });

        // Stop Behavior Section
        DrawSection("Stop Behavior", showStopBehavior, () =>
        {
            SerializedProperty stopActionProp = serializedObject.FindProperty("stopAction");
            EditorGUILayout.PropertyField(stopActionProp);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("DensityThreshold"));

            if (stopActionProp.enumValueIndex == (int)JosStamSmokeEmitterJobs.StopAction.Callback)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("OnStopped"));
        });

        // Grid Settings Section
        DrawSection("Grid Settings", showGridSettings, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("GridSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("CellSize"));
        });

        // Smoke Properties Section
        DrawSection("Smoke Properties", showSmokeProperties, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SmokeGradient"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_viscosity"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_diffusion"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_timeStep"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_smokeDecay"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_linearSmokeDecay"));
        });

        // Vorticity Section
        DrawSection("Optional Style - Vorticity", showVorticityFade, () =>
        {
            var enableVorticityProp = serializedObject.FindProperty("_useVorticity");
            EditorGUILayout.PropertyField(enableVorticityProp);

            if (enableVorticityProp.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_vorticityStrength"));
            }
        });

        // Boundary Fade Section
        DrawSection("Boundary Fade", showBoundaryFade, () =>
        {
            var enableBoundaryFadeProp = serializedObject.FindProperty("_enableBoundaryFade");
            EditorGUILayout.PropertyField(enableBoundaryFadeProp);

            if (enableBoundaryFadeProp.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("FadeWidth"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("FadeCurve"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("VelocityDamping"));
            }
        });

        // Grid Positioning Section
        DrawSection("Grid Positioning", showGridPositioning, () =>
        {
            var useAutoPosProp = serializedObject.FindProperty("_useAutomaticPositioning");
            EditorGUILayout.PropertyField(useAutoPosProp);

            if (useAutoPosProp.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_gridBuffer"));
            }
        });

        // Adaptive Grid Section
        DrawSection("Adaptive Grid Settings", showAdaptiveGrid, () =>
        {
            var useAdaptiveGridProp = serializedObject.FindProperty("_useAdaptiveGrid");
            EditorGUILayout.PropertyField(useAdaptiveGridProp);

            if (useAdaptiveGridProp.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_gridPadding"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_densityThresholdForGrid"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_minGridSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_inactiveRegionDamping"));
            }
        });

        // References Section
        DrawSection("References", showReferences, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_spritePrefab"));
        });

        // Performance Optimization Section
        DrawSection("ADVANCED Performance Optimization", showPerformanceOptimization, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_showPerformanceReadout"));
            var useJobsBatchSize = serializedObject.FindProperty("_changeJobsBatchSize");
            EditorGUILayout.PropertyField(useJobsBatchSize);
            if (useJobsBatchSize.boolValue)
            {
                EditorGUILayout.HelpBox("WARNING: Batch size should be a power of 2, and should ONLY be touched if you know what you're doing.", MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_batchSize"));
            }
            var smootherProp = serializedObject.FindProperty("_smootherType");
            EditorGUILayout.PropertyField(smootherProp);
            var smootherValue = (JosStamSmokeEmitterJobs.SmootherType)smootherProp.enumValueIndex;

            switch (smootherValue)
            {
                case JosStamSmokeEmitterJobs.SmootherType.Jacobi:
                    EditorGUILayout.HelpBox("Jacobi is often FASTER per-iteration due to being more SIMD-friendly, but may require more pre and post smoothing sweeps for a visually stable result.", MessageType.Info);
                    break;
                case JosStamSmokeEmitterJobs.SmootherType.RedBlackGaussSeidel:
                    EditorGUILayout.HelpBox("Red-Black Gauss-Seidel converges faster algorithmically and can produce a 'SHARPER' look, but may be slower per-iteration than Jacobi.", MessageType.Info);
                    break;
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_diffuseIterations"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_mgPreSmoothIterations"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_mgPostSmoothIterations"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_mgBottomSolveIterations"));
            EditorGUILayout.Space(10f);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("PauseWhenOffscreen"));
        });

        // Debug Section
        DrawSection("Debug Visualizations", showDebug, () =>
        {
            var showVelocityFieldProp = serializedObject.FindProperty("_showVelocityField");  
            EditorGUILayout.PropertyField(showVelocityFieldProp);

            if (showVelocityFieldProp.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_velocityLineWidth"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_velocityLineLength"));                
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_showBoundaries"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_showAdaptiveGridBounds"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_boundaryColor"));
        });

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSection(string title, SerializedProperty foldout, System.Action content)
    {
        EditorGUILayout.BeginVertical(boxStyle);

        // Use the property's boolValue for the foldout state
        foldout.boolValue = EditorGUILayout.Foldout(foldout.boolValue, title, true, foldoutStyle);

        if (foldout.boolValue)
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

    private void DrawSubSection(string title, SerializedProperty foldout, System.Action content)
    {
        var subBoxStyle = new GUIStyle(GUI.skin.box);
        subBoxStyle.padding = new RectOffset(8, 8, 4, 4);
        subBoxStyle.margin = new RectOffset(10, 10, 2, 2);

        var subFoldoutStyle = new GUIStyle(EditorStyles.foldout);
        subFoldoutStyle.fontStyle = FontStyle.Normal;

        EditorGUILayout.BeginVertical(subBoxStyle);

        foldout.boolValue = EditorGUILayout.Foldout(foldout.boolValue, title, true, subFoldoutStyle);

        if (foldout.boolValue)
        {
            EditorGUI.indentLevel++;
            GUILayout.Space(3);
            content?.Invoke();
            GUILayout.Space(3);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(2);
    }
}
#endif

[System.Serializable]
public class EditorFoldoutStates
{
    public bool showControls = true;
    public bool showEmissionSettings = true;
    public bool showEmissionPosition = true;
    public bool showEmissionShape = true;
    public bool showEmissionProperties = true;
    public bool showRadialControls = true;
    public bool showTemporalControl = true;
    public bool showStopBehavior = true;
    public bool showGridSettings = true;
    public bool showSmokeProperties = true;
    public bool showBoundaryFade = true;
    public bool showVorticityFade = true;
    public bool showGridPositioning = true;
    public bool showAdaptiveGrid = true;
    public bool showReferences = true;
    public bool showPerformanceOptimization = true;
    public bool showDebug = true;
}
#endregion
