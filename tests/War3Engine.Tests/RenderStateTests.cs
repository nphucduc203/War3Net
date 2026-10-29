using Microsoft.Xna.Framework.Graphics;
using War3Engine.Core.Models;
using War3Engine.Rendering;
using Xunit;

namespace War3Engine.Tests;

public class RenderStateTests
{
    [Fact]
    public void BlendModesMatchWarcraftSemantics()
    {
        // Values verified against open-realm renderer/r_draw.c and MDLX_SetLayerBlend.
        Assert.Equal(Blend.SourceAlpha, RenderStates.AlphaBlend.ColorSourceBlend);
        Assert.Equal(Blend.InverseSourceAlpha, RenderStates.AlphaBlend.ColorDestinationBlend);

        Assert.Equal(Blend.One, RenderStates.Additive.ColorSourceBlend);
        Assert.Equal(Blend.One, RenderStates.Additive.ColorDestinationBlend);

        Assert.Equal(Blend.SourceAlpha, RenderStates.AddAlpha.ColorSourceBlend);
        Assert.Equal(Blend.One, RenderStates.AddAlpha.ColorDestinationBlend);

        Assert.Equal(Blend.DestinationColor, RenderStates.Modulate.ColorSourceBlend);
        Assert.Equal(Blend.Zero, RenderStates.Modulate.ColorDestinationBlend);

        Assert.Equal(Blend.DestinationColor, RenderStates.Modulate2x.ColorSourceBlend);
        Assert.Equal(Blend.SourceColor, RenderStates.Modulate2x.ColorDestinationBlend);
    }

    [Theory]
    [InlineData(LayerFilterMode.None, false)]
    [InlineData(LayerFilterMode.Transparent, false)]
    [InlineData(LayerFilterMode.Blend, true)]
    [InlineData(LayerFilterMode.Additive, true)]
    [InlineData(LayerFilterMode.AddAlpha, true)]
    [InlineData(LayerFilterMode.Modulate, true)]
    [InlineData(LayerFilterMode.Modulate2x, true)]
    public void BlendedPassClassification(LayerFilterMode mode, bool blended)
    {
        Assert.Equal(blended, RenderStates.IsBlended(mode));
    }

    [Fact]
    public void AlphaKeyOnlyForTransparentFilterMode()
    {
        Assert.True(RenderStates.AlphaTestFor(new LayerData { FilterMode = LayerFilterMode.Transparent }));
        Assert.False(RenderStates.AlphaTestFor(new LayerData { FilterMode = LayerFilterMode.Blend }));
        Assert.False(RenderStates.AlphaTestFor(new LayerData { FilterMode = LayerFilterMode.None }));
    }

    [Fact]
    public void NoDepthTestFlagDisablesDepth()
    {
        var layer = new LayerData { FilterMode = LayerFilterMode.Blend, Flags = LayerFlags.NoDepthTest };
        Assert.Same(RenderStates.NoDepthTest, RenderStates.DepthFor(layer, true));

        var opaque = new LayerData { FilterMode = LayerFilterMode.None };
        Assert.Same(RenderStates.DepthWrite, RenderStates.DepthFor(opaque, true));

        var blended = new LayerData { FilterMode = LayerFilterMode.Blend };
        Assert.Same(RenderStates.DepthReadOnly, RenderStates.DepthFor(blended, true));
    }
}
