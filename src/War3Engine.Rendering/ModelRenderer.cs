using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using War3Engine.Core.Animation;
using War3Engine.Core.Models;

namespace War3Engine.Rendering;

public sealed class RenderStats
{
    public int DrawCalls { get; set; }

    public int Triangles { get; set; }

    public int GeosetsDrawn { get; set; }

    public int LayersSkipped { get; set; }
}

/// <summary>
/// Draws MDX models with the engine's own effect, explicit blend/depth states and CPU skinning.
/// Rendering runs in two ordered passes per model: opaque layers, then blended layers.
/// </summary>
public sealed class ModelRenderer
{
    private readonly GraphicsDevice _device;
    private readonly Effect _effect;
    private readonly EffectParameter _pWorld;
    private readonly EffectParameter _pView;
    private readonly EffectParameter _pProjection;
    private readonly EffectParameter _pBones;
    private readonly EffectParameter _pLightDirection;
    private readonly EffectParameter _pAmbient;
    private readonly EffectParameter _pLayerColor;
    private readonly EffectParameter _pUnshaded;
    private readonly EffectParameter _pAlphaTest;
    private readonly EffectParameter _pUseUv1;
    private readonly EffectParameter _pTexture;
    private readonly Microsoft.Xna.Framework.Matrix[] _bonePalette = new Microsoft.Xna.Framework.Matrix[ModelInstance.MaxBones];

    public ModelRenderer(GraphicsDevice device, Effect effect)
    {
        _device = device;
        _effect = effect;
        _pWorld = effect.Parameters["World"] ?? throw new InvalidOperationException("Effect missing World");
        _pView = effect.Parameters["View"] ?? throw new InvalidOperationException("Effect missing View");
        _pProjection = effect.Parameters["Projection"] ?? throw new InvalidOperationException("Effect missing Projection");
        _pBones = effect.Parameters["Bones"] ?? throw new InvalidOperationException("Effect missing Bones");
        _pLightDirection = effect.Parameters["LightDirection"]!;
        _pAmbient = effect.Parameters["AmbientLight"]!;
        _pLayerColor = effect.Parameters["LayerColor"]!;
        _pUnshaded = effect.Parameters["Unshaded"]!;
        _pAlphaTest = effect.Parameters["AlphaTest"]!;
        _pUseUv1 = effect.Parameters["UseUv1"]!;
        _pTexture = effect.Parameters["Texture"]!;
    }

    public void Draw(
        RenderModel model,
        ModelInstance instance,
        Matrix4x4 view,
        Matrix4x4 projection,
        RenderStats? stats = null,
        ISet<int>? visibleGeosets = null)
    {
        var data = model.Data;
        instance.Evaluate();

        _effect.CurrentTechnique = _effect.Techniques[0];
        _pWorld.SetValue(ToXna(instance.World));
        _pView.SetValue(ToXna(view));
        _pProjection.SetValue(ToXna(projection));
        _pLightDirection?.SetValue(new Microsoft.Xna.Framework.Vector3(-0.35f, -0.8f, -0.5f));
        _pAmbient?.SetValue(0.55f);

        for (var i = 0; i < instance.Bones.Length && i < _bonePalette.Length; i++)
        {
            _bonePalette[i] = ToXna(instance.Bones[i]);
        }

        _pBones.SetValue(_bonePalette);

        for (var pass = 0; pass < 2; pass++)
        {
            var blendedPass = pass == 1;
            for (var g = 0; g < data.Geosets.Length; g++)
            {
                if (visibleGeosets is not null && !visibleGeosets.Contains(g))
                {
                    continue;
                }

                var geoset = data.Geosets[g];
                var buffers = model.Geosets[g];
                if (buffers.Vertices is null || geoset.MaterialId < 0 || geoset.MaterialId >= data.Materials.Length)
                {
                    continue;
                }

                var (geosetAlpha, geosetColor) = instance.GetGeosetState(g);
                if (geosetAlpha <= 0.01f)
                {
                    continue;
                }

                var material = data.Materials[geoset.MaterialId];
                var drewGeoset = false;
                foreach (var layer in material.Layers)
                {
                    if (RenderStates.IsBlended(layer.FilterMode) != blendedPass)
                    {
                        continue;
                    }

                    var textureIndex = layer.TextureId;
                    if (layer.TextureIds is { Keys.Length: > 0 } textureIds)
                    {
                        textureIndex = (int)TrackSampler.Sample(textureIds, instance.TrackFrame(textureIds));
                    }

                    if (textureIndex < 0 || textureIndex >= model.Textures.Length || model.Textures[textureIndex] is null)
                    {
                        if (stats is not null)
                        {
                            stats.LayersSkipped++;
                        }

                        continue;
                    }

                    var layerAlpha = layer.Alphas is { Keys.Length: > 0 } alphas
                        ? TrackSampler.Sample(alphas, instance.TrackFrame(alphas))
                        : layer.Alpha;

                    _pLayerColor.SetValue(new Microsoft.Xna.Framework.Vector4(
                        geosetColor.X,
                        geosetColor.Y,
                        geosetColor.Z,
                        Math.Clamp(layerAlpha * geosetAlpha, 0f, 1f)));
                    _pUnshaded.SetValue((layer.Flags & LayerFlags.Unshaded) != 0);
                    _pAlphaTest.SetValue(RenderStates.AlphaTestFor(layer));
                    _pUseUv1.SetValue(layer.CoordId == 1);
                    _pTexture.SetValue(model.Textures[textureIndex]);

                    _device.BlendState = RenderStates.BlendFor(layer.FilterMode);
                    _device.DepthStencilState = RenderStates.DepthFor(layer, writeDepth: true);
                    _device.RasterizerState = RenderStates.RasterFor(layer);
                    _device.SetVertexBuffer(buffers.Vertices);
                    _device.Indices = buffers.Indices;

                    _effect.CurrentTechnique.Passes[0].Apply();
                    _device.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        buffers.PrimitiveCount);

                    drewGeoset = true;
                    if (stats is not null)
                    {
                        stats.DrawCalls++;
                        stats.Triangles += buffers.PrimitiveCount;
                    }
                }

                if (drewGeoset && stats is not null)
                {
                    stats.GeosetsDrawn++;
                }
            }
        }

        _device.BlendState = BlendState.Opaque;
        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = RasterizerState.CullCounterClockwise;
    }

    private static Microsoft.Xna.Framework.Matrix ToXna(Matrix4x4 m) => new(
        m.M11, m.M12, m.M13, m.M14,
        m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34,
        m.M41, m.M42, m.M43, m.M44);
}
