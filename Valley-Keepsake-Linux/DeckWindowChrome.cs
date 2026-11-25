using Avalonia;
using Avalonia.Controls;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TradutorModsStardew;

namespace TradutorModsDeck;

// KDE does not expose the Windows DWM caption-color API. Draw the caption in
// the Avalonia client area so every app window uses the selected theme.
internal sealed class DeckWindowChrome : Grid
{
    private readonly Border caption;
    private readonly TextBlock captionText;
    private readonly Button minimize;
    private readonly Button maximize;
    private readonly Button close;

    private DeckWindowChrome(Window window, Control content)
    {
        RowDefinitions = new RowDefinitions("34,*");
        captionText = new TextBlock
        {
            Text = window.Title,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            Margin = new Thickness(9, 0, 0, 0)
        };
        var icon = new Image
        {
            Source = new Avalonia.Media.Imaging.Bitmap(DeckAssets.PathFor("app.png")),
            Width = 17,
            Height = 17,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        minimize = CaptionButton(CaptionIcon("minimize"));
        maximize = CaptionButton(CaptionIcon("maximize"));
        close = CaptionButton(CaptionIcon("close"));
        minimize.Click += (_, _) => window.WindowState = WindowState.Minimized;
        maximize.Click += (_, _) => ToggleMaximize(window);
        close.Click += (_, _) => window.Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        buttons.Children.Add(minimize);
        buttons.Children.Add(maximize);
        buttons.Children.Add(close);
        maximize.IsVisible = window.CanResize;
        var titleRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        titleRow.Children.Add(icon);
        Grid.SetColumn(captionText, 1);
        titleRow.Children.Add(captionText);
        Grid.SetColumn(buttons, 2);
        titleRow.Children.Add(buttons);
        caption = new Border { Child = titleRow };
        caption.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(caption).Properties.IsLeftButtonPressed) return;
            if (e.Source is Visual source &&
                (source is Button || source.GetVisualAncestors().OfType<Button>().Any())) return;
            if (e.ClickCount == 2 && window.CanResize) ToggleMaximize(window);
            else if (e.ClickCount == 1) window.BeginMoveDrag(e);
            e.Handled = true;
        };
        Children.Add(caption);
        Grid.SetRow(content, 1);
        Children.Add(content);
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty)
                maximize.Content = CaptionIcon(window.WindowState == WindowState.Maximized ? "restore" : "maximize");
            if (e.Property == Window.TitleProperty) captionText.Text = window.Title;
        };
        if (window.CanResize)
        {
            AddResizeGrip(window, WindowEdge.West, HorizontalAlignment.Left, VerticalAlignment.Stretch);
            AddResizeGrip(window, WindowEdge.East, HorizontalAlignment.Right, VerticalAlignment.Stretch);
            AddResizeGrip(window, WindowEdge.North, HorizontalAlignment.Stretch, VerticalAlignment.Top);
            AddResizeGrip(window, WindowEdge.South, HorizontalAlignment.Stretch, VerticalAlignment.Bottom);
        }
    }

    public static DeckWindowChrome Attach(Window window, Control content, AppTheme theme)
    {
        window.SystemDecorations = SystemDecorations.None;
        var chrome = new DeckWindowChrome(window, content);
        window.Content = chrome;
        chrome.ApplyTheme(theme);
        return chrome;
    }

    public void ApplyTheme(AppTheme theme)
    {
        var color = theme.Kind switch
        {
            ThemeKind.Parchment => theme.Panel,
            ThemeKind.Garden or ThemeKind.Wood => theme.Header,
            _ => theme.Background
        };
        caption.Background = DeckTheme.Brush(color);
        caption.BorderBrush = DeckTheme.Brush(theme.BorderDark);
        caption.BorderThickness = new Thickness(0, 0, 0, 1);
        captionText.Foreground = DeckTheme.Brush(theme.Text);
        foreach (var button in new[] { minimize, maximize, close })
        {
            button.Foreground = DeckTheme.Brush(theme.Text);
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.Resources["ButtonBackgroundPointerOver"] = DeckTheme.Brush(theme.Selection);
            button.Resources["ButtonForegroundPointerOver"] = DeckTheme.Brush(theme.GridSelectionText);
            button.Resources["ButtonBackgroundPressed"] = DeckTheme.Brush(theme.Selection);
            button.Resources["ButtonForegroundPressed"] = DeckTheme.Brush(theme.GridSelectionText);
            if (button.Content is ShapePath icon) icon.Stroke = DeckTheme.Brush(theme.Text);
        }
    }

    private static Button CaptionButton(ShapePath icon) => new()
    {
        Content = icon,
        Width = 43,
        Height = 33,
        Padding = new Thickness(0),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        CornerRadius = new CornerRadius(0)
    };

    private static ShapePath CaptionIcon(string name) => new()
    {
        Data = Geometry.Parse(name switch
        {
            "minimize" => "M 2,11 L 14,11",
            "maximize" => "M 2,2 L 14,2 L 14,12 L 2,12 Z",
            "restore" => "M 4,2 L 14,2 L 14,10 M 2,4 L 12,4 L 12,12 L 2,12 Z",
            _ => "M 3,2 L 13,12 M 13,2 L 3,12"
        }),
        Width = 16,
        Height = 14,
        StrokeThickness = 1.5,
        Stroke = DeckTheme.Brush(ThemeStore.Current.Text)
    };

    private static void ToggleMaximize(Window window) =>
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void AddResizeGrip(Window window, WindowEdge edge, HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var grip = new Border
        {
            Width = edge is WindowEdge.West or WindowEdge.East ? 6 : double.NaN,
            Height = edge is WindowEdge.North or WindowEdge.South ? 5 : double.NaN,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            Background = Brushes.Transparent
        };
        Grid.SetRowSpan(grip, 2);
        grip.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed && window.WindowState == WindowState.Normal)
                window.BeginResizeDrag(edge, e);
        };
        Children.Add(grip);
    }
}
