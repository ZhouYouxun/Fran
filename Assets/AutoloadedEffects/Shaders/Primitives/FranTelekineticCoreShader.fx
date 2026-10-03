// Continuous blue pressure wake beneath the segmented rails.
float globalTime;
float3 uBodyColor;
float3 uEdgeColor;
float3 uCoreColor;
float uOpacity;
float uSeed;
matrix uWorldViewProjection;
float uDistanceProgress[31];
sampler uMaterialSampler : register(s1);

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float3 TextureCoordinates : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float3 TextureCoordinates : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;
    output.Position = mul(input.Position, uWorldViewProjection);
    output.Position.z = 0;
    output.Color = input.Color;
    // Preserve the interpolated width until the pixel stage to avoid triangular UV distortion.
    output.TextureCoordinates = input.TextureCoordinates;
    int pointIndex = clamp((int)floor(input.TextureCoordinates.x * 30.0 + 0.5), 0, 30);
    output.TextureCoordinates.x = uDistanceProgress[pointIndex];
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 coords = input.TextureCoordinates.xy;
    coords.y = (coords.y - 0.5) / max(input.TextureCoordinates.z, 0.0001) + 0.5;
    coords = saturate(coords);

    float y = coords.y * 2.0 - 1.0;
    float rail = 1.0 - smoothstep(0.05, 0.18, abs(abs(y) - 0.42));
    float phase = frac(coords.x * 6.0 - globalTime * 2.1 + uSeed);
    float cell = abs(phase - 0.5) * 2.0;
    // The moving diamond-shaped braces bind the two rails into discrete pressure cells.
    float brace = 1.0 - smoothstep(0.045, 0.15, abs(abs(y) + cell * 0.48 - 0.48));
    brace *= 1.0 - smoothstep(0.48, 0.65, abs(y));
    float packet = pow(saturate(1.0 - cell), 3.0);
    // The wake remains continuous between cells; noise modulates its surface, never cuts it out.
    float flow = tex2D(uMaterialSampler, float2(coords.x * 2.4 - globalTime * 0.9 + uSeed,
        coords.y * 1.6 + globalTime * 0.12)).r;
    float interior = (1.0 - smoothstep(0.08, 0.92, abs(y))) * (0.42 + flow * 0.24);
    float material = rail * (0.68 + packet * 0.50) + brace * 0.82 + interior;
    float tail = pow(saturate(1.0 - coords.x), 0.62);
    float coverage = saturate(material * tail * input.Color.a * uOpacity);
    float3 energy = lerp(uBodyColor, uEdgeColor, saturate(rail * 0.5 + brace * 0.55));
    energy = lerp(energy, uCoreColor, packet * rail * 0.42);
    float glow = (rail * 0.24 + brace * 0.13) * tail * input.Color.a * saturate(uOpacity);
    // AlphaBlend expects premultiplied color. The small emissive term keeps rails visible on bright terrain.
    return float4(energy * coverage + uEdgeColor * glow, coverage);
}

technique FranTelekineticCore
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
