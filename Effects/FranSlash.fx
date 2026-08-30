// Internal-test shader source. FranSlash.xnb is the compiled asset loaded by tModLoader.
// This intentionally uses the same parameter layout as the local precompiled test effect.
sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
float3 uColor;
float3 uSecondaryColor;
float uOpacity;
float uSaturation;
float uRotation;
float uTime;
float4 uSourceRect;
float2 uWorldPosition;
float uDirection;
float3 uLightSource;
float2 uImageSize0;
float2 uImageSize1;
float4 uShaderSpecificData;
float coordinateZoomFactor;
bool useStaticLine;
bool useTrueNoise;

float Random(float2 coords)
{
    return abs(frac(sin(dot(coords, float2(17.8342, 74.8819))) * 53648));
}

float4 FranPixel(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float noise = Random(coords * coordinateZoomFactor * 0.3 + float2(0, frac(uTime * -97.3)));
    float brightLine = useStaticLine ? pow(sin(uTime * 3.1 - coords.y * 5.4) * 0.5 + 0.5, 4.96) : 0;
    brightLine += pow(sin(uTime * 1.9 - coords.x * 2.4) * 0.5 + 0.5, 2.51) * 0.55;
    brightLine += noise * (useStaticLine ? 0.16 : 0.91);
    return float4(uColor, 1) * lerp(1, 6, saturate(brightLine)) * sampleColor * tex2D(uImage0, coords).a * 0.2;
}

technique FranSlash
{
    pass GlitchPass
    {
        PixelShader = compile ps_2_0 FranPixel();
    }
}
