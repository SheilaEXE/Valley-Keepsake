using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using TradutorModsStardew;

namespace TradutorModsDeck;

internal sealed class MainWindow : Window
{
    private static string T(string pt, string en) => UiLanguage.Text(pt, en);
    private readonly TranslationService service = new();
    private readonly CancellationTokenSource updateCheckCancellation = new();
    private readonly TextBox library = Field();
    private readonly TextBox configLibrary = Field();
    private readonly TextBox mods = Field(FindInitialModsPath());
    private readonly TextBox search = Field();
    private readonly ComboBox interfaceLanguage = new() { Width = 185 };
    private readonly ComboBox locale = new() { ItemsSource = new[] { "pt-BR", "en", "es", "fr", "de", "it", "ja", "ko", "zh-Hans", "zh-Hant", "ru", "tr", "pl", "cs" }.Select(TranslationLocales.DisplayOption).ToArray(), SelectedItem = TranslationLocales.DisplayOption(UiLanguage.TranslationLocale), Width = 140 };
    private readonly TextBlock summary = new() { Foreground = Brush("#AEE0E7"), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Bold };
    private readonly TextBlock subtitle = new() { Foreground = Brush("#AEE0E7"), FontWeight = FontWeight.Bold };
    private readonly DataGrid grid = new() { AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, Background = Brush("#103148"), Foreground = Brushes.White, BorderBrush = Brush("#1A617D") };
    private TextBlock? libraryLabel, configLibraryLabel, modsLabel, searchLabel, interfaceLabel, localeLabel;
    private Button? analyzeButton, prepareButton, configsButton, applyButton, undoButton;
    private readonly AppTheme initialTheme = ThemeStore.Current;
    private readonly TextBlock titleText = new() { Text = "Valley Keepsake", FontSize = 25, FontWeight = FontWeight.Bold, FontFamily = new FontFamily("avares://ValleyKeepsake/Assets#Pixelify Sans") };
    private readonly JunimoDisplay junimos = new() { Width = 360, Height = 78, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly SlimeDisplay slimes = new() { Width = 360, Height = 104, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly LeafLayer headerLeaves = new() { IsHitTestVisible = false };
    private readonly LeafLayer footerLeaves = new() { IsHitTestVisible = false };
    private readonly DeckFrame outerFrame = new();
    private readonly DeckFrame noteBackground = new() { BackgroundOnly = true };
    private Grid? rootLayout;
    private Border? fieldsFrame, listFrame;
    private Border? listDragHandle, listDragGrip;
    private double expandedFieldsHeight, dragStartY, dragStartHeight;
    private bool draggingList;
    private Grid? headerPanel, footerPanel;
    private Grid? pageGrid;
    private Button? paletteButton;
    private readonly List<Button> browseButtons = [];
    private readonly ObservableCollection<Row> rows = [];
    private List<MatchResult> results = [];
    private List<InstalledMod> installed = [];
    private readonly HashSet<string> expandedPackages = new(StringComparer.OrdinalIgnoreCase);
    private List<ApplyRecord> lastApply = [];
    private string SelectedLocale => TranslationLocales.FromDisplayOption(locale.SelectedItem as string ?? "pt-BR");

    public MainWindow()
    {
        var interfaceLanguages = UiLanguage.AvailableLanguages();
        interfaceLanguage.ItemsSource = interfaceLanguages;
        interfaceLanguage.SelectedIndex = interfaceLanguages.ToList().FindIndex(option => option.Code == UiLanguage.CurrentCode);
        Title = "Valley Keepsake";
        Icon = new WindowIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.png"));
        Width = 1280;
        Height = 760;
        MinWidth = 1020;
        MinHeight = 660;
        Background = DeckTheme.Brush(initialTheme.Background);
        var savedLibraries = LibrarySelectionStore.Load();
        library.Text = savedLibraries.Translation;
        configLibrary.Text = savedLibraries.Configuration;
        library.LostFocus += (_, _) => SaveLibraries();
        configLibrary.LostFocus += (_, _) => SaveLibraries();
        Closing += (_, _) => SaveLibraries();
        Closing += (_, _) => updateCheckCancellation.Cancel();
        Closed += (_, _) => { junimos.Dispose(); slimes.Dispose(); };
        Opened += async (_, _) => await CheckForUpdatesAsync();
        var root = new Grid { Margin = new Thickness(initialTheme.Kind switch { ThemeKind.Parchment => 84, ThemeKind.Wood => 64, ThemeKind.Mine => 56, ThemeKind.Garden => 72, _ => 20 }), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto") };
        rootLayout = root;
        headerPanel = new Grid { Height = 94, ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        titleText.Margin = new Thickness(8, 2, 0, 0);
        headerPanel.Children.Add(titleText);
        subtitle.Margin = new Thickness(8, 3, 0, 0);
        Grid.SetRow(subtitle, 1); headerPanel.Children.Add(subtitle);
        var choices = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
        var interfaceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        interfaceLabel = new TextBlock { Text = "Interface:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 0), FontWeight = FontWeight.Bold };
        interfaceLanguage.VerticalAlignment = VerticalAlignment.Center;
        interfaceRow.Children.Add(interfaceLabel); interfaceRow.Children.Add(interfaceLanguage);
        paletteButton = new Button { Width = 32, Height = 32, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        ToolTip.SetTip(paletteButton, T("Escolher tema", "Choose theme"));
        var palettePath = DeckAssets.PathFor("PaintPalette.png");
        if (File.Exists(palettePath)) paletteButton.Content = new Image { Source = new Avalonia.Media.Imaging.Bitmap(palettePath), Width = 27, Height = 27, Stretch = Stretch.Uniform };
        paletteButton.Click += (_, _) => OpenThemeMenu();
        interfaceRow.Children.Add(paletteButton);
        var localeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        localeLabel = new TextBlock { Text = "Idioma da tradução:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 0), FontWeight = FontWeight.Bold };
        locale.VerticalAlignment = VerticalAlignment.Center;
        localeRow.Children.Add(localeLabel); localeRow.Children.Add(locale);
        choices.Children.Add(interfaceRow); choices.Children.Add(localeRow);
        Grid.SetColumn(choices, 1); Grid.SetRowSpan(choices, 2); headerPanel.Children.Add(choices);
        root.Children.Add(headerPanel);

        var fields = new Grid { ColumnDefinitions = new ColumnDefinitions("205,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto") };
        libraryLabel = AddFolderField(fields, 0, "Biblioteca de traduções:", library, () => T("Escolha sua biblioteca de traduções", "Choose your translation library"));
        configLibraryLabel = AddFolderField(fields, 1, "Biblioteca de configurações:", configLibrary, () => T("Escolha sua biblioteca de configurações", "Choose your settings library"));
        modsLabel = AddFolderField(fields, 2, "Pasta Mods do Stardew:", mods, () => T("Escolha a pasta Mods do Stardew Valley", "Choose the Stardew Valley Mods folder"));
        searchLabel = AddField(fields, 3, "Pesquisar na lista:", search);
        search.Watermark = "Digite o nome do mod, tradução ou UniqueID…";
        search.TextChanged += (_, _) => RefreshRows();
        interfaceLanguage.SelectionChanged += (_, _) =>
        {
            if (interfaceLanguage.SelectedItem is not UiLanguage.LanguageOption selected) return;
            var selectedKeys = results.Where(result => result.Selected).Select(result => MappingKey(result.Translation)).ToHashSet();
            UiLanguage.Set(selected.Code);
            ApplyInterfaceLanguage();
            if (results.Count > 0)
            {
                Analyze(this, new RoutedEventArgs());
                foreach (var result in results.Where(result => result.State == MatchState.Safe))
                    result.Selected = selectedKeys.Contains(MappingKey(result.Translation));
            }
            RefreshRows();
        };
        locale.SelectionChanged += (_, _) => { if (locale.SelectedItem is string code) { UiLanguage.SetTranslationLocale(TranslationLocales.FromDisplayOption(code)); RefreshMissing(); RefreshRows(); } };
        fieldsFrame = new Border { Child = fields, Padding = new Thickness(3), Margin = new Thickness(0, 6, 0, 8), BorderThickness = new Thickness(3), ClipToBounds = true };
        Grid.SetRow(fieldsFrame, 1); root.Children.Add(fieldsFrame);

        listDragGrip = new Border { Width = 76, Height = 5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        listDragHandle = new Border { Height = 24, Child = listDragGrip, IsVisible = false };
        ToolTip.SetTip(listDragHandle, T("Arraste para aumentar ou diminuir a lista", "Drag to expand or shrink the list"));
        listDragHandle.PointerPressed += (_, e) =>
        {
            if (fieldsFrame is null || e.Pointer.Type != PointerType.Touch && !e.GetCurrentPoint(listDragHandle).Properties.IsLeftButtonPressed) return;
            expandedFieldsHeight = Math.Max(expandedFieldsHeight, fieldsFrame.Bounds.Height);
            dragStartHeight = fieldsFrame.IsVisible ? fieldsFrame.Bounds.Height : 0;
            dragStartY = e.GetPosition(root).Y;
            draggingList = true;
            e.Pointer.Capture(listDragHandle);
            e.Handled = true;
        };
        listDragHandle.PointerMoved += (_, e) =>
        {
            if (!draggingList || fieldsFrame is null) return;
            double height = Math.Clamp(dragStartHeight + e.GetPosition(root).Y - dragStartY, 0, expandedFieldsHeight);
            fieldsFrame.IsVisible = height > 0;
            fieldsFrame.Height = height;
            e.Handled = true;
        };
        listDragHandle.PointerReleased += (_, e) => { draggingList = false; e.Pointer.Capture(null); };
        listDragHandle.PointerCaptureLost += (_, _) => draggingList = false;
        Grid.SetRow(listDragHandle, 2); root.Children.Add(listDragHandle);

        grid.ItemsSource = rows;
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Aplicar", Binding = new Binding("Selected") });
        grid.Columns.Add(new DataGridTextColumn { Header = "Tradução", Binding = new Binding("Translation"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Mod encontrado", Binding = new Binding("Target"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Situação", Binding = new Binding("Status"), Width = new DataGridLength(160) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Detalhes", Binding = new Binding("Detail"), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
        grid.DoubleTapped += async (_, _) => { if (grid.SelectedItem is Row row) await OpenRowAsync(row); };
        grid.LoadingRow += (_, args) => PaintRow(args.Row);
        listFrame = new Border { Child = grid, Padding = new Thickness(3), BorderThickness = new Thickness(3) };
        Grid.SetRow(listFrame, 3); root.Children.Add(listFrame);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        footerPanel = footer;
        footer.Children.Add(footerLeaves); Grid.SetRowSpan(footerLeaves, 2); Grid.SetColumnSpan(footerLeaves, 2);
        summary.TextWrapping = TextWrapping.Wrap;
        summary.Margin = new Thickness(0, 0, 0, 8);
        footer.Children.Add(summary);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Stretch };
        analyzeButton = Button("Analisar", "#1A617D"); analyzeButton.Click += Analyze;
        prepareButton = Button("Procurar novos mods", "#685BA6"); prepareButton.Click += PrepareNewMods;
        configsButton = Button("Backup de configurações", "#685BA6"); configsButton.Click += OpenConfigBackup;
        applyButton = Button("Aplicar selecionadas", "#286A58"); applyButton.Click += Apply;
        undoButton = Button("Desfazer última aplicação", "#685BA6"); undoButton.Click += UndoLast;
        actions.Children.Add(analyzeButton); actions.Children.Add(prepareButton); actions.Children.Add(configsButton); actions.Children.Add(applyButton); actions.Children.Add(undoButton);
        foreach (var button in actions.Children.OfType<Button>()) button.Margin = new Thickness(0, 0, 8, 8);
        Grid.SetRow(actions, 1); footer.Children.Add(actions);
        Grid.SetRow(junimos, 1); Grid.SetColumn(junimos, 1); footer.Children.Add(junimos);
        Grid.SetRow(slimes, 1); Grid.SetColumn(slimes, 1); footer.Children.Add(slimes);
        Grid.SetRow(footer, 4); root.Children.Add(footer);
        var page = new Grid();
        pageGrid = page;
        page.Children.Add(noteBackground);
        headerLeaves.VerticalAlignment = VerticalAlignment.Top;
        headerLeaves.Height = 166;
        page.Children.Add(headerLeaves);
        page.Children.Add(root);
        page.Children.Add(outerFrame);
        Content = page;
        ApplyInterfaceLanguage();
        ApplyTheme();
    }

    private async Task CheckForUpdatesAsync()
    {
        var update = await UpdateChecker.CheckAsync(updateCheckCancellation.Token);
        if (update is null || !IsVisible) return;

        var theme = ThemeStore.Current;
        var dialog = new Window
        {
            Title = T("Atualização disponível", "Update available"),
            Width = 500,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = DeckTheme.Brush(theme.Background)
        };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 18 };
        panel.Children.Add(new TextBlock
        {
            Text = string.Format(T("A versão {0} do Valley Keepsake está disponível. Quer atualizar agora?",
                "Valley Keepsake version {0} is available. Update now?"), update.Version),
            TextWrapping = TextWrapping.Wrap,
            Foreground = DeckTheme.Brush(theme.Text)
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button(T("Cancelar", "Cancel"), "#685BA6");
        var openRelease = Button(T("Atualizar", "Update"), "#286A58");
        DeckTheme.Button(cancel, theme, theme.BorderLight);
        DeckTheme.Button(openRelease, theme, theme.ScrollThumb);
        cancel.Click += (_, _) => dialog.Close(false);
        openRelease.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(openRelease);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        if (!await dialog.ShowDialog<bool>(this)) return;

        try
        {
            var start = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
            start.ArgumentList.Add(update.ReleasePage.AbsoluteUri);
            Process.Start(start);
        }
        catch (Exception ex)
        {
            await NoticeAsync(T("Não foi possível abrir o release", "Could not open release"), ex.Message);
        }
    }

    private void OpenThemeMenu()
    {
        if (paletteButton is null) return;
        var menu = new ContextMenu();
        foreach (ThemeKind kind in Enum.GetValues<ThemeKind>())
        {
            var item = new MenuItem { Header = (kind == ThemeStore.CurrentKind ? "✓  " : "    ") + ThemeStore.Name(kind) };
            item.Click += (_, _) => { ThemeStore.Set(kind); ApplyTheme(); };
            menu.Items.Add(item);
        }
        menu.Open(paletteButton);
    }

    private void ApplyTheme()
    {
        var theme = ThemeStore.Current;
        if (rootLayout is not null)
            rootLayout.Margin = new Thickness(theme.Kind switch { ThemeKind.Parchment => 84, ThemeKind.Wood => 64, ThemeKind.Mine => 56, ThemeKind.Garden => 72, _ => 20 });
        MinWidth = 1020;
        MinHeight = 660;
        Background = DeckTheme.Brush(theme.Background);
        titleText.Foreground = DeckTheme.Brush(theme.Title);
        subtitle.Foreground = DeckTheme.Brush(theme.MutedText);
        summary.Foreground = DeckTheme.Brush(theme.MutedText);
        foreach (var label in new[] { libraryLabel, configLibraryLabel, modsLabel, searchLabel, interfaceLabel, localeLabel })
            if (label is not null) label.Foreground = DeckTheme.Brush(theme.Text);
        foreach (var field in new[] { library, configLibrary, mods, search }) DeckTheme.Field(field, theme);
        DeckTheme.Choice(interfaceLanguage, theme); DeckTheme.Choice(locale, theme);
        if (paletteButton is not null) paletteButton.Foreground = DeckTheme.Brush(theme.Text);
        if (fieldsFrame is not null)
        {
            fieldsFrame.Background = DeckTheme.Brush(theme.Panel);
            fieldsFrame.BorderBrush = DeckTheme.Brush(theme.BorderDark);
        }
        if (listFrame is not null)
        {
            listFrame.Background = DeckTheme.Brush(theme.Row);
            listFrame.BorderBrush = DeckTheme.Brush(theme.BorderDark);
        }
        if (listDragHandle is not null) listDragHandle.Background = DeckTheme.Brush(theme.Panel);
        if (listDragGrip is not null) listDragGrip.Background = DeckTheme.Brush(theme.BorderDark);
        DeckTheme.Grid(grid, theme);
        var gardenRose = System.Drawing.Color.FromArgb(243, 145, 182);
        var green = theme.Kind == ThemeKind.Classic ? System.Drawing.Color.FromArgb(161, 232, 74) : theme.ScrollThumb;
        var yellow = System.Drawing.Color.FromArgb(255, 198, 82);
        var red = System.Drawing.Color.FromArgb(239, 91, 96);
        var neutral = theme.Kind == ThemeKind.Classic ? System.Drawing.Color.FromArgb(108, 224, 230)
            : theme.IsLight ? theme.BorderDark : System.Drawing.Color.FromArgb(111, 101, 176);
        if (analyzeButton is not null) DeckTheme.Button(analyzeButton, theme, theme.Kind == ThemeKind.Garden ? gardenRose : neutral);
        if (prepareButton is not null) DeckTheme.Button(prepareButton, theme, theme.Kind == ThemeKind.Garden ? gardenRose : green);
        if (configsButton is not null) DeckTheme.Button(configsButton, theme, yellow);
        if (applyButton is not null) DeckTheme.Button(applyButton, theme, theme.Kind == ThemeKind.Garden ? gardenRose : green);
        if (undoButton is not null) DeckTheme.Button(undoButton, theme, red);
        foreach (var browse in browseButtons) DeckTheme.Button(browse, theme, theme.Kind == ThemeKind.Garden ? gardenRose : theme.Kind == ThemeKind.Classic ? green : neutral);
        junimos.ApplyTheme(theme);
        junimos.IsVisible = theme.Kind != ThemeKind.Mine;
        slimes.IsVisible = theme.Kind == ThemeKind.Mine;
        if (footerPanel is not null && pageGrid is not null)
        {
            if (theme.Kind == ThemeKind.Mine)
            {
                if (footerPanel.Children.Contains(slimes)) footerPanel.Children.Remove(slimes);
                if (!pageGrid.Children.Contains(slimes)) pageGrid.Children.Add(slimes);
                slimes.VerticalAlignment = VerticalAlignment.Bottom;
                slimes.Margin = new Thickness(0, 0, 52, 32);
                footerPanel.ColumnDefinitions[1].Width = new GridLength(360);
            }
            else if (pageGrid.Children.Contains(slimes))
            {
                pageGrid.Children.Remove(slimes);
                footerPanel.Children.Add(slimes);
                Grid.SetRow(slimes, 1);
                Grid.SetColumn(slimes, 1);
                slimes.VerticalAlignment = VerticalAlignment.Stretch;
                slimes.Margin = new Thickness(0);
            }
            if (theme.Kind is ThemeKind.Parchment or ThemeKind.Wood or ThemeKind.Garden)
            {
                if (footerPanel.Children.Contains(junimos)) footerPanel.Children.Remove(junimos);
                if (!pageGrid.Children.Contains(junimos)) pageGrid.Children.Add(junimos);
                footerPanel.ColumnDefinitions[1].Width = new GridLength(360);
                junimos.VerticalAlignment = VerticalAlignment.Bottom;
                junimos.Margin = theme.Kind switch
                {
                    ThemeKind.Parchment => new Thickness(0, 0, 60, 55),
                    ThemeKind.Garden => new Thickness(0, 0, 72, 19),
                    _ => new Thickness(0, 0, 52, 47)
                };
            }
            else
            {
                if (pageGrid.Children.Contains(junimos)) pageGrid.Children.Remove(junimos);
                if (!footerPanel.Children.Contains(junimos)) footerPanel.Children.Add(junimos);
                Grid.SetRow(junimos, 1);
                Grid.SetColumn(junimos, 1);
                footerPanel.ColumnDefinitions[1].Width = theme.Kind == ThemeKind.Mine
                    ? new GridLength(360) : GridLength.Auto;
                junimos.VerticalAlignment = VerticalAlignment.Stretch;
                junimos.Margin = new Thickness(0);
            }
        }
        headerLeaves.ApplyTheme(theme); footerLeaves.ApplyTheme(theme);
        outerFrame.ApplyTheme(theme);
        noteBackground.ApplyTheme(theme);
        foreach (var row in grid.GetVisualDescendants().OfType<DataGridRow>()) PaintRow(row);
    }

    private void PaintRow(DataGridRow row)
    {
        if (row.DataContext is not Row result) return;
        var theme = ThemeStore.Current;
        var color = result.Result is { } single ? theme.RowFor(single.State) : theme.Header;
        DeckTheme.Row(row, theme, color);
    }

    private void ApplyInterfaceLanguage()
    {
        subtitle.Text = T("Transfira traduções com segurança e preserve configurações dos mods.", "Safely transfer translations and save mod settings.");
        if (libraryLabel is not null) libraryLabel.Text = T("Biblioteca de traduções:", "Translation library:");
        if (configLibraryLabel is not null) configLibraryLabel.Text = T("Biblioteca de configurações:", "Settings library:");
        if (modsLabel is not null) modsLabel.Text = T("Pasta Mods do Stardew:", "Stardew Mods folder:");
        if (searchLabel is not null) searchLabel.Text = T("Pesquisar na lista:", "Search list:");
        if (interfaceLabel is not null) interfaceLabel.Text = T("Interface:", "Interface:");
        if (localeLabel is not null) localeLabel.Text = T("Idioma da tradução:", "Translation language:");
        search.Watermark = T("Digite o nome do mod, tradução ou UniqueID…", "Search for a mod, translation, or UniqueID…");
        if (analyzeButton is not null) analyzeButton.Content = T("Analisar", "Analyze");
        if (prepareButton is not null) prepareButton.Content = T("Procurar novos mods", "Find new mods");
        if (configsButton is not null) configsButton.Content = T("Backup de configurações", "Settings backup");
        if (applyButton is not null) applyButton.Content = T("Aplicar selecionadas", "Apply selected");
        if (undoButton is not null) undoButton.Content = T("Desfazer última aplicação", "Undo last application");
        foreach (var browse in browseButtons) browse.Content = T("Escolher…", "Browse…");
        if (paletteButton is not null) ToolTip.SetTip(paletteButton, T("Escolher tema", "Choose theme"));
        if (grid.Columns.Count == 5)
        {
            grid.Columns[0].Header = T("Aplicar", "Apply");
            grid.Columns[1].Header = T("Tradução", "Translation");
            grid.Columns[2].Header = T("Mod encontrado", "Matched mod");
            grid.Columns[3].Header = T("Situação", "Status");
            grid.Columns[4].Header = T("Detalhes", "Details");
        }
    }

    private async void Analyze(object? sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(library.Text) || !Directory.Exists(mods.Text)) { summary.Text = T("Para analisar traduções, escolha a biblioteca de traduções e a pasta Mods.", "To analyze translations, choose the translation library and Mods folder."); return; }
        try
        {
            installed = service.ScanMods(mods.Text!);
            results = service.Match(service.ScanLibrary(library.Text!), installed);
            LoadSavedMappings();
            RefreshMissing();
            RefreshRows();
        }
        catch (Exception ex) { summary.Text = T("Erro na análise: ", "Analysis failed: ") + ex.Message; }
        await Task.CompletedTask;
    }

    private async void Apply(object? sender, RoutedEventArgs e)
    {
        var selected = results.Where(x => x.Translation.Locale == SelectedLocale && x.Selected && x.Target is not null && x.State == MatchState.Safe).ToList();
        if (selected.Count == 0) return;
        if (!await ConfirmAsync(T("Confirmar aplicação", "Confirm application"),
            string.Format(T("Aplicar {0} tradução(ões) em {1}?\n\nTraduções existentes serão preservadas.",
                "Apply {0} translation(s) in {1}?\n\nExisting translations will be preserved."), selected.Count, SelectedLocale))) return;
        var backup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TradutorModsStardew", "Backups", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        var done = 0;
        lastApply = [];
        foreach (var result in selected) { try { lastApply.Add(service.Apply(result, backup)); result.State = MatchState.Applied; result.Selected = false; done++; } catch (Exception ex) { result.State = MatchState.Failed; result.Detail = ex.Message; } }
        RefreshRows(); summary.Text = string.Format(T("{0} tradução(ões) aplicada(s).", "{0} translation(s) applied."), done);
        await Task.CompletedTask;
    }

    private async void UndoLast(object? sender, RoutedEventArgs e)
    {
        if (lastApply.Count == 0) { summary.Text = T("Nenhuma aplicação desta sessão para desfazer.", "No application from this session to undo."); return; }
        if (!await ConfirmAsync(T("Confirmar", "Confirm"), T("Desfazer os arquivos aplicados na última operação?", "Undo the files applied in the last operation?"))) return;
        try { service.Undo(lastApply); lastApply = []; Analyze(this, e); }
        catch (Exception ex) { summary.Text = T("Não foi possível desfazer: ", "Could not undo: ") + ex.Message; }
    }

    private async void OpenConfigBackup(object? sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(mods.Text))
        {
            summary.Text = T("Escolha uma pasta Mods existente antes de abrir o backup de configurações.", "Choose an existing Mods folder before opening settings backup.");
            return;
        }
        try
        {
            if (!string.IsNullOrWhiteSpace(configLibrary.Text) && !Directory.Exists(configLibrary.Text) &&
                Path.GetFullPath(configLibrary.Text).Equals(Path.GetFullPath(ConfigBackupService.DefaultStorageRoot(library.Text!)), StringComparison.OrdinalIgnoreCase))
                Directory.CreateDirectory(configLibrary.Text);
        }
        catch (Exception ex) { summary.Text = T("Não foi possível abrir a biblioteca de configurações: ", "Could not open settings library: ") + ex.Message; return; }
        if (!Directory.Exists(configLibrary.Text))
        {
            summary.Text = T("Escolha uma pasta existente para a biblioteca de configurações.", "Choose an existing settings library folder.");
            return;
        }
        var window = new ConfigWindow(configLibrary.Text!, mods.Text!, service);
        await window.ShowDialog(this);
    }

    private async void PrepareNewMods(object? sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(library.Text) || !Directory.Exists(mods.Text))
        {
            summary.Text = T("Escolha duas pastas existentes antes de preparar os novos mods.", "Choose two existing folders before finding new mods.");
            return;
        }

        try
        {
            installed = service.ScanMods(mods.Text!);
            var report = service.PrepareLibrary(library.Text!, installed, SelectedLocale);
            var imported = service.ImportInstalledTranslations(library.Text!, installed, SelectedLocale);
            Analyze(this, e);
            summary.Text = string.Format(T("Busca concluída: {0} pasta(s), {1} referência(s) i18n, {2} tradução(ões) copiadas. Os mods instalados não foram alterados.",
                "Search complete: {0} folder(s), {1} i18n reference(s), {2} translation(s) copied. Installed mods were not changed."),
                report.CreatedFolders, report.CopiedI18nReferences, imported.Imported);
        }
        catch (Exception ex)
        {
            summary.Text = T("Erro ao preparar novos mods: ", "Could not find new mods: ") + ex.Message;
        }
        await Task.CompletedTask;
    }

    private void RefreshRows()
    {
        var term = search.Text?.Trim() ?? "";
        rows.Clear();
        var visible = results.Where(x => x.Translation.Locale == SelectedLocale &&
            (string.IsNullOrWhiteSpace(term) || (x.Translation.LibraryPackage + " " + x.Translation.ComponentName + " " + x.Target?.DisplayName + " " + x.Target?.UniqueId).Contains(term, StringComparison.CurrentCultureIgnoreCase)));
        foreach (var group in visible.GroupBy(x => x.Translation.LibraryPackage, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Min(x => StateOrder(x.State))).ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var members = group.OrderBy(x => x.Translation.RelativeComponentPath, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (members.Count == 1) rows.Add(new Row(members[0], false, ScheduleSelectionRefresh));
            else
            {
                rows.Add(new Row(group.Key, members, expandedPackages.Contains(group.Key), ScheduleSelectionRefresh));
                if (expandedPackages.Contains(group.Key))
                    foreach (var member in members) rows.Add(new Row(member, true, ScheduleSelectionRefresh));
            }
        }
        var safe = results.Count(x => x.Translation.Locale == SelectedLocale && x.State == MatchState.Safe);
        if (listDragHandle is not null) listDragHandle.IsVisible = installed.Count > 0;
        var existing = results.Count(x => x.Translation.Locale == SelectedLocale && x.State == MatchState.AlreadyTranslated);
        summary.Text = results.Count == 0 ? T("Para analisar traduções, escolha a biblioteca de traduções e a pasta Mods.", "To analyze translations, choose the translation library and Mods folder.")
            : T($"{installed.Count} mods • {safe} prontas • {existing} já traduzidas • {rows.Count} linhas exibidas",
                $"{installed.Count} mods • {safe} ready • {existing} already translated • {rows.Count} rows shown");
    }

    private void ScheduleSelectionRefresh() => Dispatcher.UIThread.Post(() =>
    {
        foreach (var row in rows) row.NotifySelectionChanged();
    }, DispatcherPriority.Background);

    private void RefreshMissing()
    {
        results.RemoveAll(result => result.State == MatchState.MissingTranslationFile);
        if (installed.Count > 0)
            results.AddRange(service.FindMissingTranslationFiles(installed, results, SelectedLocale));
    }

    private async Task OpenRowAsync(Row row)
    {
        if (row.PackageName is { } package)
        {
            var parentIndex = rows.IndexOf(row);
            if (parentIndex < 0) return;
            if (expandedPackages.Add(package))
            {
                row.Expanded = true;
                var children = row.Members!;
                for (var i = 0; i < children.Count; i++)
                    rows.Insert(parentIndex + 1 + i, new Row(children[i], true, ScheduleSelectionRefresh));
            }
            else
            {
                row.Expanded = false;
                for (var i = 0; i < row.Members!.Count; i++) rows.RemoveAt(parentIndex + 1);
            }
            return;
        }
        var result = row.Result;
        if (result is null) return;
        if (result.State == MatchState.MissingTranslationFile && result.Target is { } missing)
        {
            await ImportMissingAsync(missing);
            return;
        }
        var target = await PickTargetAsync(result);
        if (target is null) return;
        result.Target = target;
        if (service.HasTranslation(target, result.Translation))
        {
            result.State = MatchState.AlreadyTranslated;
            result.Selected = false;
            result.Detail = T("O componente escolhido já possui tradução neste idioma.", "The selected component already has a translation in this language.");
        }
        else
        {
            result.State = MatchState.Safe;
            result.Selected = true;
            result.Detail = T("Destino confirmado manualmente.", "Target confirmed manually.");
            SaveMapping(result);
        }
        RefreshMissing();
        RefreshRows();
    }

    private async Task<InstalledMod?> PickTargetAsync(MatchResult result)
    {
        var dialog = DialogWindow(T("Confirmar mod de destino", "Confirm target mod"), 1050, 650);
        var panel = new DockPanel { Margin = new Thickness(16) };
        var top = new StackPanel { Spacing = 8 };
        top.Children.Add(new TextBlock { Text = T("Origem: ", "Source: ") + result.Translation.SourcePath, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        var open = Button(T("Abrir origem no gerenciador de arquivos", "Open source in file manager"), "#1A617D");
        open.Click += (_, _) =>
        {
            var path = result.Translation.SourcePath;
            var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (folder is not null && Directory.Exists(folder))
                Process.Start(new ProcessStartInfo("xdg-open", folder) { UseShellExecute = false });
        };
        top.Children.Add(open);
        top.Children.Add(new TextBlock { Text = T("Escolha pelo caminho completo e UniqueID:", "Choose by full path and UniqueID:"), Foreground = Brushes.White });
        var filter = Field(result.Translation.ComponentName);
        top.Children.Add(filter);
        var destinationDetails = new TextBlock { Text = T("Selecione um componente para ver seu destino completo.", "Select a component to see its full destination."), TextWrapping = TextWrapping.Wrap, Foreground = Brush("#AEE0E7") };
        top.Children.Add(destinationDetails);
        DockPanel.SetDock(top, Dock.Top); panel.Children.Add(top);
        var picker = new ListBox { Background = Brush("#133A53"), Foreground = Brushes.White };
        picker.SelectionChanged += (_, _) => destinationDetails.Text = picker.SelectedItem is PickerItem chosen
            ? string.Format(T("Destino: {0}\nUniqueID: {1}", "Target: {0}\nUniqueID: {1}"), chosen.Mod.Directory, chosen.Mod.UniqueId)
            : T("Selecione um componente para ver seu destino completo.", "Select a component to see its full destination.");
        void Fill()
        {
            var query = filter.Text?.Trim() ?? "";
            picker.ItemsSource = installed.Where(mod => query.Length == 0 || mod.PickerName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .Select(mod => new PickerItem(mod)).ToList();
            picker.SelectedItem = (picker.ItemsSource as IEnumerable<PickerItem>)?.FirstOrDefault(item => item.Mod == result.Target);
        }
        filter.TextChanged += (_, _) => Fill();
        Fill();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button(T("Cancelar", "Cancel"), "#685BA6"); cancel.Click += (_, _) => dialog.Close((InstalledMod?)null);
        var confirm = Button(T("Confirmar", "Confirm"), "#286A58"); confirm.Click += (_, _) => dialog.Close((picker.SelectedItem as PickerItem)?.Mod);
        buttons.Children.Add(cancel); buttons.Children.Add(confirm);
        DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        panel.Children.Add(picker);
        dialog.Content = panel;
        return await dialog.ShowDialog<InstalledMod?>(this);
    }

    private async Task ImportMissingAsync(InstalledMod mod)
    {
        var kind = await ChooseImportKindAsync();
        if (kind == 0) return;
        var initialPath = Path.Combine(mod.Directory, "i18n");
        if (!Directory.Exists(initialPath)) initialPath = mod.Directory;
        var start = await StorageProvider.TryGetFolderFromPathAsync(new Uri(initialPath));
        string source;
        string? detected;
        if (kind == 1)
        {
            var chosen = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = string.Format(T("Escolha a pasta de tradução de {0}", "Choose the translation folder for {0}"), mod.DisplayName), AllowMultiple = false, SuggestedStartLocation = start
            });
            if (chosen.Count == 0) return;
            source = chosen[0].Path.LocalPath;
            detected = TranslationLocales.Normalize(Path.GetFileName(source));
        }
        else
        {
            var chosen = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = string.Format(T("Escolha o arquivo de tradução de {0}", "Choose the translation file for {0}"), mod.DisplayName), AllowMultiple = false, SuggestedStartLocation = start,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (chosen.Count == 0) return;
            source = chosen[0].Path.LocalPath;
            detected = TranslationLocales.FromFileName(source);
        }
        if (TranslationLocales.IsDefaultReference(source))
        {
            await ImportDefaultAsync(mod, source);
            return;
        }
        if (detected is not null && !TranslationLocales.SameTargetLanguage(detected, SelectedLocale))
        {
            await NoticeAsync(T("Idioma diferente", "Different language"),
                string.Format(T("A origem indica {0}, mas o idioma escolhido é {1}. Nada foi copiado.",
                    "The source indicates {0}, but the selected language is {1}. Nothing was copied."), detected, SelectedLocale));
            return;
        }
        if (detected is null && !await ConfirmAsync(T("Confirmar idioma", "Confirm language"),
            string.Format(T("O nome não identifica o idioma. Confirmar que esta tradução é em {0}?",
                "The name does not identify the language. Confirm that this translation is in {0}?"), SelectedLocale))) return;
        var language = detected ?? SelectedLocale;
        if (!await ConfirmAsync(T("Confirmar importação", "Confirm import"),
            string.Format(T("Guardar esta {0} na biblioteca?\n\nOrigem: {1}\nIdioma: {2}\n\nA origem não será alterada.",
                "Save this {0} to the library?\n\nSource: {1}\nLanguage: {2}\n\nThe source will not be changed."),
                kind == 1 ? T("pasta inteira", "entire folder") : T("tradução", "translation"), source, language))) return;
        try
        {
            if (kind == 1) service.ImportI18nDirectory(library.Text!, mod, source, language);
            else
            {
                var destination = service.GetImportDestination(library.Text!, mod, language);
                service.ImportI18nFile(library.Text!, mod, source, language, File.Exists(destination));
            }
            Analyze(this, new RoutedEventArgs());
            await NoticeAsync(T("Concluído", "Done"), T("A tradução foi guardada na biblioteca. A origem não foi alterada.", "The translation was saved to the library. The source was not changed."));
        }
        catch (Exception ex) { await NoticeAsync(T("Não foi possível importar", "Could not import"), ex.Message); }
    }

    private async Task ImportDefaultAsync(InstalledMod mod, string source)
    {
        if (!TranslationLocales.SameTargetLanguage(SelectedLocale, "en"))
        {
            await NoticeAsync(T("Idioma diferente", "Different language"), T("default é a referência em inglês. Selecione en (default) para guardá-la; nada foi copiado.",
                "default is the English reference. Select en (default) to save it; nothing was copied."));
            return;
        }
        if (!await ConfirmAsync(T("Confirmar importação", "Confirm import"),
            string.Format(T("Guardar {0} como referência em inglês?\n\nEla não será aplicada por cima do default de um mod atualizado.",
                "Save {0} as an English reference?\n\nIt will not be applied over an updated mod's default file."), source))) return;
        try
        {
            service.ImportDefaultReference(library.Text!, mod, source);
            Analyze(this, new RoutedEventArgs());
            await NoticeAsync(T("Concluído", "Done"), T("Referência em inglês salva na biblioteca. A origem não foi alterada.",
                "English reference saved to the library. The source was not changed."));
        }
        catch (Exception ex) { await NoticeAsync(T("Não foi possível importar", "Could not import"), ex.Message); }
    }

    private async Task<int> ChooseImportKindAsync()
    {
        var dialog = DialogWindow(T("Localizar tradução", "Find translation"), 620, 230);
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = T("Escolha onde está a tradução deste mod.\nO navegador começará na pasta i18n do mod, se ela existir.", "Choose where this mod's translation is stored.\nThe browser will start in the mod's i18n folder when available."), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var file = Button(T("Escolher arquivo JSON", "Choose JSON file"), "#1A617D"); file.Click += (_, _) => dialog.Close(2);
        var folder = Button(T("Escolher pasta do idioma", "Choose language folder"), "#286A58"); folder.Click += (_, _) => dialog.Close(1);
        var cancel = Button(T("Cancelar", "Cancel"), "#685BA6"); cancel.Click += (_, _) => dialog.Close(0);
        buttons.Children.Add(file); buttons.Children.Add(folder); buttons.Children.Add(cancel);
        panel.Children.Add(buttons); dialog.Content = panel;
        return await dialog.ShowDialog<int>(this);
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = DialogWindow(title, 600, 260);
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var no = Button(T("Cancelar", "Cancel"), "#685BA6"); no.Click += (_, _) => dialog.Close(false);
        var yes = Button(T("Confirmar", "Confirm"), "#286A58"); yes.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(no); buttons.Children.Add(yes); panel.Children.Add(buttons);
        dialog.Content = panel;
        return await dialog.ShowDialog<bool>(this);
    }

    private async Task NoticeAsync(string title, string message)
    {
        var dialog = DialogWindow(title, 600, 240);
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        var ok = Button(T("OK", "OK"), "#1A617D"); ok.HorizontalAlignment = HorizontalAlignment.Right; ok.Click += (_, _) => dialog.Close();
        panel.Children.Add(ok); dialog.Content = panel;
        await dialog.ShowDialog(this);
    }

    private static Window DialogWindow(string title, int width, int height) => new()
    {
        Title = title, Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = Brush("#0A1F35")
    };

    private sealed record PickerItem(InstalledMod Mod)
    {
        public override string ToString() => Mod.PickerName;
    }

    private async Task ChooseFolderAsync(TextBox field, string title)
    {
        var chosen = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        if (chosen.Count > 0)
        {
            field.Text = chosen[0].Path.LocalPath;
            if (ReferenceEquals(field, library) || ReferenceEquals(field, configLibrary)) SaveLibraries();
        }
    }

    private void SaveLibraries()
    {
        try { LibrarySelectionStore.Save(library.Text, configLibrary.Text); }
        catch (Exception ex) { summary.Text = T("Não foi possível salvar as bibliotecas: ", "Could not save library folders: ") + ex.Message; }
    }

    private void LoadSavedMappings()
    {
        var path = MappingPath();
        if (!File.Exists(path)) return;
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
            foreach (var result in results.Where(result => result.State is MatchState.NeedsConfirmation or MatchState.NoMatch))
            {
                if (!map.TryGetValue(MappingKey(result.Translation), out var id)) continue;
                var target = installed.FirstOrDefault(mod => mod.UniqueId.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (target is null) continue;
                var exact = installed.FirstOrDefault(mod => mod.RelativeFolder.Equals(result.Translation.RelativeComponentPath, StringComparison.OrdinalIgnoreCase));
                if (exact is not null && !exact.UniqueId.Equals(target.UniqueId, StringComparison.OrdinalIgnoreCase)) continue;
                result.Target = target;
                result.State = service.HasTranslation(target, result.Translation) ? MatchState.AlreadyTranslated : MatchState.Safe;
                result.Selected = result.State == MatchState.Safe;
                result.Detail = result.Selected
                    ? T("Destino lembrado de uma confirmação anterior.", "Target remembered from an earlier confirmation.")
                    : T("O destino já possui tradução neste idioma.", "The target already has a translation in this language.");
            }
        }
        catch { }
    }

    private void SaveMapping(MatchResult result)
    {
        if (result.Target is null || string.IsNullOrWhiteSpace(result.Target.UniqueId)) return;
        var path = MappingPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Dictionary<string, string> map = [];
        try { if (File.Exists(path)) map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? []; } catch { }
        map[MappingKey(result.Translation)] = result.Target.UniqueId;
        File.WriteAllText(path, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string MappingKey(TranslationUnit unit) => unit.LibraryPackage + "|" + unit.ComponentName + "|" + unit.TargetRelativePath;
    private static string MappingPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TradutorModsStardew", "associacoes.json");

    private static int StateOrder(MatchState state) => state switch
    {
        MatchState.NeedsConfirmation => 0,
        MatchState.MissingTranslationFile => 1,
        MatchState.Safe => 2,
        MatchState.Failed => 3,
        MatchState.AlreadyTranslated => 4,
        MatchState.NoMatch => 5,
        _ => 6
    };

    private TextBlock AddFolderField(Grid grid, int row, string label, TextBox field, Func<string> pickerTitle)
    {
        var text = AddField(grid, row, label, field);
        var choose = Button(T("Escolher…", "Browse…"), "#1A617D");
        choose.Margin = new Thickness(0, 7, 16, 7);
        choose.Click += async (_, _) => await ChooseFolderAsync(field, pickerTitle());
        browseButtons.Add(choose);
        Grid.SetRow(choose, row);
        Grid.SetColumn(choose, 2);
        grid.Children.Add(choose);
        return text;
    }

    private static TextBlock AddField(Grid grid, int row, string label, TextBox field)
    {
        var text = new TextBlock { Text = label, Foreground = Brush("#C2EFEB"), Margin = new Thickness(16, 10), VerticalAlignment = VerticalAlignment.Center };
        field.Margin = new Thickness(8, 7); Grid.SetRow(text, row); Grid.SetRow(field, row); Grid.SetColumn(field, 1); grid.Children.Add(text); grid.Children.Add(field);
        return text;
    }

    private static TextBlock AddChoice(Grid grid, int row, string label, ComboBox choice, int labelColumn = 0, int choiceColumn = 1)
    {
        var text = new TextBlock { Text = label, Foreground = Brush("#C2EFEB"), Margin = new Thickness(16, 10), VerticalAlignment = VerticalAlignment.Center };
        choice.Margin = new Thickness(8, 5);
        Grid.SetRow(text, row); Grid.SetRow(choice, row); Grid.SetColumn(text, labelColumn); Grid.SetColumn(choice, choiceColumn);
        grid.Children.Add(text); grid.Children.Add(choice);
        return text;
    }

    private static string FindInitialModsPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "Stardew Valley", "Mods"),
            Path.Combine(home, ".steam", "steam", "steamapps", "common", "Stardew Valley", "Mods")
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }
    private static TextBox Field(string text = "") => new() { Text = text, Background = Brush("#0C293D"), Foreground = Brush("#E7F9F7"), BorderBrush = Brush("#4DAEC0") };
    private static Button Button(string text, string color) => new() { Content = text, Background = Brush(color), Foreground = Brushes.White, BorderBrush = Brush("#6CE0E6"), Padding = new Thickness(17, 8), FontWeight = FontWeight.SemiBold };
    private static IBrush Brush(string hex) => SolidColorBrush.Parse(hex);

    private sealed class Row : INotifyPropertyChanged
    {
        private readonly List<MatchResult>? members;
        private readonly bool child;
        private bool expanded;
        private readonly Action changed;
        public event PropertyChangedEventHandler? PropertyChanged;
        public MatchResult? Result { get; }
        public string? PackageName { get; }
        public IReadOnlyList<MatchResult>? Members => members;
        public bool Expanded
        {
            get => expanded;
            set { expanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Translation))); }
        }
        public Row(MatchResult result, bool child, Action changed) { Result = result; this.child = child; this.changed = changed; }
        public Row(string package, List<MatchResult> members, bool expanded, Action changed)
        { PackageName = package; this.members = members; this.expanded = expanded; this.changed = changed; }
        public bool Selected
        {
            get => Result is not null ? Result.Selected : members?.Any(item => item.State == MatchState.Safe) == true && members.Where(item => item.State == MatchState.Safe).All(item => item.Selected);
            set
            {
                if (Result is not null && Result.State == MatchState.Safe) Result.Selected = value;
                else if (members is not null)
                    foreach (var item in members.Where(item => item.State == MatchState.Safe)) item.Selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
                changed();
            }
        }
        public void NotifySelectionChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail)));
        }
        public string Translation => PackageName is not null ? (expanded ? "▼ " : "▶ ") + PackageName
            : child ? "    ↳ " + Result!.Translation.ComponentName
            : Result!.Translation.DisplayName;
        public string Target => PackageName is not null ? T($"{members!.Count} componentes", $"{members!.Count} components")
            : Result!.Target is { } mod ? $"{mod.DisplayName} › {mod.RelativeFolder}" : "—";
        public string Status => PackageName is not null ? T($"{members!.Count(item => item.State == MatchState.Safe)} prontas", $"{members!.Count(item => item.State == MatchState.Safe)} ready") : Result!.State switch
        {
            MatchState.Safe => T("Pronta", "Ready"), MatchState.AlreadyTranslated => T("Já traduzido", "Already translated"), MatchState.NeedsConfirmation => T("Confirmar destino", "Confirm target"),
            MatchState.MissingTranslationFile => T("Arquivo de tradução ausente", "Missing translation file"), MatchState.Applied => T("Aplicada", "Applied"), MatchState.Failed => T("Falhou", "Failed"), _ => T("Não encontrada", "Not found")
        };
        public string Detail => PackageName is not null
            ? T($"{members!.Count(item => item.Selected && item.State == MatchState.Safe)} selecionadas • {members!.Count(item => item.State == MatchState.NeedsConfirmation)} a confirmar",
                $"{members!.Count(item => item.Selected && item.State == MatchState.Safe)} selected • {members!.Count(item => item.State == MatchState.NeedsConfirmation)} to review")
            : Result!.Detail;
    }
}
