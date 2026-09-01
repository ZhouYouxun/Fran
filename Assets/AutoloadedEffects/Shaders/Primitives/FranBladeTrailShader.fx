float globalTime;
float3 uBodyColor;
float3 uEdgeColor;
float3 uCoreColor;
float uOpacity;
float uPassMode;
float uProfile;
float uFlowSpeed;
float uNoiseStrength;
float uSeed;
matrix uWorldViewProjection;

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
    // These project-local material maps are opaque grayscale textures, so alpha is not a mask.
    return dot(sampleColor.rgb, float3(0.299, 0.587, 0.114));
}

float Band(float value, float center, float innerWidth, float outerWidth)
{
    return 1.0 - smoothstep(innerWidth, outerWidth, abs(value - center));
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    const float Pi = 3.14159265;
    float2 uv = input.TextureCoordinates;
    uv.x = saturate(uv.x);
    uv.y = saturate(uv.y);

    float profile = clamp(uProfile, 0.0, 3.0);
    float across = abs(uv.y - 0.5) * 2.0;
    float center = saturate(1.0 - across);
    float time = globalTime;
    float seedAngle = uSeed * Pi * 2.0;
    float noiseAmount = saturate(uNoiseStrength);

    float tailFade = 1.0;
    float bodyMask = 0.0;
    float edgeBand = 0.0;
    float coreMask = 0.0;
    float auraMask = 0.0;
    float density = 1.0;
    float traveling = 0.0;
    float material = 0.0;
    float detail = 0.0;

    // Telekinetic needle: a sharp pressure wake with fine electric threads and fast scan pulses.
    if (profile < 0.5)
    {
        float2 materialUv = float2(uv.x * 3.8 - time * uFlowSpeed * 0.34 + uSeed,
            uv.y * 1.55 + sin(uv.x * 17.0 - time * 1.8 + seedAngle) * 0.045);
        float2 detailUv = float2(uv.x * 5.2 - time * uFlowSpeed * 0.19 + uSeed * 0.37,
            uv.y * 2.4 - cos(uv.x * 23.0 + time * 1.3 + seedAngle) * 0.035);
        material = SampleValue(tex2D(uMaterialSampler, materialUv));
        detail = SampleValue(tex2D(uDetailSampler, detailUv));

        float filaments = smoothstep(0.42, 0.88, material);
        float sparks = smoothstep(0.67, 0.96, detail);
        float scanWave = sin((uv.x - time * 0.76 + uSeed) * 58.0) * 0.5 + 0.5;
        traveling = pow(saturate(scanWave), 12.0);
        float edgeWarp = (material - 0.5) * 0.075 * noiseAmount;
        float boundary = 0.67 + edgeWarp;

        bodyMask = 1.0 - smoothstep(boundary, 0.98 + edgeWarp, across);
        edgeBand = Band(across, boundary, 0.015, 0.105);
        coreMask = pow(center, 7.2) * (0.70 + filaments * 0.30);
        auraMask = pow(center, 1.45);
        density = 0.48 + filaments * 0.34 + sparks * 0.12 + traveling * 0.15;
        tailFade = pow(saturate(1.0 - uv.x), 0.82);
    }
    // Arcane wisp: broad rolling vapor with eroded edges and a slow breathing center.
    else if (profile < 1.5)
    {
        float2 materialUv = float2(uv.x * 1.48 - time * uFlowSpeed * 0.20 + uSeed,
            uv.y * 1.72 + sin(uv.x * 8.0 + time * 0.72 + seedAngle) * 0.11);
        material = SampleValue(tex2D(uMaterialSampler, materialUv));
        float2 detailUv = float2(uv.x * -2.25 + time * 0.31 + uSeed * 0.51,
            uv.y * 2.65 + material * 0.19 + cos(uv.x * 11.0 - time * 0.54) * 0.08);
        detail = SampleValue(tex2D(uDetailSampler, detailUv));

        float turbulence = saturate(material * 0.72 + detail * 0.38);
        float edgeWarp = (turbulence - 0.5) * 0.28 * noiseAmount;
        float boundary = 0.78 + edgeWarp;
        float breathe = sin(uv.x * 10.5 + time * 1.15 + seedAngle) * 0.5 + 0.5;

        bodyMask = 1.0 - smoothstep(boundary - 0.24, boundary + 0.08, across);
        edgeBand = Band(across, boundary - 0.06, 0.035, 0.19);
        coreMask = pow(center, 2.35) * (0.46 + detail * 0.42 + breathe * 0.12);
        auraMask = pow(center, 0.82) * (0.55 + turbulence * 0.45);
        density = 0.30 + smoothstep(0.16, 0.82, turbulence) * 0.70;
        traveling = pow(saturate(sin((uv.x + time * 0.16 + uSeed) * 16.0) * 0.5 + 0.5), 4.0);
        tailFade = pow(saturate(1.0 - uv.x), 0.46);
    }
    // Spectral blade: a hard cutting wedge made of facets, fracture veins and moving glints.
    else if (profile < 2.5)
    {
        float2 materialUv = float2(uv.x * 2.72 - time * uFlowSpeed * 0.16 + uSeed,
            uv.y * 1.88 + uv.x * 0.24);
        float2 detailUv = float2(uv.x * 4.4 - time * 0.42 + uSeed * 0.71,
            uv.y * 2.15 - uv.x * 0.31);
        material = SampleValue(tex2D(uMaterialSampler, materialUv));
        detail = SampleValue(tex2D(uDetailSampler, detailUv));

        float facets = floor(saturate(material) * 4.0) / 3.0;
        float fractures = smoothstep(0.58, 0.91, material);
        float glintWave = sin((uv.x * 8.0 + uv.y * 1.7 - time * 0.68 + uSeed) * Pi) * 0.5 + 0.5;
        traveling = pow(saturate(glintWave), 10.0);
        float boundary = 0.76 + (facets - 0.5) * 0.055 * noiseAmount;

        bodyMask = 1.0 - smoothstep(boundary - 0.055, boundary + 0.055, across);
        edgeBand = Band(across, boundary, 0.008, 0.075) + fractures * Band(across, 0.36, 0.025, 0.12) * 0.45;
        coreMask = pow(center, 5.4) + Band(across, 0.31, 0.025, 0.10) * fractures * 0.46;
        auraMask = pow(center, 1.70) * (0.68 + fractures * 0.32);
        density = 0.42 + facets * 0.28 + fractures * 0.22 + detail * 0.08;
        tailFade = pow(saturate(1.0 - uv.x), 0.68);
    }
    // Heavy hammer: broad compression wake with fractured mass and regularly spaced shock knots.
    else
    {
        float2 materialUv = float2(uv.x * 2.15 - time * uFlowSpeed * 0.12 + uSeed,
            uv.y * 1.65 + sin(uv.x * 14.0 - time * 0.9 + seedAngle) * 0.055);
        float2 detailUv = float2(uv.x * -1.32 + time * 0.27 + uSeed * 0.43,
            uv.y * 1.92 + material * 0.12);
        material = SampleValue(tex2D(uMaterialSampler, materialUv));
        detail = SampleValue(tex2D(uDetailSampler, detailUv));

        float fractures = smoothstep(0.54, 0.90, material);
        float shockWave = sin((uv.x * 11.0 - time * 0.72 + uSeed) * Pi) * 0.5 + 0.5;
        float shockKnots = pow(saturate(shockWave), 8.0);
        traveling = shockKnots;
        float boundary = 0.82 + (detail - 0.5) * 0.11 * noiseAmount;

        bodyMask = 1.0 - smoothstep(boundary - 0.11, boundary + 0.07, across);
        edgeBand = Band(across, boundary - 0.035, 0.025, 0.14) + fractures * 0.24;
        coreMask = pow(center, 6.2) * (0.62 + shockKnots * 0.52) +
            Band(across, 0.48, 0.035, 0.13) * shockKnots * 0.30;
        auraMask = pow(center, 1.18) * (0.64 + detail * 0.22 + shockKnots * 0.25);
        density = 0.46 + fractures * 0.28 + detail * 0.13 + shockKnots * 0.18;
        tailFade = pow(saturate(1.0 - uv.x), 0.34);
    }

    float headFlash = 1.0 - smoothstep(0.0, profile < 0.5 ? 0.10 : 0.16, uv.x);
    float opacity = saturate(uOpacity) * input.Color.a * tailFade;

    if (uPassMode < 0.5)
    {
        float alpha = saturate(bodyMask * opacity * density);
        float highlight = saturate(edgeBand * 0.64 + material * 0.13 + traveling * 0.12 + headFlash * 0.18);
        float3 body = lerp(uBodyColor, uEdgeColor, highlight);
        return float4(body * alpha, alpha);
    }

    if (uPassMode < 1.5)
    {
        float emission = saturate(auraMask * opacity *
            (0.13 + density * 0.18 + edgeBand * 0.13 + traveling * 0.10 + headFlash * 0.16));
        float3 glow = lerp(uBodyColor, uEdgeColor, saturate(0.66 + edgeBand * 0.24 + headFlash * 0.10));
        return float4(glow, emission);
    }

    float filament = saturate(coreMask * opacity *
        (0.54 + detail * 0.15 + traveling * 0.34 + headFlash * 0.24));
    float3 hot = lerp(uEdgeColor, uCoreColor, saturate(0.64 + coreMask * 0.30 + traveling * 0.18));
    return float4(hot, filament);
}

technique FranBladeTrail
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
