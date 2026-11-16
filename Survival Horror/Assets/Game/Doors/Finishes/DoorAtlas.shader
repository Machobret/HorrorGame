Shader "Horror Game/Doors/Door Atlas"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap("Door atlas (4 columns, 2 rows)", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.15
        _Metallic("Metallic", Range(0,1)) = 0
        [PerRendererData] _AtlasTile("Door design", Float) = 0
        [HideInInspector] _AtlasLayout("Columns, rows, unit UVs", Vector) = (4,2,0,0)
        [PerRendererData] _VisibleHeight("Visible leaf fraction", Float) = 1
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
            #pragma vertex AtlasVertex
            #pragma fragment LitPassFragment
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            float4 _AtlasLayout;
            UNITY_INSTANCING_BUFFER_START(DoorAtlas)
                UNITY_DEFINE_INSTANCED_PROP(float, _AtlasTile)
                UNITY_DEFINE_INSTANCED_PROP(float, _VisibleHeight)
            UNITY_INSTANCING_BUFFER_END(DoorAtlas)

            Varyings AtlasVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = LitPassVertex(input);
                uint2 grid = max(uint2(1,1), (uint2)_AtlasLayout.xy);
                uint tile = (uint)clamp(round(UNITY_ACCESS_INSTANCED_PROP(DoorAtlas, _AtlasTile)), 0.0, (float)(grid.x * grid.y - 1));
                float2 offset = float2(tile % grid.x, -(float)(tile / grid.x)) / grid;
                output.uv = input.texcoord + offset; // Existing doors have first-cell UVs baked into the mesh.
                if (_AtlasLayout.z > .5)
                {
                    float2 uv = saturate(input.texcoord);
                    uv.y *= UNITY_ACCESS_INSTANCED_PROP(DoorAtlas, _VisibleHeight);
                    float2 inset = .5 * _BaseMap_TexelSize.xy;
                    output.uv = float2(0, 1 - 1.0/grid.y) + offset + lerp(inset, 1.0/grid-inset, uv);
                }
                return output;
            }
            ENDHLSL
        }
        // Opaque geometry has no atlas-dependent cutouts or normal map; reuse URP's instanced passes.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    Fallback Off
}
