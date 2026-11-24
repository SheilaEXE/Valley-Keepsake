using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Controls.Primitives;
using TradutorModsStardew;
using DrawingColor = System.Drawing.Color;

namespace TradutorModsDeck;

internal static class DeckTheme
{
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
        }
        colors.Theme = theme;
        colors.Accent = accent;
        PaintButton(button, colors);
    }

    private static void PaintButton(Button button, ButtonColors colors)
    {
        var theme = colors.Theme;
        var fill = colors.Hovered ? colors.Accent : theme.Button;
        button.Background = Brush(fill);
        button.BorderBrush = Brush(colors.Hovered ? theme.BorderLight : theme.ButtonBorder);
        bool brightGreen = fill.G > fill.R * 1.2 && fill.G > fill.B * 1.1;
        bool brightYellow = fill.R > 180 && fill.G > 150 && fill.B < 150;
        button.Foreground = Brush(colors.Hovered && (brightGreen || brightYellow) ? theme.BorderDark :
            colors.Hovered ? theme.HoverText : theme.ButtonText);
        button.CornerRadius = new CornerRadius(8);
        button.BorderThickness = new Thickness(1);
    }

    public static void Field(TextBox box, AppTheme theme)
    {
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
                new Setter(DataGridColumnHeader.BorderBrushProperty, Brush(theme.BorderDark))
            }
        });
        grid.Styles.Add(new Style(selector => selector.OfType<ScrollBar>())
        {
            Setters =
            {
                new Setter(ScrollBar.BackgroundProperty, Brush(theme.ScrollTrack)),
                new Setter(ScrollBar.AllowAutoHideProperty, false),
                new Setter(ScrollBar.MinWidthProperty, 18d),
                new Setter(ScrollBar.MinHeightProperty, 18d)
            }
        });
        grid.Styles.Add(new Style(selector => selector.OfType<ScrollBar>().Descendant().OfType<Thumb>())
        {
            Setters =
            {
                new Setter(Thumb.BackgroundProperty, Brush(theme.ScrollThumb)),
                new Setter(Thumb.MinWidthProperty, 12d),
                new Setter(Thumb.MinHeightProperty, 24d)
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
        bool highlighted = colors.Hovered || row.IsSelected;
        var highlight = colors.Theme.IsLight
            ? AppTheme.Blend(colors.Theme.Row, colors.Theme.Selection, .55)
            : colors.Theme.GridSelection;
        row.Background = Brush(highlighted ? highlight : colors.BaseColor);
        row.Foreground = Brush(colors.Theme.Text);
    }
}
