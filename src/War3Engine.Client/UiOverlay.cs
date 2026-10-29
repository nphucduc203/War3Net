using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace War3Engine.Client;

/// <summary>Minimal immediate-mode overlay: info panel plus a clickable animation list.</summary>
public sealed class UiOverlay : IDisposable
{
    private readonly SpriteBatch _batch;
    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private MouseState _previousMouse;

    public UiOverlay(GraphicsDevice device, SpriteFont font)
    {
        _batch = new SpriteBatch(device);
        _font = font;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _previousMouse = Mouse.GetState();
    }

    public sealed class ModelInfo
    {
        public required string Title { get; init; }

        public required string Subtitle { get; init; }

        public required IReadOnlyList<string> Animations { get; init; }

        public int SelectedAnimation { get; init; }

        public float TimeMs { get; init; }

        public bool Paused { get; init; }

        public int DrawCalls { get; init; }

        public int Triangles { get; init; }
    }

    public void DrawModel(ModelInfo info, MouseState mouse)
    {
        const int x = 16;
        const int y = 16;
        var width = 380;
        var shown = Math.Min(info.Animations.Count, 14);
        var height = 12 + (22 * 4) + 12 + (shown * 22) + (info.Animations.Count > 14 ? 20 : 0) + 30;
        DrawPanel(new Rectangle(x, y, width, height));

        var line = y + 12;
        DrawText(info.Title, x + 12, line, Color.Gold);
        line += 22;
        DrawText(info.Subtitle, x + 12, line, Color.LightGray);
        line += 22;
        DrawText($"frame {info.TimeMs:F0} ms  {(info.Paused ? "PAUSED" : "playing")}", x + 12, line, Color.White);
        line += 20;
        DrawText($"draws {info.DrawCalls}  tris {info.Triangles}", x + 12, line, Color.LightGray);
        line += 24;

        var clicked = -1;
        var leftClick = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        for (var i = 0; i < shown; i++)
        {
            var row = new Rectangle(x + 8, line - 2, width - 16, 20);
            var hover = row.Contains(mouse.Position);
            if (i == info.SelectedAnimation)
            {
                Fill(row, new Color(70, 110, 170));
            }
            else if (hover)
            {
                Fill(row, new Color(60, 60, 70));
            }

            DrawText(info.Animations[i], x + 14, line, i == info.SelectedAnimation ? Color.White : Color.LightGray);
            if (leftClick && hover)
            {
                clicked = i;
            }

            line += 22;
        }

        if (info.Animations.Count > 14)
        {
            DrawText($"... {info.Animations.Count - 14} more [ / ]", x + 14, line, Color.Gray);
        }

        DrawText("drag: orbit   wheel: zoom   space: pause   [ ]: anim   esc: exit", x + 12, y + height - 20, Color.Gray);
        if (clicked >= 0)
        {
            PendingAnimationSelection = clicked;
        }

        _previousMouse = mouse;
    }

    public void DrawMap(string title, IReadOnlyList<string> lines)
    {
        const int x = 16;
        const int y = 16;
        var width = 460;
        var height = 56 + (lines.Count * 20);
        DrawPanel(new Rectangle(x, y, width, height));
        var line = y + 12;
        DrawText(title, x + 12, line, Color.Gold);
        line += 24;
        foreach (var text in lines)
        {
            DrawText(text, x + 12, line, Color.LightGray);
            line += 20;
        }

        DrawText("drag: orbit   wheel: zoom   esc: exit", x + 12, y + height - 20, Color.Gray);
    }

    public int? PendingAnimationSelection { get; private set; }

    public void ClearSelection() => PendingAnimationSelection = null;

    public void Dispose()
    {
        _batch.Dispose();
        _pixel.Dispose();
    }

    private void DrawPanel(Rectangle rect) => Fill(rect, new Color(0, 0, 0, 190));

    private void Fill(Rectangle rect, Color color)
    {
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_pixel, rect, color);
        _batch.End();
    }

    private void DrawText(string text, int x, int y, Color color)
    {
        _batch.Begin(samplerState: SamplerState.LinearClamp);
        _batch.DrawString(_font, text, new Vector2(x, y), color);
        _batch.End();
    }
}
