using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline :  RenderPipeline
{
    bool useDynamicBatching;
    bool useGPUInstancing;
    public CustomRenderPipeline (bool useDynamicBatching,
        bool useGPUInstancing, bool useSRPBatcher)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;
    }
    CameraRenderer renderer = new CameraRenderer();
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        
    }

    protected override void Render(ScriptableRenderContext context,  List<Camera> cameras)
    {
        for (int i = 0; i < cameras.Count; i++) {
            renderer.Render(context, cameras[i], useDynamicBatching, useGPUInstancing);
        }
    }
}