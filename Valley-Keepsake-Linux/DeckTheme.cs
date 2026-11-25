using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using TradutorModsStardew;
using DrawingColor = System.Drawing.Color;

namespace TradutorModsDeck;

internal static class DeckTheme
{
    public static void ApplyApplicationScrollBars(AppTheme theme)
    {
        if (Application.Current is not { } app) return;
        app.Resources["DeckScrollTrack"] = Brush(theme.ScrollTrack);
        app.Resources["DeckScrollThumb"] = Brush(theme.ScrollThumb);
        app.Resources["DeckScrollBorder"] = Brush(theme.BorderDark);
    }

    private sealed class ButtonColors
    {
        public AppTheme Theme = ThemeStore.Current;
        public DrawingColor Accent;
        public bool Hovered;
    }

    private sealed class RowColors
    {
        public AppTheme Theme = ThemeStore.Current;
        public DrawingColor BaseColor;
        public bool Hovered;
    }

    public static IBrush Brush(DrawingColor color) => new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));

    public static void Button(Button button, AppTheme theme, DrawingColor accent)
    {
        if (button.Tag is not ButtonColors colors)
        {
            colors = new ButtonColors();
            button.Tag = colors;
            button.PointerEntered += (_, _) => { colors.Hovered = true; PaintButton(button, colors); };
            button.PointerExited += (_, _) => { colors.Hovered = false; PaintButton(button, colors); };
            button.PropertyChanged += (_, args) =>
            {
                if (args.Property == Avalonia.Input.InputElement.IsEnabledProperty) PaintButton(button, colors);
            };
        }
        colors.Theme = theme;
        colors.Accent = accent;
        PaintButton(button, colors);
    }

    private static void PaintButton(Button button, ButtonColors colors)
    {
        var theme = colors.Theme;
        bool hovered = colors.Hovered && button.IsEnabled;
        var fill = !button.IsEnabled
            ? theme.Kind == ThemeKind.Classic ? DrawingColor.FromArgb(47, 69, 86) : AppTheme.Blend(theme.Button, theme.Panel, .6)
            : hovered ? colors.Accent : theme.Button;
        bool accentBrightGreen = colors.Accent.G > colors.Accent.R * 1.2 && colors.Accent.G > colors.Accent.B * 1.1;
        bool accentBrightYellow = colors.Accent.R > 180 && colors.Accent.G > 150 && colors.Accent.B < 150;
        var hoverText = theme.Kind != ThemeKind.Classic && (accentBrightGreen || accentBrightYellow)
            ? theme.BorderDark : theme.HoverText;
        var hoverBorder = theme.Kind == ThemeKind.Classic ? DrawingColor.White : theme.BorderLight;
        foreach (var suffix in new[] { "PointerOver", "Pressed" })
        {
            button.Resources[$"ButtonBackground{suffix}"] = Brush(colors.Accent);
            button.Resources[$"ButtonBorderBrush{suffix}"] = Brush(hoverBorder);
            button.Resources[$"ButtonForeground{suffix}"] = Brush(hoverText);
        }
        button.Resources["ButtonBackgroundDisabled"] = Brush(theme.Kind == ThemeKind.Classic
            ? DrawingColor.FromArgb(47, 69, 86) : AppTheme.Blend(theme.Button, theme.Panel, .6));
        button.Resources["ButtonBorderBrushDisabled"] = Brush(theme.ButtonBorder);
        button.Resources["ButtonForegroundDisabled"] = Brush(theme.MutedText);
        button.Background = Brush(fill);
        button.BorderBrush = Brush(hovered
            ? theme.Kind == ThemeKind.Classic ? DrawingColor.White : theme.BorderLight
            : theme.ButtonBorder);
        button.Foreground = Brush(!button.IsEnabled ? theme.MutedText
            : hovered ? hoverText : theme.ButtonText);
        button.CornerRadius = new CornerRadius(8);
        button.BorderThickness = new Thickness(1);
    }

    public static void Field(TextBox box, AppTheme theme)
    {
        // A single-line field can scroll its text with the caret, but should
        // never show the themed list scrollbar inside the input border.
        ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Hidden);
        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Disabled);
        box.Background = Brush(theme.Field);
        box.Foreground = Brush(theme.Text);
        box.BorderBrush = Brush(theme.BorderDark);
        box.CaretBrush = Brush(theme.Text);
        box.Height = 28;
        box.CornerRadius = new CornerRadius(0);
    }

    public static void Choice(ComboBox box, AppTheme theme)
    {
        box.Background = Brush(theme.Field);
        box.Foreground = Brush(theme.Text);
        box.BorderBrush = Brush(theme.BorderDark);
        box.Height = 29;
        box.CornerRadius = new CornerRadius(0);
        box.Styles.Clear();
        box.Styles.Add(new Style(selector => selector.OfType<ComboBoxItem>())
        {
            Setters =
            {
                new Setter(ComboBoxItem.BackgroundProperty, Brush(theme.Field)),
                new Setter(ComboBoxItem.ForegroundProperty, Brush(theme.Text))
            }
        });
        box.Styles.Add(new Style(selector => selector.OfType<ComboBoxItem>().Class(":pointerover"))
        {
            Setters =
            {
                new Setter(ComboBoxItem.BackgroundProperty, Brush(theme.Selection)),
                new Setter(ComboBoxItem.ForegroundProperty, Brush(theme.GridSelectionText))
            }
        });
    }

    public static void Grid(DataGrid grid, AppTheme theme)
    {
        // O template Fluent desenha uma camada de seleção própria sobre o fundo da linha.
        // Substitua todos os estados para que a cor seja a mesma usada no Windows.
        foreach (var key in new[]
        {
            "DataGridRowSelectedBackgroundBrush", "DataGridRowSelectedHoveredBackgroundBrush",
            "DataGridRowSelectedUnfocusedBackgroundBrush", "DataGridRowSelectedHoveredUnfocusedBackgroundBrush"
        }) grid.Resources[key] = Brush(theme.GridSelection);
        foreach (var key in new[]
        {
            "DataGridRowSelectedBackgroundOpacity", "DataGridRowSelectedHoveredBackgroundOpacity",
            "DataGridRowSelectedUnfocusedBackgroundOpacity", "DataGridRowSelectedHoveredUnfocusedBackgroundOpacity"
        }) grid.Resources[key] = 1d;
        grid.Resources["DataGridRowHoveredBackgroundColor"] = Brush(AppTheme.Blend(theme.Row, theme.Selection, .25));
        grid.Resources["DataGridCellFocusVisualPrimaryBrush"] = Brush(theme.BorderLight);
        grid.Resources["DataGridCellFocusVisualSecondaryBrush"] = Brush(theme.BorderDark);
        grid.Background = Brush(theme.Row);
        grid.Foreground = Brush(theme.Text);
        grid.BorderBrush = Brush(theme.BorderDark);
        grid.RowBackground = Brush(theme.Row);
        grid.HorizontalGridLinesBrush = Brush(theme.BorderDark);
        grid.VerticalGridLinesBrush = Brush(theme.BorderDark);
        grid.GridLinesVisibility = DataGridGridLinesVisibility.All;
        // Avalonia no Linux não herda de forma confiável a cor do DataGridRow
        // nos TextBlocks gerados por DataGridTextColumn.
        foreach (var column in grid.Columns.OfType<DataGridTextColumn>())
            column.Foreground = Brush(theme.Text);
        grid.Styles.Clear();
        grid.Styles.Add(new Style(selector => selector.OfType<DataGridColumnHeader>())
        {
            Setters =
            {
                new Setter(DataGridColumnHeader.BackgroundProperty, Brush(theme.Header)),
                new Setter(DataGridColumnHeader.ForegroundProperty, Brush(theme.Text)),
                new Setter(DataGridColumnHeader.BorderBrushProperty, Brush(theme.BorderDark)),
                new Setter(DataGridColumnHeader.FontSizeProperty, 15d),
                new Setter(DataGridColumnHeader.FontWeightProperty, FontWeight.Bold),
                new Setter(DataGridColumnHeader.MinHeightProperty, 34d)
            }
        });
    }

    public static void Row(DataGridRow row, AppTheme theme, DrawingColor background)
    {
        if (row.Tag is not RowColors colors)
        {
            colors = new RowColors();
            row.Tag = colors;
            row.PointerEntered += (_, _) => { colors.Hovered = true; PaintRow(row, colors); };
            row.PointerExited += (_, _) => { colors.Hovered = false; PaintRow(row, colors); };
            row.PropertyChanged += (_, args) =>
            {
                if (args.Property == DataGridRow.IsSelectedProperty) PaintRow(row, colors);
            };
        }
        colors.Theme = theme;
        colors.BaseColor = background;
        PaintRow(row, colors);
    }

    private static void PaintRow(DataGridRow row, RowColors colors)
    {
        var theme = colors.Theme;
        var background = row.IsSelected ? theme.GridSelection
            : colors.Hovered ? AppTheme.Blend(colors.BaseColor, theme.Selection, .25)
            : colors.BaseColor;
        row.Background = Brush(background);
        row.Foreground = Brush(row.IsSelected ? theme.GridSelectionText : theme.Text);
        foreach (var text in row.GetVisualDescendants().OfType<TextBlock>())
            text.Foreground = Brush(row.IsSelected ? theme.GridSelectionText : theme.Text);
    }
}
