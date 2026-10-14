Shader "Hidden/HorrorGame/Stylized Flare Lighting"
{
 Properties
 {
  _Strength("Style strength", Range(0,1)) = 1
  _DitherPixels("Dither pixel size", Range(1,8)) = 3
  _InnerColor("Hot inner light", Color) = (0.95,0.09,0.018,1)
  _MiddleColor("Red middle light", Color) = (0.55,0.015,0.009,1)
  _OuterColor("Dark red outer light", Color) = (0.2,0.004,0.003,1)
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   ZTest Always ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   TEXTURE2D_X(_BlitTexture);
   CBUFFER_START(UnityPerMaterial)
    float _Strength, _DitherPixels;
    float4 _InnerColor, _MiddleColor, _OuterColor;
   CBUFFER_END
   int _StylizedFlareCount;
   float4 _StylizedFlarePositions[8];
   float4 _StylizedFlareSettings[8];
   struct Attributes { uint vertexID : SV_VertexID; };
   struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
   Varyings Vert(Attributes input)
   {
    Varyings o;
    o.positionCS=GetFullScreenTriangleVertexPosition(input.vertexID);
    o.uv=GetFullScreenTriangleTexCoord(input.vertexID);
    return o;
   }
   float Bayer(uint2 p)
   {
    const float values[16]={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
    return (values[(p.y & 3)*4+(p.x & 3)]+.5)/16-.5;
   }
   half4 Frag(Varyings input) : SV_Target
   {
    half4 source=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,input.uv);
    if (_StylizedFlareCount==0 || _Strength<=0) return source;
    float depth=SampleSceneDepth(input.uv);
    #if UNITY_REVERSED_Z
     if(depth<=.00001) return source;
    #else
     if(depth>=.99999) return source;
     depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
    #endif
    float3 world=ComputeWorldSpacePosition(input.uv,depth,UNITY_MATRIX_I_VP);
    float best=0; float intensity=0; float3 palette=0;
    float dither=Bayer((uint2)floor(input.positionCS.xy/max(1,_DitherPixels)));
    for(int i=0;i<min(_StylizedFlareCount,8);++i)
    {
     float3 delta=world-_StylizedFlarePositions[i].xyz;
     // An octagonal footprint rather than a perfectly circular light pool.
     float radial=max(length(delta.xz)*.92,max(abs(delta.x),abs(delta.z)));
     float distance=sqrt(radial*radial+delta.y*delta.y);
     float radius=_StylizedFlarePositions[i].w*(.975+.025*_StylizedFlareSettings[i].x);
     float edge=distance+dither*.22;
     if(edge>=radius) continue;
     float3 color=edge<1.15 ? _InnerColor.rgb : (edge<3 ? _MiddleColor.rgb : _OuterColor.rgb);
     float strength=_StylizedFlareSettings[i].x;
     float influence=strength*(1-distance/max(radius,.01));
     if(influence>best) { best=influence; intensity=strength; palette=color; }
    }
    if(best<=0) return source;
    // Quantize the visible red contribution, so existing light shadows and
    // texture detail remain meaningful rather than painting through walls.
    float red=max(0,source.r-max(source.g,source.b)*1.12);
    float visibility=saturate((red-.003)*24);
    float brightest=max(source.r,max(source.g,source.b));
    bool emissive=brightest>1.5 || (source.r>.9 && (max(source.g,source.b)<.03 || max(source.g,source.b)>.35));
    if(visibility<=0 || emissive) return source;
    float3 underlying=source.rgb-float3(red,0,0);
    float textureShade=clamp(dot(source.rgb,float3(.1,.6,.3))*2+.25,.25,.9);
    float3 styled=underlying+palette*textureShade*intensity;
    return half4(lerp(source.rgb,styled,visibility*_Strength),source.a);
   }
   ENDHLSL
  }
 }
}
