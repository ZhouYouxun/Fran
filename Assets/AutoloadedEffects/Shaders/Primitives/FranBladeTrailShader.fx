float globalTime;
float3 uBodyColor;
float3 uEdgeColor;
float3 uCoreColor;
float uOpacity;
float uPassMode;
float uProfile;
float uFlowSpeed;
float uNoiseStrength;
matrix uWorldViewProjection;

sampler uPrimarySampler : register(s1);
sampler uNoiseSampler : register(s2);

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
    float2 TextureCoordinates : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;
    output.Position = mul(input.Position, uWorldViewProjection);
    output.Position.z = 0;
    output.Color = input.Color;
    output.TextureCoordinates = input.TextureCoordinates.xy;
    output.TextureCoordinates.y = (output.TextureCoordinates.y - 0.5) / input.TextureCoordinates.z + 0.5;
    return output;
}

float SampleValue(float4 sampleColor)
{
    return max(sampleColor.a, dot(sampleColor.rgb, float3(0.299, 0.587, 0.114)));
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 uv = saturate(input.TextureCoordinates);
    float across = abs(uv.y - 0.5) * 2.0;
    float profile = clamp(uProfile, 0.0, 3.0);

    float2 authoredUv = float2(1.0 - uv.x, uv.y);
    float authored = SampleValue(tex2D(uPrimarySampler, authoredUv));
    authored = smoothstep(0.025, 0.58, authored);

    float2 flowUv = float2(uv.x * (2.0 + profile * 0.17) - globalTime * uFlowSpeed,
        uv.y * (1.35 + profile * 0.12) + sin(uv.x * 9.0 + globalTime * 0.7) * 0.06);
    float noise = SampleValue(tex2D(uNoiseSampler, flowUv));
    float secondNoise = SampleValue(tex2D(uNoiseSampler,
        float2(flowUv.x * -0.63 + globalTime * 0.21, flowUv.y * 1.77 + 0.31)));
    float materialNoise = saturate(lerp(0.52, noise, uNoiseStrength) + secondNoise * uNoiseStrength * 0.24);

    float tailPower = profile < 0.5 ? 0.72 : profile < 1.5 ? 0.50 : profile < 2.5 ? 0.62 : 0.38;
    float tailFade = pow(saturate(1.0 - uv.x), tailPower);
    float edgeWarp = (materialNoise - 0.5) * (profile > 2.5 ? 0.08 : profile > 0.5 && profile < 1.5 ? 0.13 : 0.055);
    float bodyMask = 1.0 - smoothstep(0.70 + edgeWarp, 1.0 + edgeWarp, across);
    float edgeBand = 1.0 - smoothstep(0.035, 0.20, abs(across - (0.70 + edgeWarp)));
    float coreMask = pow(saturate(1.0 - across), profile < 0.5 ? 4.8 : profile > 2.5 ? 6.2 : 3.6);
    float headFlash = 1.0 - smoothstep(0.0, 0.13, uv.x);
    float traveling = pow(saturate(sin(uv.x * (28.0 + profile * 5.0) -
        globalTime * (5.0 + abs(uFlowSpeed))) * 0.5 + 0.5), 5.0);
    float opacity = saturate(uOpacity) * input.Color.a * tailFade;

    if (uPassMode < 0.5)
    {
        float density = lerp(0.62, 1.0, authored) * lerp(0.80, 1.0, materialNoise);
        float alpha = bodyMask * opacity * density;
        float3 body = lerp(uBodyColor, uEdgeColor,
            saturate(edgeBand * 0.70 + authored * 0.18 + headFlash * 0.15));
        return float4(body * alpha, alpha);
    }

    if (uPassMode < 1.5)
    {
        float auraProfile = pow(saturate(1.0 - across), profile > 2.5 ? 1.7 : 1.25);
        float emission = auraProfile * opacity *
            (0.16 + authored * 0.24 + materialNoise * 0.16 + headFlash * 0.18);
        float3 glow = lerp(uBodyColor, uEdgeColor, 0.76 + headFlash * 0.16);
        return float4(glow, saturate(emission));
    }

    float filament = coreMask * opacity *
        (0.58 + authored * 0.24 + traveling * 0.38 + headFlash * 0.24);
    float3 hot = lerp(uEdgeColor, uCoreColor, saturate(0.70 + coreMask * 0.28));
    return float4(hot, saturate(filament));
}

technique FranBladeTrail
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
