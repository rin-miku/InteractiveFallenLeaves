using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
public class FluidSimulationRenderPass : ScriptableRenderPass
{
    private FluidSimulationSettings settings;
    private FluidSimulationVolume volume;

    private int renderResolution = 512;
    private int simulationResolution = 512;

    private float viscosity = 0.05f;
    private float deltaTime = 0.5f;
    private int solverIterations = 50;
    private float velocityEffectRadius = 15f;
    private float velocityFalloff = 3f;
    private float velocityMultiplier = 1f;
    private float densityEffectRadius = 30f;
    private float densityFalloff = 5f;

    private ComputeShader JacobiSolver;
    private ComputeShader StructuredBufferUtility;
    private ComputeShader NavierStokes;
    private ComputeShader BoundaryUtility;
    private ComputeShader AddSourceTerm;
    private ComputeShader VisualizationUtility;

    private ComputeBuffer tempBuffer;
    private ComputeBuffer velocityBuffer;
    private ComputeBuffer divergenceBuffer;
    private ComputeBuffer pressureBuffer;
    private ComputeBuffer densityBuffer;

    private int threadNum = 8;
    private int threadGroupNum;
    private int renderThreadGroupNum;
    private RenderTexture visualizationRenderTexture;
    private RTHandle visualizationRTHandle;
    private TextureHandle visualizationTextureHandle;
    private RenderTexture velocityRT;

    private float gridScale;
    private float velocityPressed;
    private float densityPressed;
    private Vector2 currentMousePosition;
    private Vector2 previousMousePosition;
    private Color densityColor = Color.cyan;
    private Color targetColor = Color.red;
    private float colorChangeInterval = 0.2f;
    private float lastColorChageTime = 0f;

    public FluidSimulationRenderPass(FluidSimulationSettings fluidSimulationSettings, VolumeProfile volumeProfile)
    {
        settings = fluidSimulationSettings;
        volumeProfile.TryGet(out volume);

        Initialize();
    }

    public void Initialize()
    {
        // compute shaders
        JacobiSolver = settings.JacobiSolver;
        StructuredBufferUtility = settings.StructuredBufferUtility;
        NavierStokes = settings.NavierStokes;
        BoundaryUtility = settings.BoundaryUtility;
        AddSourceTerm = settings.AddSourceTerm;
        VisualizationUtility = settings.VisualizationUtility;

        // compute buffer
        tempBuffer = new ComputeBuffer(simulationResolution * simulationResolution, sizeof(float) * 4);
        velocityBuffer = new ComputeBuffer(simulationResolution * simulationResolution, sizeof(float) * 4);
        divergenceBuffer = new ComputeBuffer(simulationResolution * simulationResolution, sizeof(float) * 4);
        pressureBuffer = new ComputeBuffer(simulationResolution * simulationResolution, sizeof(float) * 4);
        densityBuffer = new ComputeBuffer(simulationResolution * simulationResolution, sizeof(float) * 4);

        gridScale = renderResolution / simulationResolution;
        threadGroupNum = Mathf.CeilToInt(simulationResolution / threadNum);
        renderThreadGroupNum = Mathf.CeilToInt(renderResolution / threadNum);

        visualizationRenderTexture = new RenderTexture(renderResolution, renderResolution, 0);
        visualizationRenderTexture.enableRandomWrite = true;

        velocityRT = settings.velocityRT;
        visualizationRTHandle = RTHandles.Alloc(velocityRT);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UpdateParameters();

        var resouceData = frameData.Get<UniversalResourceData>();
        visualizationTextureHandle = renderGraph.ImportTexture(visualizationRTHandle);

        using (var builder = renderGraph.AddComputePass("FluidSimulation", out PassData passData))
        {
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            passData.visualizationTextureHandle = visualizationTextureHandle;
            builder.UseTexture(passData.visualizationTextureHandle);

            builder.SetRenderFunc((PassData data, ComputeGraphContext context) =>
            {
                PassContext passContext = new PassContext(data, context);

                passContext.SetGlobalInt("RenderResolution", renderResolution);
                passContext.SetGlobalInt("SimulationResolution", simulationResolution);
                passContext.SetGlobalFloat("DeltaTime", deltaTime);
                passContext.SetGlobalFloat("GridScale", gridScale);

                VelocityStep(passContext);
                //DensityStep(passContext);
                Visualization(passContext);
            });
        }

        //resouceData.cameraColor = visualizationTextureHandle;
    }

