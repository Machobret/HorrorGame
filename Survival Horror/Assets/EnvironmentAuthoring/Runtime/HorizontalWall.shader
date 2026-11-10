Shader "Environment Authoring/Horizontal Wall (Prototype)"
{
    Properties
    {
        [MainTexture] _BaseMap("Base color", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        [NoScaleOffset] _SurfaceMap("Linear packed surface: normal XY, roughness, height", 2D) = "white" {}
        [ToggleUI] _UseNormalMap("Use standard normal map instead of packed XY", Float) = 0
        [Normal][NoScaleOffset] _BumpMap("Normal map", 2D) = "bump" {}
        [Enum(Authored UV,0,Wall projection,1,Box projection,2,Triplanar,3)] _Mapping("Mapping", Float) = 3
        _RepeatMetres("U repeat (metres)", Float) = 2
        _RepeatHeightMetres("V repeat (metres)", Float) = 2.8
        _BlendSharpness("Triplanar blend sharpness", Range(1,16)) = 4
        _NormalStrength("Normal strength", Range(0,1)) = 0
        // Height is reserved for future layer blending, not displacement.
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _Mapping, _RepeatMetres, _RepeatHeightMetres, _NormalStrength, _BlendSharpness;
            float _UseNormalMap;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        half4 SampleWallSurface(float2 uv)
        {
            half4 packed = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, uv);
            // Unity normal-map imports can use platform-specific channel packing.
            // Decode first; the mapping code expects plain encoded normal XY.
            UNITY_BRANCH if (_UseNormalMap > .5 && _NormalStrength > 0)
                packed.rg = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv)).xy * .5 + .5;
            return packed;
        }
        #include "HorizontalWallMapping.hlsl"
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 lightmapUV : TEXCOORD1;
            float2 coverMapping : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
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
            #pragma editor_sync_compilation
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                // Compatible wall sources run along local X, with hard broad-face
                // normals along Z. Source caps/reveals keep both authored UVs.
                bool cover = input.coverMapping.x > 2.5 && input.coverMapping.x < 3.5;
                bool coverSide = cover && abs(input.normalOS.y) < .001;
                float project = _Mapping > .5 && (coverSide || abs(input.normalOS.z) > .999) ? 1 : 0;
                float3 axis = coverSide ? CoverHorizontalAxis(output.normalWS) : WallHorizontalAxis();
                output.uv = _Mapping > 1.5 ? input.uv : WallMappedUV(input.uv, position.positionWS, project, axis);
                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }
            half4 WallFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normalWS = normalize(input.normalWS);
                // Box selection happens per pixel, so smooth/beveled triangles do
                // not interpolate unrelated projection planes between vertices.
                half4 color;
                half roughness;
                half3 mappedNormal;
                // Packed surfaces need two reads per projection. A separate
                // standard normal map adds one read when normal strength is nonzero.
                UNITY_BRANCH if (_Mapping > 2.5)
                    TriplanarSurface(input.positionWS, normalWS, color, roughness, mappedNormal);
                else
                {
                    float2 uv = _Mapping > 1.5 ? BoxMappedUV(input.positionWS, normalWS) : input.uv;
                    color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                    half4 packed = SampleWallSurface(uv);
                    roughness = packed.b;
                    mappedNormal = WallMappedNormal(input.positionWS, normalWS, uv, packed.rg);
                }
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
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
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #endif
                return positionCS;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
