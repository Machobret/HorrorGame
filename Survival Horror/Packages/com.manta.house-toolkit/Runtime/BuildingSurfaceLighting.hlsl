#ifndef HOUSE_TOOLKIT_BUILDING_SURFACE_LIGHTING_INCLUDED
#define HOUSE_TOOLKIT_BUILDING_SURFACE_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

Light SoftenBuildingPointLight(uint index, Light light, half3 receiverNormal)
{
    // URP's distance factor is approximately 1 / metres squared. Join the
    // original curve at two metres with a matching slope and a finite peak.
    const float join = 0.25;
    if (light.distanceAttenuation > join)
    {
        #if !USE_CLUSTER_LIGHT_LOOP
            index = GetPerObjectLightIndex(index);
        #endif
        #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
            half2 rangeAndCone = _AdditionalLightsBuffer[index].attenuation.xz;
        #else
            half2 rangeAndCone = _AdditionalLightsAttenuation[index].xz;
        #endif
        // Only a punctual light with a range and no spot cone is a point light.
        if (rangeAndCone.x > 0 && rangeAndCone.y == 0)
        {
            // A flush diffuser illuminates the wall at grazing incidence. Give
            // its near field a finite angular extent, fading to a point at 2 m.
            half facing = dot(receiverNormal, light.direction);
            if (facing > 0)
            {
                float nearWeight = saturate(1 - join / light.distanceAttenuation);
                light.direction = normalize(light.direction + receiverNormal * (0.45 * nearWeight * nearWeight));
            }
            light.distanceAttenuation = 2 * join - join * join / light.distanceAttenuation;
        }
    }
    return light;
}

Light BuildingSurfaceLight(uint index, InputData input, half4 shadowMask, AmbientOcclusionFactor occlusion)
{
    return SoftenBuildingPointLight(index, GetAdditionalLight(index, input, shadowMask, occlusion), input.normalWS);
}

Light BuildingSurfaceLight(uint index, float3 positionWS)
{
    return SoftenBuildingPointLight(index, GetAdditionalLight(index, positionWS), half3(0, 0, 0));
}

#define GetAdditionalLight BuildingSurfaceLight
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#undef GetAdditionalLight

#endif
