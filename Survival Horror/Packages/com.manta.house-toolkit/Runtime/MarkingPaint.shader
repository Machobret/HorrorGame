Shader "House Toolkit/Instanced Marking Paint"
{
    Properties
    {
        [NoScaleOffset] _WearMap("Paint scuffs", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0,1)) = .12
        [PerRendererData] _PaintTint("Tint", Color) = (.48,.24,.02,1)
        [PerRendererData] _PaintWear("Wear, seed and scale", Vector) = (.35,0,0,1)
        [PerRendererData] _Corner0("Corner 0", Vector) = (0,0,0,0)
        [PerRendererData] _Corner1("Corner 1", Vector) = (1,0,0,0)
        [PerRendererData] _Corner2("Corner 2", Vector) = (1,0,1,0)
        [PerRendererData] _Corner3("Corner 3", Vector) = (0,0,1,0)
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half _Smoothness;
        CBUFFER_END
        UNITY_INSTANCING_BUFFER_START(Paint)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Corner0)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Corner1)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Corner2)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Corner3)
            UNITY_DEFINE_INSTANCED_PROP(float4, _PaintTint)
            UNITY_DEFINE_INSTANCED_PROP(float4, _PaintWear)
        UNITY_INSTANCING_BUFFER_END(Paint)
        TEXTURE2D(_WearMap); SAMPLER(sampler_WearMap);
        #include "PaintWear.hlsl"
        struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            float3 normalWS:TEXCOORD1;
            float2 paintUV:TEXCOORD2;
            float2 edgeUV:TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vertex(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Varyings o = (Varyings)0;
            UNITY_TRANSFER_INSTANCE_ID(input,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 a = UNITY_ACCESS_INSTANCED_PROP(Paint,_Corner0).xyz;
            float3 b = UNITY_ACCESS_INSTANCED_PROP(Paint,_Corner1).xyz;
            float3 c = UNITY_ACCESS_INSTANCED_PROP(Paint,_Corner2).xyz;
            float3 d = UNITY_ACCESS_INSTANCED_PROP(Paint,_Corner3).xyz;
            // Piecewise affine deformation matches the CPU triangles used for bounds/picking.
            float3 p = input.uv.y >= input.uv.x ? a + (c-d)*input.uv.x + (d-a)*input.uv.y : a + (b-a)*input.uv.x + (c-b)*input.uv.y;
            o.positionWS = TransformObjectToWorld(p);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(normalize(cross(d-a,b-a)));
            o.paintUV = p.xz * .75;
            o.edgeUV = input.uv;
            return o;
        }
        float ErosionNoise(float2 p)
        {
            float2 cell = floor(p);
            float2 f = frac(p);
            f = f*f*(3-2*f);
            // Interpolate the hashed corners: a continuous field, never isolated
            // square pixels. Thresholding this field shrinks connected patches.
            float4 x = cell.x + float4(0,1,0,1);
            float4 y = cell.y + float4(0,0,1,1);
            float4 h = frac(x*.1031 + y*.11369);
            h = frac(h*(h+33.33)*(h+h));
            return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
        }
        void CutPaint(Varyings input)
        {
            float4 wear = UNITY_ACCESS_INSTANCED_PROP(Paint,_PaintWear);
            float2 uv = input.paintUV / max(.1,wear.w);
            half amount = saturate(wear.x);
            half coverage = PaintCoverage(uv + wear.yz, amount);
            // Fine chips also reach the long edges; no translucent overlap or glow.
            half edge = min(input.edgeUV.x, 1-input.edgeUV.x);
            half chip = SAMPLE_TEXTURE2D(_WearMap,sampler_WearMap,uv*3 + wear.zy).a;
            clip(min(coverage-.5, edge - amount * .09 * (1-chip)));
            if(wear.x>1)
            {
                // Broad scuffs with a little edge breakup, scaled with the existing
                // wear map. Increasing wear erodes these islands instead of punching
                // unrelated pinholes through every surviving paint fragment.
                float2 p = uv*5 + wear.yz;
                float erosion = ErosionNoise(p)*.8 + ErosionNoise(p*3.13+17.7)*.2;
                clip(smoothstep(.2,.8,erosion)-(wear.x-1)-.00001);
            }
        }
        half4 Depth(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input); CutPaint(input); return 0;
        }
        half4 Normals(Varyings input):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input); CutPaint(input); return half4(normalize(input.normalWS),0);
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            half4 Fragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                CutPaint(input);
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = normalize(input.normalWS);
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                lighting.bakedGI = SampleSH(lighting.normalWS);
                lighting.shadowMask = half4(1,1,1,1);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = UNITY_ACCESS_INSTANCED_PROP(Paint,_PaintTint).rgb;
                surface.alpha = 1; surface.occlusion = 1; surface.smoothness = _Smoothness;
                half4 result = UniversalFragmentPBR(lighting,surface);
                result.rgb = MixFog(result.rgb,ComputeFogFactor(TransformWorldToHClip(input.positionWS).z));
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
            #pragma vertex Vertex
            #pragma fragment Depth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Normals
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    FallBack Off
}
