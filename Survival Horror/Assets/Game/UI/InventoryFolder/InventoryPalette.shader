Shader "HorrorGame/UI/Inventory Palette"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white" {}
        _Saturation("Saturation",Range(0,1))=.45
        _Contrast("Texture contrast",Range(0,1))=1
        _Tint("Palette tint",Color)=(1,1,1,1)
        _StencilComp("Stencil comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil operation",Float)=0
        _StencilWriteMask("Stencil write mask",Float)=255
        _StencilReadMask("Stencil read mask",Float)=255
        _ColorMask("Color mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Alpha clip",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"}
        Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Input {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct Varying {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;};
            sampler2D _MainTex;
            float4 _TextureSampleAdd,_ClipRect,_Tint;
            float _Saturation,_Contrast;
            Varying vert(Input v){Varying o;o.local=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            float4 frag(Varying i):SV_Target
            {
                float4 c=tex2D(_MainTex,i.uv)+_TextureSampleAdd;
                float gray=dot(c.rgb,float3(.299,.587,.114));
                c.rgb=lerp(gray.xxx,c.rgb,_Saturation);
                c.rgb=(c.rgb-.5)*_Contrast+.5;
                c*=i.color*_Tint;
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
