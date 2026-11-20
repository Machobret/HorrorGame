Shader "HorrorGame/Neon Sign"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Frame Atlas", 2D) = "white" {}
        [HideInInspector] _Layout("Frame Size, Columns, Count", Vector) = (1, 1, 1, 1)
        [HideInInspector] _HorrorVisibilityExempt("Self-Illuminated", Float) = 1
        _FramesPerSecond("Frames Per Second", Float) = 12
        _Glow("Glow", Range(1, 4)) = 1.5
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                float4 _Layout;
                float _FramesPerSecond;
                float _Glow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                uint frameCount = max(1u, (uint)_Layout.w);
                uint columns = max(1u, (uint)_Layout.z);
                uint ticks = (uint)floor(max(0, _Time.y) * max(0, _FramesPerSecond));
                uint frame = ticks % frameCount;
                float2 tile = float2(frame % columns, frame / columns);
                float2 pixel = tile * _Layout.xy + lerp(0.001, _Layout.xy - 0.001, input.uv);

                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = pixel * _MainTex_TexelSize.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return half4(color.rgb * _Glow, color.a);
            }
            ENDHLSL
        }
    }
}
