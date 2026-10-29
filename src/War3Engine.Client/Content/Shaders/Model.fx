#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_4_0
#define PS_SHADERMODEL ps_4_0
#else
#define VS_SHADERMODEL vs_4_0
#define PS_SHADERMODEL ps_5_0
#endif

float4x4 World;
float4x4 View;
float4x4 Projection;
float4x4 Bones[128];
float3 LightDirection;
float AmbientLight;
float4 LayerColor;
bool Unshaded;
bool AlphaTest;
bool UseUv1;

Texture2D Texture;
SamplerState TextureSampler
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Point;
    AddressU = Wrap;
    AddressV = Wrap;
};

struct VSInput
{
    float3 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 Uv0 : TEXCOORD0;
    float2 Uv1 : TEXCOORD1;
    float4 BoneIndices : BLENDINDICES0;
    float4 BoneWeights : BLENDWEIGHT0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
    float3 Normal : TEXCOORD1;
};

VSOutput VS(VSInput input)
{
    VSOutput output;
    float4x4 skin =
          Bones[(int)input.BoneIndices.x] * input.BoneWeights.x
        + Bones[(int)input.BoneIndices.y] * input.BoneWeights.y
        + Bones[(int)input.BoneIndices.z] * input.BoneWeights.z
        + Bones[(int)input.BoneIndices.w] * input.BoneWeights.w;

    float4 position = mul(float4(input.Position, 1.0f), skin);
    position = mul(position, World);
    output.Position = mul(mul(position, View), Projection);

    float3 normal = normalize(mul(input.Normal, (float3x3)skin));
    output.Normal = normalize(mul(normal, (float3x3)World));
    output.Uv = UseUv1 ? input.Uv1 : input.Uv0;
    return output;
}

float4 PS(VSOutput input) : SV_TARGET
{
    float4 texel = Texture.Sample(TextureSampler, input.Uv);
    if (AlphaTest)
    {
        clip(texel.a - 0.75f);
    }

    float light = 1.0f;
    if (!Unshaded)
    {
        float ndl = saturate(dot(normalize(input.Normal), -LightDirection));
        light = AmbientLight + ((1.0f - AmbientLight) * ndl);
    }

    return float4(texel.rgb * LayerColor.rgb * light, texel.a * LayerColor.a);
}

technique ModelTechnique
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL VS();
        PixelShader = compile PS_SHADERMODEL PS();
    }
};
