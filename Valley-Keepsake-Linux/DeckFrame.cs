using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TradutorModsStardew;
using DrawingColor = System.Drawing.Color;

namespace TradutorModsDeck;

internal sealed class DeckFrame : Control
{
    private AppTheme theme = ThemeStore.Current;
    private static readonly Lazy<Bitmap?> JunimoNote = new(() =>
    {
        var path = DeckAssets.PathFor("Junimo-Note.png");
        return File.Exists(path) ? new Bitmap(path) : null;
    });
    private static readonly Lazy<Bitmap?> DialogueBox = new(() =>
    {
        var path = DeckAssets.PathFor("DialogueBoxBorder.png");
        return File.Exists(path) ? new Bitmap(path) : null;
    });
    private static readonly Lazy<Bitmap?> SlimeHutch = new(() =>
    {
        var path = DeckAssets.PathFor("SlimeHutchBorder.png");
        return File.Exists(path) ? new Bitmap(path) : null;
    });
    private static readonly Lazy<Bitmap?> GardenBackground = new(() =>
    {
        var path = DeckAssets.PathFor("GardenBackground.png");
        return File.Exists(path) ? new Bitmap(path) : null;
    });
    private static readonly Lazy<Bitmap?> GardenFrame = new(() =>
    {
        var path = DeckAssets.PathFor("GardenFrame.png");
        return File.Exists(path) ? new Bitmap(path) : null;
    });

    public bool BackgroundOnly { get; init; }

    public DeckFrame() => IsHitTestVisible = false;

