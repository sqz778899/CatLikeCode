using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

    CullingResults cullingResults;
    ScriptableRenderContext context;
    Camera camera;
    Lighting lighting = new Lighting();
    
    public void Render (ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing)
    {
        this.context = context;
        this.camera = camera;
        
        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull()) { return; }
        
        Setup();
        lighting.Setup(context,cullingResults);
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
        DrawUnsupportedShaders();
        DrawGizmos();
        Submit();
    }
    
    void Setup () 
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, 
            flags <= CameraClearFlags.Color, 
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }
    
    void DrawVisibleGeometry (bool useDynamicBatching, bool useGPUInstancing) 
    {
        var sortingSettings = new SortingSettings(camera){criteria = SortingCriteria.CommonOpaque};
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        context.DrawSkybox(camera);
        
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }
    
    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }
    
    void ExecuteBuffer() 
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    
    bool Cull()
    {
        //ScriptableCullingParameters p
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p)) 
        {
            cullingResults = context.Cull(ref p);
            return true; 
        }
        return false;
    }
}