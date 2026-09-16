using System.Collections.Generic;
using UnityEngine;

public class GlobalSmokeManager : MonoBehaviour
{
    public ComputeShader smokeCompute;

    [Header("Dual-Grid Space Settings")]
    public float smokeWorldSize = 40f;
    [Tooltip("Size of the coarse physics simulation.")]
    public int physicsResolution = 256;
    [Tooltip("Size of the high-res visual smoke.")]
    public int visualResolution = 1024;

    [Header("Simulation Settings")]
    public float fixedTimeStep = 1f / 60f;
    [Range(0.25f, 5f)] public float simulationSpeed = 1.0f;
    public float fluidDecay = 0.995f;
    public int multigridLevels = 5;
    public int preSmoothIterations = 2;
    public int postSmoothIterations = 2;
    public int bottomSolveIterations = 20;

    [Header("Forces")]
    public float windInfluence = 1.0f;
    public float vorticityStrength = 2.0f;

    [Header("Curl Noise (Stylization)")]
    public float noiseTimeScale = 0.5f;
    [Header("Macro Noise (Physics Structure)")]
    public float macroNoiseScale = 0.1f;
    public float macroNoiseStrength = 15.0f;
    [Header("Micro Noise (Visual Detail)")]
    public float microNoiseScale = 0.5f;
    public float microNoiseStrength = 5.0f;

    public enum AdvectionMethod { SemiLagrangian, MacCormack }
    [Header("Style & Solver")]
    public AdvectionMethod advectionMethod = AdvectionMethod.SemiLagrangian;
    [Range(0f, 1f)] public float macCormackStrength = 0.5f;

    [Header("MGPCG Solver")]
    [Range(1, 20)] public int cgIterations = 1;

    private RenderTexture velRead, velWrite, velShiftTemp;
    private RenderTexture dyeRead, dyeWrite, dyeShiftTemp;
    private RenderTexture divergence;
    private RenderTexture cgPressure, cgResidual, searchDir, qVector;

    private RenderTexture[] pressurePyramid;
    private RenderTexture[] residualPyramid;
    private RenderTexture pressureShiftTemp;
    private RenderTexture expansionRead, expansionWrite;
    private RenderTexture obstacleMaskRT;
    private RenderTexture velInt, velBack, dyeInt, dyeBack;

    // Dual Grid Kernels
    private int kShiftVel, kShiftDye, kShiftPressure;
    private int kAdvectVelocity, kAdvectHighResDye;
    private int kApplySDF_Physics;
    private int kInjectVelocity, kInjectHighResDye;
    private int kAdvectMacCormack1_Physics, kAdvectMacCormack2_Physics, kAdvectMacCormack3_Physics;
    private int kAdvectMacCormack1_Visual, kAdvectMacCormack2_Visual, kAdvectMacCormack3_Visual;

    // Physics Kernels
    private int kDiv, kJacobi, kRestrict, kProlongate, kProject;
    private int kCalcVorticity, kApplyVorticity;
    private int kInjectWind;

    private int kInjectMacroCurlNoise;

    private Vector2 _lastSnappedOrigin;
    private float _physicsTexelSize;
    private float _accumulatedTime = 0f;
    private float _simulatedTime = 0f;

    private RenderTexture vorticityRT;
    private RenderTexture[] dotPyramid;
    private RenderTexture rhoTex, rhoOldTex, sDotQTex, alphaTex, betaTex;
    private RenderTexture microNoiseRT;
    private int kDotMultiply, kDotReduce, kCalcBeta, kCalcAlpha;
    private int kUpdateSearchDir, kCalcLaplacian, kUpdateCGState;
    private int kClear;
    private int kCalcMicroNoise;
    private int kBakeObstacleMask;

