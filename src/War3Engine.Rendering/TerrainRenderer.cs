using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using War3Engine.Core.Map;

namespace War3Engine.Rendering;

/// <summary>Draws the static terrain mesh with per-texture groups using the shared model effect.</summary>
public sealed class TerrainRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly Effect _effect;
    private readonly VertexBuffer _vertices;
    private readonly IndexBuffer _indices;
    private readonly Texture2D?[] _groupTextures;
    private readonly TerrainTextureGroup[] _groups;
    private readonly Microsoft.Xna.Framework.Matrix[] _identityBones = new Microsoft.Xna.Framework.Matrix[ModelInstance.MaxBones];
    private readonly EffectParameter _pWorld;
    private readonly EffectParameter _pView;
    private readonly EffectParameter _pProjection;
    private readonly EffectParameter _pBones;
    private readonly EffectParameter _pLayerColor;
    private readonly EffectParameter _pUnshaded;
    private readonly EffectParameter _pAlphaTest;
    private readonly EffectParameter _pUseUv1;
    private readonly EffectParameter _pTexture;
    private readonly EffectParameter? _pLightDirection;
    private readonly EffectParameter? _pAmbient;

    public TerrainRenderer(GraphicsDevice device, Effect effect, TerrainMeshData mesh, Func<string, Texture2D?> textureLoader)
    {
        _device = device;
        _effect = effect;
        _pWorld = effect.Parameters["World"] ?? throw new InvalidOperationException("Effect missing World");
        _pView = effect.Parameters["View"] ?? throw new InvalidOperationException("Effect missing View");
        _pProjection = effect.Parameters["Projection"] ?? throw new InvalidOperationException("Effect missing Projection");
        _pBones = effect.Parameters["Bones"] ?? throw new InvalidOperationException("Effect missing Bones");
        _pLayerColor = effect.Parameters["LayerColor"]!;
        _pUnshaded = effect.Parameters["Unshaded"]!;
        _pAlphaTest = effect.Parameters["AlphaTest"]!;
        _pUseUv1 = effect.Parameters["UseUv1"]!;
        _pTexture = effect.Parameters["Texture"]!;
        _pLightDirection = effect.Parameters["LightDirection"];
        _pAmbient = effect.Parameters["AmbientLight"];

        var vertices = new SkinnedVertex[mesh.Positions.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new SkinnedVertex
            {
                Position = mesh.Positions[i],
                Normal = mesh.Normals[i],
                Uv0 = mesh.Uvs[i],
                Uv1 = mesh.Uvs[i],
                BoneIndices = Vector4.Zero,
                BoneWeights = new Vector4(1f, 0f, 0f, 0f),
            };
        }

        _vertices = new VertexBuffer(device, SkinnedVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
        _vertices.SetData(vertices);

        var indices = new int[mesh.Indices.Length];
        for (var i = 0; i < indices.Length; i++)
        {
            indices[i] = mesh.Indices[i];
        }

        _indices = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
        _indices.SetData(indices);

        _groups = mesh.TextureGroups;
        _groupTextures = new Texture2D?[_groups.Length];
        for (var i = 0; i < _groups.Length; i++)
        {
            _groupTextures[i] = string.IsNullOrEmpty(_groups[i].TexturePath) ? null : textureLoader(_groups[i].TexturePath);
        }

        for (var i = 0; i < _identityBones.Length; i++)
        {
            _identityBones[i] = Microsoft.Xna.Framework.Matrix.Identity;
        }

        TriangleCount = indices.Length / 3;
        MissingTextures = _groupTextures.Count(t => t is null);
    }

    public int TriangleCount { get; }

    public int MissingTextures { get; }

    public void Draw(Matrix4x4 view, Matrix4x4 projection)
    {
        _effect.CurrentTechnique = _effect.Techniques[0];
        _pWorld.SetValue(Microsoft.Xna.Framework.Matrix.Identity);
        _pView.SetValue(ToXna(view));
        _pProjection.SetValue(ToXna(projection));
        _pBones.SetValue(_identityBones);
        _pLayerColor.SetValue(Microsoft.Xna.Framework.Vector4.One);
        _pUnshaded.SetValue(false);
        _pAlphaTest.SetValue(false);
        _pUseUv1.SetValue(false);
        _pLightDirection?.SetValue(new Microsoft.Xna.Framework.Vector3(-0.35f, -0.8f, -0.5f));
        _pAmbient?.SetValue(0.55f);

        _device.RasterizerState = RasterizerState.CullNone;
        _device.BlendState = BlendState.Opaque;
        _device.DepthStencilState = RenderStates.DepthWrite;
        _device.SetVertexBuffer(_vertices);
        _device.Indices = _indices;

        for (var i = 0; i < _groups.Length; i++)
        {
            var texture = _groupTextures[i];
            if (texture is null || _groups[i].IndexCount == 0)
            {
                continue;
            }

            _pTexture.SetValue(texture);
            _effect.CurrentTechnique.Passes[0].Apply();
            // 4-arg overload: (type, baseVertex, startIndex, primitiveCount).
            _device.DrawIndexedPrimitives(
                PrimitiveType.TriangleList,
                0,
                _groups[i].StartIndex,
                _groups[i].IndexCount / 3);
        }

        _device.RasterizerState = RasterizerState.CullCounterClockwise;
    }

    public void Dispose()
    {
        _vertices.Dispose();
        _indices.Dispose();
    }

    private static Microsoft.Xna.Framework.Matrix ToXna(Matrix4x4 m) => new(
        m.M11, m.M12, m.M13, m.M14,
        m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34,
        m.M41, m.M42, m.M43, m.M44);
}
