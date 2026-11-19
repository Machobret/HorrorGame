Shader "Horror Game/Lighting/Streetlight Beam"
{
    Properties
    {
        _BeamTint ("Light color", Color) = (1,0.96,0.9,1)
        _Opacity ("Beam opacity", Range(0,0.3)) = 0.09
        _FadeDistance ("Start fading from beam (m)", Range(2,10)) = 5
        _HideDistance ("Fully hidden from beam (m)", Range(0.1,4)) = 1.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "FixtureEffect"="Beam" }
        Pass
        {
            Blend SrcAlpha One
            ColorMask RGB
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Opacity, _FadeDistance, _HideDistance;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Beam)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BeamTint)
            UNITY_INSTANCING_BUFFER_END(Beam)
            struct Input { float3 position : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 clipPos : SV_POSITION;
                float edgeTimesRadius : TEXCOORD0;
                float depth : TEXCOORD2;
                float cameraFade : TEXCOORD3;
                half3 tint : TEXCOORD4;
            };
            Varyings Vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 origin = TransformObjectToWorld(float3(0,0,0));
                float3 axis = TransformObjectToWorldDir(float3(0,0,1));
                float3 view = GetWorldSpaceNormalizeViewDir(origin);
                float3 across = cross(axis, view);
                across = dot(across, across) > 0.00001 ? normalize(across) : TransformObjectToWorldDir(float3(1,0,0));
                // Convert back to local units so the two authored radii still work.
                float radiusScale = rcp(max(length(TransformWorldToObjectDir(across, false)), 0.0001));
                float3 center = TransformObjectToWorld(float3(0,0,v.position.z));
                o.clipPos = TransformWorldToHClip(center + across * radiusScale * v.position.x);
                // Interpolate across a taper without letting the quad diagonal skew the fade.
                o.edgeTimesRadius = v.color.a * lerp(0.025, 1.0, v.position.z);
                o.depth = v.position.z;
                // The mesh has unit length and radius. Measure from the camera to the
                // nearest solid cross-section, including beneath the open end.
                float3 cameraOS = TransformWorldToObject(GetCameraPositionWS());
                float depth = saturate(cameraOS.z);
                float radius = lerp(0.025, 1.0, depth);
                float2 nearestXY = cameraOS.xy * min(1.0, radius / max(length(cameraOS.xy), 0.0001));
                float distanceWS = distance(GetCameraPositionWS(), TransformObjectToWorld(float3(nearestXY, depth)));
                o.cameraFade = smoothstep(_HideDistance, max(_HideDistance + 0.01, _FadeDistance), distanceWS);
                o.tint = UNITY_ACCESS_INSTANCED_PROP(Beam, _BeamTint).rgb;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float edge = smoothstep(0.0, 1.0, i.edgeTimesRadius / lerp(0.025, 1.0, i.depth));
                float taper = 1.0 - smoothstep(0.05, 1.0, i.depth);
                return half4(i.tint, _Opacity * edge * taper * i.cameraFade);
            }
            ENDHLSL
        }
    }
}
