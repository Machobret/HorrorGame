Shader "Hidden/HorrorGame/Retro Composite"
{
    Properties
    {
        _VirtualResolution ("Virtual Resolution", Vector) = (320, 180, 0, 0)
        _ColorDepth ("Colour Depth", Range(2, 64)) = 32
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.7
        _ColorDitherStrength ("Colour Dither", Range(0, 0.5)) = 0.08
        _LightingSteps ("Lighting Steps", Range(8, 64)) = 24
        _LightingBandStrength ("Lighting Band Strength", Range(0, 1)) = 0.18
        _PixelWobble ("Pixel Wobble", Range(0, 1)) = 0.4
        _PixelShiftStrength ("Pixel Shift", Range(0, 1)) = 0.18
        _PixelShiftRate ("Pixel Shift Rate", Range(1, 12)) = 3
        _PixelShiftNearDistance ("Pixel Shift Near Distance", Float) = 4
        _PixelShiftFarDistance ("Pixel Shift Far Distance", Float) = 14
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_BlitTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _VirtualResolution;
                float _ColorDepth;
                float _DitherStrength;
                float _ColorDitherStrength;
                float _LightingSteps;
                float _LightingBandStrength;
                float _PixelWobble;
                float _PixelShiftStrength;
                float _PixelShiftRate;
                float _PixelShiftNearDistance;
                float _PixelShiftFarDistance;
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
                float2 snappedUv = (virtualPixel + 0.5) / virtualResolution;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(snappedUv), _ZBufferParams);
                float nearShiftFactor = 1.0 - smoothstep(_PixelShiftNearDistance, _PixelShiftFarDistance, sceneDepth);
                float2 pixelUvSize = 1.0 / virtualResolution;
                float leftDepth = LinearEyeDepth(SampleSceneDepth(saturate(snappedUv - float2(pixelUvSize.x, 0.0))), _ZBufferParams);
                float rightDepth = LinearEyeDepth(SampleSceneDepth(saturate(snappedUv + float2(pixelUvSize.x, 0.0))), _ZBufferParams);
                float upDepth = LinearEyeDepth(SampleSceneDepth(saturate(snappedUv + float2(0.0, pixelUvSize.y))), _ZBufferParams);
                float downDepth = LinearEyeDepth(SampleSceneDepth(saturate(snappedUv - float2(0.0, pixelUvSize.y))), _ZBufferParams);
                float edgeDepthDifference = max(max(abs(sceneDepth - leftDepth), abs(sceneDepth - rightDepth)), max(abs(sceneDepth - upDepth), abs(sceneDepth - downDepth)));
                float surfaceInteriorFactor = 1.0 - step(max(0.08, sceneDepth * 0.025), edgeDepthDifference);
                float pixelShiftFrame = floor(_Time.y * _PixelShiftRate);
                float shiftMask = step(1.0 - _PixelShiftStrength * 0.1 * nearShiftFactor * surfaceInteriorFactor, RetroNoise(virtualPixel + pixelShiftFrame * 17.0));
                float2 shiftDirection = float2(
                    floor(RetroNoise(virtualPixel + float2(pixelShiftFrame * 31.0, pixelShiftFrame * 47.0)) * 3.0),
                    floor(RetroNoise(virtualPixel + float2(pixelShiftFrame * 59.0, pixelShiftFrame * 83.0)) * 3.0)) - 1.0;
                virtualPixel = clamp(virtualPixel + shiftDirection * shiftMask, 0.0, virtualResolution - 1.0);
                snappedUv = (virtualPixel + 0.5) / virtualResolution;
                float2 chromaticOffset = float2(_ChromaticAberration / virtualResolution.x, 0.0);
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, snappedUv);
                color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, snappedUv + chromaticOffset).r;
                color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, snappedUv - chromaticOffset).b;
                uint2 pixelCoordinate = (uint2)virtualPixel;
                float dither = Bayer4x4(pixelCoordinate) * _DitherStrength;
                float colorDither = Bayer4x4(pixelCoordinate + uint2(1u, 2u)) * _ColorDitherStrength;
                float luminance = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
                float lightingSteps = max(_LightingSteps, 1.0);
                float bandedLuminance = floor(luminance * lightingSteps + 0.5 + dither * 0.5) / lightingSteps;
                float lightingRatio = luminance > 0.0001 ? bandedLuminance / luminance : 0.0;
                color.rgb = lerp(color.rgb, saturate(color.rgb * lightingRatio), _LightingBandStrength);
                float levels = max(_ColorDepth - 1.0, 1.0);
                color.rgb = floor(saturate(color.rgb) * levels + 0.5 + float3(dither + colorDither, dither, dither - colorDither)) / levels;

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
