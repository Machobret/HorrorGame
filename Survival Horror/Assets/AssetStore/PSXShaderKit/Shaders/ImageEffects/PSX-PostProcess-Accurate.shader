Shader "Hidden/PSX-PostProcess-Accurate-URP"
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
            Name "PSX Dither Accurate"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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

            int PSX_GetDitherOffset(int2 pixelPosition)
            {
                const int ditheringMatrix4x4[16] =
                {
                    -4, +0, -3, +1,
                    +2, -2, +3, -1,
                    -3, +1, -4, +0,
                    +3, -1, +2, -2
                };

                return ditheringMatrix4x4[pixelPosition.x % 4 + (pixelPosition.y % 4) * 4];
            }

            half4 PSX_DitherColor(float4 color, int2 pixelPosition)
            {
                int4 col255 = round(color * 255) + PSX_GetDitherOffset(pixelPosition);
                col255 = col255 >> 3;
                return col255 / 31.0;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float2 pixelPos = screenUV * _ScreenParams.xy;

                return PSX_DitherColor(col, (int2)floor(pixelPos * _DitheringScale));
            }

            ENDHLSL
        }
    }
}