Shader "Hidden/EnvironmentAuthoring/ComponentOutline"
{
    SubShader
    {
        Pass
        {
            // This depth buffer contains only the highlighted target, not the scene.
            ZTest LEqual ZWrite On Cull Off
            HLSLPROGRAM
            #pragma editor_sync_compilation
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float4x4 _ViewProjection;
            float4 Vert(float4 position : POSITION) : SV_POSITION
            {
                return mul(_ViewProjection, mul(unity_ObjectToWorld, position));
            }
            float4 Frag() : SV_Target { return 1; }
            ENDHLSL
        }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma editor_sync_compilation
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _Mask;
            float4 _MaskPixel;
            float4 _OutlineColor;
            v2f_img Vert(uint id : SV_VertexID)
            {
                v2f_img output;
                float2 uv = float2((id << 1) & 2, id & 2);
                output.pos = float4(uv * 2 - 1, 0, 1);
                output.uv = uv;
                #if UNITY_UV_STARTS_AT_TOP
                    output.uv.y = 1 - output.uv.y;
                #endif
                return output;
            }
            float4 Frag(v2f_img input) : SV_Target
            {
                float center = tex2D(_Mask, input.uv).r;
                float border = 0;
                // Three physical pixels outside the union: no triangle or modular edges.
                float2 pixel = _MaskPixel.xy;
                border = max(border, tex2D(_Mask, input.uv + float2(pixel.x, 0)).r);
                border = max(border, tex2D(_Mask, input.uv - float2(pixel.x, 0)).r);
                border = max(border, tex2D(_Mask, input.uv + float2(0, pixel.y)).r);
                border = max(border, tex2D(_Mask, input.uv - float2(0, pixel.y)).r);
                return float4(_OutlineColor.rgb, _OutlineColor.a * border * (1 - center));
            }
            ENDHLSL
        }
    }
}