    private static readonly int id_DeltaTime = Shader.PropertyToID("_DeltaTime");
    private static readonly int id_PhysicsRes = Shader.PropertyToID("_PhysicsRes");
    private static readonly int id_VisualRes = Shader.PropertyToID("_VisualRes");
    private static readonly int id_GridSize = Shader.PropertyToID("_GridSize");
    private static readonly int id_WorldOrigin = Shader.PropertyToID("_WorldOrigin");
    private static readonly int id_WorldSize = Shader.PropertyToID("_WorldSize");
    private static readonly int id_SmokeDecay = Shader.PropertyToID("_SmokeDecay");
    private static readonly int id_SimplexTime = Shader.PropertyToID("_SimplexTime");
    private static readonly int id_MacroNoiseScale = Shader.PropertyToID("_MacroNoiseScale");
    private static readonly int id_MacroNoiseStrength = Shader.PropertyToID("_MacroNoiseStrength");
    private static readonly int id_MicroNoiseScale = Shader.PropertyToID("_MicroNoiseScale");
    private static readonly int id_MicroNoiseStrength = Shader.PropertyToID("_MicroNoiseStrength");
    private static readonly int id_VelocityRead = Shader.PropertyToID("_VelocityRead");
    private static readonly int id_VelocityWrite = Shader.PropertyToID("_VelocityWrite");
    private static readonly int id_DyeRead = Shader.PropertyToID("_DyeRead");
    private static readonly int id_DyeWrite = Shader.PropertyToID("_DyeWrite");
    private static readonly int id_VelIntermediate = Shader.PropertyToID("_VelIntermediate");
    private static readonly int id_VelBackward = Shader.PropertyToID("_VelBackward");
    private static readonly int id_DyeIntermediate = Shader.PropertyToID("_DyeIntermediate");
    private static readonly int id_DyeBackward = Shader.PropertyToID("_DyeBackward");
    private static readonly int id_WindInfluence = Shader.PropertyToID("_WindInfluence");
    private static readonly int id_WindVelocityTex = Shader.PropertyToID("_WindVelocityTex");
    private static readonly int id_WindCameraBottomLeft = Shader.PropertyToID("_WindCameraBottomLeft");
    private static readonly int id_WindCameraSize = Shader.PropertyToID("_WindCameraSize");
    private static readonly int id_EmitWorldPos = Shader.PropertyToID("_EmitWorldPos");
    private static readonly int id_EmitColor = Shader.PropertyToID("_EmitColor");
    private static readonly int id_EmitRadius = Shader.PropertyToID("_EmitRadius");
    private static readonly int id_EmitDensity = Shader.PropertyToID("_EmitDensity");
    private static readonly int id_EmitShape = Shader.PropertyToID("_EmitShape");
    private static readonly int id_EmitVelocityMode = Shader.PropertyToID("_EmitVelocityMode");
    private static readonly int id_EmitThickness = Shader.PropertyToID("_EmitThickness");
    private static readonly int id_EmitLineEnd = Shader.PropertyToID("_EmitLineEnd");
    private static readonly int id_EmitVelocityStrength = Shader.PropertyToID("_EmitVelocityStrength");
    private static readonly int id_EmitVelocity = Shader.PropertyToID("_EmitVelocity");
    private static readonly int id_EmitExpansionRate = Shader.PropertyToID("_EmitExpansionRate");
    private static readonly int id_ExpansionRead = Shader.PropertyToID("_ExpansionRead");
    private static readonly int id_ExpansionWrite = Shader.PropertyToID("_ExpansionWrite");
    private static readonly int id_EmitTurbulenceStrength = Shader.PropertyToID("_EmitTurbulenceStrength");
    private static readonly int id_EmitTurbulenceScale = Shader.PropertyToID("_EmitTurbulenceScale");
    private static readonly int id_VorticityStrength = Shader.PropertyToID("_VorticityStrength");
    private static readonly int id_VorticityRead = Shader.PropertyToID("_VorticityRead");
    private static readonly int id_VorticityWrite = Shader.PropertyToID("_VorticityWrite");
    private static readonly int id_DistanceFieldTex_Compute = Shader.PropertyToID("_DistanceFieldTex_Compute");
    private static readonly int id_DistanceFieldTex_Physics = Shader.PropertyToID("_DistanceFieldTex_Physics");
    private static readonly int id_GlobalSDFCenter = Shader.PropertyToID("_GlobalSDFCenter");
    private static readonly int id_GlobalSDFSize = Shader.PropertyToID("_GlobalSDFSize");
    private static readonly int id_GlobalPhysicsSDFCenter = Shader.PropertyToID("_GlobalPhysicsSDFCenter");
    private static readonly int id_GlobalPhysicsSDFSize = Shader.PropertyToID("_GlobalPhysicsSDFSize");
    private static readonly int id_DivergenceRead = Shader.PropertyToID("_DivergenceRead");
    private static readonly int id_DivergenceWrite = Shader.PropertyToID("_DivergenceWrite");
    private static readonly int id_PressureRead = Shader.PropertyToID("_PressureRead");
    private static readonly int id_PressureWrite = Shader.PropertyToID("_PressureWrite");
    private static readonly int id_ResidualRead = Shader.PropertyToID("_ResidualRead");
    private static readonly int id_ResidualWrite = Shader.PropertyToID("_ResidualWrite");
    private static readonly int id_LevelRes = Shader.PropertyToID("_LevelRes");
    private static readonly int id_CoarseRes = Shader.PropertyToID("_CoarseRes");
    private static readonly int id_DotReadA = Shader.PropertyToID("_DotReadA");
    private static readonly int id_DotReadB = Shader.PropertyToID("_DotReadB");
    private static readonly int id_ReduceRead = Shader.PropertyToID("_ReduceRead");
    private static readonly int id_ReduceWrite = Shader.PropertyToID("_ReduceWrite");
    private static readonly int id_RhoTex = Shader.PropertyToID("_RhoTex");
    private static readonly int id_RhoOldTex = Shader.PropertyToID("_RhoOldTex");
    private static readonly int id_BetaTexWrite = Shader.PropertyToID("_BetaTexWrite");
    private static readonly int id_BetaTexRead = Shader.PropertyToID("_BetaTexRead");
    private static readonly int id_ZVectorRead = Shader.PropertyToID("_ZVectorRead");
    private static readonly int id_SearchDirRead = Shader.PropertyToID("_SearchDirRead");
    private static readonly int id_SearchDirWrite = Shader.PropertyToID("_SearchDirWrite");
    private static readonly int id_QVectorRead = Shader.PropertyToID("_QVectorRead");
    private static readonly int id_QVectorWrite = Shader.PropertyToID("_QVectorWrite");
    private static readonly int id_SDotQTex = Shader.PropertyToID("_SDotQTex");
    private static readonly int id_AlphaTexWrite = Shader.PropertyToID("_AlphaTexWrite");
    private static readonly int id_AlphaTexRead = Shader.PropertyToID("_AlphaTexRead");
    private static readonly int id_CGPressureRead = Shader.PropertyToID("_CGPressureRead");
    private static readonly int id_CGResidualRead = Shader.PropertyToID("_CGResidualRead");
    private static readonly int id_CGPressureWrite = Shader.PropertyToID("_CGPressureWrite");
    private static readonly int id_CGResidualWrite = Shader.PropertyToID("_CGResidualWrite");
    private static readonly int id_ClearWrite = Shader.PropertyToID("_ClearWrite");
    private static readonly int id_ShiftOffset = Shader.PropertyToID("_ShiftOffset");
    private static readonly int id_VisualShiftOffset = Shader.PropertyToID("_VisualShiftOffset");
    private static readonly int id_VelShiftRead = Shader.PropertyToID("_VelShiftRead");
    private static readonly int id_VelShiftWrite = Shader.PropertyToID("_VelShiftWrite");
    private static readonly int id_PressureShiftRead = Shader.PropertyToID("_PressureShiftRead");
    private static readonly int id_PressureShiftWrite = Shader.PropertyToID("_PressureShiftWrite");
    private static readonly int id_DyeShiftRead = Shader.PropertyToID("_DyeShiftRead");
    private static readonly int id_DyeShiftWrite = Shader.PropertyToID("_DyeShiftWrite");
    private static readonly int id_MicroNoiseWrite = Shader.PropertyToID("_MicroNoiseWrite");
    private static readonly int id_MicroNoiseTex = Shader.PropertyToID("_MicroNoiseTex");
    private static readonly int id_ObstacleMaskWrite = Shader.PropertyToID("_ObstacleMaskWrite");
    private static readonly int id_ObstacleMaskTex = Shader.PropertyToID("_ObstacleMaskTex");

