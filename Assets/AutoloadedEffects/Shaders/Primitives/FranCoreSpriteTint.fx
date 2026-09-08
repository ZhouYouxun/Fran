sampler uImage0 : register(s0);
float3 uBlue;
struct PixelInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 UV : TEXCOORD0;
};
float4 PixelShaderFunction(PixelInput input) : COLOR0
{
    float4 source = tex2D(uImage0, input.UV);
    float high = max(source.r, max(source.g, source.b));
    float low = min(source.r, min(source.g, source.b));
    float white = pow(saturate(low / max(high, 0.0001)), 3.0);
    return float4(lerp(uBlue, float3(1, 1, 1), white) * high, source.a) * input.Color;
}
technique FranCoreSpriteTint
{
    pass AutoloadPass { PixelShader = compile ps_3_0 PixelShaderFunction(); }
}
