Shader "Horror Game/Environment/Filler Facade Atlas"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap("Facade atlas (6 x 6)", 2D) = "white" {}
        [NoScaleOffset] _DisplayMap("Storefront atlas", 2D) = "white" {}
        [HideInInspector] _RoofTrimHeight("Roofline height (m)", Float) = 0.32
        [Toggle] _PlainMetalCoping("Use plain metal coping", Float) = 0
        [HideInInspector] _DepotLayout("Depot atlas layout", Float) = 0
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.15
        _Metallic("Metallic", Range(0,1)) = 0
        [IntRange] _FamilyRow("Building family row", Range(0,4)) = 0
        [Enum(Regular,0,Blank,1,Display,2)] _BayType("Bay type", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
        [HideInInspector] _BumpScale("Normal scale", Float) = 1
        [HideInInspector] _SpecColor("Specular", Color) = (0.2,0.2,0.2,1)
        [HideInInspector] _EmissionColor("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }
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
            #define _ENVIRONMENTREFLECTIONS_OFF 1
            #define _SPECULARHIGHLIGHTS_OFF 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            TEXTURE2D(_DisplayMap);
            SAMPLER(sampler_DisplayMap);

            half4 FacadeAlbedo(float2 uv, TEXTURE2D_PARAM(baseMap, baseSampler))
            {
                // Negative U identifies quads using the separate shop-window atlas.
                if (uv.x < 0) return SAMPLE_TEXTURE2D(_DisplayMap, sampler_DisplayMap, float2(-uv.x, uv.y));
                return SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(baseMap, baseSampler));
            }

            #define SampleAlbedoAlpha FacadeAlbedo
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #undef SampleAlbedoAlpha
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            float _FamilyRow, _BayType, _PlainMetalCoping, _DepotLayout;

            uint Mix(uint value)
            {
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                return value * 0x846ca68bu;
            }

            Varyings AtlasVertex(Attributes input,float4 bay : TEXCOORD5, float4 style : COLOR)
            {
                Varyings output=LitPassVertex(input);
                bool entrance=bay.z<0;
                uint index=(uint)(entrance ? -1-bay.z : bay.z);
                uint hash=Mix((uint)bay.w ^ Mix((uint)bay.x) ^ Mix((uint)bay.y+127u) ^ Mix(index+257u));
                // White vertex colours retain the source material settings on custom / legacy meshes.
                bool packed=style.r<.5 && style.r>0;
                uint family=packed ? (uint)round(style.r*255)-1u : (uint)clamp(round(_FamilyRow),0,4);
                float mode=packed ? round(style.g*255) : _BayType;
                float metal=packed ? round(style.b*255) : _PlainMetalCoping;
                uint variant=(hash^(hash>>16))%5u;
                uint tile=family*6u+(entrance ? 5u : mode>.5 ? 0u : variant);
                float2 within=lerp(.015,.985,saturate(input.texcoord));
                if (_DepotLayout>.5)
                {
                    bool ground=bay.y==0;
                    uint column=ground ? (entrance ? 1u : 0u) : (hash&1u);
                    // Texel bounds on the square depot sheet, measured from the top.
                    float2 rows=ground ? float2(0,427) : float2(427,683);
                    if (bay.x<0) rows=float2(683,715);
                    else if ((ground && mode==1) || (!ground && hash%5u==0u)) rows=float2(715,1024);
                    output.uv=float2((column+within.x)*.5,1-lerp(rows.y,rows.x,within.y)/1024.0);
                    return output;
                }
                if (bay.x<0)
                {
                    tile=30u+(metal>.5 ? 5u : family);
                    within.y=lerp(217.0/256.0,254.0/256.0,saturate(input.texcoord.y));
                }
                else if (!entrance && mode>1.5)
                {
                    float2 displayUV=(float2(family%3u,2u-family/3u)+within)/3.0;
                    output.uv=float2(-displayUV.x,displayUV.y);
                    return output;
                }
                output.uv=(float2(tile%6u,5u-tile/6u)+within)/6.0;
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