    private void Awake()
    {
        InitializeKernels();
        InitializeBuffers();
        _physicsTexelSize = smokeWorldSize / physicsResolution;
        _lastSnappedOrigin = GetSnappedCameraOrigin();
    }

    private void InitializeKernels()
    {
        kShiftVel = smokeCompute.FindKernel("CSShiftVelocity");
        kShiftDye = smokeCompute.FindKernel("CSShiftDye");
        kShiftPressure = smokeCompute.FindKernel("CSShiftPressure");

        kAdvectVelocity = smokeCompute.FindKernel("AdvectVelocity");
        kAdvectHighResDye = smokeCompute.FindKernel("AdvectHighResDye");

        kApplySDF_Physics = smokeCompute.FindKernel("ApplySDF_Physics");

        kInjectVelocity = smokeCompute.FindKernel("InjectVelocity");
        kInjectHighResDye = smokeCompute.FindKernel("InjectHighResDye");

        kDiv = smokeCompute.FindKernel("Divergence");
        kJacobi = smokeCompute.FindKernel("JacobiSmooth");
        kRestrict = smokeCompute.FindKernel("Restrict");
        kProlongate = smokeCompute.FindKernel("ProlongateAndCorrect");
        kProject = smokeCompute.FindKernel("Project");
        kCalcVorticity = smokeCompute.FindKernel("CalcVorticity");
        kApplyVorticity = smokeCompute.FindKernel("ApplyVorticity");
        kInjectWind = smokeCompute.FindKernel("InjectWind");
        kAdvectMacCormack1_Physics = smokeCompute.FindKernel("AdvectMacCormackStep1_Physics");
        kAdvectMacCormack2_Physics = smokeCompute.FindKernel("AdvectMacCormackStep2_Physics");
        kAdvectMacCormack3_Physics = smokeCompute.FindKernel("AdvectMacCormackStep3_Physics");

        kAdvectMacCormack1_Visual = smokeCompute.FindKernel("AdvectMacCormackStep1_Visual");
        kAdvectMacCormack2_Visual = smokeCompute.FindKernel("AdvectMacCormackStep2_Visual");
        kAdvectMacCormack3_Visual = smokeCompute.FindKernel("AdvectMacCormackStep3_Visual");
        kDotMultiply = smokeCompute.FindKernel("DotMultiply");
        kDotReduce = smokeCompute.FindKernel("DotReduce");
        kCalcBeta = smokeCompute.FindKernel("CalcBeta");
        kCalcAlpha = smokeCompute.FindKernel("CalcAlpha");
        kUpdateSearchDir = smokeCompute.FindKernel("UpdateSearchDir");
        kCalcLaplacian = smokeCompute.FindKernel("CalcLaplacian");
        kUpdateCGState = smokeCompute.FindKernel("UpdateCGState");
        kInjectMacroCurlNoise = smokeCompute.FindKernel("InjectMacroCurlNoise");
        kClear = smokeCompute.FindKernel("ClearTexture");
        kCalcMicroNoise = smokeCompute.FindKernel("CalcMicroNoise");
        kBakeObstacleMask = smokeCompute.FindKernel("BakeObstacleMask");
    }

    private void InitializeBuffers()
    {
        velRead = CreateRT(physicsResolution, RenderTextureFormat.RGFloat);
        velWrite = CreateRT(physicsResolution, RenderTextureFormat.RGFloat);
        velShiftTemp = CreateRT(physicsResolution, RenderTextureFormat.RGFloat);

        divergence = CreateRT(physicsResolution, RenderTextureFormat.RFloat);
        pressureShiftTemp = CreateRT(physicsResolution, RenderTextureFormat.RFloat);
        vorticityRT = CreateRT(physicsResolution, RenderTextureFormat.RFloat);
        expansionRead = CreateRT(physicsResolution, RenderTextureFormat.RFloat);
        expansionWrite = CreateRT(physicsResolution, RenderTextureFormat.RFloat);

        velInt = CreateRT(physicsResolution, RenderTextureFormat.RGFloat);
        velBack = CreateRT(physicsResolution, RenderTextureFormat.RGFloat);

        cgPressure = CreateRT(physicsResolution, RenderTextureFormat.RGFloat);
        cgResidual = CreateRT(physicsResolution, RenderTextureFormat.RGFloat);
        searchDir = CreateRT(physicsResolution, RenderTextureFormat.RGFloat);
        qVector = CreateRT(physicsResolution, RenderTextureFormat.RGFloat);

        int dotLevels = (int)Mathf.Log(physicsResolution, 2);
        dotPyramid = new RenderTexture[dotLevels];
        int currentDotRes = physicsResolution / 2;
        for (int i = 0; i < dotLevels; i++)
        {
            dotPyramid[i] = CreateRT(currentDotRes, RenderTextureFormat.RFloat);
            currentDotRes /= 2;
        }

        pressurePyramid = new RenderTexture[multigridLevels];
        residualPyramid = new RenderTexture[multigridLevels];
        int currentRes = physicsResolution;
        for (int i = 0; i < multigridLevels; i++)
        {
            pressurePyramid[i] = CreateRT(currentRes, RenderTextureFormat.RFloat);
            residualPyramid[i] = CreateRT(currentRes, RenderTextureFormat.RFloat);
            currentRes /= 2;
        }

        dyeRead = CreateRT(visualResolution, RenderTextureFormat.ARGBHalf);
        dyeWrite = CreateRT(visualResolution, RenderTextureFormat.ARGBHalf);
        dyeShiftTemp = CreateRT(visualResolution, RenderTextureFormat.ARGBHalf);

        dyeInt = CreateRT(visualResolution, RenderTextureFormat.ARGBHalf);
        dyeBack = CreateRT(visualResolution, RenderTextureFormat.ARGBHalf);

        rhoTex = CreateRT(1, RenderTextureFormat.RFloat);
        rhoOldTex = CreateRT(1, RenderTextureFormat.RFloat);
        sDotQTex = CreateRT(1, RenderTextureFormat.RFloat);
        alphaTex = CreateRT(1, RenderTextureFormat.RFloat);
        betaTex = CreateRT(1, RenderTextureFormat.RFloat);

        microNoiseRT = CreateRT(physicsResolution, RenderTextureFormat.RGFloat);
        obstacleMaskRT = CreateRT(physicsResolution, RenderTextureFormat.RFloat);
    }

