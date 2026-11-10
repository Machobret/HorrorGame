Shader "Horror Game/Windows/Gravel Glass"
{
    Properties
    {
        [MainColor] _Tint("Glass tint", Color) = (0.97, 0.99, 1, 1)
        _Opacity("Opacity", Range(0, 1)) = 0.9
        [NoScaleOffset][Normal] _NormalMap("Gravel normal", 2D) = "bump" {}
        _BumpScale("Gravel scale (repeats / metre)", Range(0.1, 20)) = 1
        _NormalStrength("Gravel normal strength", Range(0, 2)) = 0.3
        _RefractionDistance("Refraction distance (metres)", Range(0, 0.05)) = 0.012
        _MinimumRefractionPixels("Minimum refraction scale (reference pixels)", Range(0, 16)) = 0
        _RefractionReferenceHeight("Refraction reference image height", Float) = 180
        _Smoothness("Smoothness", Range(0, 1)) = 0.96
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Glass"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GlassVertex
            #pragma fragment GlassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Opacity, _NormalStrength, _RefractionDistance, _Smoothness;
                float _MinimumRefractionPixels, _RefractionReferenceHeight, _BumpScale;
            CBUFFER_END
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings GlassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half3 WorldGravelNormal(float3 positionWS, half3 normalWS)
            {
                // World projections keep one physical grain size across UV seams,
                // mesh dimensions, rotations and non-uniform object scales.
                float3 p = positionWS * max(_BumpScale, .001);
                half3 x = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p.zy));
                half3 y = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p.xz));
                half3 z = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p.xy));
                half3 weights = normalWS * normalWS;
                weights *= weights;
                weights /= max(dot(weights, half3(1, 1, 1)), .0001);
                half3 gradient = half3(0, x.y, x.x) * (weights.x * sign(normalWS.x))
                    + half3(y.x, 0, y.y) * (weights.y * sign(normalWS.y))
                    + half3(z.x, z.y, 0) * (weights.z * sign(normalWS.z));
                gradient -= normalWS * dot(gradient, normalWS);
                return normalize(normalWS + gradient * _NormalStrength);
            }
            half4 GlassFragment(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 geometricNormal = normalize(input.normalWS);
                half faceSign = IS_FRONT_VFACE(facing, 1, -1);
                geometricNormal *= faceSign;
                half3 normalWS = WorldGravelNormal(input.positionWS, geometricNormal);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float eyeDepth = max(-TransformWorldToView(input.positionWS).z, .01);
                float projectionDepth = lerp(eyeDepth, 1, unity_OrthoParams.w);
                // Only the normal-map perturbation bends the view. Strength zero
                // therefore removes refraction even when viewing the pane obliquely.
                float3 normalDeltaVS = TransformWorldToViewDir(normalWS - geometricNormal, false);
                float2 worldScale = abs(float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11))
                    * (_RefractionDistance * .5 / projectionDepth);
                // Keep authored micro-refraction readable after pixelation. This
                // is a static normal-driven scale floor, without animated noise.
                // A zero floor retains the original world-displacement behavior.
                float2 pixelScale = float2(_ScaledScreenParams.y / _ScaledScreenParams.x, 1)
                    * (_MinimumRefractionPixels / max(_RefractionReferenceHeight, 1));
                float2 offset = normalDeltaVS.xy * max(worldScale, pixelScale);
                float2 halfTexel = _CameraOpaqueTexture_TexelSize.xy * .5;
                float2 refractedUV = clamp(screenUV + offset, halfTexel, 1 - halfTexel);
                // Do not drag the frame or a foreground object across the pane.
                float rawDepth = SampleSceneDepth(refractedUV);
                float sampledDepth = unity_OrthoParams.w > .5 ? LinearDepthToEyeDepth(rawDepth) : LinearEyeDepth(rawDepth, _ZBufferParams);
                if (sampledDepth < eyeDepth - .002) refractedUV = screenUV;
                half3 transmission = SampleSceneColor(refractedUV) * _Tint.rgb;

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = normalWS;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.normalizedScreenSpaceUV = screenUV;
                lighting.bakedGI = SampleSH(normalWS);
                lighting.shadowMask = half4(1, 1, 1, 1);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                lighting.vertexLighting = VertexLighting(input.positionWS, normalWS);
                SurfaceData surface = (SurfaceData)0;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                surface.alpha = 1;
                half3 reflectedLight = UniversalFragmentPBR(lighting, surface).rgb;
                half grazing = 1 - saturate(dot(normalWS, lighting.viewDirectionWS));
                half fresnel = .04 + .96 * grazing * grazing * grazing * grazing * grazing;
                return half4(transmission * (1 - fresnel) + reflectedLight, saturate(_Opacity));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