    private void VelocityStep(PassContext passContext)
    {
        AddVelocity(passContext, velocityBuffer);
        Boundary(passContext, velocityBuffer, -1f);
        Diffuse(passContext, velocityBuffer);
        Boundary(passContext, velocityBuffer, -1f);
        Project(passContext, velocityBuffer, divergenceBuffer, pressureBuffer);
        Advect(passContext, velocityBuffer, velocityBuffer, 0.99f);
        Boundary(passContext, velocityBuffer, -1f);
        Project(passContext, velocityBuffer, divergenceBuffer, pressureBuffer);
    }

    private void DensityStep(PassContext passContext)
    {
        AddDensity(passContext, densityBuffer);
        Boundary(passContext, densityBuffer, 0f);
        Diffuse(passContext, densityBuffer);
        Boundary(passContext, densityBuffer, 0f);
        Advect(passContext, densityBuffer, velocityBuffer, 0.996f);
        Boundary(passContext, densityBuffer, 0f);
    }

    private void Visualization(PassContext passContext)
    {
        DrawVelocity(passContext, velocityBuffer);
        //DrawDensity(passContext, densityBuffer);
    }

    private void AddVelocity(PassContext passContext, ComputeBuffer velocityBuffer)
    {
        AddSourceTerm.SetVector("CurrentMousePosition", currentMousePosition);
        AddSourceTerm.SetVector("PreviousMousePosition", previousMousePosition);
        AddSourceTerm.SetFloat("VelocityPressed", velocityPressed);
        AddSourceTerm.SetFloat("VelocityEffectRadius", velocityEffectRadius);
        AddSourceTerm.SetFloat("VelocityFalloff", velocityFalloff);
        AddSourceTerm.SetFloat("VelocityMultiplier", velocityMultiplier);
        passContext.SetGlobalBuffer("AddedVelocityBuffer", velocityBuffer);
        passContext.DispatchCompute(AddSourceTerm, "AddVelocity", threadGroupNum, threadGroupNum, 1);
    }

    private void AddDensity(PassContext passContext, ComputeBuffer densityBuffer)
    {
        AddSourceTerm.SetVector("DensityColor", densityColor);
        AddSourceTerm.SetVector("MousePosition", currentMousePosition);
        AddSourceTerm.SetFloat("DensityPressed", densityPressed);
        AddSourceTerm.SetFloat("DensityEffectRadius", densityEffectRadius);
        AddSourceTerm.SetFloat("DensityFalloff", densityFalloff);
        passContext.SetGlobalBuffer("AddedDensityBuffer", densityBuffer);
        passContext.DispatchCompute(AddSourceTerm, "AddDensity", threadGroupNum, threadGroupNum, 1);
    }

    private void Diffuse(PassContext passContext, ComputeBuffer velocityBuffer)
    {
        float centerFactor = 1f / (viscosity * deltaTime);
        float diagonalFactor = (viscosity * deltaTime) / (1f + 4f * viscosity * deltaTime);
        passContext.SetGlobalFloat("CenterFactor", centerFactor);
        passContext.SetGlobalFloat("DiagonalFactor", diagonalFactor);

        bool resultInTempBuffer = false;
        for (int i = 0; i < solverIterations; i++)
        {
            resultInTempBuffer = !resultInTempBuffer;
            if (resultInTempBuffer)
            {
                passContext.SetGlobalBuffer("RightHandSideBuffer", velocityBuffer);
                passContext.SetGlobalBuffer("CurrentBuffer", velocityBuffer);
                passContext.SetGlobalBuffer("ResultBuffer", tempBuffer);
            }
            else
            {
                passContext.SetGlobalBuffer("RightHandSideBuffer", tempBuffer);
                passContext.SetGlobalBuffer("CurrentBuffer", tempBuffer);
                passContext.SetGlobalBuffer("ResultBuffer", velocityBuffer);
            }

            passContext.DispatchCompute(JacobiSolver, "JacobiSolver", threadGroupNum, threadGroupNum, 1);
        }

        if (resultInTempBuffer)
            CopyStructuredBuffer(passContext, tempBuffer, velocityBuffer);

        ClearStructuredBuffer(passContext, Vector4.zero, tempBuffer);
    }

