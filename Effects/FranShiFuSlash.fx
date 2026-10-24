// Original ShiFu material. UV.x follows the swing; UV.y runs from tip to hilt.
matrix ArcTransform;
float AnimationTime;
float3 EnergyTint;
float3 ShadowTint;
float PassMode;
sampler FlowMap : register(s0);
sampler ErosionMap : register(s1);

struct VIn { float4 Position : POSITION0; float4 Color : COLOR0; float2 UV : TEXCOORD0; };
struct VOut { float4 Position : SV_POSITION; float4 Color : COLOR0; float2 UV : TEXCOORD0; };
VOut VertexMain(VIn v)
{
    VOut o;
    o.Position = mul(v.Position, ArcTransform);
    o.Color = v.Color;
    o.UV = v.UV;
    return o;
}

float4 FragmentMain(VOut v) : COLOR0
{
    float2 uv = v.UV;
    float age = saturate(uv.x);
    float radial = saturate(uv.y);
    float2 flowUV = float2(age * 1.3 - AnimationTime * 0.37, radial * 0.84);
    float flow = tex2D(FlowMap, flowUV).r;
    float bend = tex2D(ErosionMap, float2(age * 1.1 - AnimationTime * 0.09, radial * 0.7)).r;
    float2 foldedUV = flowUV * float2(1.8, 1.25) + float2(bend * 0.12, flow * 0.16);
    float folded = tex2D(FlowMap, foldedUV + float2(0.31, AnimationTime * 0.045)).r;
    float grain = tex2D(ErosionMap, uv * float2(4.5, 2.4) + float2(-AnimationTime * 0.24, flow * 0.10)).r;

    // A curved, ragged cutting lip and a porous inner boundary, rather than a filled disc.
    float lip = 0.025 + (bend - 0.5) * 0.030;
    float outside = smoothstep(lip - 0.012, lip + 0.020, radial);
    float inside = 1.0 - smoothstep(0.62 + flow * 0.23, 0.88 + flow * 0.12, radial);
    float tail = pow(saturate(1.0 - age), 0.62);
    float head = smoothstep(0.0, 0.022, age);
    float erosion = smoothstep(age * 0.67 + radial * 0.18 - 0.12,
        age * 0.67 + radial * 0.18 + 0.18, grain * 0.50 + flow * 0.50);
    float mask = outside * inside * head * tail * erosion * v.Color.a;

    // Broad moving folds carry the surface; fine ridges only pick out their contours.
    float ridge = pow(saturate(1.0 - abs(flow - 0.52) * 3.8), 1.8);
    float veins = pow(saturate(1.0 - abs(folded - 0.50) * 6.5), 2.0);
    float rim = exp2(-abs(radial - lip - 0.035) * 72.0);
    float innerRim = exp2(-abs(radial - 0.23 - (flow - 0.5) * 0.07) * 110.0);
    float lit = saturate(ridge * 0.64 + veins * 0.30 + folded * 0.22);
    float3 tint = lerp(ShadowTint, EnergyTint, saturate(0.25 + lit * 0.87 - radial * 0.24));
    float3 frost = lerp(EnergyTint, float3(0.91, 0.99, 1.0), 0.83);

    if (PassMode < 0.5)
    {
        float alpha = mask * (0.52 + flow * 0.26);
        return float4(tint * alpha, alpha);
    }
    float light = rim * 1.1 + innerRim * (0.14 + grain * 0.25) +
        ridge * (1.0 - radial) * 0.54 + veins * 0.28 +
        smoothstep(0.30, 0.75, folded) * (1.0 - radial) * 0.22;
    float emission = saturate(light * mask * 1.45);
    return float4(lerp(EnergyTint, frost, saturate(rim + veins * 0.65)), emission);
}

technique ShiFuSlash
{
    pass Surface
    {
        VertexShader = compile vs_3_0 VertexMain();
        PixelShader = compile ps_3_0 FragmentMain();
    }
}
