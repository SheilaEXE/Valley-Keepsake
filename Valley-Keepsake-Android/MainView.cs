using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Button = Avalonia.Controls.Button;

namespace ValleyKeepsake.Android;

public sealed class MainView : UserControl
{
    private static readonly IBrush BackgroundColor = Brush("#0B2238");
    private static readonly IBrush PanelColor = Brush("#143A50");
    private static readonly IBrush TextColor = Brush("#EAF8F5");
    private readonly MobileOperations operations = new();
    private readonly TextBlock title = Label("Valley Keepsake", 31, Brush("#9FE34D"), FontWeight.Bold);
    private readonly TextBlock subtitle = Label("", 14);
    private readonly TextBlock introCredit = Label("made by NatrollEXE", 11);
    private readonly TextBlock introLanguageLabel = Label("", 14, TextColor, FontWeight.Bold);
    private readonly ComboBox introLanguage = new() { ItemsSource = new[] { "Português (Brasil)", "English" }, MinHeight = 44, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock localeLabel = Label("", 14, TextColor, FontWeight.Bold);
    private readonly TextBlock modsLabel = Label("", 13);
    private readonly TextBlock translationsLabel = Label("", 13);
    private readonly TextBlock configsLabel = Label("", 13);
    private readonly TextBlock status = Label("", 14);
    private readonly TextBlock restorePrompt = Label("", 16, TextColor, FontWeight.Bold);
    private readonly TextBlock themePrompt = Label("", 17, TextColor, FontWeight.Bold);
    private readonly ComboBox locale = new() { ItemsSource = new[] { "pt-BR", "en", "es", "fr", "de", "it", "ja", "ko", "ru" }, SelectedIndex = 0, MinHeight = 44, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBox search = new() { MinHeight = 48 };
    private readonly ListBox list = new() { Height = 340, Width = 946, Background = PanelColor,
        Foreground = TextColor, Padding = new Thickness(0), BorderThickness = new Thickness(0) };
    private readonly List<Button> actions = [];
    private readonly List<(TextBlock Text, string Pt, string En)> buttonTexts = [];
    private readonly List<Border> folderPanels = [];
    private readonly List<Border> headerCells = [];
    private readonly Dictionary<MobileThemeKind, Button> themeOptions = [];
    private readonly TextBlock[] headers = [Label("", 14), Label("", 14), Label("", 14), Label("", 14)];
    private readonly List<MobileMod> mods = [];
    private readonly HashSet<string> selectedRestoreFolders = new(StringComparer.Ordinal);
    private IStorageFolder? modsFolder, translationsFolder, configsFolder;
    private FolderBookmarks bookmarks;
    private MobileThemeKind themeKind;
    private MobileTheme theme = MobileThemes.Get(MobileThemeKind.Classic);
    private bool portuguese;
    private bool busy;
    private Control? mainScreen;
    private Control? tableHost;
    private Button? restoreSelectedButton;
    private Border? restoreDialog;
    private Border? introBackground, mainBackground, restoreCard, themeDialog, themeCard, tableFrame, tableBody;
    private Grid? tableHeader;
    private ScrollViewer? mainScroller, tableScroller;
    private Button? restoreCloseButton, themeCloseButton;

    public MainView()
    {
        bookmarks = MobileStorage.Load();
        portuguese = !string.Equals(bookmarks.InterfaceLanguage, "en", StringComparison.OrdinalIgnoreCase);
        if (!Enum.TryParse(bookmarks.Theme, true, out themeKind) || !Enum.IsDefined(themeKind))
            themeKind = MobileThemeKind.Classic;
        theme = MobileThemes.Get(themeKind);
        title.FontFamily = new FontFamily("avares://ValleyKeepsake.Android/Assets#Pixelify Sans");
        foreach (var text in new[] { title, subtitle, introLanguageLabel, localeLabel, modsLabel, translationsLabel, configsLabel, status })
        {
            text.TextAlignment = TextAlignment.Center;
            text.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        var page = new StackPanel { Spacing = 12, Margin = new Thickness(16, 16, 28, 16) };
        page.Children.Add(localeLabel);
        page.Children.Add(locale);
        locale.SelectionChanged += (_, _) =>
        {
            selectedRestoreFolders.Clear(); UpdateRestoreSelectionButton();
            mods.Clear(); RefreshList();
            SetStatus("Idioma alterado. Toque em Analisar.", "Language changed. Tap Analyze.");
        };
        page.Children.Add(FolderSection(modsLabel, "Escolher pasta Mods", "Choose Mods folder", () => ChooseAsync(FolderKind.Mods)));
        page.Children.Add(FolderSection(translationsLabel, "Escolher biblioteca de traduções", "Choose translation library", () => ChooseAsync(FolderKind.Translations)));
        page.Children.Add(FolderSection(configsLabel, "Escolher biblioteca de configurações", "Choose settings library", () => ChooseAsync(FolderKind.Configurations)));
        introLanguage.SelectedIndex = portuguese ? 0 : 1;
        introLanguage.SelectionChanged += (_, _) =>
        {
            portuguese = introLanguage.SelectedIndex != 1;
            bookmarks = bookmarks with { InterfaceLanguage = portuguese ? "pt" : "en" };
            MobileStorage.Save(bookmarks);
            ApplyLanguage();
        };
        page.Children.Add(search);
        search.TextChanged += (_, _) => RefreshList();
        page.Children.Add(Action("Analisar", "Analyze", AnalyzeAsync));
        tableHost = BuildTable();
        page.Children.Add(tableHost);
        restoreSelectedButton = Action("Restaurar selecionados", "Restore selected", RestoreSelectedModsAsync);
        restoreSelectedButton.IsVisible = false;
        page.Children.Add(restoreSelectedButton);
        page.Children.Add(status);
        page.Children.Add(Action("Guardar traduções instaladas", "Save installed translations", SaveTranslationsAsync));
        page.Children.Add(Action("Aplicar traduções ausentes", "Apply missing translations", ApplyTranslationsAsync));
        page.Children.Add(Action("Desfazer última aplicação", "Undo last application", UndoAsync));
        page.Children.Add(Action("Backup de configurações", "Back up settings", BackupConfigsAsync));
        page.Children.Add(Action("Restaurar Configurações dos mods", "Restore mod settings", ShowRestoreDialogAsync));
        mainScroller = new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        mainBackground = new Border { Background = BackgroundColor, Child = mainScroller };
        var root = new Grid();
        root.Children.Add(mainBackground);
        restoreDialog = BuildRestoreDialog();
        root.Children.Add(restoreDialog);
        mainScreen = root;
        Content = BuildIntro();
        ApplyLanguage();
        ApplyTheme();
        AttachedToVisualTree += async (_, _) => await RestoreFoldersAsync();
    }

    private Control BuildIntro()
    {
        var center = new StackPanel
        {
            Spacing = 20, HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        center.Children.Add(title);
        center.Children.Add(new JunimoDisplay
        {
            MaxSpriteSize = 48, Height = 60, HorizontalAlignment = HorizontalAlignment.Stretch
        });
        center.Children.Add(subtitle);

        var footer = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Bottom };
        footer.Children.Add(introLanguageLabel);
        footer.Children.Add(introLanguage);
        footer.Children.Add(Action("Avançar", "Continue", () =>
        {
            Content = mainScreen;
            return Task.CompletedTask;
        }));
        introCredit.TextAlignment = TextAlignment.Center;
        introCredit.HorizontalAlignment = HorizontalAlignment.Stretch;
        introCredit.Margin = new Thickness(0, 5, 0, 0);
        footer.Children.Add(introCredit);

        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        layout.Children.Add(center);
        Grid.SetRow(footer, 1);
        layout.Children.Add(footer);

        Image? icon = null;
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://ValleyKeepsake.Android/Assets/PaintPalette.png"));
            icon = new Image { Source = new Bitmap(stream), Width = 37, Height = 37 };
        }
        catch { }
        var palette = new Button
        {
            Content = (Control?)icon ?? new TextBlock { Text = "🎨", FontSize = 28 },
            Width = 48, Height = 48, Padding = new Thickness(0),
            BorderThickness = new Thickness(0), Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top
        };
        palette.Click += (_, _) => { if (themeDialog is not null) themeDialog.IsVisible = true; };

        var content = new Grid();
        content.Children.Add(layout);
        content.Children.Add(palette);
        themeDialog = BuildThemeDialog();
        content.Children.Add(themeDialog);
        introBackground = new Border
        {
            Background = BackgroundColor,
            Padding = new Thickness(24, 16, 24, 26),
            Child = content
        };
        return introBackground;
    }

    private Border BuildThemeDialog()
    {
        themeCloseButton = new Button
        {
            Content = "×", FontSize = 24, Width = 42, Height = 42,
            HorizontalAlignment = HorizontalAlignment.Right,
            CornerRadius = new CornerRadius(8)
        };
        themeCloseButton.Click += (_, _) => { if (themeDialog is not null) themeDialog.IsVisible = false; };
        var options = new StackPanel { Spacing = 10 };
        options.Children.Add(themeCloseButton);
        options.Children.Add(themePrompt);
        foreach (var (kind, pt, en) in new[]
        {
            (MobileThemeKind.Classic, "Azul clássico", "Classic blue"),
            (MobileThemeKind.Garden, "Jardim", "Garden"),
            (MobileThemeKind.Mine, "Galpão de Slimes", "Slime Hutch"),
            (MobileThemeKind.Parchment, "Nota dos Junimos", "Junimo Note"),
            (MobileThemeKind.Wood, "Caixa de Diálogo", "Dialogue Box")
        })
        {
            var choice = Action(pt, en, () => ChooseThemeAsync(kind));
            themeOptions.Add(kind, choice);
            options.Children.Add(choice);
        }
        themeCard = new Border
        {
            Background = PanelColor, BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(16),
            MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center, Child = options
        };
        return new Border
        {
            IsVisible = false, Background = Brush("#B8000000"),
            Padding = new Thickness(8), Child = themeCard
        };
    }

    private Task ChooseThemeAsync(MobileThemeKind kind)
    {
        themeKind = kind;
        theme = MobileThemes.Get(kind);
        bookmarks = bookmarks with { Theme = kind.ToString() };
        MobileStorage.Save(bookmarks);
        ApplyTheme();
        if (themeDialog is not null) themeDialog.IsVisible = false;
        return Task.CompletedTask;
    }

    private Border BuildRestoreDialog()
    {
        restorePrompt.TextAlignment = TextAlignment.Center;
        restoreCloseButton = new Button
        {
            Content = "×", FontSize = 24, Width = 42, Height = 42,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = PanelColor, Foreground = TextColor,
            CornerRadius = new CornerRadius(8)
        };
        restoreCloseButton.Click += (_, _) => HideRestoreDialog();

        var options = new StackPanel { Spacing = 12 };
        options.Children.Add(restoreCloseButton);
        options.Children.Add(restorePrompt);
        options.Children.Add(Action("Selecionar mods para restaurar", "Select mods to restore", SelectModsToRestoreAsync));
        options.Children.Add(Action("Restaurar tudo de uma só vez", "Restore all at once", RestoreAllAsync));

        restoreCard = new Border
        {
            Background = BackgroundColor, BorderBrush = Brush("#34657A"),
            BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(18), MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center, Child = options
        };
        return new Border
        {
            IsVisible = false, Background = Brush("#B8000000"),
            Padding = new Thickness(24), Child = restoreCard
        };
    }

    private void HideRestoreDialog()
    {
        if (restoreDialog is not null) restoreDialog.IsVisible = false;
    }

    private Control BuildTable()
    {
        const string widths = "300,180,190,260";
        var table = new Grid { Width = 946, Height = 386, RowDefinitions = new RowDefinitions("46,340"),
            Background = Brush(theme.Panel) };
        tableHeader = new Grid { Width = 930, HorizontalAlignment = HorizontalAlignment.Left,
            ColumnDefinitions = new ColumnDefinitions(widths), Background = Brush("#1C607D") };
        for (var i = 0; i < headers.Length; i++)
        {
            headers[i].FontWeight = FontWeight.Bold;
            headers[i].VerticalAlignment = VerticalAlignment.Center;
            var cell = new Border { Child = headers[i], Padding = new Thickness(10, 0),
                BorderBrush = Brush(theme.BorderDark), BorderThickness = new Thickness(0, 0, 1, 0) };
            Grid.SetColumn(cell, i);
            tableHeader.Children.Add(cell);
            headerCells.Add(cell);
        }
        table.Children.Add(tableHeader);
        tableBody = new Border { Width = 930, HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brush(theme.Row) };
        Grid.SetRow(tableBody, 1);
        table.Children.Add(tableBody);
        list.ItemTemplate = new FuncDataTemplate<MobileRow>((row, _) =>
        {
            var columns = new Grid { Width = 930, ColumnDefinitions = new ColumnDefinitions(widths), MinHeight = 42 };
            var paths = new[] { "Mod.Name", nameof(MobileRow.Translation),
                nameof(MobileRow.Configuration), nameof(MobileRow.Situation) };
            for (var i = 0; i < paths.Length; i++)
            {
                var cell = Label("", 13);
                cell.Bind(TextBlock.TextProperty, new Binding(paths[i]));
                cell.Foreground = row is not null && selectedRestoreFolders.Contains(row.Mod.RelativeFolder)
                    ? Brushes.White : Brush(theme.Text);
                cell.TextTrimming = TextTrimming.CharacterEllipsis;
                cell.TextWrapping = TextWrapping.NoWrap;
                cell.VerticalAlignment = VerticalAlignment.Center;
                var area = new Border { Child = cell, Padding = new Thickness(10, 0),
                    BorderBrush = Brush(theme.BorderDark), BorderThickness = new Thickness(0, 0, 1, 0) };
                Grid.SetColumn(area, i);
                columns.Children.Add(area);
            }
            var surface = new Border
            {
                Child = columns, BorderBrush = Brush(theme.BorderDark),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Background = row is not null && selectedRestoreFolders.Contains(row.Mod.RelativeFolder)
                    ? Brush("#287C51") : Brush(theme.Row)
            };
            surface.DataContextChanged += (_, _) =>
            {
                var selected = surface.DataContext is MobileRow current &&
                    selectedRestoreFolders.Contains(current.Mod.RelativeFolder);
                surface.Background = selected ? Brush("#287C51") : Brush(theme.Row);
                foreach (var column in columns.Children.OfType<Border>())
                    if (column.Child is TextBlock text)
                        text.Foreground = selected ? Brushes.White : Brush(theme.Text);
            };
            var held = false;
            surface.Holding += (_, args) =>
            {
                if (args.HoldingState == HoldingState.Started)
                {
                    held = true;
                    if (surface.DataContext is MobileRow current)
                        ToggleRestoreSelection(current.Mod, surface, true);
                    args.Handled = true;
                }
                else if (args.HoldingState is HoldingState.Completed or HoldingState.Canceled)
                    DispatcherTimer.RunOnce(() => held = false, TimeSpan.FromMilliseconds(150));
            };
            surface.Tapped += (_, args) =>
            {
                if (held) held = false;
                else if (surface.DataContext is MobileRow current)
                    ToggleRestoreSelection(current.Mod, surface, false);
                args.Handled = true;
            };
            return surface;
        });
        Grid.SetRow(list, 1);
        table.Children.Add(list);
        tableScroller = new ScrollViewer { Content = table, Height = 392,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        tableFrame = new Border { Background = PanelColor, CornerRadius = new CornerRadius(8),
            Padding = new Thickness(3), Child = tableScroller };
        return tableFrame;
    }

    private enum FolderKind { Mods, Translations, Configurations }
    private string T(string pt, string en) => portuguese ? pt : en;
    private void SetStatus(string pt, string en) => status.Text = T(pt, en);

    private void ApplyLanguage()
    {
        subtitle.Text = T("Salve e Restaure as traduções e configurações dos mods no Android",
            "Save and restore mod translations and settings on Android");
        introLanguageLabel.Text = T("Idioma da interface", "Interface language");
        themePrompt.Text = T("Escolha um tema", "Choose a theme");
        restorePrompt.Text = T("Como deseja restaurar as configurações?", "How would you like to restore settings?");
        localeLabel.Text = T("Idioma da tradução", "Translation language");
        search.PlaceholderText = T("Pesquisar mod ou UniqueID", "Search mod or UniqueID");
        headers[0].Text = "Mod";
        headers[1].Text = T("Tradução", "Translation");
        headers[2].Text = T("Configuração", "Settings");
        headers[3].Text = T("Situação", "Status");
        foreach (var item in buttonTexts) item.Text.Text = T(item.Pt, item.En);
        UpdateFolderLabels();
        if (mods.Count == 0) SetStatus("Escolha as pastas e toque em Analisar.", "Choose folders and tap Analyze.");
        else UpdateSummary();
        RefreshList();
        UpdateRestoreSelectionButton();
    }

    private void ApplyTheme()
    {
        var background = Brush(theme.Background);
        var panel = Brush(theme.Panel);
        var foreground = Brush(theme.Text);
        var border = Brush(theme.BorderDark);
        if (introBackground is not null) introBackground.Background = background;
        if (mainBackground is not null) mainBackground.Background = background;
        if (restoreCard is not null)
        {
            restoreCard.Background = panel;
            restoreCard.BorderBrush = border;
        }
        if (themeCard is not null)
        {
            themeCard.Background = panel;
            themeCard.BorderBrush = border;
        }
        if (tableFrame is not null) tableFrame.Background = panel;
        if (tableBody is not null) tableBody.Background = Brush(theme.Row);
        if (tableBody?.Parent is Grid table) table.Background = panel;
        if (tableHeader is not null) tableHeader.Background = Brush(theme.Header);
        foreach (var cell in headerCells) cell.BorderBrush = border;
        foreach (var section in folderPanels) section.Background = panel;

        title.Foreground = Brush(theme.Title);
        introCredit.Foreground = Brush(theme.MutedText);
        foreach (var label in new[] { subtitle, introLanguageLabel, themePrompt, localeLabel,
            modsLabel, translationsLabel, configsLabel, status, restorePrompt })
            label.Foreground = foreground;
        foreach (var label in headers) label.Foreground = foreground;

        foreach (var button in actions)
        {
            button.Background = Brush(theme.Button);
            button.Foreground = Brush(theme.ButtonText);
            button.BorderBrush = Brush(theme.ButtonBorder);
            button.BorderThickness = new Thickness(1);
            button.CornerRadius = new CornerRadius(8);
        }
        foreach (var item in buttonTexts) item.Text.Foreground = Brush(theme.ButtonText);
        foreach (var (kind, button) in themeOptions)
        {
            button.BorderBrush = Brush(kind == themeKind ? theme.ScrollThumb : theme.ButtonBorder);
            button.BorderThickness = new Thickness(kind == themeKind ? 2 : 1);
        }
        foreach (var close in new[] { restoreCloseButton, themeCloseButton })
        {
            if (close is null) continue;
            close.Background = panel;
            close.Foreground = foreground;
            close.BorderBrush = border;
        }

        ThemeChoice(introLanguage);
        ThemeChoice(locale);
        search.Background = Brush(theme.Field);
        search.Foreground = foreground;
        search.PlaceholderForeground = Brush(theme.MutedText);
        search.CaretBrush = foreground;
        search.BorderBrush = border;
        search.SelectionBrush = Brush(theme.Selection);

        list.Background = Brushes.Transparent;
        list.Foreground = foreground;
        ThemeList();
        if (mainScroller is not null) ThemeScrollBars(mainScroller.Styles);
        if (tableScroller is not null) ThemeScrollBars(tableScroller.Styles);
        RefreshList();
    }

    private void ThemeChoice(ComboBox box)
    {
        box.Background = Brush(theme.Field);
        box.Foreground = Brush(theme.Text);
        box.BorderBrush = Brush(theme.BorderDark);
        box.BorderThickness = new Thickness(1);
        box.CornerRadius = new CornerRadius(8);
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
                new Setter(ComboBoxItem.ForegroundProperty, Brush(theme.Text))
            }
        });
    }

