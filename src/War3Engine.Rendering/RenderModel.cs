using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using War3Engine.Core;
using War3Engine.Core.Models;

namespace War3Engine.Rendering;

/// <summary>GPU resources for one MDX model: per-geoset buffers plus resolved textures.</summary>
public sealed class RenderModel : IDisposable
{
    private readonly List<Texture2D> _ownedTextures = new();

    public RenderModel(GraphicsDevice device, ModelData data, Func<TextureData, Texture2D?> textureLoader)
    {
        Data = data;
        Geosets = new GeosetBuffers[data.Geosets.Length];
        for (var i = 0; i < data.Geosets.Length; i++)
        {
            Geosets[i] = CreateGeoset(device, data.Geosets[i]);
        }

        Textures = new Texture2D?[data.Textures.Length];
        for (var i = 0; i < data.Textures.Length; i++)
        {
            var texture = textureLoader(data.Textures[i]);
            if (texture is null)
            {
                UnresolvedTextures.Add(string.IsNullOrEmpty(data.Textures[i].Path)
                    ? $"texture[{i}] replaceable={data.Textures[i].ReplaceableId}"
                    : data.Textures[i].Path);
                texture = CreateFallbackTexture(device);
            }
            else
            {
                _ownedTextures.Add(texture);
            }

            Textures[i] = texture;
        }
    }

    public ModelData Data { get; }

    public GeosetBuffers[] Geosets { get; }

    public Texture2D?[] Textures { get; }

    public List<string> UnresolvedTextures { get; } = new();

    public void Dispose()
    {
        foreach (var geoset in Geosets)
        {
            geoset.Vertices?.Dispose();
            geoset.Indices?.Dispose();
        }

        foreach (var texture in _ownedTextures)
        {
            texture.Dispose();
        }

        _ownedTextures.Clear();
    }

    public readonly struct GeosetBuffers
    {
        public GeosetBuffers(VertexBuffer? vertices, IndexBuffer? indices, int primitiveCount, int vertexCount)
        {
            Vertices = vertices;
            Indices = indices;
            PrimitiveCount = primitiveCount;
            VertexCount = vertexCount;
        }

        public VertexBuffer? Vertices { get; }

        public IndexBuffer? Indices { get; }

        public int PrimitiveCount { get; }

        public int VertexCount { get; }
    }

    private static GeosetBuffers CreateGeoset(GraphicsDevice device, GeosetData geoset)
    {
        if (geoset.Positions.Length == 0 || geoset.Indices.Length < 3)
        {
            return default;
        }

        var vertices = new SkinnedVertex[geoset.Positions.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new SkinnedVertex
            {
                Position = geoset.Positions[i],
                Normal = geoset.Normals[i],
                Uv0 = geoset.TextureCoordinateSets.Length > 0 && i < geoset.TextureCoordinateSets[0].Length
                    ? geoset.TextureCoordinateSets[0][i]
                    : Vector2.Zero,
                Uv1 = geoset.TextureCoordinateSets.Length > 1 && i < geoset.TextureCoordinateSets[1].Length
                    ? geoset.TextureCoordinateSets[1][i]
                    : Vector2.Zero,
                BoneIndices = geoset.BoneIndices[i],
                BoneWeights = geoset.BoneWeights[i],
            };
        }

        var vertexBuffer = new VertexBuffer(device, SkinnedVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
        vertexBuffer.SetData(vertices);

        var indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, geoset.Indices.Length, BufferUsage.WriteOnly);
        indexBuffer.SetData(geoset.Indices);

        return new GeosetBuffers(vertexBuffer, indexBuffer, geoset.Indices.Length / 3, vertices.Length);
    }

    private static Texture2D CreateFallbackTexture(GraphicsDevice device)
    {
        const int size = 64;
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                pixels[(y * size) + x] = ((x / 8) + (y / 8)) % 2 == 0
                    ? new Color(255, 0, 255)
                    : new Color(32, 32, 32);
            }
        }

        var texture = new Texture2D(device, size, size);
        texture.SetData(pixels);
        return texture;
    }
}
