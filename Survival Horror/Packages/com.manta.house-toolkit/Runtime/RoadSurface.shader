Shader "House Toolkit/Road Surface"
{
    Properties
    {
        [MainTexture] _BaseMap("Base color", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        [NoScaleOffset] _WearMap("Paint scuffs (alpha)", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0,1)) = .18
        [Toggle] _MetricAsphalt("Tile asphalt RGB in road-local metres", Float) = 0
        [Toggle] _EdgeMask("Base map alpha marks painted edges", Float) = 0
        _EdgeColor("Edge paint", Color) = (.79,.76,.67,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EdgeColor;
            half _Smoothness, _EdgeMask, _MetricAsphalt;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_WearMap); SAMPLER(sampler_WearMap);

        float WheelBands(float across, float laneCount)
        {
            // Two tracks per lane. The field is zero outside the width of the approach.
            float d = (frac(across * 2 * laneCount) - .5) / .22;
            return (across > 0 && across < 1) ? exp(-d*d) : 0;
        }

        // Distances to the finite mouths, so angled neighbors cannot attenuate a strip at its seam.
        float4 MouthDistances(float4 across, float4 inward)
        {
            float4 outside = max(abs(2*across-1)-1, 0);
            return abs(inward) + outside;
        }

        float AcrossRoute(float2 across, float2 distance)
        {
            float t = smoothstep(0, 1, distance.x / max(.001, distance.x + distance.y));
            return lerp(across.x, across.y, t);
        }

        float WornWidth(float across)
        {
            return smoothstep(0, .35, min(across, 1-across));
        }

        float TurnTracks(float a, float b)
        {
            // Coordinates are measured inward from the two adjoining curbs in lane widths.
            // Quarter ellipses join the two straight tire paths tangentially, even on a skew.
            const float setback = .18;
            float arc = length(max(float2(a,b) + setback, 0)) - setback;
            float tire = min(abs(arc-.25), abs(arc-.75)) / .145;
            return exp(-tire*tire);
        }

        float Traffic(float4 uv, float4 lanes, float4 across, float4 inward, float2 metres)
        {
            if (uv.z < 2) return WheelBands(uv.x, lanes.x);
            float4 d = MouthDistances(across, inward);
            float mainDistance = min(d.x, d.y);
            float sideDistance = lanes.w > 0 ? min(d.z, d.w) : d.z;
            float mainU = AcrossRoute(across.xy, d.xy);
            float sideU = lanes.w > 0 ? AcrossRoute(across.zw, d.zw) : across.z;
            float balance = (sideDistance-mainDistance) / max(.001, sideDistance+mainDistance);

            // Only the nearer approach contributes individual tracks. Where traffic overlaps,
            // blend those tracks into a single broad rubbed surface.
            bool main = balance >= 0;
            float2 count = main ? lanes.xy : lanes.zw;
            float2 dist = main ? d.xy : d.zw;
            float u = main ? mainU : sideU;
            float bands = WheelBands(u, count.x);
            if (count.y > 0 && count.y != count.x)
                bands = lerp(bands, WheelBands(u,count.y), smoothstep(0,1,dist.x/max(.001,dist.x+dist.y)));
            float sideWidth = WornWidth(sideU);
            if (lanes.w == 0) sideWidth *= 1-smoothstep(.6, 1.15, inward.z);
            float swath = max(WornWidth(mainU), sideWidth);
            float variation = sin(dot(metres,float2(.62,.29))) + .7*sin(dot(metres,float2(-.34,1.21)));
            // Traffic spreads sideways in the intersection. Leave broad swaths along the
            // through route, and dissolve them before individual side-street tracks appear.
            float uShift = mainU + .025*variation;
            float2 offsets = (uShift - float2(.27,.72)) / float2(.16,.19);
            float rubbed = dot(exp(-offsets*offsets), float2(.43,.36));
            float patch = swath*(.31+.045*variation) + rubbed*smoothstep(-.32,0,balance);
            int turnMask = (int)round(uv.w);
            float turns = 0;
            if ((turnMask & 1) != 0) turns = TurnTracks((1-across.x)*lanes.x, across.z*lanes.z);
            if ((turnMask & 2) != 0) turns = max(turns,TurnTracks((1-across.y)*lanes.y, (1-across.z)*lanes.z));
            if ((turnMask & 4) != 0) turns = max(turns,TurnTracks(across.x*lanes.x, across.w*lanes.w));
            if ((turnMask & 8) != 0) turns = max(turns,TurnTracks(across.y*lanes.y, (1-across.w)*lanes.w));
            // Merge overlapping paths into the same dark asphalt instead of adding bands.
            float through = lerp(patch, bands, smoothstep(.16,.83,abs(balance)));
            float2 corePoint = (float2(mainU,sideU)-.5) / float2(.56,.58);
            float core = 1-smoothstep(.46,1.2,dot(corePoint,corePoint)+.05*variation);
            float crossingTraffic = lanes.w > 0 ? 1 : 1-smoothstep(1.05,1.80,inward.z);
            float wear = max(through, turns * (.95+.025*variation));
            wear = max(wear, core * crossingTraffic * (.91+.035*variation));
            return lerp(wear, bands, smoothstep(.82,1,abs(balance)));
        }

        half PaintCoverage(float2 wearUV, half wear)
        {
            half mask = SAMPLE_TEXTURE2D(_WearMap, sampler_WearMap, wearUV).a;
            // Reserve a clean endpoint even for fully chipped texels.
            half threshold = lerp(-.03, .97, wear);
            return smoothstep(threshold, threshold + .025, mask);
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 lightmapUV : TEXCOORD1;
            float4 uv : TEXCOORD0;
            float4 traffic : TEXCOORD2;
            float4 across : TEXCOORD3;
            float4 inward : TEXCOORD4;
            half4 style : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
            float4 uv : TEXCOORD4;
            float2 wearUV : TEXCOORD5;
            half4 style : TEXCOORD6;
            float4 traffic : TEXCOORD7;
            float4 across : TEXCOORD8;
            float4 inward : TEXCOORD9;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        struct DepthVaryings
        {
            float4 positionCS : SV_POSITION;
            float4 wear : TEXCOORD0; // pattern UV, surface kind, family wear
            float2 uv : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        DepthVaryings DepthVertex(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            DepthVaryings output = (DepthVaryings)0;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.wear = float4(input.positionOS.xz * .5, input.uv.z, input.style.a);
            output.uv = TRANSFORM_TEX(input.uv.xy, _BaseMap);
            return output;
        }
        half4 DepthFragment(DepthVaryings input) : SV_Target
        {
            if (input.wear.z > .5 && input.wear.z < 2.5)
                clip(PaintCoverage(input.wear.xy, input.wear.w) *
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - .5);
            return 0;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RoadVertex
            #pragma fragment RoadFragment
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

            Varyings RoadVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.uv = input.uv;
                output.wearUV = input.positionOS.xz * .5;
                output.style = input.style;
                output.traffic = input.traffic;
                output.across = input.across;
                output.inward = input.inward;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }
            half4 RoadFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normalWS = normalize(input.normalWS);
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(input.uv.xy, _BaseMap));
                half3 albedo = texel.rgb * _BaseColor.rgb;
                half coverage = PaintCoverage(input.wearUV, input.style.a);
                if (input.uv.z > .5 && input.uv.z < 2.5)
                {
                    clip(coverage * texel.a - .5);
                    if (input.uv.z > 1.5) albedo *= input.style.rgb;
                    albedo *= lerp(1, .86, input.style.a);
                }
                else
                {
                    // The edge mask follows the junction UVs; asphalt grain tiles continuously
                    // across roads, corner triangles and junctions at the same physical scale.
                    if (_MetricAsphalt > .5)
                        albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearRepeat, input.wearUV).rgb * _BaseColor.rgb;
                    albedo *= lerp(half3(1,1,1), input.style.rgb, Traffic(input.uv, input.traffic, input.across, input.inward, input.wearUV*2));
                    albedo = lerp(albedo, _EdgeColor.rgb, texel.a * _EdgeMask * coverage);
                }
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = normalWS;
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
                surface.albedo = albedo;
                surface.smoothness = _Smoothness;
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
            DepthVaryings ShadowVertex(Attributes input)
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
                DepthVaryings output = DepthVertex(input);
                output.positionCS = positionCS;
                return output;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