    private void Advect(PassContext passContext, ComputeBuffer sourceBuffer, ComputeBuffer velocityBuffer, float dissipationFactor)
    {
        passContext.SetGlobalBuffer("SourceBuffer", sourceBuffer);
        passContext.SetGlobalBuffer("VelocityBuffer", velocityBuffer);
        passContext.SetGlobalBuffer("AdvectedBuffer", tempBuffer);
        passContext.SetGlobalFloat("DissipationFactor", dissipationFactor);

        passContext.DispatchCompute(NavierStokes, "Advect", threadGroupNum, threadGroupNum, 1);

        CopyStructuredBuffer(passContext, tempBuffer, sourceBuffer);

        ClearStructuredBuffer(passContext, Vector4.zero, tempBuffer);
    }

    private void Project(PassContext passContext, ComputeBuffer velocityBuffer, ComputeBuffer divergenceBuffer, ComputeBuffer pressureBuffer)
    {
        passContext.SetGlobalBuffer("RawBuffer", velocityBuffer);
        passContext.SetGlobalBuffer("DivergenceBuffer", divergenceBuffer);
        passContext.DispatchCompute(NavierStokes, "CalculateDivergence", threadGroupNum, threadGroupNum, 1);

        float centerFactor = -1f * gridScale * gridScale;
        float diagonalFactor = 0.25f;
        passContext.SetGlobalFloat("CenterFactor", centerFactor);
        passContext.SetGlobalFloat("DiagonalFactor", diagonalFactor);

        passContext.SetGlobalBuffer("RightHandSideBuffer", divergenceBuffer);

        bool resultInTempBuffer = false;
        for (int i = 0; i < solverIterations; i++)
        {
            resultInTempBuffer = !resultInTempBuffer;
            if (resultInTempBuffer)
            {
                Boundary(passContext, pressureBuffer, 1f);
                passContext.SetGlobalBuffer("CurrentBuffer", pressureBuffer);
                passContext.SetGlobalBuffer("ResultBuffer", tempBuffer);
            }
            else
            {
                Boundary(passContext, tempBuffer, 1f);
                passContext.SetGlobalBuffer("CurrentBuffer", tempBuffer);
                passContext.SetGlobalBuffer("ResultBuffer", pressureBuffer);
            }

            passContext.DispatchCompute(JacobiSolver, "JacobiSolver", threadGroupNum, threadGroupNum, 1);
        }

        if (resultInTempBuffer)
            CopyStructuredBuffer(passContext, tempBuffer, pressureBuffer);

        ClearStructuredBuffer(passContext, Vector4.zero, tempBuffer);

        Boundary(passContext, pressureBuffer, 1f);

        passContext.SetGlobalBuffer("VelocityWithDivergenceBuffer", velocityBuffer);
        passContext.SetGlobalBuffer("PressureBuffer", pressureBuffer);
        passContext.SetGlobalBuffer("FreeDivergenceBuffer", tempBuffer);
        passContext.DispatchCompute(NavierStokes, "CalculateFreeDivergence", threadGroupNum, threadGroupNum, 1);

        CopyStructuredBuffer(passContext, tempBuffer, velocityBuffer);

        ClearStructuredBuffer(passContext, Vector4.zero, tempBuffer);
    }

    private void Boundary(PassContext passContext, ComputeBuffer boundaryBuffer, float boundaryScale)
    {
        passContext.SetGlobalFloat("BoundaryScale", boundaryScale);
        passContext.SetGlobalBuffer("BoundaryBuffer", boundaryBuffer);
        passContext.DispatchCompute(BoundaryUtility, "Boundary", threadGroupNum * 4, 1, 1);
    }

    private void DrawVelocity(PassContext passContext, ComputeBuffer velocityBuffer)
    {
        VisualizationUtility.SetBuffer(0, "DrawVelocityBuffer", velocityBuffer);
        VisualizationUtility.SetTexture(0, "VelocityTexture", velocityRT);

        passContext.DispatchCompute(VisualizationUtility, "DrawVelocity", renderThreadGroupNum, renderThreadGroupNum, 1);
    }

