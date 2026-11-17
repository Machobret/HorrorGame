Shader "Horror Game/Fences/Chain Link"
{
    Properties
    {
        [MainTexture] _BaseMap("Chain link", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.52
        _Smoothness("Smoothness", Range(0,1)) = 0.25
        [HideInInspector] _Cull("Cull", Float) = 0
        [HideInInspector] _AlphaClip("Alpha clip", Float) = 1
        [HideInInspector] _AlphaToMask("Alpha to coverage", Float) = 1
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _BumpScale("Normal scale", Float) = 1
        [HideInInspector] _OcclusionStrength("Occlusion", Float) = 1
        [HideInInspector] _EmissionColor("Emission", Color) = (0,0,0,1)
        [HideInInspector] _SpecColor("Specular", Color) = (0.2,0.2,0.2,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            AlphaToMask [_AlphaToMask]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment FenceFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

            #define LitPassFragment StandardLitFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #undef LitPassFragment

            void FenceFragment(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC,
                out half4 outColor : SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
                #endif
            )
            {
                // A thin sheet needs an outward normal on whichever face is visible.
                input.normalWS *= IS_FRONT_VFACE(facing, 1, -1);
                StandardLitFragment(input, outColor
                    #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
                    #endif
                );
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment FenceDepthNormals
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"

            void FenceDepthNormals(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC,
                out half4 outNormalWS : SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
                #endif
            )
            {
                input.normalWS *= IS_FRONT_VFACE(facing, 1, -1);
                DepthNormalsFragment(input, outNormalWS
                    #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
                    #endif
                );
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }
    FallBack Off
}
