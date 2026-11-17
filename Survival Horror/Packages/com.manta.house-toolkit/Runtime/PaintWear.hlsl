// Shared by road paint and independent warehouse markings.
half PaintCoverage(float2 wearUV, half wear)
{
    half mask = SAMPLE_TEXTURE2D(_WearMap, sampler_WearMap, wearUV).a;
    half threshold = lerp(-.03, .97, wear);
    return smoothstep(threshold, threshold + .025, mask);
}
