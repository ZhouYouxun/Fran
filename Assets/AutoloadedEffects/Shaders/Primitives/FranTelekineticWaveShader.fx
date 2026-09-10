// Broad blue wave ribbon for Fran's telekinetic pressure blades.
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
    float along = saturate(coords.x);
    coords.y = (coords.y - 0.5) / input.TextureCoordinates.z + 0.5;
    float cross = saturate(coords.y);

    float centered = abs(cross * 2.0 - 1.0);
    float softBody = saturate(1.0 - centered);
    float hotRail = exp2(-54.0 * centered * centered);
    float shoulderA = exp2(-90.0 * (centered - 0.36) * (centered - 0.36));
    float shoulderB = exp2(-90.0 * (centered - 0.58) * (centered - 0.58));

    float drift = tex2D(uMaterialSampler, coords * float2(1.35, 0.82) +
        float2(globalTime * -0.43, globalTime * 0.07)).r;
    float grain = tex2D(uMaterialSampler, coords * float2(0.58, 1.72) +
        float2(globalTime * 0.19, globalTime * -0.31)).r;
    float turbulence = saturate(drift * 0.68 + grain * 0.32);

    float nose = 1.0 + smoothstep(0.55, 0.0, along) * 2.35;
    float tail = pow(saturate(1.0 - along), 1.08);
    float interiorFlicker = pow(saturate(softBody + turbulence * 1.15 - 0.34), 1.7);
    float ribbonLight = hotRail * 1.08 + (shoulderA + shoulderB) * 0.28 +
        interiorFlicker * lerp(0.10, 0.44, pow(along, 0.38));

    return input.Color * ribbonLight * nose * tail * 2.85 * saturate(uOpacity);
}

technique FranTelekineticWave
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