    public void ApplyTheme(AppTheme value) { theme = value; InvalidateVisual(); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        double w = Bounds.Width, h = Bounds.Height;
        if (w < 30 || h < 30) return;
        if (theme.Kind == ThemeKind.Garden)
        {
            if (BackgroundOnly && GardenBackground.Value is { } background)
            {
                using var options = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None });
                context.DrawImage(background,
                    new Rect(0, 0, background.PixelSize.Width, background.PixelSize.Height), new Rect(0, 0, w, h));
            }
            if (GardenFrame.Value is { } garden)
                DrawArtworkFrame(context, garden, w, h, BackgroundOnly, 64, 72, false);
            return;
        }
        if (theme.Kind == ThemeKind.Parchment && JunimoNote.Value is { } note)
        {
            DrawArtworkFrame(context, note, w, h, BackgroundOnly, 90, 72, true);
            return;
        }
        if (theme.Kind == ThemeKind.Wood && DialogueBox.Value is { } dialogue)
        {
            DrawArtworkFrame(context, dialogue, w, h, BackgroundOnly, 56, 56, false);
            return;
        }
        if (theme.Kind == ThemeKind.Mine && SlimeHutch.Value is { } slimeBorder)
        {
            if (!BackgroundOnly)
                DrawArtworkFrame(context, slimeBorder, w, h, false, 52, 52, false);
            return;
        }
        if (BackgroundOnly) return;
        var dark = DeckTheme.Brush(theme.BorderDark);
        var light = DeckTheme.Brush(theme.BorderLight);
        var fill = DeckTheme.Brush(theme.Kind == ThemeKind.Mine
            ? DrawingColor.FromArgb(53, 49, 86) : theme.Kind == ThemeKind.Wood
                ? DrawingColor.FromArgb(151, 73, 27) : theme.BorderDark);

        if (theme.Kind == ThemeKind.Mine)
        {
            // Um único bloco em cada canto impede a sobreposição das peças.
            const int edge = 12, tile = 36;
            context.FillRectangle(dark, new Rect(0, 0, w, edge));
            context.FillRectangle(dark, new Rect(0, h - edge, w, edge));
            context.FillRectangle(dark, new Rect(0, edge, edge, h - 2 * edge));
            context.FillRectangle(dark, new Rect(w - edge, edge, edge, h - 2 * edge));
            for (double x = edge; x < w - edge; x += tile)
            {
                var width = Math.Min(tile - 2, w - edge - x);
                context.FillRectangle(fill, new Rect(x, 2, width, edge - 4));
                context.FillRectangle(fill, new Rect(x, h - edge + 2, width, edge - 4));
                context.FillRectangle(light, new Rect(x + 2, 3, Math.Max(0, width - 4), 2));
                context.FillRectangle(light, new Rect(x + 2, h - edge + 3, Math.Max(0, width - 4), 2));
            }
            for (double y = edge; y < h - edge; y += tile)
            {
                var height = Math.Min(tile - 2, h - edge - y);
                context.FillRectangle(fill, new Rect(2, y, edge - 4, height));
                context.FillRectangle(fill, new Rect(w - edge + 2, y, edge - 4, height));
                context.FillRectangle(light, new Rect(3, y + 2, 2, Math.Max(0, height - 4)));
                context.FillRectangle(light, new Rect(w - edge + 3, y + 2, 2, Math.Max(0, height - 4)));
            }
            foreach (var x in new[] { 2.0, w - edge + 2 })
                foreach (var y in new[] { 2.0, h - edge + 2 })
                    context.FillRectangle(fill, new Rect(x, y, edge - 4, edge - 4));
        }
        else
        {
            var edge = theme.Kind == ThemeKind.Classic ? 3 : 7;
            context.FillRectangle(dark, new Rect(0, 0, w, edge));
            context.FillRectangle(dark, new Rect(0, h - edge, w, edge));
            context.FillRectangle(dark, new Rect(0, edge, edge, h - 2 * edge));
            context.FillRectangle(dark, new Rect(w - edge, edge, edge, h - 2 * edge));
            if (edge > 3)
            {
                context.FillRectangle(fill, new Rect(2, 2, w - 4, 3));
                context.FillRectangle(fill, new Rect(2, h - 5, w - 4, 3));
                context.FillRectangle(fill, new Rect(2, 5, 3, h - 10));
                context.FillRectangle(fill, new Rect(w - 5, 5, 3, h - 10));
                context.FillRectangle(light, new Rect(5, 5, w - 10, 1));
                context.FillRectangle(light, new Rect(5, 5, 1, h - 10));
            }
        }
    }

    private static void DrawArtworkFrame(DrawingContext context, Bitmap image, double width, double height,
        bool backgroundOnly, double sourceEdge, double targetEdge, bool preserveCenterAspect)
    {
        double sw = image.PixelSize.Width, sh = image.PixelSize.Height;
        if (width <= targetEdge * 2 || height <= targetEdge * 2) return;
        double cw = width - targetEdge * 2, ch = height - targetEdge * 2;
        void Slice(Rect source, Rect destination) => context.DrawImage(image, source, destination);
        using var options = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None });
        if (backgroundOnly)
        {
            double sx = sourceEdge, sy = sourceEdge, sourceWidth = sw - sourceEdge * 2, sourceHeight = sh - sourceEdge * 2;
            double ratio = cw / ch;
            if (preserveCenterAspect && sourceWidth / sourceHeight > ratio)
            {
                double trimmed = sourceHeight * ratio;
                sx += (sourceWidth - trimmed) / 2;
                sourceWidth = trimmed;
            }
            else if (preserveCenterAspect)
            {
                double trimmed = sourceWidth / ratio;
                sy += (sourceHeight - trimmed) / 2;
                sourceHeight = trimmed;
            }
            Slice(new Rect(sx, sy, sourceWidth, sourceHeight), new Rect(targetEdge, targetEdge, cw, ch));
            return;
        }
        Slice(new Rect(0, 0, sourceEdge, sourceEdge), new Rect(0, 0, targetEdge, targetEdge));
        Slice(new Rect(sw - sourceEdge, 0, sourceEdge, sourceEdge), new Rect(width - targetEdge, 0, targetEdge, targetEdge));
        Slice(new Rect(0, sh - sourceEdge, sourceEdge, sourceEdge), new Rect(0, height - targetEdge, targetEdge, targetEdge));
        Slice(new Rect(sw - sourceEdge, sh - sourceEdge, sourceEdge, sourceEdge), new Rect(width - targetEdge, height - targetEdge, targetEdge, targetEdge));
        Slice(new Rect(sourceEdge, 0, sw - sourceEdge * 2, sourceEdge), new Rect(targetEdge, 0, cw, targetEdge));
        Slice(new Rect(sourceEdge, sh - sourceEdge, sw - sourceEdge * 2, sourceEdge), new Rect(targetEdge, height - targetEdge, cw, targetEdge));
        Slice(new Rect(0, sourceEdge, sourceEdge, sh - sourceEdge * 2), new Rect(0, targetEdge, targetEdge, ch));
        Slice(new Rect(sw - sourceEdge, sourceEdge, sourceEdge, sh - sourceEdge * 2), new Rect(width - targetEdge, targetEdge, targetEdge, ch));
    }
}
