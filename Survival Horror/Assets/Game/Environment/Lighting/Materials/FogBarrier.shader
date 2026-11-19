Shader "HorrorGame/Fog Barrier"
{
    Properties
    {
        _MistColor("Mist Color", Color) = (0.48,0.51,0.52,1)
        [Toggle] _MistSheet("Mist Sheet", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" "HorrorVisibilityExempt"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZTest [_ZTest]
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                half4 _MistColor;
                float _MistSheet;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.world);
                output.uv = input.uv;
                return output;
            }
            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                            lerp(Hash(cell + float2(0,1)), Hash(cell + 1), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Render the visible fog surface with normal world depth testing.
                // This works from either side of the alley and needs no depth copy.
                if (_MistSheet > 0.5)
                {
                    float3 p = TransformWorldToObject(input.world) + 0.5;
                    float horizontal = dot(input.world.xz, float2(.85, 1.37));
                    float2 drift = float2(horizontal, p.y * 5.0) + _Time.y * float2(.24, -.34);
                    float billows = Noise(drift) * .7 + Noise(drift * 2.3) * .3;
                    float edge = smoothstep(0.0, .14, min(p.x, 1.0 - p.x));
                    float top = lerp(.64, .98, billows);
                    float height = 1.0 - smoothstep(.34, top, p.y);
                    float alpha = lerp(.42, 1.0, edge) * height * lerp(.60, .98, billows) * _MistColor.a;
                    return half4(_MistColor.rgb * lerp(.86, 1.12, billows), alpha);
                }
                float3 direction = GetWorldSpaceNormalizeViewDir(input.world);
                float cameraDistance = distance(input.world, _WorldSpaceCameraPos);
                if (unity_OrthoParams.w > 0.5)
                    cameraDistance = -TransformWorldToView(input.world).z;
                float3 exitPoint = TransformWorldToObject(input.world);
                float3 localDirection = mul((float3x3)GetWorldToObjectMatrix(), direction);
                float3 oppositeFace = lerp(-0.5, 0.5, step(0.0, localDirection));
                float3 axisSpeed = abs(localDirection);
                float3 faceDistance = (oppositeFace - exitPoint) * sign(localDirection) / max(axisSpeed, 1e-5);
                // A ray parallel to a box side never exits through that side.
                faceDistance = lerp(cameraDistance.xxx, faceDistance, step(1e-5, axisSpeed));
                float segment = max(0.0, min(cameraDistance, min(faceDistance.x, min(faceDistance.y, faceDistance.z))));
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = unity_OrthoParams.w > 0.5
                    ? LinearDepthToEyeDepth(rawDepth)
                    : LinearEyeDepth(rawDepth, _ZBufferParams);
                float exitDepth = -TransformWorldToView(input.world).z;
                float depthRate = max(TransformWorldToViewDir(direction, false).z, 1e-4);
                // A near-plane sample can also be URP's fallback when no
                // camera depth is supplied. Keep rendering in that case.
                float firstVisible = sceneDepth > _ProjectionParams.y * 1.001 && sceneDepth < exitDepth
                    ? (exitDepth - sceneDepth) / depthRate
                    : 0.0;
                float stepSize = max(0.0, segment - firstVisible) / 24.0;
                float opticalDepth = 0.0;
                float billowWeight = 0.0;
                for (int i = 0; i < 24; ++i)
                {
                    float travel = firstVisible + (i + 0.5) * stepSize;
                    float3 p = exitPoint + localDirection * travel + 0.5;
                    float2 drift = float2(input.world.x * 0.85 + input.world.z * 1.37, p.y * 5);
                    drift.x += travel * (direction.x * 0.85 + direction.z * 1.37);
                    drift += float2(_Time.y * 0.14, _Time.y * 0.65);
                    float billows = Noise(drift) * 0.65 + Noise(drift * 2.1) * 0.35;
                    float sides = smoothstep(0.0, 0.24, min(p.x, 1.0 - p.x));
                    float depthFade = smoothstep(0.0, 0.24, min(p.z, 1.0 - p.z));
                    float top = lerp(0.54, 0.98, billows);
                    float height = 1.0 - smoothstep(0.12, top, p.y);
                    float pooling = 1.0 - smoothstep(0.02, 0.28, p.y);
                    float density = height * (0.12 + billows * billows * 0.85)
                                  + pooling * (0.24 + 0.45 * Noise(drift * float2(0.65, 2)));
                    float weight = sides * depthFade * density * stepSize;
                    opticalDepth += weight;
                    billowWeight += weight * billows;
                }
                float tint = lerp(0.74, 1.12, billowWeight / max(opticalDepth, 1e-4));
                return half4(_MistColor.rgb * tint, 1.0 - exp(-opticalDepth * 0.85));
            }
            ENDHLSL
        }
    }
}
