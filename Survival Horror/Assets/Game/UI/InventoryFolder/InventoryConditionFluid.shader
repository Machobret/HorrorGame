Shader "HorrorGame/UI/Condition Fluid"
{
    Properties
    {
        [PerRendererData] _MainTex("UI texture",2D)="white" {}
        _Damage("Blood amount",Range(0,1))=0
        _FluidTime("Unscaled clock",Float)=0
        _MotionSpeed("Fluid drift speed",Range(0,1))=.3
        _StencilComp("Stencil comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil operation",Float)=0
        _StencilWriteMask("Stencil write mask",Float)=255
        _StencilReadMask("Stencil read mask",Float)=255
        _ColorMask("Color mask",Float)=15
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
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
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Input {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct Varying {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;};
            float _Damage,_FluidTime,_MotionSpeed;
            float4 _ClipRect;
            Varying vert(Input v){Varying o;o.local=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
            float4 frag(Varying i):SV_Target
            {
                // Coarse sample grid gives the liquid a restrained PSX texture cadence.
                float2 uv=(floor(i.uv*float2(256,72))+.5)/float2(256,72);
                float t=_FluidTime*_MotionSpeed;
                float2 p=(uv-.5)*float2(3.8,2);
                float2 q=float2(abs(p.x),p.y);
                q+=float2(sin(p.y*4+t)*.022,sin(p.x*3-t*.7)*.024);
                float d=saturate(_Damage);
                float spread=.06+d*2.15;
                float warp=noise(q*3+float2(t*.25,-t*.2))*.1;
                float angle=atan2(q.y,q.x+.03);
                float lobes=(.18*sin(angle*8+sin(t*.3)*.4)+.09*sin(q.y*13-t*.5))*(1-d*.5);
                float body=1-length(float2(q.x/(spread*.78+.01),q.y/(.12+d*.95)))+lobes;
                float field=body;
                // Tapered curved fingers grow from the central ink mass in mirrored pairs.
                for(int k=0;k<5;k++)
                {
                    float f=k/4.0;
                    float reach=spread*(.65+.3*sin(k*3.7));
                    float x=saturate(q.x/max(.01,reach));
                    float curve=(f-.5)*1.5*(sin(x*2.4)+.18*x*sin(t*.55+k+x*5));
                    float width=(.07+d*.18)*(1-x*.84);
                    float finger=1-abs(q.y-curve)/max(width,.005);
                    finger=min(finger,(reach-q.x)*12);
                    field=max(field,finger*.3);
                }
                field+=warp-.045;
                float ink=smoothstep(-.07,.07,field)*smoothstep(.005,.09,d);
                float suspended=noise(q*6+float2(t*.2,-t*.3));
                float3 clear=float3(.64,.66,.59)+.15*(1-saturate(length(p/float2(2.3,1.4))));
                float3 blood=lerp(float3(.14,.024,.025),float3(.30,.055,.045),suspended*.75+.12);
                float3 col=lerp(clear,blood,ink);
                // Small rising trapped bubbles; slower and less frequent inside blood.
                for(int j=0;j<7;j++)
                {
                    float x=(hash(float2(j,4))-.5)*3.4;
                    float y=frac(hash(float2(j,9))+t*(.045+.018*hash(float2(j,2))))*1.6-.8;
                    float radius=.022+.017*hash(float2(j,6));
                    float bubble=abs(length(p-float2(x,y))-radius);
                    float rim=1-smoothstep(.007,.018,bubble);
                    col+=rim*lerp(float3(.13,.14,.12),float3(.12,.06,.05),ink);
                }
                // Recessed charcoal bezel, worn trim and a faint glass reflection.
                float edge=min(min(uv.x,1-uv.x)*3.8,min(uv.y,1-uv.y)*2);
                col=lerp(float3(.11,.105,.095),col,smoothstep(.10,.125,edge));
                col+=float3(.18,.17,.14)*(1-smoothstep(.008,.022,abs(edge-.055)));
                float glass=(1-smoothstep(.015,.045,abs(uv.y-(.76+uv.x*.07))))*.075;
                col+=glass*smoothstep(.12,.15,edge);
                col*=.96+.04*step(.5,frac(uv.y*72));
                float alpha=i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                alpha*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                return float4(col*i.color.rgb,alpha);
            }
            ENDCG
        }
    }
}