    private RenderTexture CreateRT(int res, RenderTextureFormat format)
    {
        RenderTexture rt = new RenderTexture(res, res, 0, format)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();
        return rt;
    }

    private void Swap(ref RenderTexture read, ref RenderTexture write)
    {
        RenderTexture temp = read;
        read = write;
        write = temp;
    }

    private Vector2 GetCameraOrigin() => (Vector2)Camera.main.transform.position - new Vector2(smokeWorldSize / 2f, smokeWorldSize / 2f);

    private Vector2 GetSnappedCameraOrigin()
    {
        Vector2 target = GetCameraOrigin();
        return new Vector2(
            Mathf.Round(target.x / _physicsTexelSize) * _physicsTexelSize,
            Mathf.Round(target.y / _physicsTexelSize) * _physicsTexelSize
        );
    }

    private void Update()
    {
        _accumulatedTime += Time.deltaTime * simulationSpeed;

        while (_accumulatedTime >= fixedTimeStep)
        {
            HandleBufferShifting();
            StepSimulation(fixedTimeStep);
            _accumulatedTime -= fixedTimeStep;
        }

        Shader.SetGlobalTexture("_GlobalSmokeDyeTex", dyeRead);
        Shader.SetGlobalVector("_GlobalSmokeBottomLeft", _lastSnappedOrigin);
        Shader.SetGlobalVector("_GlobalSmokeSize", new Vector2(smokeWorldSize, smokeWorldSize));
    }


