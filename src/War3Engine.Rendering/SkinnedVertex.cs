using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace War3Engine.Rendering;

public struct SkinnedVertex : IVertexType
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 Uv0;
    public Vector2 Uv1;
    public Vector4 BoneIndices;
    public Vector4 BoneWeights;

    public static readonly VertexDeclaration Declaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(32, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1),
        new VertexElement(40, VertexElementFormat.Vector4, VertexElementUsage.BlendIndices, 0),
        new VertexElement(56, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0));

    VertexDeclaration IVertexType.VertexDeclaration => Declaration;
}
