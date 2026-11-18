Shader "HorrorGame/Map Circuit Range"
{
    Properties { _Phase("Dash phase", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Phase;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 offset = input.uv * 2.0 - 1.0;
                float radius = length(offset);
                float angle = atan2(offset.y, offset.x) / 6.2831853;
                float dash = 1.0 - step(.57, frac(angle * 32.0 - _Phase));
                float edgeWidth = max(fwidth(radius), .003);
                float outline = dash * (1.0 - smoothstep(edgeWidth * .65, edgeWidth * 1.5, abs(radius - .987)));
                float fill = saturate(1.0 - radius) * .18;
                return half4(lerp(half3(.92, .32, .30), half3(.015, .01, .012), outline),
                    max(fill, outline * .48));
            }
            ENDHLSL
        }
    }
}
