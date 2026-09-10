// Layered blue energy filament for Fran's condensed mind core.
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

    float center = saturate(1.0 - abs(coords.y * 2.0 - 1.0));
    float core = center * center;
    core *= core * center;

    float2 flowA = coords * float2(2.65, 1.18) + float2(globalTime * -1.87, globalTime * 0.10);
    float2 flowB = coords * float2(1.18, 2.34) + float2(globalTime * -0.74, globalTime * -0.21);
    float vapor = tex2D(uMaterialSampler, flowA).r;
    float filament = tex2D(uDetailSampler, flowB).r;
    float crossing = saturate(vapor * 0.72 + filament * 0.62);
    float edgeLift = pow(saturate(center + crossing * 0.45), 3.4);
    float tail = pow(saturate(1.0 - coords.x), 1.48);
    float envelope = edgeLift * tail * input.Color.a * saturate(uOpacity);

    float3 energy = lerp(uBodyColor, uEdgeColor, saturate(vapor * 0.82 + filament * 0.22));
    float hotLine = core * (0.70 + filament * 0.85);
    return float4(energy + uCoreColor * hotLine, 1.0 + hotLine) * envelope;
}

technique FranTelekineticCore
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
