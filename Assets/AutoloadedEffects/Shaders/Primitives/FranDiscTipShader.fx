// Blade-tip ribbon: white incandescent seam with two flowing filaments; skill-tintable.
float globalTime;
float3 uTint;
float uOpacity;
matrix uWorldViewProjection;
sampler uNoise : register(s1);
struct VSIn { float4 Position : POSITION0; float4 Color : COLOR0; float3 UV : TEXCOORD0; };
struct VSOut { float4 Position : SV_POSITION; float4 Color : COLOR0; float3 UV : TEXCOORD0; };
VSOut VertexShaderFunction(VSIn input)
{
    VSOut output = (VSOut)0;
    output.Position = mul(input.Position, uWorldViewProjection);
    output.Position.z = 0;
    output.Color = input.Color;
    output.UV = input.UV;
    return output;
}
float4 PixelShaderFunction(VSOut input) : COLOR0
{
    float x = saturate(input.UV.x);
    float y = (input.UV.y - 0.5) / max(input.UV.z, 0.0001) * 2;
    float noise = tex2D(uNoise, float2(x * 3.0 - globalTime * 1.4, y * 0.7)).r;
    float sheath = pow(saturate(1.0 - abs(y)), 1.4) * (0.45 + noise * 0.55);
    float core = pow(saturate(1.0 - abs(y) * 5.0), 2.0);
    float filament = pow(saturate(1.0 - abs(y - sin(x * 26 - globalTime * 8) * 0.35) * 13), 2);
    float fade = pow(1.0 - x, 0.8) * uOpacity * input.Color.a;
    float alpha = saturate((sheath * 0.7 + core * 0.7 + filament * 0.3) * fade);
    float3 tint = lerp(uTint, float3(1,1,1), saturate(core * 0.85 + filament * 0.35));
    return float4(tint * alpha + uTint * filament * fade * 0.2, alpha);
}
technique FranDiscTip
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
