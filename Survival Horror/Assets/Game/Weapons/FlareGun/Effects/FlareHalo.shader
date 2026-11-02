Shader "HorrorGame/Flare Halo"
{
    Properties { _BaseColor("Glow", Color) = (2, 0.008, 0.004, 1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // A small sprite grid and three color bands keep the glow crisp
                // at full display resolution without pixelating the whole game.
                float2 cell = floor(saturate(input.uv) * 16);
                float2 uv = (cell + .5) / 16;
                float radius = length(uv * 2 - 1);
                clip(.94 - radius);
                float checker = fmod(cell.x + cell.y, 2);
                clip(radius < .7 ? 1 : checker - .5);
                half3 color = radius < .24 ? half3(1.8, .65, .24)
                    : (radius < .55 ? _BaseColor.rgb : _BaseColor.rgb * .45);
                float alpha = radius < .55 ? 1 : .45;
                return half4(color * input.color.rgb, alpha * input.color.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
