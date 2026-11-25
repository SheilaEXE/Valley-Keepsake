using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Collections.ObjectModel;
using TradutorModsStardew;

namespace TradutorModsDeck;

internal sealed class ConfigWindow : Window
{
    private static string T(string pt, string en) => UiLanguage.Text(pt, en);
    private readonly string libraryRoot;
    private readonly string modsRoot;
    private readonly TranslationService modService;
    private readonly ConfigBackupService configService = new();
    private readonly ObservableCollection<Row> rows = [];
    private readonly DeckGridColumnSizer columnSizer = new();
    private readonly DataGrid grid;
    private readonly TextBlock summary = new() { Foreground = Brush("#AEE0E7"), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Bold };
    private readonly Button restoreSelectedButton = Button(T("Restaurar selecionados", "Restore selected"), "#286A58");
    private List<ConfigResult> results = [];
    private readonly AppTheme theme = ThemeStore.Current;

    public ConfigWindow(string libraryRoot, string modsRoot, TranslationService modService)
    {
        this.libraryRoot = libraryRoot;
        this.modsRoot = modsRoot;
        this.modService = modService;
        Title = T("Backup de configurações dos mods", "Mod settings backup");
        Icon = new WindowIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.png"));
        Width = theme.Kind switch { ThemeKind.Parchment or ThemeKind.Garden => 1180, ThemeKind.Wood or ThemeKind.Mine => 1120, _ => 1080 };
        Height = theme.Kind switch { ThemeKind.Parchment or ThemeKind.Garden => 740, ThemeKind.Wood or ThemeKind.Mine => 720, _ => 670 };
        MinWidth = theme.Kind switch { ThemeKind.Parchment or ThemeKind.Garden => 980, ThemeKind.Wood or ThemeKind.Mine => 930, _ => 820 };
        MinHeight = theme.Kind switch { ThemeKind.Parchment or ThemeKind.Garden => 640, ThemeKind.Wood or ThemeKind.Mine => 610, _ => 510 };
        Background = DeckTheme.Brush(theme.Background);
        var root = new Grid { Margin = new Thickness(theme.Kind switch { ThemeKind.Parchment => 84, ThemeKind.Wood => 64, ThemeKind.Mine => 56, ThemeKind.Garden => 72, _ => 20 }), RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        root.Children.Add(new StackPanel { Spacing = 4, Children =
        {
            new TextBlock { Text = T("Backup de configurações dos mods", "Mod settings backup"), FontSize = 22, FontWeight = FontWeight.Bold, Foreground = DeckTheme.Brush(theme.Title) },
            new TextBlock { Text = T("Salve seus config.json antes de atualizar. A configuração atual é guardada antes de cada restauração.", "Save config.json files before updating. Current settings are backed up before each restore."), TextWrapping = TextWrapping.Wrap, Foreground = DeckTheme.Brush(theme.MutedText), FontWeight = FontWeight.Bold }
        }});
        grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brush("#103148"), Foreground = Brushes.White, BorderBrush = Brush("#1A617D") };
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = T("Restaurar", "Restore"), Binding = new Binding("Selected"), MinWidth = 76 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Mod", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 280 });
        grid.Columns.Add(new DataGridTextColumn { Header = "UniqueID", Binding = new Binding("UniqueId"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 300 });
        grid.Columns.Add(new DataGridTextColumn { Header = T("Situação", "Status"), Binding = new Binding("Status"), Width = new DataGridLength(190), MinWidth = 190 });
        grid.Columns.Add(new DataGridTextColumn { Header = T("Detalhes", "Details"), Binding = new Binding("Detail"), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star), MinWidth = 540 });
        DeckTheme.Grid(grid, theme);
        grid.LoadingRow += (_, args) => PaintRow(args.Row);
        var listFrame = new Border { Child = grid, BorderThickness = new Thickness(3), Padding = new Thickness(3), Margin = new Thickness(0, 18, 0, 0), BorderBrush = DeckTheme.Brush(theme.BorderDark), Background = DeckTheme.Brush(theme.Row) };
        Grid.SetRow(listFrame, 1); root.Children.Add(listFrame);
        var footer = new Grid { Margin = new Thickness(0, 14, 0, 0), RowDefinitions = new RowDefinitions("Auto,Auto") };
        footer.Children.Add(summary);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var refresh = Button(T("Atualizar lista", "Refresh list"), "#685BA6"); refresh.Click += (_, _) => LoadResults();
        var export = Button(T("Exportar todos os configs", "Export all settings"), "#1A617D"); export.Click += ExportAll;
        restoreSelectedButton.IsEnabled = false;
        restoreSelectedButton.Click += RestoreSelected;
        var restoreAll = Button(T("Restaurar tudo", "Restore all"), "#286A58"); restoreAll.Click += RestoreAll;
        var rose = System.Drawing.Color.FromArgb(243, 145, 182);
        var neutral = theme.IsLight ? theme.BorderDark : theme.BorderLight;
        DeckTheme.Button(refresh, theme, theme.Kind == ThemeKind.Garden ? rose : neutral);
        DeckTheme.Button(export, theme, theme.Kind == ThemeKind.Garden ? rose : neutral);
        DeckTheme.Button(restoreSelectedButton, theme, theme.Kind == ThemeKind.Garden ? rose : theme.ScrollThumb);
        DeckTheme.Button(restoreAll, theme, theme.Kind == ThemeKind.Garden ? rose : theme.ScrollThumb);
        ToolTip.SetTip(refresh, T("Procura config.json novos nos mods e atualiza a lista; não cria backup automaticamente.", "Finds new mod config.json files and refreshes the list; it does not create backups automatically."));
        ToolTip.SetTip(export, T("Guarda uma cópia dos config.json dos mods na biblioteca.", "Saves copies of the mods' config.json files in the library."));
        ToolTip.SetTip(restoreSelectedButton, T("Restaura os configs marcados, pedindo confirmação para cada mod.", "Restores checked configs, asking for confirmation for each mod."));
        ToolTip.SetTip(restoreAll, T("Restaura todos os configs ausentes ou diferentes com uma confirmação única. Os arquivos atuais substituídos são guardados antes.", "Restores all missing or different configs with one confirmation. Existing files are backed up first."));
        actions.Children.Add(refresh); actions.Children.Add(export); actions.Children.Add(restoreSelectedButton); actions.Children.Add(restoreAll);
        Grid.SetRow(actions, 1); footer.Children.Add(actions);
        Grid.SetRow(footer, 2); root.Children.Add(footer);
        var page = new Grid();
        page.Children.Add(new DeckFrame { BackgroundOnly = true });
        page.Children.Add(root);
        var outerFrame = new DeckFrame(); outerFrame.ApplyTheme(theme);
        page.Children.Add(outerFrame);
        DeckWindowChrome.Attach(this, page, theme);
        summary.Foreground = DeckTheme.Brush(theme.MutedText);
        LoadResults();
    }

