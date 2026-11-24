using System.Drawing.Drawing2D;

namespace TradutorModsStardew;

internal static class ThemeFramePainter
{
    private static readonly Lazy<Image?> JunimoNote = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Junimo-Note.png");
        return File.Exists(path) ? Image.FromFile(path) : null;
    });
    private static readonly Lazy<Image?> DialogueBox = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "DialogueBoxBorder.png");
        return File.Exists(path) ? Image.FromFile(path) : null;
    });
    private static readonly Lazy<Image?> SlimeHutch = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "SlimeHutchBorder.png");
        return File.Exists(path) ? Image.FromFile(path) : null;
    });
    private static readonly Lazy<Image?> GardenBackground = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "GardenBackground.png");
        return File.Exists(path) ? Image.FromFile(path) : null;
    });
    private static readonly Lazy<Image?> GardenFrame = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "GardenFrame.png");
        return File.Exists(path) ? Image.FromFile(path) : null;
    });

    public static void DrawFrame(Graphics graphics, Rectangle bounds, AppTheme theme, bool outer = false,
        Action<Graphics>? drawBehindGardenFrame = null)
    {
        if (theme.Kind == ThemeKind.Classic || bounds.Width < 24 || bounds.Height < 24) return;
        graphics.SmoothingMode = SmoothingMode.None;
        if (outer && theme.Kind == ThemeKind.Parchment &&
            DrawArtFrame(graphics, bounds, JunimoNote.Value, 90, 72, preserveCenterAspect: true)) return;
        if (outer && theme.Kind == ThemeKind.Wood &&
            DrawArtFrame(graphics, bounds, DialogueBox.Value, 56, 56, preserveCenterAspect: false)) return;
        if (outer && theme.Kind == ThemeKind.Mine &&
            DrawArtFrame(graphics, bounds, SlimeHutch.Value, 52, 52, preserveCenterAspect: false, borderOnly: true)) return;
        if (outer && theme.Kind == ThemeKind.Garden)
        {
            if (GardenBackground.Value is { } background)
            {
                var state = graphics.Save();
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(background, bounds, new Rectangle(0, 0, background.Width, background.Height), GraphicsUnit.Pixel);
                graphics.Restore(state);
            }
            drawBehindGardenFrame?.Invoke(graphics);
            if (DrawArtFrame(graphics, bounds, GardenFrame.Value, 64, 72, preserveCenterAspect: false)) return;
        }
        if (theme.Kind == ThemeKind.Mine)
        {
            if (outer) DrawStoneFrame(graphics, bounds, theme);
            else DrawInsetFrame(graphics, bounds, theme, 4);
            return;
        }
        if (theme.Kind == ThemeKind.Wood)
        {
            if (outer) DrawWoodFrame(graphics, bounds, theme);
            else DrawWoodInset(graphics, bounds, theme);
            return;
        }
        int thickness = outer ? 10 : 5;
        using var dark = new SolidBrush(theme.BorderDark);
        using var light = new SolidBrush(theme.BorderLight);
        using var accent = new SolidBrush(AppTheme.Blend(theme.BorderDark, theme.BorderLight, .48));
        Border(graphics, dark, bounds, thickness);
        Border(graphics, light, Rectangle.Inflate(bounds, -3, -3), 2);
        Border(graphics, accent, Rectangle.Inflate(bounds, -thickness, -thickness), 1);

        switch (theme.Kind)
        {
            case ThemeKind.Parchment:
                graphics.FillRectangle(light, bounds.X + 3, bounds.Y + 3, 12, 4);
                graphics.FillRectangle(light, bounds.Right - 15, bounds.Bottom - 7, 12, 4);
                break;
        }
    }

    private static bool DrawArtFrame(Graphics graphics, Rectangle bounds, Image? image,
        int sourceEdge, int targetEdge, bool preserveCenterAspect, bool borderOnly = false)
    {
        if (image is null || bounds.Width < targetEdge * 2 || bounds.Height < targetEdge * 2) return false;
        int sw = image.Width, sh = image.Height;
        int dw = bounds.Width, dh = bounds.Height;
        int centerWidth = dw - targetEdge * 2, centerHeight = dh - targetEdge * 2;
        var state = graphics.Save();
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        void Slice(Rectangle destination, Rectangle source) =>
            graphics.DrawImage(image, destination, source, GraphicsUnit.Pixel);

        var center = new Rectangle(sourceEdge, sourceEdge, sw - sourceEdge * 2, sh - sourceEdge * 2);
        if (preserveCenterAspect)
        {
            // Crop the note's paper center instead of changing the tree's proportions.
            double destinationRatio = (double)centerWidth / centerHeight;
            if ((double)center.Width / center.Height > destinationRatio)
            {
                int width = Math.Max(1, (int)Math.Round(center.Height * destinationRatio));
                center = new Rectangle(center.X + (center.Width - width) / 2, center.Y, width, center.Height);
            }
            else
            {
                int height = Math.Max(1, (int)Math.Round(center.Width / destinationRatio));
                center = new Rectangle(center.X, center.Y + (center.Height - height) / 2, center.Width, height);
            }
        }
        if (!borderOnly)
            Slice(new Rectangle(bounds.X + targetEdge, bounds.Y + targetEdge, centerWidth, centerHeight), center);
        Slice(new Rectangle(bounds.X, bounds.Y, targetEdge, targetEdge), new Rectangle(0, 0, sourceEdge, sourceEdge));
        Slice(new Rectangle(bounds.Right - targetEdge, bounds.Y, targetEdge, targetEdge), new Rectangle(sw - sourceEdge, 0, sourceEdge, sourceEdge));
        Slice(new Rectangle(bounds.X, bounds.Bottom - targetEdge, targetEdge, targetEdge), new Rectangle(0, sh - sourceEdge, sourceEdge, sourceEdge));
        Slice(new Rectangle(bounds.Right - targetEdge, bounds.Bottom - targetEdge, targetEdge, targetEdge), new Rectangle(sw - sourceEdge, sh - sourceEdge, sourceEdge, sourceEdge));
        Slice(new Rectangle(bounds.X + targetEdge, bounds.Y, centerWidth, targetEdge), new Rectangle(sourceEdge, 0, sw - sourceEdge * 2, sourceEdge));
        Slice(new Rectangle(bounds.X + targetEdge, bounds.Bottom - targetEdge, centerWidth, targetEdge), new Rectangle(sourceEdge, sh - sourceEdge, sw - sourceEdge * 2, sourceEdge));
        Slice(new Rectangle(bounds.X, bounds.Y + targetEdge, targetEdge, centerHeight), new Rectangle(0, sourceEdge, sourceEdge, sh - sourceEdge * 2));
        Slice(new Rectangle(bounds.Right - targetEdge, bounds.Y + targetEdge, targetEdge, centerHeight), new Rectangle(sw - sourceEdge, sourceEdge, sourceEdge, sh - sourceEdge * 2));
        graphics.Restore(state);
        return true;
    }

    public static void DrawFieldFrame(Graphics graphics, Rectangle bounds, AppTheme theme)
    {
        if (theme.Kind == ThemeKind.Mine && bounds.Width > 12 && bounds.Height > 12)
            DrawInsetFrame(graphics, bounds, theme, 3);
    }

    public static void DrawGridFrame(Graphics graphics, Rectangle bounds, AppTheme theme)
    {
        if (theme.Kind != ThemeKind.Mine)
        {
            DrawFrame(graphics, bounds, theme);
            return;
        }
        if (bounds.Width < 24 || bounds.Height < 24) return;
        // The panel above already supplies the dividing line. Repeat only the sides and bottom.
        using var outline = new SolidBrush(theme.BorderDark);
        using var highlight = new SolidBrush(theme.BorderLight);
        graphics.FillRectangle(outline, bounds.X, bounds.Y, 4, bounds.Height);
        graphics.FillRectangle(outline, bounds.Right - 4, bounds.Y, 4, bounds.Height);
        graphics.FillRectangle(outline, bounds.X, bounds.Bottom - 4, bounds.Width, 4);
        graphics.FillRectangle(highlight, bounds.X + 2, bounds.Y, 1, bounds.Height - 3);
        graphics.FillRectangle(highlight, bounds.Right - 3, bounds.Y, 1, bounds.Height - 3);
        graphics.FillRectangle(highlight, bounds.X + 3, bounds.Bottom - 3, bounds.Width - 6, 1);
    }

    private static void Border(Graphics graphics, Brush brush, Rectangle bounds, int size)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        graphics.FillRectangle(brush, bounds.X, bounds.Y, bounds.Width, size);
        graphics.FillRectangle(brush, bounds.X, bounds.Bottom - size, bounds.Width, size);
        graphics.FillRectangle(brush, bounds.X, bounds.Y, size, bounds.Height);
        graphics.FillRectangle(brush, bounds.Right - size, bounds.Y, size, bounds.Height);
    }

    private static void DrawWoodFrame(Graphics graphics, Rectangle bounds, AppTheme theme)
    {
        using var outline = new SolidBrush(Color.FromArgb(72, 31, 14));
        using var shade = new SolidBrush(Color.FromArgb(121, 58, 25));
        using var face = new SolidBrush(Color.FromArgb(183, 98, 43));
        using var highlight = new SolidBrush(Color.FromArgb(242, 164, 83));
        using var inner = new SolidBrush(theme.BorderDark);
        Border(graphics, outline, bounds, 17);
        Border(graphics, shade, Rectangle.Inflate(bounds, -2, -2), 13);
        Border(graphics, face, Rectangle.Inflate(bounds, -5, -5), 9);
        Border(graphics, highlight, Rectangle.Inflate(bounds, -6, -6), 2);
        Border(graphics, inner, Rectangle.Inflate(bounds, -13, -13), 2);
        // Offset corner pixels echo the stepped edges of Stardew dialogue windows.
        graphics.FillRectangle(outline, bounds.X, bounds.Y, 4, 4);
        graphics.FillRectangle(outline, bounds.Right - 4, bounds.Y, 4, 4);
        graphics.FillRectangle(outline, bounds.X, bounds.Bottom - 4, 4, 4);
        graphics.FillRectangle(outline, bounds.Right - 4, bounds.Bottom - 4, 4, 4);
    }

    private static void DrawWoodInset(Graphics graphics, Rectangle bounds, AppTheme theme)
    {
        using var outline = new SolidBrush(theme.BorderDark);
        using var light = new SolidBrush(theme.BorderLight);
        Border(graphics, outline, bounds, 2);
        Border(graphics, light, Rectangle.Inflate(bounds, -2, -2), 1);
    }

    private static void DrawInsetFrame(Graphics graphics, Rectangle bounds, AppTheme theme, int depth)
    {
        using var outline = new SolidBrush(theme.BorderDark);
        using var middle = new SolidBrush(AppTheme.Blend(theme.BorderDark, theme.BorderLight, .45));
        using var highlight = new SolidBrush(theme.BorderLight);
        Border(graphics, outline, bounds, depth);
        Border(graphics, middle, Rectangle.Inflate(bounds, -1, -1), Math.Max(1, depth - 2));
        graphics.FillRectangle(highlight, bounds.X + 3, bounds.Y + 2, bounds.Width - 6, 1);
        graphics.FillRectangle(highlight, bounds.X + 2, bounds.Y + 3, 1, bounds.Height - 6);
        graphics.FillRectangle(outline, bounds.X + 3, bounds.Bottom - 3, bounds.Width - 6, 1);
        graphics.FillRectangle(outline, bounds.Right - 3, bounds.Y + 3, 1, bounds.Height - 6);
        graphics.FillRectangle(outline, bounds.X, bounds.Y, 2, 2);
        graphics.FillRectangle(outline, bounds.Right - 2, bounds.Y, 2, 2);
        graphics.FillRectangle(outline, bounds.X, bounds.Bottom - 2, 2, 2);
        graphics.FillRectangle(outline, bounds.Right - 2, bounds.Bottom - 2, 2, 2);
    }

    private static void DrawStoneFrame(Graphics graphics, Rectangle bounds, AppTheme theme)
    {
        const int depth = 20;
        int[] lengths = [72, 88, 64, 96];
        using var mortar = new SolidBrush(theme.BorderDark);
        using var stone = new SolidBrush(AppTheme.Blend(theme.Header, theme.BorderLight, .30));
        using var light = new SolidBrush(AppTheme.Blend(theme.BorderLight, Color.White, .16));
        using var shade = new SolidBrush(AppTheme.Blend(theme.Header, theme.BorderDark, .40));
        using var fleck = new SolidBrush(AppTheme.Blend(theme.Header, theme.BorderLight, .65));
        Border(graphics, mortar, bounds, depth);
        var state = graphics.Save();
        graphics.SetClip(new Rectangle(bounds.X, bounds.Y, bounds.Width, depth));
        for (int x = bounds.X + 2, index = 0; x < bounds.Right; index++)
        {
            int length = lengths[index % lengths.Length];
            DrawStoneBlock(graphics, x, bounds.Y + 1, length - 3, depth - 2, false, index,
                stone, light, shade, fleck);
            x += length;
        }
        graphics.Restore(state);

        state = graphics.Save();
        graphics.SetClip(new Rectangle(bounds.X, bounds.Bottom - depth, bounds.Width, depth));
        for (int x = bounds.X + 2, index = 0; x < bounds.Right; index++)
        {
            int length = lengths[(index + 2) % lengths.Length];
            DrawStoneBlock(graphics, x, bounds.Bottom - depth + 1, length - 3, depth - 2, false, index + 2,
                stone, light, shade, fleck);
            x += length;
        }
        graphics.Restore(state);

        state = graphics.Save();
        graphics.SetClip(new Rectangle(bounds.X, bounds.Y + depth, depth, bounds.Height - depth * 2));
        for (int y = bounds.Y + depth, index = 0; y < bounds.Bottom - depth; index++)
        {
            int length = lengths[(index + 1) % lengths.Length];
            DrawStoneBlock(graphics, bounds.X + 1, y, depth - 2, length - 3, true, index + 1,
                stone, light, shade, fleck);
            y += length;
        }
        graphics.Restore(state);

        state = graphics.Save();
        graphics.SetClip(new Rectangle(bounds.Right - depth, bounds.Y + depth, depth, bounds.Height - depth * 2));
        for (int y = bounds.Y + depth, index = 0; y < bounds.Bottom - depth; index++)
        {
            int length = lengths[(index + 1) % lengths.Length];
            DrawStoneBlock(graphics, bounds.Right - depth + 1, y, depth - 2, length - 3, true, index + 3,
                stone, light, shade, fleck);
            y += length;
        }
        graphics.Restore(state);
        using var inner = new Pen(theme.BorderLight);
        graphics.DrawRectangle(inner, bounds.X + depth - 1, bounds.Y + depth - 1,
            bounds.Width - depth * 2 + 1, bounds.Height - depth * 2 + 1);
    }

    private static void DrawStoneBlock(Graphics graphics, int x, int y, int width, int height,
        bool vertical, int index, Brush stone, Brush light, Brush shade, Brush fleck)
    {
        graphics.FillRectangle(stone, x + 2, y + 2, width - 3, height - 3);
        graphics.FillRectangle(stone, x + 4, y, width - 7, height);
        graphics.FillRectangle(light, x + 5, y + 1, width - 10, 2);
        graphics.FillRectangle(light, x + 2, y + 4, 2, height - 8);
        graphics.FillRectangle(shade, x + 4, y + height - 3, width - 8, 2);
        graphics.FillRectangle(shade, x + width - 3, y + 4, 2, height - 8);
        if (vertical)
        {
            int mark = 14 + index % 3 * 6;
            graphics.FillRectangle(fleck, x + 7, y + mark, 3, 13);
            graphics.FillRectangle(shade, x + 10, y + mark + 5, 4, 2);
            graphics.FillRectangle(shade, x + 5, y + height / 2, 5, 3);
            graphics.FillRectangle(fleck, x + 11, y + height - 23, 3, 8);
        }
        else
        {
            int mark = 14 + index % 3 * 7;
            graphics.FillRectangle(shade, x + mark, y + 5, 13, 2);
            graphics.FillRectangle(shade, x + mark + 5, y + 7, 3, 2);
            graphics.FillRectangle(fleck, x + width / 2, y + 10, 11, 3);
            graphics.FillRectangle(fleck, x + width / 2 + 7, y + 8, 4, 2);
            graphics.FillRectangle(shade, x + width - 22, y + 13, 9, 2);
            graphics.FillRectangle(light, x + width - 27, y + 5, 5, 2);
        }
    }

}
