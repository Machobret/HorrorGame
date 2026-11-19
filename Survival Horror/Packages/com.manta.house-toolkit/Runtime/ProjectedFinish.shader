Shader "House Toolkit/Projected Finish"
{
    Properties
    {
        [MainTexture] _BaseMap("Base color", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        [NoScaleOffset] _SurfaceMap("Linear packed surface: normal XY, roughness, height", 2D) = "white" {}
        _RepeatMetres("U repeat (metres)", Float) = 2
        _RepeatHeightMetres("V repeat (metres)", Float) = 2.8
        _NormalStrength("Normal strength (enable after assigning packed surface)", Range(0,1)) = 0
        [HideInInspector] _HouseRight("House right", Vector) = (1,0,0,0)
        [HideInInspector] _HouseUp("House up", Vector) = (0,1,0,0)
        [HideInInspector] _HouseForward("House forward", Vector) = (0,0,1,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "BuildingSurfaceLighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _RepeatMetres, _RepeatHeightMetres, _NormalStrength;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
        #include "HouseFinishUV.hlsl"
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
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 lightmapUV : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        float4 DepthVertex(Attributes input) : SV_POSITION
        {
            UNITY_SETUP_INSTANCE_ID(input);
            return TransformObjectToHClip(input.positionOS.xyz);
        }
        half4 DepthFragment() : SV_Target { return 0; }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WallVertex
            #pragma fragment WallFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog

            Varyings WallVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }
            half4 WallFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normalWS = normalize(input.normalWS);
                float2 uv = FinishUV(input.positionWS, normalWS);
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                half4 packed = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, uv);
                half roughness = packed.b;
                half3 mappedNormal = WallMappedNormal(input.positionWS, normalWS, uv, packed.rg);
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                #if defined(_ADDITIONAL_LIGHT_SHADOWS)
                    // Keep grazing point lights from sampling the wall's own shadow.
                    lighting.positionWS += normalWS * 0.003;
                #endif
                lighting.normalWS = mappedNormal;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                lighting.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, lighting.normalWS);
                lighting.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = color.rgb;
                surface.smoothness = 1 - roughness;
                surface.occlusion = 1;
                surface.alpha = 1;
                half4 result = UniversalFragmentPBR(lighting, surface);
                result.rgb = MixFog(result.rgb, ComputeFogFactor(TransformWorldToHClip(input.positionWS).z));
                return result;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;
            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                return ApplyShadowClamping(positionCS);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
