Shader "ExaltFall/Tutorial Ground Envelope"
{
    Properties
    {
        _SurfaceColor ("Surface", Color) = (.23,.24,.23,1)
        _FogColor ("Fog", Color) = (.91,.92,.91,1)
        _Radius ("Visibility radius", Float) = 2.3
        _Falloff ("Edge falloff", Float) = .9
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _SurfaceColor;
            half4 _FogColor;
            float _Radius;
            float _Falloff;
            CBUFFER_END
            float4 _TutorialFogPlayerCenter;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float distanceToPlayer = distance(input.positionWS.xz, _TutorialFogPlayerCenter.xz);
                float fog = smoothstep(max(0, _Radius - _Falloff), _Radius, distanceToPlayer);
                float2 world = input.positionWS.xz;
                float haze = sin(world.x * 1.9 + world.y * 1.15 + _Time.y * .25)
                    * sin(world.y * 2.1 - world.x * .65 - _Time.y * .19);
                fog = saturate(fog + (.16 + haze * .075) * (1 - fog));
                return lerp(_SurfaceColor, _FogColor, fog);
            }
            ENDHLSL
        }
    }
}
