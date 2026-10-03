// Adapted from Lucille Karma / WrathOfTheGodsPublic RealBlackHoleShader.fx,
// commit 7cb5b86. Preserves the 75-step torus/lightmarch appearance. This pass
// renders only the body into a small transparent target; lensing is separate.
sampler noiseTexture : register(s1);
float globalTime;
float tilt;

float Hash13(float3 p)
{
    return frac(sin(dot(p, float3(12.9898, 78.233, 51.9852))) * 30000);
}

float3 Rotate(float3 v, float3 axis, float angle)
{
    return v * cos(angle) + cross(v, axis) * sin(angle) + axis * dot(axis, v) * (1 - cos(angle));
}

float4 SampleDisk(float3 position)
{
    float3 p = position / float3(1, 0.2, 1);
    float2 torus = float2(length(p.xz) - 0.75, p.y);
    float density = pow(saturate((0.33 - length(torus)) / 0.33), 0.9);
    float2 radial = float2(atan2(p.x, p.z) / 6.2831853 + 0.5, length(p));
    density *= tex2D(noiseTexture, radial * float2(3, 3.5) + globalTime * float2(6.3, -2)).r;
    float3 orange = float3(245, 105, 61) / 255.0;
    float4 light = float4(pow(orange, 1.1), 1) * density * 0.75;
    return lerp(light, float4(0, 0, 0, 1), smoothstep(0.01, 0, length(p) - 0.3));
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 xy = (coords * 2 - 1) / 0.85;
    float3 start = float3(xy, 0);
    float3 axis = normalize(float3(1, 0, tilt));
    float3 samplePoint = Rotate(float3(xy, -0.9), axis, 0.32);
    float3 forward = Rotate(float3(0, 0, 1), axis, 0.32);
    samplePoint += forward * Hash13(samplePoint * 10 + globalTime) * 0.0175;
    float4 result = 0;
    float captured = 0;
    float distanceToCenter = 1;
    for (float i = 0; i < 75; i++)
    {
        distanceToCenter = max(length(samplePoint), 0.0001);
        captured = smoothstep(0.01, -0.1, distanceToCenter - 0.3);
        float t = i / 75;
        float stepSize = lerp(0.02, 0.021, 1 - t * (4 - t * 4));
        float pull = clamp(0.005 / (distanceToCenter * distanceToCenter), 0, 0.1) * 0.3;
        samplePoint += -samplePoint / distanceToCenter * pull;
        samplePoint += forward * (1 - captured) * stepSize;
        result += SampleDisk(samplePoint);
    }

    float4 orange = float4(float3(245, 105, 61) / 255.0, 1);
    float attenuation = smoothstep(5, 0, distanceToCenter / 0.3);
    result += clamp(0.3 / pow(distanceToCenter, 3) * orange, 0, 2) * attenuation * lerp(1, orange * 0.12, captured);
    float rimDistance = length(start) - 0.3 * 1.7;
    result += 0.015 / max(abs(rimDistance), 0.001) * smoothstep(0.2, 0.1, rimDistance);

    // Smooth the local target boundary, retain an opaque dark core and bound HDR
    // accumulation before AlphaBlend. No full-screen white flash or bloom pass.
    float border = smoothstep(0.5, 0.45, max(abs(coords.x - 0.5), abs(coords.y - 0.5)));
    result = saturate(result);
    // Fold WoTG's extra BlackOnly overlay into this pass so the dark center
    // stays dark even where the analytic halo contributes a little orange.
    float brightness = dot(result.rgb, float3(0.333333, 0.333333, 0.333333));
    result.rgb *= lerp(1, smoothstep(0.1, 0.3, brightness), result.a);
    return result * border * sampleColor;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
