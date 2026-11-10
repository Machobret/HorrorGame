#ifndef ENVIRONMENT_AUTHORING_HORIZONTAL_WALL_MAPPING
#define ENVIRONMENT_AUTHORING_HORIZONTAL_WALL_MAPPING

// World registration keeps coplanar modules in phase without per-placement data.
// The sign rule also makes reversed wall fragments use the same horizontal axis.
float3 WallHorizontalAxis()
{
    float3 axis = normalize(mul((float3x3)GetObjectToWorldMatrix(), float3(1, 0, 0)));
    float signAxis = abs(axis.x) >= abs(axis.z) ? axis.x : axis.z;
    return signAxis < 0 ? -axis : axis;
}

float2 WallMappedUV(float2 authoredUV, float3 positionWS, float project, float3 axis)
{
    float3 world = GetAbsolutePositionWS(positionWS);
    float2 projected = float2(dot(world, axis) / max(_RepeatMetres, .001), world.y / max(_RepeatHeightMetres, .001));
    return lerp(authoredUV, projected, project) * _BaseMap_ST.xy + _BaseMap_ST.zw;
}

float2 BoxMappedUV(float3 positionWS, float3 normalWS)
{
    // Unit object directions follow the trim's orientation but discard its scale.
    // World positions preserve physical repeat size through resizing and cropping.
    float3 along = WallHorizontalAxis();
    float3 up = normalize(mul((float3x3)GetObjectToWorldMatrix(), float3(0, 1, 0)));
    float3 across = normalize(cross(along, up));
    float3 world = GetAbsolutePositionWS(positionWS);
    float3 position = float3(dot(world, along), dot(world, up), dot(world, across));
    float3 normal = abs(float3(dot(normalWS, along), dot(normalWS, up), dot(normalWS, across)));
    // Prefer tops, then ends at ties. A small tolerance prevents rotated 45-degree
    // bevels switching planes solely from floating-point/normal precision.
    float2 plane = normal.y + .001 >= max(normal.x, normal.z) ? position.xz
        : normal.x + .001 >= normal.z ? position.zy : position.xy;
    return plane / max(float2(_RepeatMetres, _RepeatHeightMetres), .001) * _BaseMap_ST.xy + _BaseMap_ST.zw;
}

float3 CoverHorizontalAxis(float3 normalWS)
{
    float3 axis = normalize(cross(float3(0, 1, 0), normalWS));
    float signAxis = abs(axis.x) >= abs(axis.z) ? axis.x : axis.z;
    return signAxis < 0 ? -axis : axis;
}

// Build the normal-map frame from the actual sampled UVs. This handles mirrored
// UV axes and reversed projected U without assuming an imported tangent sign.
half3 WallMappedNormal(float3 positionWS, half3 normalWS, float2 uv, half2 encodedXY)
{
    // Keep an initialized result through the uniform material branch. This also
    // avoids derivative work for colour/roughness-only construction finishes.
    half3 result = normalWS;
    UNITY_BRANCH if (_NormalStrength > 0)
    {
        float3 dx = ddx(positionWS), dy = ddy(positionWS);
        float2 du = ddx(uv), dv = ddy(uv);
        float determinant = du.x * dv.y - du.y * dv.x;
        if (abs(determinant) >= 1e-10)
        {
            // Invert the signed UV derivative matrix. Normalize independently so
            // changing repeat width cannot change apparent normal strength.
            float3 tangent = (dx * dv.y - dy * du.y) * sign(determinant);
            float3 authoredUp = (dy * du.x - dx * dv.x) * sign(determinant);
            tangent = normalize(tangent - normalWS * dot(normalWS, tangent));
            float3 bitangent = cross(normalWS, tangent);
            bitangent *= dot(bitangent, authoredUp) < 0 ? -1 : 1;
            half2 xy = (encodedXY * 2 - 1) * _NormalStrength;
            half3 normalTS = half3(xy, sqrt(saturate(1 - dot(xy, xy))));
            result = normalize(tangent * normalTS.x + bitangent * normalTS.y + normalWS * normalTS.z);
        }
    }
    return result;
}

// Blend three world projections. Each normal uses the same UV-derived frame as
// our existing mapping, then blends in world space; flat maps retain the mesh normal.
void TriplanarSurface(float3 positionWS, half3 normalWS, out half4 color,
    out half roughness, out half3 mappedNormal)
{
    float3 world = GetAbsolutePositionWS(positionWS);
    float3 weight = pow(abs(normalWS), max(_BlendSharpness, 1));
    weight /= max(dot(weight, 1), .00001);
    float2 repeat = max(float2(_RepeatMetres, _RepeatHeightMetres), .001);
    float2 uvX = world.zy / repeat * _BaseMap_ST.xy + _BaseMap_ST.zw;
    float2 uvY = world.xz / repeat * _BaseMap_ST.xy + _BaseMap_ST.zw;
    float2 uvZ = world.xy / repeat * _BaseMap_ST.xy + _BaseMap_ST.zw;
    half4 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX);
    half4 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY);
    half4 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ);
    half4 px = SampleWallSurface(uvX);
    half4 py = SampleWallSurface(uvY);
    half4 pz = SampleWallSurface(uvZ);
    color = (x * weight.x + y * weight.y + z * weight.z) * _BaseColor;
    roughness = px.b * weight.x + py.b * weight.y + pz.b * weight.z;
    mappedNormal = normalize(WallMappedNormal(positionWS, normalWS, uvX, px.rg) * weight.x
        + WallMappedNormal(positionWS, normalWS, uvY, py.rg) * weight.y
        + WallMappedNormal(positionWS, normalWS, uvZ, pz.rg) * weight.z);
}
#endif
