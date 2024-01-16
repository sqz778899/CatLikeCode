using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class ShadowSettings 
{
    [Min(0.001f)]
    public float maxDistance = 100f;
    
    [Range(0.001f, 1f)]
    public float distanceFade = 0.1f;
    
    public enum MapSize 
    {
        _256 = 256, _512 = 512, _1024 = 1024,
        _2048 = 2048, _4096 = 4096, _8192 = 8192
    }
    [System.Serializable]
    public struct Directional
    {
        public MapSize  atlasSize;
        
        [Range(1, 4)]
        public int cascadeCount;

        [Range(0f, 1f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
        
        [Range(0.001f, 1f)]
        public float cascadeFade;
        
        public Vector3 CascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
    }

    public Directional directional = new Directional
    {
        atlasSize = MapSize ._1024,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f,
        cascadeFade = 0.1f
    };
}

public class Shadows
{
    const int maxShadowedDirectionalLightCount = 4;
    const int maxCascades = 4;
    
    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    static int cascadeDataId = Shader.PropertyToID("_CascadeData");
    static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
    
    static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];
    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];
    static Vector4[] cascadeData = new Vector4[maxCascades];
    
    const string bufferName = "Shadows";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    ScriptableRenderContext context;

    CullingResults cullingResults;
    ShadowSettings settings;
    
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
    ShadowedDirectionalLight[] ShadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    int ShadowedDirectionalLightCount;
    
    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults,
        ShadowSettings settings) 
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        ShadowedDirectionalLightCount = 0;
    }

    void ExecuteBuffer () 
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    
    public void Render ()
    {
        if (ShadowedDirectionalLightCount > 0)
            RenderDirectionalShadows();
        else 
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1,
                32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
    }
    
    public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount&&
            light.shadows != LightShadows.None && light.shadowStrength > 0f &&
            cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            ShadowedDirectionalLights[ShadowedDirectionalLightCount] =
                new ShadowedDirectionalLight { visibleLightIndex = visibleLightIndex ,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane};
            return new Vector3(light.shadowStrength, settings.directional.cascadeCount * ShadowedDirectionalLightCount++,
                light.shadowNormalBias);
        }
        return Vector3.zero;
    }

    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        
        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split =  tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        
        for (int i = 0; i < ShadowedDirectionalLightCount; i++) 
        {
            RenderDirectionalShadows(i, split, tileSize);
        }
        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId, 
            new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f)));
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }
    
    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;
        
        for (int i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, 
                tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData);
            shadowSettings.splitData = splitData;
            if (index == 0) 
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }
            int tileIndex = tileOffset + i;
           
            dirShadowMatrices[tileIndex ] =  ConvertToAtlasMatrix(projectionMatrix * viewMatrix,
                SetTileViewport(tileIndex, split, tileSize), split);//获得每一个像素在光源视角下的深度信息
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }
    
    void SetCascadeData (int index, Vector4 cullingSphere, float tileSize) 
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        //cascadeData[index].x = 1f / cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cullingSphere.w *= cullingSphere.w;
        cascadeData[index] = new Vector4(1f / cullingSphere.w, texelSize * 1.4142136f);
    }
    Vector2 SetTileViewport(int index, int split,float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, 
            offset.y * tileSize, tileSize, tileSize));
        return offset;
    }
    
    Matrix4x4 ConvertToAtlasMatrix (Matrix4x4 m, Vector2 offset, int split) 
    {
        if (SystemInfo.usesReversedZBuffer) 
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }
    
    public void Cleanup () 
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }
}