#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED


float3 IncomingLight (Surface surface, Light light)
{
    return dot(surface.normal, light.direction) * light.color * light.attenuation;
}

float3 GetLighting (Surface surface,BRDF brdf, Light light)
{
    return IncomingLight(surface, light) * brdf.diffuse * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Surface surfaceWS, BRDF brdf)
{
    float3 color = 0.0;
    for (int i = 0; i < GetDirectionalLightCount(); i++)
        color += GetLighting(surfaceWS, brdf, GetDirectionalLight(i,surfaceWS));
    return color;
}
#endif