    private void ThemeList()
    {
        list.Styles.Clear();
        list.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(ListBoxItem.PaddingProperty, new Thickness(0)),
                new Setter(ListBoxItem.MarginProperty, new Thickness(0)),
                new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)),
                new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
                new Setter(ListBoxItem.ForegroundProperty, Brush(theme.Text)),
                new Setter(ListBoxItem.TemplateProperty,
                    new FuncControlTemplate<ListBoxItem>((_, scope) => new ContentPresenter
                    {
                        Name = "PART_ContentPresenter",
                        Padding = new Thickness(0),
                        [~ContentPresenter.ContentProperty] = new TemplateBinding(ContentControl.ContentProperty),
                        [~ContentPresenter.ContentTemplateProperty] = new TemplateBinding(ContentControl.ContentTemplateProperty),
                        [~ContentPresenter.HorizontalContentAlignmentProperty] =
                            new TemplateBinding(ContentControl.HorizontalContentAlignmentProperty),
                        [~ContentPresenter.VerticalContentAlignmentProperty] =
                            new TemplateBinding(ContentControl.VerticalContentAlignmentProperty)
                    }.RegisterInNameScope(scope)))
            }
        });
        ThemeScrollBars(list.Styles, clear: false);
    }

    private void ThemeScrollBars(Styles styles, bool clear = true)
    {
        if (clear) styles.Clear();
        styles.Add(new Style(x => x.OfType<ScrollBar>())
        {
            Setters =
            {
                new Setter(ScrollBar.BackgroundProperty, Brush(theme.ScrollTrack)),
                new Setter(ScrollBar.AllowAutoHideProperty, false)
            }
        });
        styles.Add(new Style(x => x.OfType<ScrollBar>().Descendant().OfType<Thumb>())
        {
            Setters = { new Setter(Thumb.BackgroundProperty, Brush(theme.ScrollThumb)) }
        });
    }

    private async Task RestoreFoldersAsync()
    {
        bookmarks = MobileStorage.Load();
        var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (provider is null) return;
        modsFolder = await OpenBookmarkAsync(provider, bookmarks.Mods);
        translationsFolder = await OpenBookmarkAsync(provider, bookmarks.Translations);
        configsFolder = await OpenBookmarkAsync(provider, bookmarks.Configurations);
        UpdateFolderLabels();
    }

    private static async Task<IStorageFolder?> OpenBookmarkAsync(IStorageProvider provider, string? bookmark)
    {
        if (string.IsNullOrWhiteSpace(bookmark)) return null;
        try { return await provider.OpenFolderBookmarkAsync(bookmark); }
        catch { return null; }
    }

    private async Task ChooseAsync(FolderKind kind)
    {
        var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (provider is null) return;
        try
        {
            var selected = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = kind switch
                {
                    FolderKind.Mods => T("Escolha a pasta Mods", "Choose the Mods folder"),
                    FolderKind.Translations => T("Escolha a biblioteca de traduções", "Choose the translation library"),
                    _ => T("Escolha a biblioteca de configurações", "Choose the settings library")
                }, AllowMultiple = false
            });
            var folder = selected.FirstOrDefault();
            if (folder is null) return;
            var bookmark = folder.CanBookmark ? await folder.SaveBookmarkAsync() : null;
            if (bookmark is null)
            { SetStatus("O Android não concedeu acesso persistente a esta pasta.", "Android did not grant persistent access to this folder."); return; }
            switch (kind)
            {
                case FolderKind.Mods: modsFolder = folder; bookmarks = bookmarks with { Mods = bookmark }; break;
                case FolderKind.Translations: translationsFolder = folder; bookmarks = bookmarks with { Translations = bookmark }; break;
                case FolderKind.Configurations: configsFolder = folder; bookmarks = bookmarks with { Configurations = bookmark }; break;
            }
            MobileStorage.Save(bookmarks);
            UpdateFolderLabels();
            selectedRestoreFolders.Clear(); UpdateRestoreSelectionButton();
            mods.Clear(); RefreshList();
            SetStatus("Pasta selecionada. Toque em Analisar.", "Folder selected. Tap Analyze.");
        }
        catch (Exception ex) { status.Text = T("Não foi possível acessar a pasta: ", "Could not access folder: ") + ex.Message; }
    }

    private void UpdateFolderLabels()
    {
        modsLabel.Text = T("Pasta Mods: ", "Mods folder: ") + FolderName(modsFolder);
        translationsLabel.Text = T("Biblioteca de traduções: ", "Translation library: ") +
            FolderName(translationsFolder);
        configsLabel.Text = T("Biblioteca de configurações: ", "Settings library: ") +
            FolderName(configsFolder);
    }

    private string FolderName(IStorageFolder? folder)
    {
        var name = folder?.Name;
        if (name?.StartsWith("primary:", StringComparison.OrdinalIgnoreCase) == true)
            name = name["primary:".Length..];
        return string.IsNullOrWhiteSpace(name) ? T("escolha uma pasta", "choose a folder") : name;
    }

    private async Task AnalyzeAsync()
    {
        if (modsFolder is null) { SetStatus("Escolha a pasta Mods primeiro.", "Choose the Mods folder first."); return; }
        await RunAsync(ScanCoreAsync);
    }

    private async Task ScanCoreAsync()
    {
        if (modsFolder is null) return;
        selectedRestoreFolders.Clear();
        UpdateRestoreSelectionButton();
        mods.Clear();
        mods.AddRange(await operations.ScanAsync(modsFolder, translationsFolder, configsFolder, locale.SelectedItem as string ?? "pt-BR"));
        RefreshList(); UpdateSummary();
    }

    private void UpdateSummary()
    {
        var translated = mods.Count(mod => mod.InstalledTranslation is not null);
        var ready = mods.Count(mod => mod.InstalledTranslation is null && mod.SavedTranslation is not null);
        status.Text = T($"{mods.Count} mods encontrados · {translated} já traduzidos · {ready} prontos para aplicar",
            $"{mods.Count} mods found · {translated} translated · {ready} ready to apply");
    }

    private async Task SaveTranslationsAsync()
    {
        if (translationsFolder is null) { SetStatus("Escolha a biblioteca de traduções primeiro.", "Choose the translation library first."); return; }
        if (mods.Count == 0) await AnalyzeAsync();
        if (mods.Count == 0) return;
        await RunAsync(async () =>
        {
            var count = await operations.SaveTranslationsAsync(translationsFolder, mods);
            await ScanCoreAsync();
            SetStatus($"{count} tradução(ões) guardada(s) na biblioteca.", $"{count} translation(s) saved to the library.");
        });
    }

    private async Task ApplyTranslationsAsync()
    {
        if (modsFolder is null || translationsFolder is null)
        { SetStatus("Escolha a pasta Mods e a biblioteca de traduções.", "Choose the Mods folder and translation library."); return; }
        if (mods.Count == 0) await AnalyzeAsync();
        if (mods.Count == 0) return;
        await RunAsync(async () =>
        {
            var count = await operations.ApplyTranslationsAsync(mods);
            await ScanCoreAsync();
            SetStatus($"{count} tradução(ões) aplicada(s).", $"{count} translation(s) applied.");
        });
    }

    private async Task UndoAsync() => await RunAsync(async () =>
    {
        var count = await operations.UndoAsync();
        await ScanCoreAsync();
        SetStatus($"{count} aplicação(ões) desfeita(s).", $"{count} application(s) undone.");
    });

    private async Task BackupConfigsAsync()
    {
        if (configsFolder is null) { SetStatus("Escolha a biblioteca de configurações primeiro.", "Choose the settings library first."); return; }
        if (mods.Count == 0) await AnalyzeAsync();
        if (mods.Count == 0) return;
        await RunAsync(async () =>
        {
            var count = await operations.BackupConfigsAsync(configsFolder, mods);
            await ScanCoreAsync();
            SetStatus($"{count} configuração(ões) guardada(s).", $"{count} settings file(s) backed up.");
        });
    }

    private Task ShowRestoreDialogAsync()
    {
        if (configsFolder is null)
            SetStatus("Escolha a biblioteca de configurações primeiro.", "Choose the settings library first.");
        else if (restoreDialog is not null)
            restoreDialog.IsVisible = true;
        return Task.CompletedTask;
    }

    private async Task SelectModsToRestoreAsync()
    {
        HideRestoreDialog();
        if (configsFolder is null) return;
        if (mods.Count == 0) await AnalyzeAsync();
        if (mods.Count == 0) return;
        tableHost?.BringIntoView();
        SetStatus("Mantenha o dedo em um mod com backup salvo; depois toque nos demais para selecioná-los.",
            "Press and hold a mod with saved settings, then tap the others to select them.");
    }

    private void ToggleRestoreSelection(MobileMod mod, Border surface, bool held)
    {
        if (mod.SavedConfig is null)
        {
            if (held) SetStatus("Este mod não tem configuração salva para restaurar.",
                "This mod has no saved settings to restore.");
            return;
        }
        if (!held && selectedRestoreFolders.Count == 0) return;
        if (!selectedRestoreFolders.Add(mod.RelativeFolder))
            selectedRestoreFolders.Remove(mod.RelativeFolder);
        var selected = selectedRestoreFolders.Contains(mod.RelativeFolder);
        surface.Background = selected ? Brush("#287C51") : Brush(theme.Row);
        if (surface.Child is Grid columns)
            foreach (var column in columns.Children.OfType<Border>())
                if (column.Child is TextBlock text)
                    text.Foreground = selected ? Brushes.White : Brush(theme.Text);
        UpdateRestoreSelectionButton();
        if (selectedRestoreFolders.Count == 0) UpdateSummary();
        else SetStatus($"{selectedRestoreFolders.Count} mod(s) selecionado(s) para restaurar.",
            $"{selectedRestoreFolders.Count} mod(s) selected for restoration.");
    }

    private void UpdateRestoreSelectionButton()
    {
        if (restoreSelectedButton is null) return;
        var count = selectedRestoreFolders.Count;
        restoreSelectedButton.IsVisible = count > 0;
        if (restoreSelectedButton.Content is TextBlock text)
            text.Text = T($"Restaurar selecionados ({count})", $"Restore selected ({count})");
    }

    private async Task RestoreSelectedModsAsync()
    {
        if (configsFolder is null) return;
        var selected = mods.Where(mod => selectedRestoreFolders.Contains(mod.RelativeFolder)
            && mod.SavedConfig is not null).ToList();
        if (selected.Count == 0) return;
        await RunAsync(async () =>
        {
            var count = await operations.RestoreConfigsAsync(configsFolder, selected,
                MobileBackupFolderName.ForLanguage(portuguese ? "pt-BR" : "en"));
            await ScanCoreAsync();
            if (count == 0)
                SetStatus("As configurações selecionadas já são iguais às cópias salvas.",
                    "The selected settings already match their saved copies.");
            else
                SetStatus($"{count} configuração(ões) restaurada(s). As versões anteriores foram guardadas na biblioteca.",
                    $"{count} settings file(s) restored. Previous versions were saved in the library.");
        });
    }

    private async Task RestoreAllAsync()
    {
        HideRestoreDialog();
        if (configsFolder is null) return;
        if (mods.Count == 0) await AnalyzeAsync();
        if (mods.Count == 0) return;
        await RunAsync(async () =>
        {
            var count = await operations.RestoreConfigsAsync(configsFolder, mods,
                MobileBackupFolderName.ForLanguage(portuguese ? "pt-BR" : "en"));
            await ScanCoreAsync();
            SetStatus($"{count} configuração(ões) restaurada(s). As versões anteriores foram guardadas na biblioteca.",
                $"{count} settings file(s) restored. Previous versions were saved in the library.");
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        foreach (var button in actions) button.IsEnabled = false;
        SetStatus("Processando…", "Working…");
        try { await action(); }
        catch (Exception ex) { status.Text = T("Operação interrompida: ", "Operation stopped: ") + ex.Message; }
        finally { busy = false; foreach (var button in actions) button.IsEnabled = true; }
    }

    private void RefreshList()
    {
        var query = search.Text?.Trim() ?? "";
        list.ItemsSource = mods.Where(mod => query.Length == 0 || mod.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || mod.UniqueId.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(mod => new MobileRow(mod,
                mod.InstalledTranslation is not null ? T("Instalada", "Installed") : mod.SavedTranslation is not null ? T("Salva", "Saved") : "—",
                mod.SavedConfig is not null ? T("Backup salvo", "Backed up") : mod.CurrentConfig is not null ? T("Sem backup", "No backup") : "—",
                mod.InstalledTranslation is not null ? T("Já traduzido", "Already translated") :
                    mod.SavedTranslation is not null ? T("Pronto para aplicar", "Ready to apply") : T("Sem tradução salva", "No saved translation")))
            .ToList();
    }

    private sealed record MobileRow(MobileMod Mod, string Translation, string Configuration, string Situation);

    private Border FolderSection(TextBlock label, string pt, string en, Func<Task> click)
    {
        var panel = new StackPanel { Spacing = 5 };
        panel.Children.Add(label);
        panel.Children.Add(Action(pt, en, click));
        var section = new Border { Background = PanelColor, CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10), Child = panel };
        folderPanels.Add(section);
        return section;
    }

    private Button Action(string pt, string en, Func<Task> click)
    {
        var content = Label(T(pt, en), 15);
        content.TextAlignment = TextAlignment.Center;
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        var button = new Button
        {
            Content = content, MinHeight = 52, Padding = new Thickness(12, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brush("#1C607D"), Foreground = TextColor,
            CornerRadius = new CornerRadius(8)
        };
        button.Click += async (_, _) => await click();
        actions.Add(button);
        buttonTexts.Add((content, pt, en));
        return button;
    }

    private static TextBlock Label(string text, double size, IBrush? color = null, FontWeight? weight = null) => new()
    {
        Text = text, FontSize = size, Foreground = color ?? TextColor,
        FontWeight = weight ?? FontWeight.Normal, TextWrapping = TextWrapping.Wrap
    };

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
    private static IBrush Brush(Color color) => new SolidColorBrush(color);
}
