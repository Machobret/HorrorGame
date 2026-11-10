Shader "Horror Game/Environment/Weathered Brick"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap("Clean brick", 2D) = "white" {}
        [NoScaleOffset] _WeatheredMap("Weathered brick", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _RepeatMetres("Brick tile width (metres)", Float) = 2
        _RepeatHeightMetres("Brick tile height (metres)", Float) = 2.8
        _WeatheringPatchSize("Weathering patch size (metres)", Float) = 6
        _WeatheringAmount("Weathering amount", Range(0,1)) = 0.5
        _WeatheringSoftness("Blend softness", Range(0.01,1)) = 0.4
        _WeatheringOffset("Weathering pattern offset", Vector) = (13.7,29.1,47.6,0)
        _Roughness("Clean roughness", Range(0,1)) = 0.9
        _WeatheredRoughness("Weathered roughness", Range(0,1)) = 0.98
        [Normal][NoScaleOffset] _BumpMap("Optional shared normal map", 2D) = "bump" {}
        _NormalStrength("Normal strength", Range(0,1)) = 0
        [Enum(Authored UV,0,Wall projection,1,Box projection,2,Triplanar,3)] _Mapping("Mapping", Float) = 1
        _BlendSharpness("Triplanar sharpness", Range(1,16)) = 4
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
            float4 _WeatheringOffset;
            float _RepeatMetres, _RepeatHeightMetres, _Mapping, _BlendSharpness;
            float _WeatheringPatchSize, _WeatheringAmount, _WeatheringSoftness;
            float _NormalStrength, _Roughness, _WeatheredRoughness;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_WeatheredMap); SAMPLER(sampler_WeatheredMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        half4 SampleWallSurface(float2 uv)
        {
            half2 xy = half2(.5, .5);
            UNITY_BRANCH if (_NormalStrength > 0)
                xy = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv)).xy * .5 + .5;
            return half4(xy, _Roughness, 0);
        }
        // Share the engine's wall/corner mapping and derivative normal frame.
        #include "Assets/EnvironmentAuthoring/Runtime/HorizontalWallMapping.hlsl"

        float WeatheringHash(float3 p)
        {
            p = frac(p * .1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }
        float WeatheringNoise(float3 p)
        {
            float3 cell = floor(p), f = frac(p);
            f = f * f * f * (f * (f * 6 - 15) + 10);
            float a = lerp(WeatheringHash(cell), WeatheringHash(cell + float3(1,0,0)), f.x);
            float b = lerp(WeatheringHash(cell + float3(0,1,0)), WeatheringHash(cell + float3(1,1,0)), f.x);
            float c = lerp(WeatheringHash(cell + float3(0,0,1)), WeatheringHash(cell + float3(1,0,1)), f.x);
            float d = lerp(WeatheringHash(cell + float3(0,1,1)), WeatheringHash(cell + float3(1,1,1)), f.x);
            return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
        }
        half WeatheringMask(float3 positionWS)
        {
            // Exact endpoints let one material range from clean to fully weathered.
            UNITY_BRANCH if (_WeatheringAmount <= 0) return 0;
            UNITY_BRANCH if (_WeatheringAmount >= 1) return 1;
            float3 p = GetAbsolutePositionWS(positionWS) / max(_WeatheringPatchSize, .01) + _WeatheringOffset.xyz;
            float noise = .75 * WeatheringNoise(p) + .25 * WeatheringNoise(p * 2.11 + float3(17,31,47));
            float threshold = 1 - _WeatheringAmount;
            float halfWidth = max(_WeatheringSoftness, .001) * .5;
            return smoothstep(threshold - halfWidth, threshold + halfWidth, noise);
        }
        half4 SampleWeatheredColor(float3 positionWS, half3 normalWS, float2 uv)
        {
            UNITY_BRANCH if (_Mapping > 2.5)
            {
                float3 world = GetAbsolutePositionWS(positionWS);
                float3 weights = pow(abs(normalWS), max(_BlendSharpness, 1));
                weights /= max(dot(weights, float3(1,1,1)), .00001);
                float2 repeat = max(float2(_RepeatMetres, _RepeatHeightMetres), .001);
                float2 uvX = world.zy / repeat * _BaseMap_ST.xy + _BaseMap_ST.zw;
                float2 uvY = world.xz / repeat * _BaseMap_ST.xy + _BaseMap_ST.zw;
                float2 uvZ = world.xy / repeat * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return (SAMPLE_TEXTURE2D(_WeatheredMap, sampler_WeatheredMap, uvX) * weights.x
                    + SAMPLE_TEXTURE2D(_WeatheredMap, sampler_WeatheredMap, uvY) * weights.y
                    + SAMPLE_TEXTURE2D(_WeatheredMap, sampler_WeatheredMap, uvZ) * weights.z) * _BaseColor;
            }
            return SAMPLE_TEXTURE2D(_WeatheredMap, sampler_WeatheredMap, uv) * _BaseColor;
        }
        void BrickSurface(float3 positionWS, half3 normalWS, float2 authoredUV,
            out half4 color, out half roughness, out half3 mappedNormal)
        {
            float2 uv = _Mapping > 1.5 ? BoxMappedUV(positionWS, normalWS) : authoredUV;
            UNITY_BRANCH if (_Mapping > 2.5)
                TriplanarSurface(positionWS, normalWS, color, roughness, mappedNormal);
            else
            {
                color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                half4 packed = SampleWallSurface(uv);
                roughness = packed.b;
                mappedNormal = WallMappedNormal(positionWS, normalWS, uv, packed.rg);
            }
            half weathering = WeatheringMask(positionWS);
            color = lerp(color, SampleWeatheredColor(positionWS, normalWS, uv), weathering);
            roughness = lerp(roughness, _WeatheredRoughness, weathering);
        }
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 lightmapUV : TEXCOORD1;
            float2 dynamicLightmapUV : TEXCOORD2;
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
        Varyings BrickVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            bool coverSide = input.coverMapping.x > 2.5 && input.coverMapping.x < 3.5 && abs(input.normalOS.y) < .001;
            float project = _Mapping > .5 && (coverSide || abs(input.normalOS.z) > .999) ? 1 : 0;
            float3 axis = coverSide ? CoverHorizontalAxis(output.normalWS) : WallHorizontalAxis();
            output.uv = _Mapping > 1.5 ? input.uv : WallMappedUV(input.uv, position.positionWS, project, axis);
            OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
            OUTPUT_SH(output.normalWS, output.vertexSH);
            return output;
        }
        half4 BrickFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 color;
            half roughness;
            half3 mappedNormal;
            BrickSurface(input.positionWS, normalize(input.normalWS), input.uv, color, roughness, mappedNormal);
            InputData lighting = (InputData)0;
            lighting.positionWS = input.positionWS;
            lighting.normalWS = mappedNormal;
            lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
            #else
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #endif
            lighting.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, mappedNormal);
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
        float4 DepthVertex(Attributes input) : SV_POSITION
        { UNITY_SETUP_INSTANCE_ID(input); return TransformObjectToHClip(input.positionOS.xyz); }
        half4 DepthFragment() : SV_Target { return 0; }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BrickVertex
            #pragma fragment BrickFragment
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
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BrickVertex
            #pragma fragment BrickDepthNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 BrickDepthNormals(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color;
                half roughness;
                half3 normal;
                BrickSurface(input.positionWS, normalize(input.normalWS), input.uv, color, roughness, normal);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(octNormal * .5 + .5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BrickMetaVertex
            #pragma fragment BrickMetaFragment
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            Varyings BrickMetaVertex(Attributes input)
            {
                Varyings output = BrickVertex(input);
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.lightmapUV, input.dynamicLightmapUV,
                    unity_LightmapST, unity_DynamicLightmapST);
                return output;
            }
            half4 BrickMetaFragment(Varyings input) : SV_Target
            {
                half4 color;
                half roughness;
                half3 normal;
                BrickSurface(input.positionWS, normalize(input.normalWS), input.uv, color, roughness, normal);
                MetaInput meta = (MetaInput)0;
                meta.Albedo = color.rgb;
                return MetaFragment(meta);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
