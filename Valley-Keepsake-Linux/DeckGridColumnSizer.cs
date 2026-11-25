using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;

namespace TradutorModsDeck;

// Horizontal scrolling only reveals complete text when a cell is wide enough
// to draw it. Measure the actual strings instead of relying on fixed minimums.
internal sealed class DeckGridColumnSizer
{
    private readonly Dictionary<string, double> measured = new(StringComparer.Ordinal);

    public void Fit(DataGrid grid, int columnIndex, IEnumerable<string> values, double minimum)
    {
        var typeface = new Typeface(grid.FontFamily);
        double fontSize = grid.FontSize;
        double width = minimum;
        foreach (var value in values)
        {
            if (string.IsNullOrEmpty(value)) continue;
            if (!measured.TryGetValue(value, out var textWidth))
            {
                textWidth = new FormattedText(value, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, fontSize, Brushes.Black).Width;
                measured[value] = textWidth;
            }
            width = Math.Max(width, Math.Ceiling(textWidth * 1.08 + 36));
        }
        grid.Columns[columnIndex].MinWidth = width;
    }
}
