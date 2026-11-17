Shader "Horror Game/Environment/Shelf Trim"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap("Trim sheet", 2D) = "white" {}
        _FaceRegion("Face UV region (origin XY, size ZW)", Vector) = (0,0,0,0)
        _EndRegion("End grain UV region (origin XY, size ZW)", Vector) = (0,0,0,0)
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.15
        _Metallic("Metallic", Range(0,1)) = 0
        [PerRendererData] _FinishValue("Finish brightness", Float) = 1
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
        [HideInInspector] _BumpScale("Normal scale", Float) = 1
        [HideInInspector] _SpecColor("Specular", Color) = (0.2,0.2,0.2,1)
        [HideInInspector] _EmissionColor("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" "DisableBatching"="True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TrimVertex
            #pragma fragment TrimFragment
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            // The shared steel texture uses point filtering on other props. Blend its texels on the rack.
            #define sampler_BaseMap sampler_TrilinearRepeat
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #undef sampler_BaseMap

            UNITY_INSTANCING_BUFFER_START(ShelfFinish)
                UNITY_DEFINE_INSTANCED_PROP(float, _FinishValue)
            UNITY_INSTANCING_BUFFER_END(ShelfFinish)

            float4 _FaceRegion, _EndRegion;

            void TrimVertex(Attributes input, out Varyings output, out float4 region : TEXCOORD11)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                output = LitPassVertex(input);
                float3 tangent = input.tangentOS.xyz;
                float3 bitangent = cross(input.normalOS, tangent);
                float3x3 model = (float3x3)GetObjectToWorldMatrix();
                float2 scale = float2(length(mul(model, tangent)), length(mul(model, bitangent)));
                float2 uv = input.texcoord;
                bool endGrain = all(_EndRegion.zw > 0) && all(uv >= _EndRegion.xy)
                    && all(uv <= _EndRegion.xy + _EndRegion.zw);
                region = endGrain ? _EndRegion : _FaceRegion;
                output.uv = (uv - region.xy) * scale;
            }

            void TrimFragment(Varyings input, float4 region : TEXCOORD11, out half4 color : SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                , out uint layers : SV_Target1
                #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // A zero-sized region uses the texture's normal repeat. Mirror inside an atlas
                // strip so stretched planks keep their grain density and never sample another trim.
                if (all(region.zw > 0))
                    input.uv = region.xy + region.zw * (1 - abs(2 * frac(.5 * input.uv / region.zw) - 1));
                LitPassFragment(input, color
                    #ifdef _WRITE_RENDERING_LAYERS
                    , layers
                    #endif
                );
                color.rgb *= UNITY_ACCESS_INSTANCED_PROP(ShelfFinish, _FinishValue);
            }
            ENDHLSL
        }
        // Both finishes are opaque; the depth and shadow passes only need the shared mesh.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    Fallback Off
}