    private void StepSimulation(float dt)
    {
        _simulatedTime += dt;

        int physicsGroups = Mathf.CeilToInt(physicsResolution / 8f);
        int visualGroups = Mathf.CeilToInt(visualResolution / 8f);

        smokeCompute.SetFloat(id_DeltaTime, dt);
        smokeCompute.SetFloat(id_PhysicsRes, physicsResolution);
        smokeCompute.SetFloat(id_VisualRes, visualResolution);
        smokeCompute.SetFloat(id_GridSize, smokeWorldSize / physicsResolution);
        smokeCompute.SetVector(id_WorldOrigin, _lastSnappedOrigin);
        smokeCompute.SetFloat(id_WorldSize, smokeWorldSize);

        float scaledDecay = Mathf.Pow(fluidDecay, dt / fixedTimeStep);
        smokeCompute.SetFloat(id_SmokeDecay, scaledDecay);

        smokeCompute.SetFloat(id_SimplexTime, _simulatedTime * noiseTimeScale);
        smokeCompute.SetFloat(id_MacroNoiseScale, macroNoiseScale);
        smokeCompute.SetFloat(id_MacroNoiseStrength, macroNoiseStrength);
        smokeCompute.SetFloat(id_MicroNoiseScale, microNoiseScale);
        smokeCompute.SetFloat(id_MicroNoiseStrength, microNoiseStrength);

        Texture envSDF = Shader.GetGlobalTexture("_DistanceFieldTex_Compute");
        Texture physSDF = Shader.GetGlobalTexture("_DistanceFieldTex_Physics");
        Texture defaultTex = Texture2D.whiteTexture;

        smokeCompute.SetTexture(kBakeObstacleMask, id_DistanceFieldTex_Compute, envSDF != null ? envSDF : defaultTex);
        smokeCompute.SetTexture(kBakeObstacleMask, id_DistanceFieldTex_Physics, physSDF != null ? physSDF : defaultTex);
        smokeCompute.SetVector(id_GlobalSDFCenter, Shader.GetGlobalVector("_GlobalSDFCenter"));
        smokeCompute.SetVector(id_GlobalSDFSize, Shader.GetGlobalVector("_GlobalSDFSize"));
        smokeCompute.SetVector(id_GlobalPhysicsSDFCenter, Shader.GetGlobalVector("_GlobalPhysicsSDFCenter"));
        smokeCompute.SetVector(id_GlobalPhysicsSDFSize, Shader.GetGlobalVector("_GlobalPhysicsSDFSize"));

        smokeCompute.SetTexture(kBakeObstacleMask, id_ObstacleMaskWrite, obstacleMaskRT);
        smokeCompute.Dispatch(kBakeObstacleMask, physicsGroups, physicsGroups, 1);

        //INJECT MACRO CURL NOISE (PHYSICS ONLY)
        smokeCompute.SetTexture(kInjectMacroCurlNoise, id_VelocityRead, velRead);
        smokeCompute.SetTexture(kInjectMacroCurlNoise, id_VelocityWrite, velWrite);
        smokeCompute.Dispatch(kInjectMacroCurlNoise, physicsGroups, physicsGroups, 1);
        Swap(ref velRead, ref velWrite);

        //PRE-CALCULATE MICRO NOISE FOR VISUAL GRID
        if (advectionMethod == AdvectionMethod.MacCormack)
        {
            smokeCompute.SetTexture(kCalcMicroNoise, id_MicroNoiseWrite, microNoiseRT);
            smokeCompute.Dispatch(kCalcMicroNoise, physicsGroups, physicsGroups, 1);
        }

        //ADVECT (DUAL GRID)
        if (advectionMethod == AdvectionMethod.SemiLagrangian)
        {
            smokeCompute.SetTexture(kAdvectVelocity, id_VelocityRead, velRead);
            smokeCompute.SetTexture(kAdvectVelocity, id_VelocityWrite, velWrite);
            smokeCompute.Dispatch(kAdvectVelocity, physicsGroups, physicsGroups, 1);

            smokeCompute.SetTexture(kAdvectHighResDye, id_VelocityRead, velRead);
            smokeCompute.SetTexture(kAdvectHighResDye, id_DyeRead, dyeRead);
            smokeCompute.SetTexture(kAdvectHighResDye, id_DyeWrite, dyeWrite);
            smokeCompute.Dispatch(kAdvectHighResDye, visualGroups, visualGroups, 1);

            Swap(ref velRead, ref velWrite);
            Swap(ref dyeRead, ref dyeWrite);
        }
        else
        {
            //Forward
            smokeCompute.SetTexture(kAdvectMacCormack1_Physics, id_VelocityRead, velRead);
            smokeCompute.SetTexture(kAdvectMacCormack1_Physics, id_VelocityWrite, velInt);
            smokeCompute.Dispatch(kAdvectMacCormack1_Physics, physicsGroups, physicsGroups, 1);

            smokeCompute.SetTexture(kAdvectMacCormack1_Visual, id_VelocityRead, velRead);
            smokeCompute.SetTexture(kAdvectMacCormack1_Visual, id_DyeRead, dyeRead);
            smokeCompute.SetTexture(kAdvectMacCormack1_Visual, id_DyeWrite, dyeInt);
            smokeCompute.SetTexture(kAdvectMacCormack1_Visual, id_MicroNoiseTex, microNoiseRT);
            smokeCompute.Dispatch(kAdvectMacCormack1_Visual, visualGroups, visualGroups, 1);

            //Backward
            smokeCompute.SetTexture(kAdvectMacCormack2_Physics, id_VelocityRead, velRead);
            smokeCompute.SetTexture(kAdvectMacCormack2_Physics, id_VelIntermediate, velInt);
            smokeCompute.SetTexture(kAdvectMacCormack2_Physics, id_VelocityWrite, velBack);
            smokeCompute.Dispatch(kAdvectMacCormack2_Physics, physicsGroups, physicsGroups, 1);

            smokeCompute.SetTexture(kAdvectMacCormack2_Visual, id_VelocityRead, velRead);
            smokeCompute.SetTexture(kAdvectMacCormack2_Visual, id_DyeIntermediate, dyeInt);
            smokeCompute.SetTexture(kAdvectMacCormack2_Visual, id_DyeWrite, dyeBack);
            smokeCompute.SetTexture(kAdvectMacCormack2_Visual, id_MicroNoiseTex, microNoiseRT);
            smokeCompute.Dispatch(kAdvectMacCormack2_Visual, visualGroups, visualGroups, 1);

            //Error Correction & Limiter
            smokeCompute.SetTexture(kAdvectMacCormack3_Physics, id_VelocityRead, velRead);
            smokeCompute.SetTexture(kAdvectMacCormack3_Physics, id_VelIntermediate, velInt);
            smokeCompute.SetTexture(kAdvectMacCormack3_Physics, id_VelBackward, velBack);
            smokeCompute.SetTexture(kAdvectMacCormack3_Physics, id_VelocityWrite, velWrite);
            smokeCompute.Dispatch(kAdvectMacCormack3_Physics, physicsGroups, physicsGroups, 1);

            smokeCompute.SetTexture(kAdvectMacCormack3_Visual, id_VelocityRead, velRead);
            smokeCompute.SetTexture(kAdvectMacCormack3_Visual, id_DyeRead, dyeRead);
            smokeCompute.SetTexture(kAdvectMacCormack3_Visual, id_DyeIntermediate, dyeInt);
            smokeCompute.SetTexture(kAdvectMacCormack3_Visual, id_DyeBackward, dyeBack);
            smokeCompute.SetTexture(kAdvectMacCormack3_Visual, id_DyeWrite, dyeWrite);
            smokeCompute.SetTexture(kAdvectMacCormack3_Visual, id_MicroNoiseTex, microNoiseRT);
            smokeCompute.Dispatch(kAdvectMacCormack3_Visual, visualGroups, visualGroups, 1);

            Swap(ref velRead, ref velWrite);
            Swap(ref dyeRead, ref dyeWrite);
        }

        //INJECT WIND (PHYSICS) ---
        Texture windTex = Shader.GetGlobalTexture("_WindVelocityTex");
        if (windTex != null && windInfluence > 0f)
        {
            smokeCompute.SetFloat(id_WindInfluence, windInfluence);
            smokeCompute.SetTexture(kInjectWind, id_WindVelocityTex, windTex);
            smokeCompute.SetVector(id_WindCameraBottomLeft, Shader.GetGlobalVector("_WindCameraBottomLeft"));
            smokeCompute.SetVector(id_WindCameraSize, Shader.GetGlobalVector("_WindCameraSize"));

            smokeCompute.SetTexture(kInjectWind, id_VelocityRead, velRead);
            smokeCompute.SetTexture(kInjectWind, id_VelocityWrite, velWrite);
            smokeCompute.Dispatch(kInjectWind, physicsGroups, physicsGroups, 1);
            Swap(ref velRead, ref velWrite);
        }

        ClearRenderTexture(expansionRead);

        //1.5. INJECT EMITTERS (DUAL GRID)
        for (int i = GlobalSmokeEmitter.AllEmitters.Count - 1; i >= 0; i--)
        {
            GlobalSmokeEmitter emitter = GlobalSmokeEmitter.AllEmitters[i];
            emitter.Tick(dt);

            if (emitter.IsEmitting)
            {
                smokeCompute.SetVector(id_EmitWorldPos, emitter.transform.position);
                smokeCompute.SetVector(id_EmitColor, emitter.emitColor);
                smokeCompute.SetFloat(id_EmitRadius, emitter.radius);
                smokeCompute.SetFloat(id_EmitDensity, emitter.density);

                smokeCompute.SetInt(id_EmitShape, (int)emitter.shape);
                smokeCompute.SetInt(id_EmitVelocityMode, (int)emitter.velocityMode);
                smokeCompute.SetFloat(id_EmitThickness, emitter.thickness);

                Vector3 worldEnd = emitter.transform.TransformPoint(emitter.lineEndPoint);
                Vector3 worldOffset = worldEnd - emitter.transform.position;
                smokeCompute.SetVector(id_EmitLineEnd, new Vector2(worldOffset.x, worldOffset.y));

                smokeCompute.SetFloat(id_EmitVelocityStrength, emitter.velocityStrength);
                smokeCompute.SetVector(id_EmitVelocity, emitter.CurrentVelocity);

                smokeCompute.SetFloat(id_EmitExpansionRate, emitter.expansionRate);
                smokeCompute.SetTexture(kInjectVelocity, id_ExpansionRead, expansionRead);
                smokeCompute.SetTexture(kInjectVelocity, id_ExpansionWrite, expansionWrite);

                smokeCompute.SetFloat(id_EmitTurbulenceStrength, emitter.turbulenceStrength);
                smokeCompute.SetFloat(id_EmitTurbulenceScale, emitter.turbulenceScale);

                smokeCompute.SetTexture(kInjectVelocity, id_VelocityRead, velRead);
                smokeCompute.SetTexture(kInjectVelocity, id_VelocityWrite, velWrite);
                smokeCompute.Dispatch(kInjectVelocity, physicsGroups, physicsGroups, 1);

                smokeCompute.SetTexture(kInjectHighResDye, id_DyeRead, dyeRead);
                smokeCompute.SetTexture(kInjectHighResDye, id_DyeWrite, dyeWrite);
                smokeCompute.Dispatch(kInjectHighResDye, visualGroups, visualGroups, 1);

                Swap(ref velRead, ref velWrite);
                Swap(ref dyeRead, ref dyeWrite);
                Swap(ref expansionRead, ref expansionWrite);
            }
        }

        //VORTICITY CONFINEMENT (PHYSICS)
        smokeCompute.SetFloat(id_VorticityStrength, vorticityStrength);
        smokeCompute.SetTexture(kCalcVorticity, id_VelocityRead, velRead);
        smokeCompute.SetTexture(kCalcVorticity, id_VorticityWrite, vorticityRT);
        smokeCompute.Dispatch(kCalcVorticity, physicsGroups, physicsGroups, 1);

        smokeCompute.SetTexture(kApplyVorticity, id_VorticityRead, vorticityRT);
        smokeCompute.SetTexture(kApplyVorticity, id_VelocityRead, velRead);
        smokeCompute.SetTexture(kApplyVorticity, id_VelocityWrite, velWrite);
        smokeCompute.Dispatch(kApplyVorticity, physicsGroups, physicsGroups, 1);
        Swap(ref velRead, ref velWrite);

        //APPLY SDF BOUNDARIES (DUAL GRID)
        smokeCompute.SetTexture(kApplySDF_Physics, id_ObstacleMaskTex, obstacleMaskRT);
        smokeCompute.SetTexture(kApplySDF_Physics, id_VelocityWrite, velRead);
        smokeCompute.Dispatch(kApplySDF_Physics, physicsGroups, physicsGroups, 1);

        if (envSDF != null && physSDF != null)
        {
            smokeCompute.SetTexture(kApplySDF_Physics, id_DistanceFieldTex_Compute, envSDF);
            smokeCompute.SetTexture(kApplySDF_Physics, id_DistanceFieldTex_Physics, physSDF);
            smokeCompute.SetVector(id_GlobalSDFCenter, Shader.GetGlobalVector("_GlobalSDFCenter"));
            smokeCompute.SetVector(id_GlobalSDFSize, Shader.GetGlobalVector("_GlobalSDFSize"));
            smokeCompute.SetVector(id_GlobalPhysicsSDFCenter, Shader.GetGlobalVector("_GlobalPhysicsSDFCenter"));
            smokeCompute.SetVector(id_GlobalPhysicsSDFSize, Shader.GetGlobalVector("_GlobalPhysicsSDFSize"));

            smokeCompute.SetTexture(kApplySDF_Physics, id_VelocityWrite, velRead);
            smokeCompute.Dispatch(kApplySDF_Physics, physicsGroups, physicsGroups, 1);
        }

        //DIVERGENCE (PHYSICS)
        smokeCompute.SetTexture(kDiv, id_ObstacleMaskTex, obstacleMaskRT);
        smokeCompute.SetTexture(kDiv, id_VelocityRead, velRead);

        smokeCompute.SetTexture(kDiv, id_ExpansionRead, expansionRead);
        smokeCompute.SetTexture(kDiv, id_DivergenceWrite, divergence);
        smokeCompute.Dispatch(kDiv, physicsGroups, physicsGroups, 1);

        //MULTIGRID PRECONDITIONED CONJUGATE GRADIENT (PHYSICS)
        Graphics.Blit(divergence, cgResidual);
        ClearRenderTexture(cgPressure);
        ClearRenderTexture(searchDir);

        for (int k = 0; k < cgIterations; k++)
        {
            ClearRenderTexture(pressurePyramid[0]);
            Graphics.Blit(cgResidual, residualPyramid[0]);

            ExecuteVCycle();

            ComputeDotProduct(cgResidual, pressurePyramid[0], rhoTex);

            if (k == 0)
            {
                Graphics.Blit(pressurePyramid[0], searchDir);
            }
            else
            {
                smokeCompute.SetTexture(kCalcBeta, id_RhoTex, rhoTex);
                smokeCompute.SetTexture(kCalcBeta, id_RhoOldTex, rhoOldTex);
                smokeCompute.SetTexture(kCalcBeta, id_BetaTexWrite, betaTex);
                smokeCompute.Dispatch(kCalcBeta, 1, 1, 1);

                smokeCompute.SetTexture(kUpdateSearchDir, id_ZVectorRead, pressurePyramid[0]);
                smokeCompute.SetTexture(kUpdateSearchDir, id_SearchDirRead, searchDir);
                smokeCompute.SetTexture(kUpdateSearchDir, id_BetaTexRead, betaTex);
                smokeCompute.SetTexture(kUpdateSearchDir, id_SearchDirWrite, searchDir);
                smokeCompute.Dispatch(kUpdateSearchDir, physicsGroups, physicsGroups, 1);
            }

            smokeCompute.SetTexture(kCalcLaplacian, id_ObstacleMaskTex, obstacleMaskRT);
            smokeCompute.SetTexture(kCalcLaplacian, id_SearchDirRead, searchDir);

            smokeCompute.SetTexture(kCalcLaplacian, id_QVectorWrite, qVector);
            smokeCompute.Dispatch(kCalcLaplacian, physicsGroups, physicsGroups, 1);

            ComputeDotProduct(searchDir, qVector, sDotQTex);

            smokeCompute.SetTexture(kCalcAlpha, id_RhoTex, rhoTex);
            smokeCompute.SetTexture(kCalcAlpha, id_SDotQTex, sDotQTex);
            smokeCompute.SetTexture(kCalcAlpha, id_AlphaTexWrite, alphaTex);
            smokeCompute.Dispatch(kCalcAlpha, 1, 1, 1);

            smokeCompute.SetTexture(kUpdateCGState, id_CGPressureRead, cgPressure);
            smokeCompute.SetTexture(kUpdateCGState, id_CGResidualRead, cgResidual);
            smokeCompute.SetTexture(kUpdateCGState, id_SearchDirRead, searchDir);
            smokeCompute.SetTexture(kUpdateCGState, id_QVectorRead, qVector);
            smokeCompute.SetTexture(kUpdateCGState, id_AlphaTexRead, alphaTex);
            smokeCompute.SetTexture(kUpdateCGState, id_CGPressureWrite, cgPressure);
            smokeCompute.SetTexture(kUpdateCGState, id_CGResidualWrite, cgResidual);
            smokeCompute.Dispatch(kUpdateCGState, physicsGroups, physicsGroups, 1);

            Graphics.Blit(rhoTex, rhoOldTex);
        }

        //PROJECT (PHYSICS) 
        smokeCompute.SetTexture(kProject, id_ObstacleMaskTex, obstacleMaskRT);
        smokeCompute.SetTexture(kProject, id_PressureRead, cgPressure);

        smokeCompute.SetTexture(kProject, id_VelocityRead, velRead);
        smokeCompute.SetTexture(kProject, id_VelocityWrite, velWrite);
        smokeCompute.Dispatch(kProject, physicsGroups, physicsGroups, 1);
        Swap(ref velRead, ref velWrite);
    }

