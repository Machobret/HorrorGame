Shader "Hidden/PSX-PostProcess-URP"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "PSX Dither Custom"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float3 _ColorResolution;
            float3 _DitherResolution;
            float _HighResDitherMatrix;
            float _DitheringScale;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.screenPos = o.positionHCS;
                return o;
            }

            float DitherColorChannel(float color, float ditherOffset, float ditherStep)
            {
                float distance = fmod(color, ditherStep);
                float baseValue = floor(color / ditherStep) * ditherStep;
                float nudgedValue = (distance + ditherOffset * ditherStep) / ditherStep;
                return baseValue + ditherStep * floor(nudgedValue);
            }

            float DitherCol(float color, float ditherOffset, float ditherStep)
            {
                float distance = fmod(color, ditherStep);
                float baseValue = floor(color / ditherStep) * ditherStep;
                return (ditherOffset < distance / ditherStep - 0.001f) ? baseValue + ditherStep : baseValue;
            }

            float GetDitherThreshold(uint2 pixelPosition)
            {
                const int ditheringMatrix4x4[16] =
                {
                    0, 8, 2, 10,
                    12, 4, 14, 6,
                    3, 11, 1, 9,
                    15, 7, 13, 5
                };

                const int ditheringMatrix4x4PS1[16] =
                {
                    -4, +0, -3, +1,
                    +2, -2, +3, -1,
                    -3, +1, -4, +0,
                    +3, -1, +2, -2
                };

                const int ditheringMatrix2x2[4] =
                {
                    0, 3,
                    2, 1
                };

                if (_HighResDitherMatrix > 0.75)
                {
                    return ditheringMatrix4x4PS1[(pixelPosition.x % 4) + (pixelPosition.y % 4) * 4] * 0.125;
                }
                else if (_HighResDitherMatrix > 0.25)
                {
                    return ditheringMatrix4x4[(pixelPosition.x % 4) + (pixelPosition.y % 4) * 4] * 0.0625;
                }
                else
                {
                    return ditheringMatrix2x2[(pixelPosition.x % 2) + (pixelPosition.y % 2) * 2] * 0.25;
                }
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // Convert to pixel space
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float2 pixelPos = screenUV * _ScreenParams.xy;

                float threshold = GetDitherThreshold((uint2)floor(pixelPos * _DitheringScale));

                float3 ditherStep = 1.0 / max(_DitherResolution, 1.0);
                float3 colorStep = 1.0 / max(_ColorResolution, 1.0);

                col.r = DitherCol(col.r, 0.5, colorStep.r);
                col.g = DitherCol(col.g, 0.5, colorStep.g);
                col.b = DitherCol(col.b, 0.5, colorStep.b);

                col.r = DitherColorChannel(col.r, threshold, ditherStep.r);
                col.g = DitherColorChannel(col.g, threshold, ditherStep.g);
                col.b = DitherColorChannel(col.b, threshold, ditherStep.b);

                return col;
            }

            ENDHLSL
        }
    }
}