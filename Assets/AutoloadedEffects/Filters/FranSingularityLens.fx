sampler screenTexture : register(s0);
float2 screenSize;
float2 centerUV;
float radiusPixels;
float effectStrength;

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 delta = (coords - centerUV) * screenSize;
    float radius = max(radiusPixels, 0.001);
    float distanceRatio = length(delta) / radius;
    float influence = exp(-distanceRatio * 3.6) * smoothstep(1, 0.65, distanceRatio) * effectStrength;
    float angle = influence * 2.4;
    float s = sin(angle);
    float c = cos(angle);
    float2 bent = float2(delta.x * c - delta.y * s, delta.x * s + delta.y * c);
    bent *= 1 - influence * 0.35;
    float2 sampledUV = clamp(centerUV + bent / screenSize, 0.001, 0.999);
    return tex2D(screenTexture, sampledUV);
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
