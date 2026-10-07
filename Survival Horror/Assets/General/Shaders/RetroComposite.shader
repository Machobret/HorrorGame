Shader "Hidden/HorrorGame/Retro Composite"
{
    Properties
    {
        _VirtualResolution ("Virtual Resolution", Vector) = (320, 180, 0, 0)
        _ColorDepth ("Colour Depth", Range(2, 64)) = 32
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.7
        _PixelWobble ("Pixel Wobble", Range(0, 1)) = 0.22
        _PixelShiftStrength ("Pixel Shift", Range(0, 1)) = 0.18
        _PixelShiftRate ("Pixel Shift Rate", Range(1, 12)) = 3
        _LensWarp ("Lens Warp", Range(0, 0.2)) = 0.025
        _JitterStrength ("Screen Jitter", Range(0, 1)) = 0.08
        _ChromaticAberration ("Chromatic Aberration", Range(0, 2)) = 0
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0
        _InterlaceIntensity ("Interlace Intensity", Range(0, 1)) = 0
        _NoiseStrength ("Noise Strength", Range(0, 0.2)) = 0
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Retro Composite"
            ZTest Always
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _VirtualResolution;
                float _ColorDepth;
                float _DitherStrength;
                float _PixelWobble;
                float _PixelShiftStrength;
                float _PixelShiftRate;
                float _LensWarp;
                float _JitterStrength;
                float _ChromaticAberration;
                float _ScanlineIntensity;
                float _InterlaceIntensity;
                float _NoiseStrength;
                float _VignetteIntensity;
            CBUFFER_END

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float Bayer4x4(uint2 pixel)
            {
                const float bayerValues[16] =
                {
                    0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0,
                    3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0
                };
                return bayerValues[(pixel.x & 3u) + ((pixel.y & 3u) << 2u)] / 16.0 - 0.5;
            }

            float RetroNoise(float2 pixel)
            {
                return frac(sin(dot(pixel, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 virtualResolution = max(_VirtualResolution.xy, 1.0);
                float frame = floor(_Time.y * 12.0);
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float2 warpedUv = saturate(input.uv + centeredUv * dot(centeredUv, centeredUv) * _LensWarp);
                float jitter = (RetroNoise(float2(frame, 1.0)) - 0.5) * _JitterStrength;
                float row = floor(warpedUv.y * virtualResolution.y);
                float wobble = (RetroNoise(float2(row, frame)) - 0.5) * _PixelWobble;
                float2 virtualPixel = clamp(floor(warpedUv * virtualResolution + float2(jitter + wobble, 0.0)), 0.0, virtualResolution - 1.0);
                float pixelShiftFrame = floor(_Time.y * _PixelShiftRate);
                float shiftMask = step(1.0 - _PixelShiftStrength * 0.1, RetroNoise(virtualPixel + pixelShiftFrame * 17.0));
                float2 shiftDirection = float2(
                    floor(RetroNoise(virtualPixel + float2(pixelShiftFrame * 31.0, pixelShiftFrame * 47.0)) * 3.0),
                    floor(RetroNoise(virtualPixel + float2(pixelShiftFrame * 59.0, pixelShiftFrame * 83.0)) * 3.0)) - 1.0;
                virtualPixel = clamp(virtualPixel + shiftDirection * shiftMask, 0.0, virtualResolution - 1.0);
                float2 snappedUv = (virtualPixel + 0.5) / virtualResolution;
                float2 chromaticOffset = float2(_ChromaticAberration / virtualResolution.x, 0.0);
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, snappedUv);
                color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, snappedUv + chromaticOffset).r;
                color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, snappedUv - chromaticOffset).b;
                float dither = Bayer4x4((uint2)virtualPixel) * _DitherStrength;
                float levels = max(_ColorDepth - 1.0, 1.0);
                color.rgb = floor(saturate(color.rgb) * levels + 0.5 + dither) / levels;

                float scanline = (fmod(virtualPixel.y, 2.0) < 1.0 ? -1.0 : 1.0) * _ScanlineIntensity;
                float interlace = (fmod(virtualPixel.y + frame, 2.0) < 1.0 ? -1.0 : 1.0) * _InterlaceIntensity;
                float grain = (RetroNoise(virtualPixel + frame) - 0.5) * _NoiseStrength;
                float vignette = 1.0 - dot(centeredUv, centeredUv) * _VignetteIntensity;
                color.rgb = max(0.0, color.rgb * (1.0 + scanline + interlace + grain) * saturate(vignette));
                return color;
            }
            ENDHLSL
        }
    }
}
