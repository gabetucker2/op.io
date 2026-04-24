#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0
    #define PS_SHADERMODEL ps_4_0
#endif

float4x4 MatrixTransform;
float4 BaseColor;
float TimeSeconds;
float FieldScale;
float BackgroundVariationStrength;
float WarpStrengthX;
float WarpStrengthY;
float CrestBrightness;
float CrestThickness;
float CrestSegmentation;
float CrestDensity;
float WaveSet1Strength;
float WaveSet2Strength;
float WaveSet3Strength;
float WorldPatternUnits;
float FieldTextureResolution;
float2 CameraOffset;
float2 RenderCenter;
float CameraZoom;

texture Texture;
texture FieldsA;
texture FieldsB;
texture FieldsC;

sampler TextureSampler = sampler_state
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler FieldsASampler = sampler_state
{
    Texture = <FieldsA>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler FieldsBSampler = sampler_state
{
    Texture = <FieldsB>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler FieldsCSampler = sampler_state
{
    Texture = <FieldsC>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

static const float Tau = 6.28318530718f;
static const float DefaultBackgroundVariationStrength = 0.055f;
static const float DefaultCrestBrightness = 1.24f;
static const float DefaultWaveSet1Strength = 0.86f;
static const float DefaultWaveSet2Strength = 0.56f;
static const float DefaultWaveSet3Strength = 0.38f;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TextureCoordinate : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinate : TEXCOORD0;
    float2 ScreenPosition : TEXCOORD1;
};

float SinTau(float value)
{
    return sin(Tau * value);
}

float SmoothStep01(float value)
{
    float t = saturate(value);
    return t * t * (3.0f - (2.0f * t));
}

float SmoothStepRange(float edge0, float edge1, float value)
{
    return SmoothStep01((value - edge0) / max(0.0001f, edge1 - edge0));
}

float2 ResolveWorldPosition(float2 screenPosition)
{
    float zoom = max(0.05f, CameraZoom);
    return ((screenPosition - RenderCenter) / zoom) + CameraOffset + RenderCenter;
}

float2 ResolvePatternPosition(float2 screenPosition)
{
    float2 worldPosition = ResolveWorldPosition(screenPosition);
    return (worldPosition / max(1.0f, WorldPatternUnits)) * max(0.20f, FieldScale);
}

float2 ResolvePeriodicFieldUv(float2 uv)
{
    float baseResolution = max(1.0f, FieldTextureResolution);
    float textureResolution = baseResolution + 1.0f;
    float2 wrapped = frac(uv);
    return ((wrapped * baseResolution) + 0.5f) / textureResolution;
}

float4 SampleFieldsA(float2 uv)
{
    return tex2D(FieldsASampler, ResolvePeriodicFieldUv(uv));
}

float4 SampleFieldsB(float2 uv)
{
    return tex2D(FieldsBSampler, ResolvePeriodicFieldUv(uv));
}

float4 SampleFieldsC(float2 uv)
{
    return tex2D(FieldsCSampler, ResolvePeriodicFieldUv(uv));
}

float PacketMaskFromField(float sampledValue, float threshold, float softness)
{
    float masked = saturate((sampledValue - threshold) / max(0.001f, softness));
    return SmoothStep01(masked);
}

float computeBackgroundField(float2 p, float timeSeconds)
{
    float x1 = p.x + (0.020f * SinTau((0.23f * p.y) - (0.02f * timeSeconds)));
    float y1 = p.y - (0.018f * timeSeconds) + (0.018f * SinTau((0.19f * p.x) + (0.03f * timeSeconds)));

    float f =
        (0.50f * SinTau((0.72f * x1) + (0.58f * y1) + 0.07f)) +
        (0.28f * SinTau((-1.11f * x1) + (0.86f * y1) + 0.31f)) +
        (0.17f * SinTau((1.63f * x1) - (1.24f * y1) + 0.63f)) +
        (0.11f * SinTau((-2.30f * x1) - (0.77f * y1) + 0.18f));

    f = f + (0.18f * f * f) - (0.10f * f * f * f);
    return saturate(0.5f + (0.5f * f));
}

float computeOceanHeight(float2 p, float timeSeconds, out float packetMix)
{
    float4 fieldsA = SampleFieldsA(p);
    float4 fieldsB = SampleFieldsB(p);

    float n1 = fieldsA.r;
    float n2 = fieldsA.g;
    float n3 = fieldsA.b;
    float rand = fieldsB.g;
    float rand2 = fieldsB.b;

    float warpXFactor = max(0.0f, WarpStrengthX);
    float warpYFactor = max(0.0f, WarpStrengthY);
    float flowY = -0.042f * timeSeconds;

    float warpX = warpXFactor * ((0.055f * (n1 - 0.5f)) + (0.026f * (n3 - 0.5f)) + (0.015f * (rand - 0.5f)));
    float warpY = warpYFactor * ((0.034f * (n2 - 0.5f)) + (0.014f * (rand2 - 0.5f)));

    float2 p1Domain = float2(p.x + warpX, p.y + flowY + warpY);

    float pack1 = SampleFieldsB(float2((p1Domain.x + 0.03f) * 0.9f, (p1Domain.y - 0.02f) * 0.8f)).a;
    float pack2 = SampleFieldsC(float2((p1Domain.x - 0.01f) * 1.2f, (p1Domain.y + 0.04f) * 1.0f)).r;
    float pack3 = SampleFieldsC(float2((p1Domain.x + 0.05f) * 1.5f, (p1Domain.y + 0.01f) * 1.3f)).g;

    float p1 = 0.35f + (0.75f * PacketMaskFromField(pack1, 0.40f, 0.25f));
    float p2 = 0.25f + (0.85f * PacketMaskFromField(pack2, 0.48f, 0.22f));
    float p3 = 0.20f + (0.95f * PacketMaskFromField(pack3, 0.50f, 0.24f));
    packetMix = saturate((p1 + p2 + p3) / 3.0f);

    float wave1Factor = max(0.0f, WaveSet1Strength / DefaultWaveSet1Strength);
    float wave2Factor = max(0.0f, WaveSet2Strength / DefaultWaveSet2Strength);
    float wave3Factor = max(0.0f, WaveSet3Strength / DefaultWaveSet3Strength);

    float h = 0.0f;
    h += wave1Factor * p1 * (
        (0.95f * SinTau((2.25f * (p1Domain.y - (0.11f * timeSeconds))) + (0.08f * p1Domain.x) + (0.15f * (n2 - 0.5f)))) +
        (0.28f * SinTau((3.70f * (p1Domain.y - (0.13f * timeSeconds))) - (0.04f * p1Domain.x) + (0.20f * (rand - 0.5f)) + 0.17f)));

    h += wave2Factor * p2 * (
        (0.55f * SinTau((2.95f * (p1Domain.y - (0.15f * timeSeconds))) - (0.15f * p1Domain.x) + (0.18f * (n3 - 0.5f)) + 0.31f)) +
        (0.22f * SinTau((4.90f * (p1Domain.y - (0.17f * timeSeconds))) + (0.09f * p1Domain.x) + (0.12f * (rand2 - 0.5f)) + 0.61f)));

    h += wave3Factor * p3 * (
        (0.32f * SinTau((3.55f * (p1Domain.y - (0.19f * timeSeconds))) + (0.19f * p1Domain.x) + (0.20f * (rand - 0.5f)) + 0.11f)) +
        (0.16f * SinTau((6.20f * (p1Domain.y - (0.22f * timeSeconds))) - (0.07f * p1Domain.x) + (0.14f * (n1 - 0.5f)) + 0.74f)));

    float body =
        (0.22f * SampleFieldsA(float2(p1Domain.x * 0.95f, p1Domain.y * 0.95f)).r) +
        (0.12f * SampleFieldsA(float2((p1Domain.x * 1.25f) + 0.05f, (p1Domain.y * 1.20f) - 0.02f)).g) +
        (0.08f * SampleFieldsA(float2((p1Domain.x * 1.90f) - 0.03f, (p1Domain.y * 1.70f) + 0.04f)).b) +
        (0.06f * SampleFieldsB(float2((p1Domain.x * 2.60f) + 0.01f, (p1Domain.y * 2.20f) - 0.03f)).g);

    return h + body;
}

float normalizeHeight(float height)
{
    return saturate(0.50f + (0.24f * height));
}

float normalizeSlope(float slope)
{
    return saturate((slope - 0.02f) / 0.42f);
}

float computeCrestMask(float2 p, float timeSeconds, float hn, float sn, float packetMix)
{
    float4 fieldsA = SampleFieldsA(p);
    float4 fieldsB = SampleFieldsB(p);
    float n1 = fieldsA.r;
    float n2 = fieldsA.g;
    float n3 = fieldsA.b;
    float rand = fieldsB.g;
    float rand2 = fieldsB.b;

    float xs = p.x + (0.07f * (n2 - 0.5f)) + (0.04f * (rand - 0.5f));
    float ys = p.y - (0.05f * timeSeconds) + (0.06f * (n1 - 0.5f)) + (0.03f * (rand2 - 0.5f));

    float segTex =
        (0.40f * SampleFieldsA(float2((xs * 1.10f) + 0.03f, (ys * 0.90f) + 0.02f)).a) +
        (0.22f * SampleFieldsB(float2((xs * 1.90f) - 0.01f, (ys * 1.20f) + 0.01f)).r) +
        (0.20f * SampleFieldsB(float2((xs * 2.70f) + 0.02f, (ys * 2.10f) - 0.04f)).g) +
        (0.18f * SampleFieldsC(float2((xs * 1.35f) + 0.01f, (ys * 1.05f) + 0.02f)).r);

    float localCenter = 0.72f + (0.03f * (SampleFieldsB(float2(xs * 1.6f, ys * 1.4f)).b - 0.5f));
    float widthFactor = lerp(0.90f, 1.30f, saturate(CrestThickness));
    float localWidth = (0.022f + (0.018f * SampleFieldsB(float2(xs * 1.9f, ys * 1.7f)).g)) * widthFactor;
    float crestBand = exp(-pow((hn - localCenter) / max(0.0001f, localWidth), 2.0f));

    float slopeBase = pow(saturate((sn - 0.12f) / 0.52f), 1.02f);
    float segLow = lerp(0.36f, 0.26f, saturate(CrestDensity));
    float segHigh = segLow + lerp(0.34f, 0.20f, saturate(CrestSegmentation));
    float segGate = SmoothStepRange(segLow, segHigh, segTex);

    float horizontalBias = saturate(0.60f + (0.40f * SinTau((0.92f * p.x) + (0.22f * p.y) - (0.03f * timeSeconds) + (0.35f * n3) + (0.40f * rand))));

    float intensityTex = saturate(
        (0.45f * SampleFieldsB(float2((xs * 2.2f) + 0.01f, (ys * 1.6f) + 0.03f)).g) +
        (0.30f * SampleFieldsB(float2((xs * 3.1f) - 0.02f, (ys * 2.4f) - 0.01f)).b) +
        (0.25f * SampleFieldsB(float2((xs * 1.3f) + 0.02f, (ys * 1.1f) + 0.01f)).a));
    float intensity = 0.76f + (0.32f * intensityTex);

    float contour = crestBand * slopeBase * segGate * horizontalBias;
    contour = saturate(contour * intensity * max(0.0f, CrestBrightness / DefaultCrestBrightness));
    return contour;
}

float4 applyOceanColor(float backgroundField, float hn, float sn, float crestMask)
{
    float backgroundFactor = max(0.0f, BackgroundVariationStrength / DefaultBackgroundVariationStrength);
    float centeredBackground = backgroundField - 0.5f;
    float compressedBackground =
        centeredBackground >= 0.0f
            ? centeredBackground * 0.42f
            : centeredBackground * 0.18f;
    float backgroundLuminance = 0.982f + ((0.040f * backgroundFactor) * compressedBackground);
    float waveLuminance = 1.0f + (0.011f * (hn - 0.5f)) + (0.009f * (sn - 0.5f));
    float3 water = saturate(BaseColor.rgb * (backgroundLuminance * waveLuminance));
    return float4(lerp(water, float3(1.0f, 1.0f, 1.0f), saturate(0.97f * crestMask)), 1.0f);
}

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TextureCoordinate = input.TextureCoordinate;
    output.ScreenPosition = input.Position.xy;
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float2 p = ResolvePatternPosition(input.ScreenPosition);

    float packetMix;
    float height = computeOceanHeight(p, TimeSeconds, packetMix);

    float slopeStep = 0.003f;
    float packetMixX;
    float packetMixY;
    float hx = computeOceanHeight(p + float2(slopeStep, 0.0f), TimeSeconds, packetMixX) - height;
    float hy = computeOceanHeight(p + float2(0.0f, slopeStep), TimeSeconds, packetMixY) - height;
    float slope = sqrt((hx * hx) + (hy * hy));

    float hn = normalizeHeight(height);
    float sn = normalizeSlope(slope);
    float backgroundField = computeBackgroundField(p, TimeSeconds);
    float crestMask = computeCrestMask(p, TimeSeconds, hn, sn, packetMix);

    return applyOceanColor(backgroundField, hn, sn, crestMask);
}

technique SpriteBatch
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