    private void PaintRow(DataGridRow row)
    {
        if (row.DataContext is Row result)
            DeckTheme.Row(row, theme, theme.ConfigRowFor(result.State));
    }

    private void LoadResults()
    {
        try
        {
            results = configService.Compare(libraryRoot, modService.ScanMods(modsRoot));
            rows.Clear();
            foreach (var result in results) rows.Add(new Row(result, UpdateRestoreSelectedButton));
            columnSizer.Fit(grid, 1, rows.Select(row => row.Name), 280);
            columnSizer.Fit(grid, 2, rows.Select(row => row.UniqueId), 300);
            columnSizer.Fit(grid, 3, rows.Select(row => row.Status), 190);
            columnSizer.Fit(grid, 4, rows.Select(row => row.Detail), 540);
            summary.Text = T($"{results.Count} mods encontrados • {results.Count(x => x.State == ConfigState.ReadyToRestore)} configuração(ões) prontas para restaurar",
                $"{results.Count} mods found • {results.Count(x => x.State == ConfigState.ReadyToRestore)} settings ready to restore");
            UpdateRestoreSelectedButton();
        }
        catch (Exception ex)
        {
            restoreSelectedButton.IsEnabled = false;
            summary.Text = T("Erro ao ler as configurações: ", "Could not read settings: ") + ex.Message;
        }
    }

    private void UpdateRestoreSelectedButton() =>
        restoreSelectedButton.IsEnabled = results.Any(result => result.Selected && result.State == ConfigState.ReadyToRestore);

    private async void ExportAll(object? sender, RoutedEventArgs e)
    {
        try
        {
            var count = configService.ExportAll(libraryRoot, modService.ScanMods(modsRoot));
            LoadResults();
            await NoticeAsync(T("Backup concluído", "Backup complete"), T($"{count} config.json salvo(s) em:\n{configService.GetStorageRoot(libraryRoot)}",
                $"{count} config.json file(s) saved in:\n{configService.GetStorageRoot(libraryRoot)}"));
        }
        catch (Exception ex) { await NoticeAsync(T("Não foi possível exportar", "Could not export"), ex.Message); }
    }

    private async void RestoreSelected(object? sender, RoutedEventArgs e)
    {
        var selected = results.Where(x => x.Selected && x.State == ConfigState.ReadyToRestore).ToList();
        var restored = 0;
        foreach (var result in selected)
        {
            var answer = await ConfirmAsync(T("Confirmar restauração", "Confirm restore"), T($"Restaurar a configuração salva de:\n{result.Mod.DisplayName}?\n\nA configuração atual será guardada em um backup datado antes da substituição.",
                $"Restore saved settings for:\n{result.Mod.DisplayName}?\n\nThe current settings will be backed up before replacement."));
            if (answer is null) break;
            if (answer != true) continue;
            try { configService.Restore(libraryRoot, result); restored++; }
            catch (Exception ex) { await NoticeAsync(T("Não foi possível restaurar ", "Could not restore ") + result.Mod.DisplayName, ex.Message); }
        }
        LoadResults();
        if (restored > 0) await NoticeAsync(T("Concluído", "Done"), T($"{restored} configuração(ões) restaurada(s).", $"{restored} settings restored."));
    }

