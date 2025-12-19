using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
[VolumeComponentMenu("Custom/FluidSimulationVolume")]
public class FluidSimulationVolume : VolumeComponent
{
    public FloatParameter velocityPressed = new FloatParameter(0f);
    public FloatParameter densityPressed = new FloatParameter(0f);
    public Vector2Parameter currentPosition = new Vector2Parameter(Vector2.zero);
    public FloatParameter playerVerticalVelocity = new FloatParameter(0f);
}