    private void ExecuteVCycle()
    {
        for (int i = 0; i < multigridLevels - 1; i++)
        {
            Smooth(i, preSmoothIterations);

            int coarseLevel = i + 1;
            int coarseGroups = Mathf.CeilToInt((physicsResolution >> coarseLevel) / 8f);

            smokeCompute.SetInt(id_LevelRes, physicsResolution >> i);
            smokeCompute.SetInt(id_CoarseRes, physicsResolution >> coarseLevel);

            smokeCompute.SetTexture(kRestrict, id_ResidualRead, residualPyramid[i]);
            smokeCompute.SetTexture(kRestrict, id_ResidualWrite, residualPyramid[coarseLevel]);
            smokeCompute.Dispatch(kRestrict, coarseGroups, coarseGroups, 1);
        }

        Smooth(multigridLevels - 1, bottomSolveIterations);

        for (int i = multigridLevels - 2; i >= 0; i--)
        {
            int fineGroups = Mathf.CeilToInt((physicsResolution >> i) / 8f);
            smokeCompute.SetInt(id_LevelRes, physicsResolution >> i);

            smokeCompute.SetTexture(kProlongate, id_PressureRead, pressurePyramid[i + 1]);
            smokeCompute.SetTexture(kProlongate, id_PressureWrite, pressurePyramid[i]);
            smokeCompute.Dispatch(kProlongate, fineGroups, fineGroups, 1);

            Smooth(i, postSmoothIterations);
        }
    }

