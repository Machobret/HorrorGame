Shader "Horror Game/Environment/Garage Door Atlas"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap("Garage doors (4 columns, 4 rows)", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.15
        _Metallic("Metallic", Range(0,1)) = 0
        [PerRendererData] _AtlasTile("Door design", Float) = 0
        [HideInInspector] _AtlasLayout("Columns, rows, unit UVs", Vector) = (4,4,1,0)
        [PerRendererData] _VisibleHeight("Visible leaf fraction", Float) = 1
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
        [HideInInspector] _BumpScale("Normal scale", Float) = 1
        [HideInInspector] _SpecColor("Specular", Color) = (0.2,0.2,0.2,1)
        [HideInInspector] _EmissionColor("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" "DisableBatching"="True" }
        UsePass "Horror Game/Doors/Door Atlas/ForwardLit"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
