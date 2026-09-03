// Local SideStreakTrail reconstruction for Fran's blue UnholyEssence_Wave counterpart.
// Keep this response intentionally close to Calamity's reference shader: the palette is
// supplied by vertices, and Terraria's Perlin texture is bound to register s1.
float globalTime;
float uOpacity;
matrix uWorldViewProjection;

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

VertexShaderOutput VertexShaderFunction(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;
    output.Position = mul(input.Position, uWorldViewProjection);
    output.Position.z = 0;
    output.Color = input.Color;
    output.TextureCoordinates = input.TextureCoordinates;
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 coords = input.TextureCoordinates.xy;
    float longitudinal = saturate(coords.x);
    coords.y = (coords.y - 0.5) / input.TextureCoordinates.z + 0.5;

    // SideStreakTrail's recognizable rounded side light.
    float bloomOpacity = pow(saturate(cos(coords.y * 4.8 - 0.8)),
        55.0 + pow(longitudinal, 4.0) * 700.0);

    // Slow Perlin blotches keep the wide strip from reading as a flat translucent trapezoid.
    if (coords.y > 0.15 && coords.y < 0.85)
    {
        float noise = tex2D(uMaterialSampler,
            coords * 1.1 + float2(globalTime * -0.6, 0.0)).r;
        float minimumOpacity = pow(saturate(1.0 - sin(coords.y * 3.14159265) + noise * 2.2), 0.2);
        bloomOpacity += pow(longitudinal, 0.3) * lerp(0.04, 0.4, minimumOpacity);
    }

    // Match the reference's strong front-half bloom without a second artificial core pass.
    if (longitudinal < 0.5)
        bloomOpacity *= (1.0 - longitudinal * 2.0) * 2.7 + 1.0;

    return input.Color * lerp(0.0, 3.6, bloomOpacity * pow(longitudinal, 0.1)) *
        pow(saturate(1.0 - longitudinal), 1.1) * saturate(uOpacity);
}

technique FranTelekineticWave
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
