// Terratomere pierce response, original BlobbyNoise/Extra_189 inputs, with Fran's blue palette.
float globalTime;
float3 uBodyColor;
float3 uEdgeColor;
float3 uCoreColor;
float uOpacity;
matrix uWorldViewProjection;
float uDistanceProgress[31];
sampler uMaterialSampler : register(s1);
sampler uDetailSampler : register(s2);

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

    float bloom = pow(saturate(sin(coords.y * 3.141)), 5.6);
    float noise = tex2D(uMaterialSampler, coords * 3.0 - float2(globalTime * 2.44, 0)).r;
    float streak = tex2D(uDetailSampler,
        coords * float2(2.0, 1.0) - float2(globalTime * 1.61, 0)).r + noise * bloom;
    float3 energy = lerp(uBodyColor, uEdgeColor, noise);
    float envelope = bloom * input.Color.a * saturate(uOpacity) * pow(1.0 - coords.x, 1.6);

    // A single premultiplied pass, with luminous detail added to RGB rather than thresholded away.
    // Preserve the reference alpha response too; do not apply a second source-alpha multiplication.
    return float4(energy + uCoreColor * streak, 1.0 + streak) * envelope;
}

technique FranTelekineticCore
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
