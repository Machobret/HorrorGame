#ifndef HOUSE_TOOLKIT_FINISH_UV_INCLUDED
#define HOUSE_TOOLKIT_FINISH_UV_INCLUDED

UNITY_INSTANCING_BUFFER_START(HouseProjection)
    UNITY_DEFINE_INSTANCED_PROP(float4, _HouseRight)
    UNITY_DEFINE_INSTANCED_PROP(float4, _HouseUp)
    UNITY_DEFINE_INSTANCED_PROP(float4, _HouseForward)
UNITY_INSTANCING_BUFFER_END(HouseProjection)

float2 FinishUV(float3 positionWS, float3 normalWS)
{
    float4 x = UNITY_ACCESS_INSTANCED_PROP(HouseProjection, _HouseRight);
    float4 y = UNITY_ACCESS_INSTANCED_PROP(HouseProjection, _HouseUp);
    float4 z = UNITY_ACCESS_INSTANCED_PROP(HouseProjection, _HouseForward);
    float4 world = float4(GetAbsolutePositionWS(positionWS), 1);
    float3 p = float3(dot(x, world), dot(y, world), dot(z, world));
    float3 n = float3(dot(x.xyz, normalWS), dot(y.xyz, normalWS), dot(z.xyz, normalWS));
    float2 plane = p.xz;
    if (abs(n.y) < .7071)
    {
        // Horizontal distance along even a diagonal wall stays in metres.
        float2 along = normalize(float2(n.z, -n.x));
        if ((abs(along.x) + .0001 >= abs(along.y) ? along.x : along.y) < 0) along = -along;
        plane = float2(dot(p.xz, along), p.y);
    }
    return plane / max(float2(_RepeatMetres, _RepeatHeightMetres), .001) * _BaseMap_ST.xy + _BaseMap_ST.zw;
}

#endif