    private void Smooth(int level, int iterations)
    {
        int levelRes = physicsResolution >> level;
        int groups = Mathf.CeilToInt(levelRes / 8f);
        smokeCompute.SetInt(id_LevelRes, levelRes);

        for (int i = 0; i < iterations; i++)
        {
            smokeCompute.SetTexture(kJacobi, id_ObstacleMaskTex, obstacleMaskRT);
            smokeCompute.SetTexture(kJacobi, id_PressureRead, pressurePyramid[level]);
            smokeCompute.SetTexture(kJacobi, id_DivergenceRead, residualPyramid[level]);
            smokeCompute.SetTexture(kJacobi, id_PressureWrite, pressurePyramid[level]);
            smokeCompute.Dispatch(kJacobi, groups, groups, 1);
        }
    }

    private void ComputeDotProduct(RenderTexture texA, RenderTexture texB, RenderTexture destination1x1)
    {
        int groups = Mathf.CeilToInt((physicsResolution / 2f) / 8f);
        smokeCompute.SetTexture(kDotMultiply, id_DotReadA, texA);
        smokeCompute.SetTexture(kDotMultiply, id_DotReadB, texB);
        smokeCompute.SetTexture(kDotMultiply, id_ReduceWrite, dotPyramid[0]);
        smokeCompute.Dispatch(kDotMultiply, groups, groups, 1);

        int currentRes = physicsResolution / 2;
        for (int i = 0; i < dotPyramid.Length - 1; i++)
        {
            currentRes /= 2;
            groups = Mathf.CeilToInt(currentRes / 8f);
            if (groups < 1) groups = 1;

            smokeCompute.SetTexture(kDotReduce, id_ReduceRead, dotPyramid[i]);
            smokeCompute.SetTexture(kDotReduce, id_ReduceWrite, dotPyramid[i + 1]);
            smokeCompute.Dispatch(kDotReduce, groups, groups, 1);
        }

        Graphics.Blit(dotPyramid[dotPyramid.Length - 1], destination1x1);
    }

