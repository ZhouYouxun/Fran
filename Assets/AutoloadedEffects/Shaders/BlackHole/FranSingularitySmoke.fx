// Broad curling sheets: black opaque interiors, translucent ragged fringes.
float progress;
float seed;

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7)) + seed) * 43758.5453);
}
float Noise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
        lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), f.x), f.y);
}
float4 PixelShaderFunction(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = uv * 2 - 1;
    float wave = sin(p.x * 4.8 - progress * 6 + seed) * 0.26;
    float2 flow = p * 3.7 + progress * float2(-1.2, 0.5);
    float turbulence = Noise(flow) * 0.65 + Noise(flow * 2.13 + 8.4) * 0.35;
    float envelope = 1 - pow(abs(p.x), 2.5);
    float width = envelope * (0.25 + turbulence * 0.25);
    float dist = abs(p.y - wave);
    float density = 1 - smoothstep(width * 0.3, width + 0.08, dist);
    density *= smoothstep(0, 0.22, envelope);
    float ribs = Noise(p * 9.1 - progress);
    density *= 0.7 + ribs * 0.3;
    float alpha = density * 0.92 * pow(saturate(1 - progress), 1.1);
    float edge = smoothstep(width * 0.3, width + 0.04, dist);
    float3 rgb = lerp(float3(0.003, 0.002, 0.007), float3(0.14, 0.06, 0.2), edge * 0.65);
    return float4(rgb * alpha, alpha) * color;
}
technique Technique1
{
    pass AutoloadPass { PixelShader = compile ps_3_0 PixelShaderFunction(); }
}
