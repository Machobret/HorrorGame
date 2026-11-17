Shader "HorrorGame/UI/Inventory Fog"
{
    Properties { [PerRendererData] _MainTex("UI texture",2D)="white" {} _Opacity("Fog visibility",Range(0,1))=.65 _Speed("Drift speed",Range(0,.2))=.035 _FogTime("Unscaled clock",Float)=0 }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Varying {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            float _Opacity,_Speed,_FogTime;
            Varying vert(Input i){Varying o;o.position=UnityObjectToClipPos(i.vertex);o.uv=i.uv;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            float fbm(float2 p){float n=0,a=.5;for(int i=0;i<5;i++){n+=a*noise(p);p=mul(float2x2(1.6,-1.2,1.2,1.6),p)+4.7;a*=.5;}return n;}
            float4 frag(Varying i):SV_Target
            {
                float t=_FogTime*_Speed;
                float2 p=i.uv*float2(4,3);
                float2 warp=float2(fbm(p+float2(t,0)),fbm(p+float2(3.1,-t*.6)));
                float cloud=fbm(p+warp*2.5+float2(-t,t*.3));
                float wisps=smoothstep(.35,.73,cloud);
                float edge=lerp(.45,1,pow(abs(i.uv.x-.5)*2,1.3));
                float intensity=wisps*edge*_Opacity;
                return float4(intensity,intensity,intensity,1);
            }
            ENDHLSL
        }
    }
}