    private async void RestoreAll(object? sender, RoutedEventArgs e)
    {
        LoadResults();
        var pending = results.Where(x => x.State == ConfigState.ReadyToRestore).ToList();
        if (pending.Count == 0)
        {
            await NoticeAsync(T("Nada para restaurar", "Nothing to restore"), T("Não há configurações diferentes ou ausentes para restaurar.", "There are no missing or different settings to restore."));
            return;
        }

        var existing = pending.Count(x => File.Exists(Path.Combine(x.Mod.Directory, "config.json")));
        var confirmed = await ConfirmAsync(T("Confirmar restauração de tudo", "Confirm restore all"),
            string.Format(T("Restaurar {0} configuração(ões) salva(s)?\n\n{1} arquivo(s) atual(is) diferente(s) serão guardados em backup datado antes da substituição. Os demais configs ausentes serão criados.",
                "Restore {0} saved configuration(s)?\n\n{1} different existing file(s) will be saved in a dated backup before replacement. Missing configs will be created."), pending.Count, existing),
            confirmAll: true);
        if (confirmed != true) return;

        var restored = 0;
        var failures = new List<string>();
        foreach (var result in pending)
        {
            try { configService.Restore(libraryRoot, result); restored++; }
            catch (Exception ex) { failures.Add($"{result.Mod.DisplayName}: {ex.Message}"); }
        }
        LoadResults();
        var message = string.Format(T("{0} configuração(ões) restaurada(s). {1} falha(s).", "{0} configuration(s) restored. {1} failure(s)."), restored, failures.Count);
        if (failures.Count > 0) message += "\n\n" + string.Join("\n", failures.Take(10));
        await NoticeAsync(T("Restauração concluída", "Restore complete"), message);
    }

    private Task NoticeAsync(string title, string message) => ConfirmAsync(title, message, false);

    private async Task<bool?> ConfirmAsync(string title, string message, bool showChoices = true, bool confirmAll = false)
    {
        var dialog = new Window { Title = title, Icon = new WindowIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.png")), Width = 540, MinHeight = 210, SizeToContent = SizeToContent.Height, CanResize = false, Background = DeckTheme.Brush(theme.Background), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(22), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = DeckTheme.Brush(theme.Text), FontSize = 15 });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        if (showChoices)
        {
            var yes = Button(T("Restaurar", "Restore"), "#286A58"); yes.Click += (_, _) => dialog.Close(true);
            DeckTheme.Button(yes, theme, theme.ScrollThumb);
            if (confirmAll)
            {
                var cancelAll = Button(T("Cancelar", "Cancel"), "#7A4147"); cancelAll.Click += (_, _) => dialog.Close(false);
                DeckTheme.Button(cancelAll, theme, theme.BorderLight);
                buttons.Children.Add(cancelAll); buttons.Children.Add(yes);
            }
            else
            {
                var no = Button(T("Pular", "Skip"), "#685BA6"); no.Click += (_, _) => dialog.Close(false);
                var cancel = Button(T("Parar", "Stop"), "#7A4147"); cancel.Click += (_, _) => dialog.Close(null);
                DeckTheme.Button(no, theme, theme.BorderLight);
                DeckTheme.Button(cancel, theme, theme.BorderLight);
                buttons.Children.Add(cancel); buttons.Children.Add(no); buttons.Children.Add(yes);
            }
        }
        else
        {
            var ok = Button("OK", "#1A617D"); ok.Click += (_, _) => dialog.Close(true); buttons.Children.Add(ok);
            DeckTheme.Button(ok, theme, theme.ScrollThumb);
        }
        panel.Children.Add(buttons);
        DeckWindowChrome.Attach(dialog, new Border { Child = panel, Margin = new Thickness(12), BorderThickness = new Thickness(3),
            BorderBrush = DeckTheme.Brush(theme.BorderDark), Background = DeckTheme.Brush(theme.Panel) }, theme);
        return await dialog.ShowDialog<bool?>(this);
    }

    private static Button Button(string text, string color) => new() { Content = text, Background = Brush(color), Foreground = Brushes.White, BorderBrush = Brush("#6CE0E6"), Padding = new Thickness(17, 8), FontWeight = FontWeight.SemiBold };
    private static IBrush Brush(string hex) => SolidColorBrush.Parse(hex);

    private sealed class Row(ConfigResult result, Action selectionChanged)
    {
        public ConfigState State => result.State;
        public bool Selected { get => result.Selected; set { result.Selected = value; selectionChanged(); } }
        public string Name => result.Mod.DisplayName;
        public string UniqueId => result.Mod.UniqueId;
        public string Detail => result.Detail;
        public string Status => result.State switch
        {
            ConfigState.ReadyToRestore => T("Confirmar restauração", "Confirm restore"),
            ConfigState.SameAsSaved => T("Já está salva", "Already saved"),
            ConfigState.NoSavedCopy => T("Sem cópia salva", "No saved copy"),
            _ => T("Falhou", "Failed")
        };
    }
}
