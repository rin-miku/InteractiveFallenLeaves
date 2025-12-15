using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FluidSimulationRendererFeature : ScriptableRendererFeature
{
    public FluidSimulationSettings settings;
    public VolumeProfile volumeProfile;

    private FluidSimulationRenderPass renderPass;

    public override void Create()
    {
        renderPass = new FluidSimulationRenderPass(settings, volumeProfile);
        renderPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(renderPass);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        renderPass.Dispose();
    }
}
