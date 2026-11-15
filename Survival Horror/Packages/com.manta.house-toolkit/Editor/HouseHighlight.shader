Shader "Hidden/HouseToolkit/SceneHighlight"
{
    Properties { _Color ("Highlight", Color) = (1,1,1,0.3) }
    SubShader
    {
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            float4 vert(float4 position : POSITION) : SV_POSITION { return UnityObjectToClipPos(position); }
            float4 frag() : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