    private void ClearRenderTexture(RenderTexture rt)
    {
        int groupsX = Mathf.CeilToInt(rt.width / 8f);
        int groupsY = Mathf.CeilToInt(rt.height / 8f);
        smokeCompute.SetTexture(kClear, id_ClearWrite, rt);
        smokeCompute.Dispatch(kClear, groupsX, groupsY, 1);
    }

    private void HandleBufferShifting()
    {
        Vector2 currentSnappedOrigin = GetSnappedCameraOrigin();

        Vector2Int physicsPixelShift = new Vector2Int(
            Mathf.RoundToInt((currentSnappedOrigin.x - _lastSnappedOrigin.x) / _physicsTexelSize),
            Mathf.RoundToInt((currentSnappedOrigin.y - _lastSnappedOrigin.y) / _physicsTexelSize)
        );

        if (physicsPixelShift.x != 0 || physicsPixelShift.y != 0)
        {
            int physicsGroups = Mathf.CeilToInt(physicsResolution / 8f);
            int visualGroups = Mathf.CeilToInt(visualResolution / 8f);

            int resolutionMultiplier = visualResolution / physicsResolution;
            Vector2Int visualPixelShift = physicsPixelShift * resolutionMultiplier;

            smokeCompute.SetInts(id_ShiftOffset, physicsPixelShift.x, physicsPixelShift.y);
            smokeCompute.SetInts(id_VisualShiftOffset, visualPixelShift.x, visualPixelShift.y);

            //Shift Velocity (Physics)
            smokeCompute.SetTexture(kShiftVel, id_VelShiftRead, velRead);
            smokeCompute.SetTexture(kShiftVel, id_VelShiftWrite, velShiftTemp);
            smokeCompute.Dispatch(kShiftVel, physicsGroups, physicsGroups, 1);
            Graphics.CopyTexture(velShiftTemp, velRead);

            //Shift Pressure (Physics)
            smokeCompute.SetTexture(kShiftPressure, id_PressureShiftRead, pressurePyramid[0]);
            smokeCompute.SetTexture(kShiftPressure, id_PressureShiftWrite, pressureShiftTemp);
            smokeCompute.Dispatch(kShiftPressure, physicsGroups, physicsGroups, 1);
            Graphics.CopyTexture(pressureShiftTemp, pressurePyramid[0]);

            //Shift Dye (Visual)
            smokeCompute.SetTexture(kShiftDye, id_DyeShiftRead, dyeRead);
            smokeCompute.SetTexture(kShiftDye, id_DyeShiftWrite, dyeShiftTemp);
            smokeCompute.Dispatch(kShiftDye, visualGroups, visualGroups, 1);
            Graphics.CopyTexture(dyeShiftTemp, dyeRead);

            _lastSnappedOrigin = currentSnappedOrigin;
        }
    }

    private void OnDestroy()
    {
        if (velRead != null) velRead.Release();
        if (velWrite != null) velWrite.Release();
        if (velShiftTemp != null) velShiftTemp.Release();

        if (dyeRead != null) dyeRead.Release();
        if (dyeWrite != null) dyeWrite.Release();
        if (dyeShiftTemp != null) dyeShiftTemp.Release();

        if (pressureShiftTemp != null) pressureShiftTemp.Release();
        if (divergence != null) divergence.Release();
        if (vorticityRT != null) vorticityRT.Release();
        if (expansionRead != null) expansionRead.Release();
        if (expansionWrite != null) expansionWrite.Release();

        if (velInt != null) velInt.Release();
        if (velBack != null) velBack.Release();
        if (dyeInt != null) dyeInt.Release();
        if (dyeBack != null) dyeBack.Release();

        if (cgPressure != null) cgPressure.Release();
        if (cgResidual != null) cgResidual.Release();
        if (searchDir != null) searchDir.Release();
        if (qVector != null) qVector.Release();

        if (rhoTex != null) rhoTex.Release();
        if (rhoOldTex != null) rhoOldTex.Release();
        if (sDotQTex != null) sDotQTex.Release();
        if (alphaTex != null) alphaTex.Release();
        if (betaTex != null) betaTex.Release();
        if (microNoiseRT != null) microNoiseRT.Release();
        if (obstacleMaskRT != null) obstacleMaskRT.Release();

        if (pressurePyramid != null)
        {
            for (int i = 0; i < pressurePyramid.Length; i++)
                if (pressurePyramid[i] != null) pressurePyramid[i].Release();
        }

        if (residualPyramid != null)
        {
            for (int i = 0; i < residualPyramid.Length; i++)
                if (residualPyramid[i] != null) residualPyramid[i].Release();
        }

        if (dotPyramid != null)
        {
            for (int i = 0; i < dotPyramid.Length; i++)
                if (dotPyramid[i] != null) dotPyramid[i].Release();
        }
    }
}