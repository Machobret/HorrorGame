Shader "HorrorGame/UI/Tablet Screen"
{
    Properties
    {
        [PerRendererData] _MainTex("Display",2D)="white"{}
        _VirtualResolution("Display resolution",Vector)=(960,540,0,0)
        _ColorDepth("Colour steps",Range(8,64))=32
        _DitherStrength("Ordered dithering",Range(0,1))=.4
        _Scanlines("Scanlines",Range(0,.3))=.08
        _Vignette("Edge shading",Range(0,.5))=.12
        _ScreenTime("Unscaled screen clock",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct Varying {float4 position:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            sampler2D _MainTex;float4 _VirtualResolution;float _ColorDepth,_DitherStrength,_Scanlines,_Vignette,_ScreenTime;
            Varying vert(Input i){Varying o;o.position=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;return o;}
            float4 frag(Varying i):SV_Target
            {
                float2 resolution=max(_VirtualResolution.xy,1);
                float2 pixel=floor(i.uv*resolution);
                float2 uv=(pixel+.5)/resolution;
                float4 result=tex2D(_MainTex,uv)*i.color;
                static const float bayer[16]={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
                int index=((int)pixel.x%4)+4*((int)pixel.y%4);
                float threshold=(bayer[index]/16-.5)*_DitherStrength;
                result.rgb=floor(saturate(result.rgb)*_ColorDepth+threshold+.5)/_ColorDepth;
                float2 centre=i.uv*2-1;
                result.rgb*=1-_Scanlines*fmod(pixel.y,2);
                result.rgb*=1-_Vignette*dot(centre,centre)*.5;
                result.rgb*=1-.02*step(.995,frac(_ScreenTime*.07));
                return result;
            }
            ENDHLSL
        }
    }
}
