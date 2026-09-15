Shader "Hidden/PSX-Interlacing-URP"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PreviousFrame ("Previous Frame", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "PSX Interlacing"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Textures
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_PreviousFrame);
            SAMPLER(sampler_PreviousFrame);

            float _InterlacedFrameIndex;
            float _InterlacingSize;

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

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half4 prev = SAMPLE_TEXTURE2D(_PreviousFrame, sampler_PreviousFrame, uv);

                // Convert to pixel coords
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float2 pixelPos = screenUV * _ScreenParams.xy;

                float row = floor(pixelPos.y / _InterlacingSize);
                float checker = fmod(row, 2.0);

                float usePrev = (checker == round(_InterlacedFrameIndex)) ? 1.0 : 0.0;

                return lerp(col, prev, usePrev);
            }

            ENDHLSL
        }
    }
}