    private void DrawDensity(PassContext passContext, ComputeBuffer densityBuffer)
    {
        VisualizationUtility.SetBuffer(1, "DrawDensityBuffer", densityBuffer);
        VisualizationUtility.SetTexture(1, "DensityTexture", visualizationRenderTexture);

        passContext.DispatchCompute(VisualizationUtility, "DrawDensity", renderThreadGroupNum, renderThreadGroupNum, 1);
    }

    #region Shader Methods
    private void CopyStructuredBuffer(PassContext passContext, ComputeBuffer copySource, ComputeBuffer copyDestination)
    {
        passContext.SetGlobalBuffer("CopySource", copySource);
        passContext.SetGlobalBuffer("CopyDestination", copyDestination);
        passContext.DispatchCompute(StructuredBufferUtility, "CopyStructuredBuffer", threadGroupNum * threadGroupNum, 1, 1);
    }

    private void ClearStructuredBuffer(PassContext passContext, Vector4 clearValue, ComputeBuffer clearBuffer)
    {
        passContext.SetGlobalVector("ClearValue", clearValue);
        passContext.SetGlobalBuffer("ClearBuffer", clearBuffer);
        passContext.DispatchCompute(StructuredBufferUtility, "ClearStructuredBuffer", threadGroupNum * threadGroupNum, 1, 1);
    }
    #endregion

    #region Public Methods
    public void UpdateParameters()
    {
        // private parameters
        viscosity = settings.viscosity;
        deltaTime = settings.deltaTime;
        solverIterations = settings.solverIterations;
        velocityEffectRadius = settings.velocityEffectRadius;
        velocityFalloff = settings.velocityFalloff;
        velocityMultiplier = settings.velocityMultiplier;
        densityEffectRadius = settings.densityEffectRadius;
        densityFalloff = settings.densityFalloff;

        // volume parameters
        velocityPressed = volume.velocityPressed.GetValue<float>();
        densityPressed = volume.densityPressed.GetValue<float>();
        previousMousePosition = currentMousePosition;
        currentMousePosition = volume.currentMousePosition.GetValue<Vector2>();
        //currentMousePosition = Camera.main.ScreenToViewportPoint(currentMousePosition);
        //currentMousePosition = new Vector2(Mathf.Clamp01(currentMousePosition.x), Mathf.Clamp01(currentMousePosition.y));
        currentMousePosition = new Vector2(currentMousePosition.x * simulationResolution, currentMousePosition.y * simulationResolution);

        // density color
        lastColorChageTime += Time.deltaTime;
        if (lastColorChageTime >= colorChangeInterval)
        {
            lastColorChageTime = 0f;
            targetColor = Color.HSVToRGB(Random.value, 0.8f, 0.7f);
        }
        densityColor = Color.Lerp(densityColor, targetColor, Time.deltaTime * 0.2f);
    }

    public void Dispose()
    {
        tempBuffer.Dispose();
        velocityBuffer.Dispose();
        divergenceBuffer.Dispose();
        pressureBuffer.Dispose();
        densityBuffer.Dispose();
    }
    #endregion

    public class PassData
    {
        public TextureHandle visualizationTextureHandle;
    }

    public class PassContext
    {
        private PassData Data { get; }
        private ComputeGraphContext Context { get; }

        public PassContext(PassData data, ComputeGraphContext context)
        {
            Data = data;
            Context = context;
        }

        public void DispatchCompute(ComputeShader computeShader, string kernelName, int threadGroupsX, int threadGroupsY, int threadGroupsZ)
        {
            int kernelIndex = computeShader.FindKernel(kernelName);
            Context.cmd.DispatchCompute(computeShader, kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
        }

        public void SetGlobalBuffer(string name, ComputeBuffer value)
        {
            Context.cmd.SetGlobalBuffer(name, value);
        }

        public void SetGlobalInt(string name, int value)
        {
            Context.cmd.SetGlobalInt(name, value);
        }

        public void SetGlobalFloat(string name, float value)
        {
            Context.cmd.SetGlobalFloat(name, value);
        }

        public void SetGlobalVector(string name, Vector4 value)
        {
            Context.cmd.SetGlobalVector(name, value);
        }
    }
}

