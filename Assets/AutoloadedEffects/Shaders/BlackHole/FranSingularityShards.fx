// Opaque black splinters with a thin violet rim. Premultiplied alpha; no additive black.
float progress;
float seed;

float4 PixelShaderFunction(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float x = uv.x;
    float center = 0.5 + sin(x * 18 + seed) * 0.055 + sin(x * 47 + seed * 2) * 0.022;
    float width = pow(saturate(1 - x), 0.7) * 0.35;
    width *= 0.83 + sin(x * 29 + seed) * 0.12;
    float distance = abs(uv.y - center);
    float silhouette = 1 - smoothstep(width * 0.8, width + 0.018, distance);
    silhouette *= smoothstep(0, 0.08, x) * (1 - smoothstep(0.91, 1, x));
    float rim = smoothstep(width * 0.64, width, distance);
    float streak = pow(saturate(1 - abs(uv.y - center) * 40), 3) * 0.2;
    float alpha = silhouette * pow(saturate(1 - progress), 0.65);
    float3 rgb = lerp(float3(0.002, 0.001, 0.005), float3(0.23, 0.07, 0.32), rim * 0.6 + streak);
    return float4(rgb * alpha, alpha) * color;
}
technique Technique1
{
    pass AutoloadPass { PixelShader = compile ps_3_0 PixelShaderFunction(); }
}
