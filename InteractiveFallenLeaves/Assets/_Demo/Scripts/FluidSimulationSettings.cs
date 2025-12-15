using System;
using UnityEngine;

[Serializable]
public class FluidSimulationSettings
{
    public ComputeShader AddSourceTerm;
    public ComputeShader JacobiSolver;
    public ComputeShader NavierStokes;
    public ComputeShader BoundaryUtility;
    public ComputeShader StructuredBufferUtility;
    public ComputeShader VisualizationUtility;

    public float viscosity = 0.05f;
    public float deltaTime = 0.5f;
    public int solverIterations = 50;
    public float velocityEffectRadius = 15f;
    public float velocityFalloff = 3f;
    public float velocityMultiplier = 1f;
    public float densityEffectRadius = 30f;
    public float densityFalloff = 5f;

    public RenderTexture velocityRT;
}
