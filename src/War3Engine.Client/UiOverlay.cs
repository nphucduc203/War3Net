using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using War3Engine.Scripting;

namespace War3Engine.Client;

/// <summary>Minimal immediate-mode overlay: info panel, animation list and runtime dialogs.</summary>
public sealed class UiOverlay : IDisposable
{
    private readonly SpriteBatch _batch;
    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private MouseState _previousMouse;
    private readonly List<(Rectangle Bounds, DialogButtonState Button)> _dialogButtons = new();
    private DialogState? _dialog;

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

    public void DrawDialog(JassGameRuntime? runtime, MouseState mouse, int width, int height)
    {
        _dialogButtons.Clear();
        _dialog = null;
        var dialog = runtime?.Dialogs.LastOrDefault(d => d.Visible && !d.Destroyed && d.PlayerId == 0);
        if (dialog is null)
        {
            return;
        }

        const int panelWidth = 520;
        var panelHeight = 92 + (dialog.Buttons.Count * 42);
        var x = Math.Max(16, (width - panelWidth) / 2);
        var y = Math.Max(16, (height - panelHeight) / 2);
        DrawPanel(new Rectangle(x, y, panelWidth, panelHeight));
        DrawText(dialog.Message, x + 18, y + 18, Color.Gold);
        var line = y + 64;
        foreach (var button in dialog.Buttons)
        {
            var bounds = new Rectangle(x + 18, line, panelWidth - 36, 32);
            Fill(bounds, bounds.Contains(mouse.Position) ? new Color(80, 120, 180) : new Color(40, 46, 58));
            DrawText(button.Label, bounds.X + 12, bounds.Y + 7, Color.White);
            _dialogButtons.Add((bounds, button));
            line += 42;
        }

        _dialog = dialog;
    }

    public void DrawSelection(Point center, string label)
    {
        const int half = 34;
        const int length = 12;
        const int thickness = 3;
        var color = Color.LimeGreen;
        var left = center.X - half;
        var right = center.X + half;
        var top = center.Y - half;
        var bottom = center.Y + half;
        Fill(new Rectangle(left, top, length, thickness), color);
        Fill(new Rectangle(left, top, thickness, length), color);
        Fill(new Rectangle(right - length, top, length, thickness), color);
        Fill(new Rectangle(right - thickness, top, thickness, length), color);
        Fill(new Rectangle(left, bottom - thickness, length, thickness), color);
        Fill(new Rectangle(left, bottom - length, thickness, length), color);
        Fill(new Rectangle(right - length, bottom - thickness, length, thickness), color);
        Fill(new Rectangle(right - thickness, bottom - length, thickness, length), color);
        var size = _font.MeasureString(Sanitize(label));
        var x = center.X - ((int)size.X / 2);
        Fill(new Rectangle(x - 4, center.Y + half + 4, (int)size.X + 8, 20), new Color(0, 0, 0, 190));
        DrawText(label, x, center.Y + half + 6, Color.Gold);
    }

    public bool TryHitDialog(Point position, out DialogState dialog, out DialogButtonState button)
    {
        foreach (var (bounds, candidate) in _dialogButtons)
        {
            if (bounds.Contains(position) && _dialog is not null)
            {
                dialog = _dialog;
                button = candidate;
                return true;
            }
        }

        dialog = null!;
        button = null!;
        return false;
    }

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
        _batch.DrawString(_font, Sanitize(text), new Vector2(x, y), color);
        _batch.End();
    }

    /// <summary>UI font covers ASCII, Latin-1/Extended-A and Vietnamese; map text may use CJK.</summary>
    private static string Sanitize(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(IsRenderable(c) ? c : Fallback(c));
        }

        return builder.ToString();
    }

    private static bool IsRenderable(char c) =>
        c is >= (char)32 and <= (char)126 ||
        c is >= (char)0x00C0 and <= (char)0x017F ||
        c is >= (char)0x01A0 and <= (char)0x01B0 ||
        c is >= (char)0x1EA0 and <= (char)0x1EF9;

    private static char Fallback(char c) => c switch
    {
        '，' or '、' => ',',
        '。' => '.',
        '！' => '!',
        '？' => '?',
        '：' => ':',
        '；' => ';',
        '（' => '(',
        '）' => ')',
        '“' or '”' => '"',
        '\u2018' or '\u2019' => '\'',
        '—' => '-',
        '…' => '.',
        '\u00A0' => ' ',
        _ => '?',
    };
}
