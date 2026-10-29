using Microsoft.Xna.Framework.Graphics;
using War3Engine.Core.Models;

namespace War3Engine.Rendering;

/// <summary>
/// Warcraft III layer blend modes and depth behavior as explicit GPU states.
/// Factor mapping follows the reference renderer (open-realm MDLX_SetLayerBlend).
/// </summary>
public static class RenderStates
{
    public static readonly BlendState Opaque = BlendState.Opaque;

    public static readonly BlendState AlphaBlend = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.InverseSourceAlpha,
    };

    public static readonly BlendState Additive = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
    };

    public static readonly BlendState AddAlpha = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
    };

    public static readonly BlendState Modulate = new()
    {
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        AlphaSourceBlend = Blend.DestinationAlpha,
        AlphaDestinationBlend = Blend.Zero,
    };

    public static readonly BlendState Modulate2x = new()
    {
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.SourceColor,
        AlphaSourceBlend = Blend.DestinationAlpha,
        AlphaDestinationBlend = Blend.SourceAlpha,
    };

    public static readonly DepthStencilState DepthWrite = new()
    {
        DepthBufferEnable = true,
        DepthBufferWriteEnable = true,
        DepthBufferFunction = CompareFunction.LessEqual,
    };

    public static readonly DepthStencilState DepthReadOnly = new()
    {
        DepthBufferEnable = true,
        DepthBufferWriteEnable = false,
        DepthBufferFunction = CompareFunction.LessEqual,
    };

    public static readonly DepthStencilState NoDepthTest = new()
    {
        DepthBufferEnable = false,
        DepthBufferWriteEnable = false,
    };

    public static readonly RasterizerState Cull = new()
    {
        CullMode = CullMode.CullCounterClockwiseFace,
    };

    public static readonly RasterizerState TwoSided = new()
    {
        CullMode = CullMode.None,
    };

    public static bool IsBlended(LayerFilterMode mode) => mode >= LayerFilterMode.Blend;

    public static BlendState BlendFor(LayerFilterMode mode) => mode switch
    {
        LayerFilterMode.Blend => AlphaBlend,
        LayerFilterMode.Additive => Additive,
        LayerFilterMode.AddAlpha => AddAlpha,
        LayerFilterMode.Modulate => Modulate,
        LayerFilterMode.Modulate2x => Modulate2x,
        _ => Opaque,
    };

    public static DepthStencilState DepthFor(LayerData layer, bool writeDepth) =>
        (layer.Flags & LayerFlags.NoDepthTest) != 0
            ? NoDepthTest
            : writeDepth && !IsBlended(layer.FilterMode) && (layer.Flags & LayerFlags.NoDepthSet) == 0
                ? DepthWrite
                : DepthReadOnly;

    public static RasterizerState RasterFor(LayerData layer) =>
        (layer.Flags & LayerFlags.TwoSided) != 0 ? TwoSided : Cull;

    public static bool AlphaTestFor(LayerData layer) => layer.FilterMode == LayerFilterMode.Transparent;
}
