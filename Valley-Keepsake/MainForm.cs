using System.Diagnostics;
using System.Drawing.Text;
using System.Text.Json;

namespace TradutorModsStardew;

internal sealed class MainForm : Form
{
    private static string T(string pt, string en) => UiLanguage.Text(pt, en);
    private static bool HasArtworkFrame(ThemeKind kind) => kind is ThemeKind.Parchment or ThemeKind.Wood or ThemeKind.Garden;
    private readonly TranslationService service = new();
    private readonly PrivateFontCollection titleFonts = new();
    private readonly TextBox libraryBox = StyledTextBox();
    private readonly TextBox configLibraryBox = StyledTextBox();
    private readonly TextBox modsBox = StyledTextBox();
    private readonly TextBox searchBox = StyledTextBox(T("Digite o nome do mod, tradução ou UniqueID…", "Search for a mod, translation, or UniqueID…"));
    private readonly DataGridView grid = new();
    private readonly Label summary = new() { AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly GlowButton applyButton = new() { Enabled = false, AccentColor = Color.FromArgb(161, 232, 74) };
    private readonly GlowButton undoButton = new() { AccentColor = Color.FromArgb(239, 91, 96) };
    private readonly GlowButton analyzeButton = new() { AccentColor = Color.FromArgb(108, 224, 230) };
    private readonly GlowButton prepareButton = new() { AccentColor = Color.FromArgb(161, 232, 74) };
    private readonly GlowButton configsButton = new() { AccentColor = Color.FromArgb(255, 198, 82) };
    private readonly Label help = new() { Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly Label libraryLabel = new();
    private readonly Label configLibraryLabel = new();
    private readonly Label modsLabel = new();
    private readonly Label searchLabel = new();
    private readonly GlowButton libraryBrowse = new() { AccentColor = Color.FromArgb(161, 232, 74), Margin = new Padding(0, 3, 0, 3) };
    private readonly GlowButton configLibraryBrowse = new() { AccentColor = Color.FromArgb(161, 232, 74), Margin = new Padding(0, 3, 0, 3) };
    private readonly GlowButton modsBrowse = new() { AccentColor = Color.FromArgb(161, 232, 74), Margin = new Padding(0, 3, 0, 3) };
    private readonly ThemedScrollBar verticalScroll = new(Orientation.Vertical) { Dock = DockStyle.Right, Width = 18, Visible = false };
    private readonly ThemedScrollBar horizontalScroll = new(Orientation.Horizontal) { Dock = DockStyle.Bottom, Height = 18, Visible = false };
    private bool updatingScrollBars;
    private readonly ThemeSelector languagePicker = new() { Width = 160 };
    private readonly ThemeSelector localePicker = new() { Width = 160 };
    private readonly ThemeSelector themePicker = new() { Width = 160 };
    private readonly ThemePaletteButton themeButton = new();
    private readonly Label interfaceLanguageLabel = new() { ForeColor = Color.FromArgb(174, 223, 231), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly Label translationLanguageLabel = new() { ForeColor = Color.FromArgb(174, 223, 231), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly ToolTip toolTips = new() { AutoPopDelay = 12000, InitialDelay = 450, ReshowDelay = 150 };
    private readonly CancellationTokenSource updateCheckCancellation = new();
    private GardenLeaves gardenLeaves = null!;
    private ThemeTitleLabel titleLabel = null!;
    private TableLayoutPanel pathsPanel = null!;
    private TableLayoutPanel footerPanel = null!;
    private TableLayoutPanel headerPanel = null!;
    private Panel bodyPanel = null!;
    private Panel gridHost = null!;
    private FlowLayoutPanel actionsPanel = null!;
    private FlowLayoutPanel titleRow = null!;
    private FlowLayoutPanel interfaceRow = null!;
    private FlowLayoutPanel translationRow = null!;
    private JunimoAnimation junimos = null!;
    private SlimeAnimation slimes = null!;
    private List<InstalledMod> mods = [];
    private List<MatchResult> results = [];
    private readonly HashSet<string> expandedPackages = new(StringComparer.OrdinalIgnoreCase);
    private bool populatingGrid;
    private List<ApplyRecord> lastApply = [];
    private string? lastBackup;

    public MainForm()
    {
        Text = "Valley Keepsake";
        HandleCreated += (_, _) => WindowsTitleBar.Apply(this, ThemeStore.Current);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
        {
            try { Icon = new Icon(iconPath); }
            catch { }
        }
        MinimumSize = new Size(1120, 740);
        Size = new Size(1400, 850);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(10, 31, 53);

        titleLabel = new ThemeTitleLabel
        {
            Text = "Valley Keepsake",
            Font = LoadTitleFont(),
            AutoSize = true,
            ForeColor = Color.FromArgb(161, 232, 74),
            Margin = new Padding(0, 0, 0, 4)
        };
        help.AutoSize = true;
        help.ForeColor = Color.FromArgb(174, 223, 231);
        help.Margin = new Padding(0, 0, 0, 16);

        pathsPanel = new BufferedTableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 4, BackColor = Color.FromArgb(19, 58, 83), Padding = new Padding(14, 8, 14, 8) };
        var paths = pathsPanel;
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddPathRow(paths, 0, libraryLabel, libraryBox, libraryBrowse, () => Browse(libraryBox));
        AddPathRow(paths, 1, configLibraryLabel, configLibraryBox, configLibraryBrowse, BrowseConfigLibrary);
        AddPathRow(paths, 2, modsLabel, modsBox, modsBrowse, () => Browse(modsBox));
        searchLabel.ForeColor = Color.FromArgb(194, 239, 235);
        searchLabel.Anchor = AnchorStyles.Left;
        searchLabel.AutoSize = true;
        searchLabel.Margin = new Padding(0, 9, 8, 5);
        paths.Controls.Add(searchLabel, 0, 3);
        paths.Controls.Add(FrameTextBox(searchBox, new Padding(0, 5, 8, 5)), 1, 3);
        var savedLibraries = LibrarySelectionStore.Load();
        libraryBox.Text = savedLibraries.Translation;
        configLibraryBox.Text = savedLibraries.Configuration;
        libraryBox.Leave += (_, _) => SaveLibraries();
        configLibraryBox.Leave += (_, _) => SaveLibraries();
        FormClosing += (_, _) => SaveLibraries();
        modsBox.Text = GuessModsPath() ?? "";

        ConfigureGrid();
        analyzeButton.Click += (_, _) => Analyze();
        prepareButton.Click += (_, _) => SearchNewMods();
        applyButton.Click += (_, _) => ApplySelected();
        undoButton.Click += (_, _) => UndoLast();
        configsButton.Click += (_, _) => OpenConfigBackup();
        searchBox.TextChanged += (_, _) => PopulateGrid();

        actionsPanel = new BufferedFlowLayoutPanel
        {
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0)
        };
        var actions = actionsPanel;
        actions.Controls.Add(analyzeButton);
        actions.Controls.Add(prepareButton);
        actions.Controls.Add(configsButton);
        actions.Controls.Add(applyButton);
        actions.Controls.Add(undoButton);
        Control? disabledTipTarget = null;
        actions.MouseMove += (_, e) =>
        {
            var target = actions.GetChildAtPoint(e.Location);
            if (target is not null && !target.Enabled && target != disabledTipTarget)
            {
                disabledTipTarget = target;
                toolTips.Show(toolTips.GetToolTip(target), actions, e.Location.X + 12, e.Location.Y - 28, 5000);
            }
            else if (target is null)
            {
                disabledTipTarget = null;
            }
        };
        actions.MouseLeave += (_, _) => { disabledTipTarget = null; toolTips.Hide(actions); };
        summary.ForeColor = Color.FromArgb(174, 223, 231);
        actions.Dock = DockStyle.Fill;
        actions.WrapContents = true;
        footerPanel = new BufferedTableLayoutPanel { Dock = DockStyle.Bottom, Height = 132, ColumnCount = 2, RowCount = 2, BackColor = Color.FromArgb(10, 31, 53), Padding = new Padding(0, 8, 0, 4) };
        var footer = footerPanel;
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.Controls.Add(summary, 0, 0);
        footer.SetColumnSpan(summary, 2);
        footer.Controls.Add(actions, 0, 1);
        junimos = new JunimoAnimation { Dock = DockStyle.Fill, Margin = Padding.Empty };
        footer.Controls.Add(junimos, 1, 1);
        slimes = new SlimeAnimation { Dock = DockStyle.Fill, Margin = Padding.Empty };
        footer.Controls.Add(slimes, 1, 1);

        bodyPanel = new BufferedPanel { Dock = DockStyle.Fill, Padding = new Padding(22), BackColor = Color.FromArgb(10, 31, 53) };
        bodyPanel.Resize += (_, _) => { PositionArtJunimos(); PositionArtSlimes(); };
        headerPanel = new BufferedTableLayoutPanel { Dock = DockStyle.Top, Height = 82, ColumnCount = 2, RowCount = 2, BackColor = Color.FromArgb(10, 31, 53) };
        var body = bodyPanel;
        var header = headerPanel;
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        titleRow = new BufferedFlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        titleLabel.Margin = new Padding(0, 0, 18, 0);
        titleRow.Controls.Add(titleLabel);
        header.Controls.Add(titleRow, 0, 0);
        header.Controls.Add(help, 0, 1);
        languagePicker.Items.AddRange(["English", "Português (Brasil)"]);
        languagePicker.SelectedIndex = UiLanguage.Portuguese ? 1 : 0;
        languagePicker.SelectedIndexChanged += (_, _) =>
        {
            try { UiLanguage.Set(languagePicker.SelectedIndex == 1); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, T("Não foi possível salvar o idioma", "Could not save language"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                languagePicker.SelectedIndex = UiLanguage.Portuguese ? 1 : 0;
                return;
            }
            ApplyLanguage();
            // Rebuild analysis details while keeping the user's current row selections.
            if (results.Count > 0)
            {
                var selectedKeys = results.Where(result => result.Selected).Select(result => MappingKey(result.Translation)).ToHashSet();
                Analyze();
                foreach (var result in results.Where(result => result.State == MatchState.Safe))
                    result.Selected = selectedKeys.Contains(MappingKey(result.Translation));
                PopulateGrid();
                UpdateSummary();
            }
            else UpdateSummary();
        };
        interfaceRow = new BufferedFlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        interfaceRow.Controls.Add(interfaceLanguageLabel);
        interfaceRow.Controls.Add(languagePicker);
        themePicker.Visible = false;
        interfaceRow.Controls.Add(themePicker);
        interfaceRow.Controls.Add(themeButton);
        themeButton.Click += (_, _) => themePicker.ShowDropDown(themeButton);
        header.Controls.Add(interfaceRow, 1, 0);
        RefreshLocales();
        translationRow = new BufferedFlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        translationRow.Controls.Add(translationLanguageLabel);
        translationRow.Controls.Add(localePicker);
        header.Controls.Add(translationRow, 1, 1);
        gridHost = new BufferedPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 14, 0, 0), BackColor = Color.FromArgb(10, 31, 53) };
        gridHost.Controls.Add(grid);
        gridHost.Controls.Add(verticalScroll);
        gridHost.Controls.Add(horizontalScroll);
        body.Controls.Add(gridHost);
        body.Controls.Add(footer);
        body.Controls.Add(paths);
        body.Controls.Add(header);
        Controls.Add(body);
        gardenLeaves = new GardenLeaves(() => body.ClientSize, InvalidateGardenBackground);
        body.Paint += (_, e) => ThemeFramePainter.DrawFrame(e.Graphics, body.ClientRectangle, ThemeStore.Current,
            true, graphics => gardenLeaves.Draw(graphics, body, body));
        paths.Paint += (_, e) => ThemeFramePainter.DrawFrame(e.Graphics, paths.ClientRectangle, ThemeStore.Current);
        gridHost.Paint += (_, e) => ThemeFramePainter.DrawGridFrame(e.Graphics, gridHost.ClientRectangle, ThemeStore.Current);
        header.Paint += (_, e) => gardenLeaves.Draw(e.Graphics, header, body);
        footer.Paint += (_, e) => gardenLeaves.Draw(e.Graphics, footer, body);
        foreach (var surface in new[] { actions, titleRow, interfaceRow, translationRow })
            surface.Paint += (_, e) => gardenLeaves.Draw(e.Graphics, surface, body);
        Shown += (_, _) => { UpdateScrollBars(); gardenLeaves.Reset(); };
        Shown += async (_, _) => await CheckForUpdatesAsync();
        FormClosed += (_, _) => { updateCheckCancellation.Cancel(); gardenLeaves.Dispose(); };
        ApplyLanguage();
        ApplyTheme();
    }

    private void ApplyLanguage()
    {
        help.Text = T("Transfira traduções com segurança e preserve configurações dos mods para futuras atualizações.", "Safely transfer translations and save mod settings before future updates.");
        interfaceLanguageLabel.Text = T("Interface:", "Interface:");
        translationLanguageLabel.Text = T("Idioma da tradução:", "Translation language:");
        foreach (var (label, picker) in new[] { (interfaceLanguageLabel, languagePicker), (translationLanguageLabel, localePicker) })
        {
            label.Width = TextRenderer.MeasureText(label.Text, label.Font).Width;
            label.Height = picker.Height;
        }
        themeButton.AccessibleName = T("Escolher tema", "Choose theme");
        RefreshThemeNames();
        libraryLabel.Text = T("Biblioteca de traduções:", "Translation library:");
        configLibraryLabel.Text = T("Biblioteca de configurações:", "Settings library:");
        modsLabel.Text = T("Pasta Mods do Stardew:", "Stardew Mods folder:");
        searchLabel.Text = T("Pesquisar na lista:", "Search list:");
        searchBox.PlaceholderText = T("Digite o nome do mod, tradução ou UniqueID…", "Search for a mod, translation, or UniqueID…");
        libraryBrowse.Text = configLibraryBrowse.Text = modsBrowse.Text = T("Escolher…", "Browse…");
        analyzeButton.Text = T("Analisar", "Analyze");
        prepareButton.Text = T("Procurar novos mods", "Find new mods");
        configsButton.Text = T("Backup de configurações", "Settings backup");
        applyButton.Text = T("Aplicar selecionadas", "Apply selected");
        undoButton.Text = T("Desfazer última aplicação", "Undo last application");
        grid.Columns["Use"]!.HeaderText = T("Aplicar", "Apply");
        grid.Columns["Translation"]!.HeaderText = T("Tradução", "Translation");
        grid.Columns["Target"]!.HeaderText = T("Mod encontrado", "Matched mod");
        grid.Columns["Status"]!.HeaderText = T("Situação", "Status");
        grid.Columns["Detail"]!.HeaderText = T("Detalhes", "Details");
        summary.Text = T("Para analisar traduções, escolha a biblioteca de traduções e a pasta Mods.", "To analyze translations, choose the translation library and Mods folder.");
        toolTips.SetToolTip(analyzeButton, T("Procura traduções na biblioteca e compara com os mods instalados.", "Finds library translations and matches them to installed mods."));
        toolTips.SetToolTip(prepareButton, T("Procura novos mods, guarda referências e copia traduções do idioma escolhido que ainda não estão na biblioteca.", "Finds new mods, saves references, and copies selected-language translations not yet in the library."));
        toolTips.SetToolTip(configsButton, T("Salva e restaura arquivos config.json dos mods.", "Back up and restore mod config.json files."));
        toolTips.SetToolTip(applyButton, T("Aplica aos mods as traduções marcadas para o idioma selecionado.", "Applies checked translations for the selected language to the mods."));
        toolTips.SetToolTip(undoButton, T("Desfaz os arquivos aplicados na última operação desta sessão.", "Undoes files applied in the last operation of this session."));
        toolTips.SetToolTip(libraryBrowse, T("Escolha onde sua biblioteca de traduções fica guardada.", "Choose where your translation library is stored."));
        toolTips.SetToolTip(configLibraryBrowse, T("Escolha diretamente a pasta que guarda os backups de config.json.", "Choose the folder that directly contains the config.json backups."));
        toolTips.SetToolTip(modsBrowse, T("Escolha a pasta Mods da instalação do Stardew Valley.", "Choose your Stardew Valley installation's Mods folder."));
        toolTips.SetToolTip(languagePicker, T("Idioma da interface do programa.", "Program interface language."));
        toolTips.SetToolTip(localePicker, T("Idioma das traduções exibidas e aplicadas; um de cada vez.", "Language of the translations shown and applied, one at a time."));
        toolTips.SetToolTip(themePicker, T("Muda as cores e molduras do aplicativo. Sua escolha fica salva.", "Changes the app colors and pixel frames. Your choice is saved."));
        toolTips.SetToolTip(themeButton, T("Escolher tema. Sua escolha fica salva.", "Choose a theme. Your choice is saved."));
        toolTips.SetToolTip(searchBox, T("Filtra a lista por nome do mod, tradução ou UniqueID.", "Filters the list by mod name, translation, or UniqueID."));
        toolTips.SetToolTip(grid, T("Clique duas vezes para confirmar um destino incerto ou procurar um arquivo de tradução ausente.", "Double-click to confirm an uncertain target or browse for a missing translation file."));
        PopulateGrid();
    }

    private void RefreshThemeNames()
    {
        themePicker.SelectedIndexChanged -= ThemeChanged;
        themePicker.Items.Clear();
        foreach (var kind in Enum.GetValues<ThemeKind>())
            themePicker.Items.Add(ThemeStore.Name(kind));
        themePicker.SelectedIndex = (int)ThemeStore.CurrentKind;
        themePicker.SelectedIndexChanged += ThemeChanged;
    }

    private void ThemeChanged(object? sender, EventArgs e)
    {
        if (themePicker.SelectedIndex < 0) return;
        try { ThemeStore.Set((ThemeKind)themePicker.SelectedIndex); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Não foi possível salvar o tema", "Could not save theme"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            RefreshThemeNames();
            return;
        }
        ApplyTheme();
    }

    private async Task CheckForUpdatesAsync()
    {
        var update = await UpdateChecker.CheckAsync(updateCheckCancellation.Token);
        if (update is null || IsDisposed || Disposing) return;

        var theme = ThemeStore.Current;
        using var dialog = new Form
        {
            Text = T("Atualização disponível", "Update available"),
            ClientSize = new Size(470, 155),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            Font = new Font("Segoe UI", 10),
            BackColor = theme.Background,
            ForeColor = theme.Text
        };
        dialog.HandleCreated += (_, _) => WindowsTitleBar.Apply(dialog, theme);
        var message = new Label
        {
            Text = string.Format(T("A versão {0} do Valley Keepsake está disponível. Quer atualizar agora?",
                "Valley Keepsake version {0} is available. Update now?"), update.Version),
            Location = new Point(22, 23),
            Size = new Size(426, 62),
            ForeColor = theme.Text
        };
        var cancel = new GlowButton { Text = T("Cancelar", "Cancel"), Location = new Point(226, 100), Size = new Size(108, 40), DialogResult = DialogResult.Cancel };
        var openRelease = new GlowButton { Text = T("Atualizar", "Update"), Location = new Point(340, 100), Size = new Size(108, 40), DialogResult = DialogResult.OK };
        cancel.ApplyTheme(theme, theme.BorderLight);
        openRelease.ApplyTheme(theme, theme.ScrollThumb);
        dialog.Controls.AddRange([message, cancel, openRelease]);
        dialog.AcceptButton = openRelease;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try { Process.Start(new ProcessStartInfo(update.ReleasePage.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Não foi possível abrir o release", "Could not open release"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyTheme()
    {
        var theme = ThemeStore.Current;
        WindowsTitleBar.Apply(this, theme);
        BackColor = bodyPanel.BackColor = headerPanel.BackColor = footerPanel.BackColor =
        gridHost.BackColor = actionsPanel.BackColor = titleRow.BackColor = interfaceRow.BackColor =
            translationRow.BackColor = theme.Background;
        bodyPanel.Padding = theme.Kind switch
        {
            ThemeKind.Parchment => new Padding(84),
            ThemeKind.Wood => new Padding(64),
            ThemeKind.Mine => new Padding(56),
            ThemeKind.Garden => new Padding(72),
            _ => new Padding(22)
        };
        footerPanel.Height = HasArtworkFrame(theme.Kind) ? 80 : 132;
        MinimumSize = new Size(1120, 740);
        if (HasArtworkFrame(theme.Kind))
            headerPanel.BackColor = footerPanel.BackColor = actionsPanel.BackColor =
                titleRow.BackColor = interfaceRow.BackColor = translationRow.BackColor = Color.Transparent;
        pathsPanel.BackColor = theme.Panel;
        pathsPanel.Padding = theme.Kind is ThemeKind.Mine or ThemeKind.Wood or ThemeKind.Garden
            ? new Padding(14, 8, 14, 6) : new Padding(14, 8, 14, 8);
        gridHost.Padding = theme.Kind == ThemeKind.Classic
            ? new Padding(0, 14, 0, 0) : theme.Kind == ThemeKind.Mine
                ? new Padding(8, 9, 8, 7) : new Padding(8, 16, 8, 8);

        titleLabel.ForeColor = theme.Title;
        titleLabel.SlimeGlow = theme.Kind == ThemeKind.Mine;
        help.ForeColor = summary.ForeColor = theme.MutedText;
        interfaceLanguageLabel.ForeColor = translationLanguageLabel.ForeColor = theme.Text;
        libraryLabel.ForeColor = configLibraryLabel.ForeColor = modsLabel.ForeColor = searchLabel.ForeColor = theme.Text;
        foreach (var box in new[] { libraryBox, configLibraryBox, modsBox, searchBox })
        {
            box.BackColor = theme.Field;
            box.ForeColor = theme.Text;
            box.BorderStyle = theme.Kind == ThemeKind.Classic ? BorderStyle.FixedSingle : BorderStyle.None;
            if (box.Parent is Panel frame)
            {
                frame.Dock = DockStyle.None;
                frame.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                frame.Height = theme.Kind is ThemeKind.Classic or ThemeKind.Parchment ? 27 : 26;
                frame.BackColor = theme.BorderDark;
                frame.Padding = theme.Kind == ThemeKind.Classic ? Padding.Empty :
                    theme.Kind == ThemeKind.Mine ? new Padding(3) : new Padding(2);
            }
        }
        foreach (var picker in new[] { languagePicker, localePicker, themePicker })
            picker.ApplyTheme(theme);
        themeButton.ApplyTheme(theme);

        Color green = theme.Kind == ThemeKind.Classic ? Color.FromArgb(161, 232, 74) : theme.ScrollThumb;
        Color yellow = Color.FromArgb(255, 198, 82);
        Color red = Color.FromArgb(239, 91, 96);
        Color neutral = theme.Kind == ThemeKind.Classic ? Color.FromArgb(108, 224, 230)
            : theme.IsLight ? theme.BorderDark : Color.FromArgb(111, 101, 176);
        analyzeButton.ApplyTheme(theme, neutral);
        prepareButton.ApplyTheme(theme, green);
        configsButton.ApplyTheme(theme, yellow);
        applyButton.ApplyTheme(theme, green);
        undoButton.ApplyTheme(theme, red);
        foreach (var browse in new[] { libraryBrowse, configLibraryBrowse, modsBrowse })
            browse.ApplyTheme(theme, theme.Kind == ThemeKind.Classic ? green : neutral);
        if (theme.Kind == ThemeKind.Garden)
        {
            var rose = Color.FromArgb(243, 145, 182);
            foreach (var button in new[] { analyzeButton, prepareButton, configsButton, applyButton, undoButton,
                libraryBrowse, configLibraryBrowse, modsBrowse })
                button.ApplyTheme(theme, rose);
            configsButton.ApplyTheme(theme, yellow);
            undoButton.ApplyTheme(theme, red);
        }

        grid.BackgroundColor = theme.Row;
        grid.GridColor = theme.BorderDark;
        grid.ColumnHeadersDefaultCellStyle.BackColor = theme.Header;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = theme.Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = theme.GridSelection;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = theme.GridSelectionText;
        grid.DefaultCellStyle.BackColor = theme.Row;
        grid.DefaultCellStyle.ForeColor = theme.Text;
        grid.DefaultCellStyle.SelectionBackColor = theme.GridSelection;
        grid.DefaultCellStyle.SelectionForeColor = theme.GridSelectionText;
        grid.AlternatingRowsDefaultCellStyle.BackColor = theme.AlternateRow;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = theme.Text;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = theme.GridSelection;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = theme.GridSelectionText;
        foreach (DataGridViewRow row in grid.Rows)
        {
            row.DefaultCellStyle.ForeColor = theme.Text;
            row.DefaultCellStyle.SelectionBackColor = theme.GridSelection;
            row.DefaultCellStyle.SelectionForeColor = theme.GridSelectionText;
            if (row.Tag is MatchResult result) row.DefaultCellStyle.BackColor = theme.RowFor(result.State);
            else if (row.Tag is PackageGroup) row.DefaultCellStyle.BackColor = AppTheme.Blend(theme.Header, theme.Row, .25);
            else row.DefaultCellStyle.BackColor = row.Index % 2 == 0 ? theme.Row : theme.AlternateRow;
        }
        verticalScroll.ApplyTheme(theme);
        horizontalScroll.ApplyTheme(theme);
        junimos.ApplyTheme(theme);
        slimes.ApplyTheme(theme);
        junimos.Visible = theme.Kind != ThemeKind.Mine;
        slimes.Visible = theme.Kind == ThemeKind.Mine;
        if (HasArtworkFrame(theme.Kind))
        {
            if (junimos.Parent != bodyPanel)
            {
                footerPanel.Controls.Remove(junimos);
                junimos.Dock = DockStyle.None;
                junimos.Size = new Size(360, 78);
                bodyPanel.Controls.Add(junimos);
                junimos.BringToFront();
            }
            PositionArtJunimos();
        }
        else if (junimos.Parent != footerPanel)
        {
            bodyPanel.Controls.Remove(junimos);
            junimos.Dock = DockStyle.Fill;
            junimos.Margin = Padding.Empty;
            footerPanel.Controls.Add(junimos, 1, 1);
        }
        if (theme.Kind == ThemeKind.Mine)
        {
            if (slimes.Parent != bodyPanel)
            {
                footerPanel.Controls.Remove(slimes);
                slimes.Dock = DockStyle.None;
                slimes.Size = new Size(360, 104);
                bodyPanel.Controls.Add(slimes);
                slimes.BringToFront();
            }
            PositionArtSlimes();
        }
        else if (slimes.Parent != footerPanel)
        {
            bodyPanel.Controls.Remove(slimes);
            slimes.Dock = DockStyle.Fill;
            slimes.Margin = Padding.Empty;
            footerPanel.Controls.Add(slimes, 1, 1);
        }
        gardenLeaves.Enabled = theme.Kind == ThemeKind.Garden;
        grid.Invalidate();
        bodyPanel.Invalidate(true);
    }

    private void PositionArtJunimos()
    {
        if (junimos?.Parent != bodyPanel || !HasArtworkFrame(ThemeStore.CurrentKind)) return;
        int horizontalOffset = ThemeStore.CurrentKind == ThemeKind.Garden ? 72
            : ThemeStore.CurrentKind == ThemeKind.Parchment ? 62 : 52;
        int verticalOffset = ThemeStore.CurrentKind == ThemeKind.Garden ? 22
            : ThemeStore.CurrentKind == ThemeKind.Parchment ? 62 : 52;
        junimos.Location = new Point(bodyPanel.ClientSize.Width - horizontalOffset - junimos.Width,
            bodyPanel.ClientSize.Height - verticalOffset - junimos.Height + 2);
    }

    private void PositionArtSlimes()
    {
        if (slimes?.Parent != bodyPanel || ThemeStore.CurrentKind != ThemeKind.Mine) return;
        // The lowest body pose lands on the inner edge of the outer frame.
        slimes.Location = new Point(bodyPanel.ClientSize.Width - 52 - slimes.Width,
            bodyPanel.ClientSize.Height - 32 - slimes.Height);
    }

    private void InvalidateGardenBackground()
    {
        if (!Visible || WindowState == FormWindowState.Minimized) return;
        foreach (var surface in new Control[] { bodyPanel, headerPanel, footerPanel,
            actionsPanel, titleRow, interfaceRow, translationRow })
            surface.Invalidate();
    }

    private Font LoadTitleFont()
    {
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "PixelifySans.ttf");
        try
        {
            if (File.Exists(fontPath))
            {
                titleFonts.AddFontFile(fontPath);
                return new Font(titleFonts.Families[0], 23, FontStyle.Bold);
            }
        }
        catch (Exception)
        {
            // Keep the title readable if the optional font cannot be loaded.
        }

        return new Font("Segoe UI Semibold", 18);
    }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill;
        grid.Margin = new Padding(0, 14, 0, 0);
        grid.BackgroundColor = Color.FromArgb(16, 49, 72);
        grid.BorderStyle = BorderStyle.None;
        grid.ScrollBars = ScrollBars.None;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoGenerateColumns = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(26, 97, 125), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9.5f), Padding = new Padding(5, 2, 5, 2), Alignment = DataGridViewContentAlignment.MiddleLeft };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(19, 58, 83), ForeColor = Color.FromArgb(231, 249, 247), SelectionBackColor = Color.FromArgb(31, 112, 132), SelectionForeColor = Color.White, Padding = new Padding(4, 3, 4, 3) };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(16, 49, 72), ForeColor = Color.FromArgb(231, 249, 247) };
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Use", HeaderText = "Aplicar", Width = 65 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Translation", HeaderText = "Tradução", Width = 260, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Target", HeaderText = "Mod encontrado", Width = 270, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Situação", Width = 170, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Detail", HeaderText = "Detalhes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        grid.CellValueChanged += GridCellValueChanged;
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellDoubleClick += GridCellDoubleClick;
        verticalScroll.ValueChanged += (_, _) =>
        {
            if (grid.Rows.Count == 0) return;
            try { grid.FirstDisplayedScrollingRowIndex = Math.Min(verticalScroll.Value, grid.Rows.Count - 1); }
            catch (InvalidOperationException) { }
        };
        horizontalScroll.ValueChanged += (_, _) => grid.HorizontalScrollingOffset = horizontalScroll.Value;
        grid.Scroll += (_, _) => UpdateScrollBars();
        grid.MouseWheel += (_, e) =>
        {
            if (!verticalScroll.Visible) return;
            int lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
            int notches = Math.Max(1, Math.Abs(e.Delta) / 120);
            verticalScroll.Value -= Math.Sign(e.Delta) * lines * notches;
        };
        grid.SizeChanged += (_, _) => UpdateScrollBars();
        grid.ColumnWidthChanged += (_, _) => UpdateScrollBars();
    }

    private static TextBox StyledTextBox(string placeholder = "") => new()
    {
        Dock = DockStyle.Fill,
        PlaceholderText = placeholder,
        BackColor = Color.FromArgb(12, 41, 61),
        ForeColor = Color.FromArgb(231, 249, 247),
        BorderStyle = BorderStyle.FixedSingle
    };

    private static void AddPathRow(TableLayoutPanel panel, int row, Label label, TextBox box, GlowButton button, Action browse)
    {
        label.ForeColor = Color.FromArgb(194, 239, 235);
        label.Anchor = AnchorStyles.Left;
        label.AutoSize = true;
        label.Margin = new Padding(0, 9, 8, 5);
        panel.Controls.Add(label, 0, row);
        panel.Controls.Add(FrameTextBox(box, new Padding(0, 5, 8, 5)), 1, row);
        button.Click += (_, _) => browse();
        panel.Controls.Add(button, 2, row);
    }

    private static Panel FrameTextBox(TextBox box, Padding margin)
    {
        var frame = new BufferedPanel { Dock = DockStyle.Fill, Size = new Size(100, 26), Margin = margin };
        frame.Paint += (_, e) => ThemeFramePainter.DrawFieldFrame(e.Graphics, frame.ClientRectangle, ThemeStore.Current);
        box.AutoSize = false;
        box.Dock = DockStyle.Fill;
        box.Margin = Padding.Empty;
        frame.Controls.Add(box);
        return frame;
    }

    private void Browse(TextBox target)
    {
        using var dialog = new FolderBrowserDialog { ShowNewFolderButton = false, InitialDirectory = Directory.Exists(target.Text) ? target.Text : "" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.SelectedPath;
            if (ReferenceEquals(target, libraryBox)) SaveLibraries();
        }
    }

    private void BrowseConfigLibrary()
    {
        using var dialog = new FolderBrowserDialog { ShowNewFolderButton = true, InitialDirectory = Directory.Exists(configLibraryBox.Text) ? configLibraryBox.Text : libraryBox.Text };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        configLibraryBox.Text = dialog.SelectedPath;
        SaveLibraries();
    }

    private void SaveLibraries()
    {
        try { LibrarySelectionStore.Save(libraryBox.Text, configLibraryBox.Text); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, T("Não foi possível salvar as bibliotecas", "Could not save library folders"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void Analyze()
    {
        if (!Directory.Exists(libraryBox.Text) || !Directory.Exists(modsBox.Text))
        {
            MessageBox.Show(this, T("Escolha pastas existentes para a biblioteca e para os mods.", "Choose existing translation-library and Mods folders."), T("Pastas necessárias", "Folders required"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            UseWaitCursor = true;
            mods = service.ScanMods(modsBox.Text);
            var translations = service.ScanLibrary(libraryBox.Text);
            results = service.Match(translations, mods);
            LoadSavedMappings();
            RefreshLocales();
            RefreshMissingTranslations();
            PopulateGrid();
            UpdateSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, T("Não foi possível concluir a análise:\n\n", "Could not complete the analysis:\n\n") + ex.Message, T("Erro", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private void PopulateGrid(string? focusPackage = null)
    {
        var oldTop = -1;
        object? topTag = null;
        var focusOffset = 0;
        if (grid.Rows.Count > 0)
        {
            try { oldTop = grid.FirstDisplayedScrollingRowIndex; } catch (InvalidOperationException) { }
            if (oldTop >= 0 && oldTop < grid.Rows.Count)
            {
                topTag = grid.Rows[oldTop].Tag;
                if (focusPackage is not null)
                {
                    var oldFocus = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row =>
                        row.Tag is PackageGroup package && package.Name.Equals(focusPackage, StringComparison.OrdinalIgnoreCase));
                    if (oldFocus is not null) focusOffset = oldFocus.Index - oldTop;
                }
            }
        }
        populatingGrid = true;
        try
        {
        grid.Rows.Clear();
        foreach (var group in FilteredResults().GroupBy(result => result.Translation.LibraryPackage, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Min(result => StateOrder(result.State)))
            .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var members = group.OrderBy(result => result.Translation.RelativeComponentPath, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (members.Count == 1)
            {
                AddResultRow(members[0], false);
                continue;
            }
            var safe = members.Where(result => result.State == MatchState.Safe).ToList();
            var selected = safe.Count(result => result.Selected);
            var expanded = expandedPackages.Contains(group.Key);
            var parentIndex = grid.Rows.Add(safe.Count > 0 && selected == safe.Count,
                (expanded ? "▼ " : "▶ ") + group.Key,
                string.Format(T("{0} componentes", "{0} components"), members.Count),
                string.Format(T("{0} prontas", "{0} ready"), safe.Count),
                string.Format(T("{0} selecionadas • {1} a confirmar", "{0} selected • {1} to review"),
                    selected, members.Count(result => result.State == MatchState.NeedsConfirmation)));
            var parent = grid.Rows[parentIndex];
            parent.Tag = new PackageGroup(group.Key, members);
            parent.Cells["Use"].ReadOnly = safe.Count == 0;
            parent.DefaultCellStyle.BackColor = AppTheme.Blend(ThemeStore.Current.Header, ThemeStore.Current.Row, .25);
            parent.DefaultCellStyle.ForeColor = ThemeStore.Current.Text;
            parent.DefaultCellStyle.SelectionBackColor = ThemeStore.Current.GridSelection;
            parent.DefaultCellStyle.SelectionForeColor = ThemeStore.Current.GridSelectionText;
            parent.DefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
            if (expanded)
                foreach (var result in members) AddResultRow(result, true);
        }
        }
        finally
        {
            populatingGrid = false;
            if (grid.Rows.Count > 0 && oldTop >= 0)
            {
                var focusRow = focusPackage is null ? null : grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row =>
                    row.Tag is PackageGroup package && package.Name.Equals(focusPackage, StringComparison.OrdinalIgnoreCase));
                var matchingTop = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row =>
                    ReferenceEquals(row.Tag, topTag) || row.Tag is PackageGroup current &&
                    topTag is PackageGroup previous && current.Name.Equals(previous.Name, StringComparison.OrdinalIgnoreCase));
                var restoreIndex = focusRow is not null ? focusRow.Index - focusOffset : matchingTop?.Index ?? oldTop;
                try { grid.FirstDisplayedScrollingRowIndex = Math.Clamp(restoreIndex, 0, grid.Rows.Count - 1); }
                catch (InvalidOperationException) { }
            }
            UpdateScrollBars();
        }
    }

    private void UpdateScrollBars()
    {
        if (updatingScrollBars || populatingGrid || !grid.IsHandleCreated) return;
        updatingScrollBars = true;
        try
        {
            int visibleRows = grid.DisplayedRowCount(false);
            bool needsVertical = grid.Rows.Count > visibleRows;
            verticalScroll.Visible = needsVertical;
            int topRow = grid.Rows.Count == 0 ? 0 : Math.Max(0, grid.FirstDisplayedScrollingRowIndex);
            verticalScroll.SetMetrics(grid.Rows.Count, Math.Max(1, visibleRows), topRow);

            int contentWidth = grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible);
            int viewportWidth = Math.Max(1, grid.ClientSize.Width);
            bool needsHorizontal = contentWidth > viewportWidth;
            horizontalScroll.Visible = needsHorizontal;
            horizontalScroll.SetMetrics(contentWidth, viewportWidth, grid.HorizontalScrollingOffset);
        }
        finally { updatingScrollBars = false; }
    }

    private void AddResultRow(MatchResult result, bool child)
    {
        var translationName = result.State == MatchState.MissingTranslationFile
            ? result.Target?.DisplayName ?? "—" : DisplayTranslation(result.Translation);
        if (child) translationName = "    ↳ " + result.Translation.ComponentName;
        var rowIndex = grid.Rows.Add(result.Selected, translationName, DisplayTarget(result.Target), StatusText(result.State), result.Detail);
        var row = grid.Rows[rowIndex];
        row.Tag = result;
        row.Cells["Use"].ReadOnly = result.State != MatchState.Safe;
        row.Cells["Translation"].ToolTipText = result.Translation.SourcePath;
        row.Cells["Target"].ToolTipText = result.Target?.Directory ?? "";
        row.DefaultCellStyle.BackColor = ThemeStore.Current.RowFor(result.State);
        row.DefaultCellStyle.ForeColor = ThemeStore.Current.Text;
        row.DefaultCellStyle.SelectionBackColor = ThemeStore.Current.GridSelection;
        row.DefaultCellStyle.SelectionForeColor = ThemeStore.Current.GridSelectionText;
    }

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

    private sealed record PackageGroup(string Name, List<MatchResult> Members);

    private string SelectedLocale => TranslationLocales.FromDisplayOption(localePicker.SelectedItem as string ?? UiLanguage.TranslationLocale);

    private void RefreshLocales()
    {
        var previous = SelectedLocale;
        var commonLocales = new[] { "pt-BR", "en", "es", "fr", "de", "it", "ja", "ko", "zh-Hans", "zh-Hant", "ru", "tr", "pl", "cs" };
        var locales = commonLocales.Append(previous).Concat(results.Select(result => result.Translation.Locale)).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToList();
        localePicker.SelectedIndexChanged -= LocaleChanged;
        localePicker.Items.Clear();
        localePicker.Items.AddRange(locales.Select(TranslationLocales.DisplayOption).Cast<object>().ToArray());
        localePicker.SelectedItem = TranslationLocales.DisplayOption(locales.FirstOrDefault(code => code.Equals(previous, StringComparison.OrdinalIgnoreCase)) ?? locales[0]);
        localePicker.SelectedIndexChanged += LocaleChanged;
        foreach (var result in results)
            if (result.Translation.Locale != SelectedLocale) result.Selected = false;
    }

    private void LocaleChanged(object? sender, EventArgs e)
    {
        try { UiLanguage.SetTranslationLocale(SelectedLocale); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Não foi possível salvar o idioma", "Could not save language"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        foreach (var result in results)
            if (result.State == MatchState.Safe && result.Translation.Locale != SelectedLocale)
                result.Selected = false;
        RefreshMissingTranslations();
        PopulateGrid();
        UpdateSummary();
    }

    private void RefreshMissingTranslations()
    {
        results.RemoveAll(result => result.State == MatchState.MissingTranslationFile);
        if (mods.Count > 0)
            results.AddRange(service.FindMissingTranslationFiles(mods, results, SelectedLocale));
    }

    private void GridCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (grid.Rows[e.RowIndex].Tag is PackageGroup package)
        {
            if (!expandedPackages.Add(package.Name)) expandedPackages.Remove(package.Name);
            PopulateGrid(package.Name);
            return;
        }
        if (grid.Rows[e.RowIndex].Tag is not MatchResult result) return;
        if (result.State == MatchState.MissingTranslationFile && result.Target is { } missingMod)
        {
            ImportTranslationForMod(missingMod);
            return;
        }
        using var picker = new TargetPickerForm(mods, result.Target, result.Translation);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedMod is null) return;
        result.Target = picker.SelectedMod;
        if (service.HasTranslation(result.Target, result.Translation))
        {
            result.State = MatchState.AlreadyTranslated;
            result.Selected = false;
            result.Detail = T("O mod escolhido já possui tradução neste idioma.", "The selected mod already has a translation in this language.");
        }
        else
        {
            result.State = MatchState.Safe;
            result.Selected = true;
            result.Detail = T("Destino confirmado manualmente.", "Target confirmed manually.");
            SaveMapping(result);
        }
        RefreshMissingTranslations(); PopulateGrid(); UpdateSummary();
    }

    private void GridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (populatingGrid) return;
        var useColumn = grid.Columns["Use"];
        if (e.RowIndex < 0 || useColumn is null || e.ColumnIndex != useColumn.Index) return;
        if (grid.Rows[e.RowIndex].Tag is PackageGroup package)
        {
            var selected = Convert.ToBoolean(grid.Rows[e.RowIndex].Cells["Use"].Value);
            foreach (var member in package.Members.Where(member => member.State == MatchState.Safe))
                member.Selected = selected;
            PopulateGrid();
        }
        else if (grid.Rows[e.RowIndex].Tag is MatchResult result)
        {
            result.Selected = Convert.ToBoolean(grid.Rows[e.RowIndex].Cells["Use"].Value);
            if (expandedPackages.Contains(result.Translation.LibraryPackage)) PopulateGrid();
        }
        UpdateSummary();
    }

    private void ApplySelected()
    {
        var selected = results.Where(p => p.Translation.Locale == SelectedLocale && p.Selected && p.Target is not null && p.State == MatchState.Safe).ToList();
        if (selected.Count == 0) return;
        var answer = MessageBox.Show(this, string.Format(T("Aplicar {0} tradução(ões) em {1}?\n\nTraduções existentes serão preservadas.", "Apply {0} translation(s) in {1}?\n\nExisting translations will be preserved."), selected.Count, SelectedLocale), T("Confirmar aplicação", "Confirm application"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;
        lastBackup = Path.Combine(AppDataDirectory(), "Backups", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        lastApply = [];
        foreach (var result in selected)
        {
            try
            {
                lastApply.Add(service.Apply(result, lastBackup));
                result.State = MatchState.Applied; result.Selected = false; result.Detail = T("Tradução aplicada com sucesso.", "Translation applied successfully.");
            }
            catch (Exception ex)
            {
                result.State = MatchState.Failed; result.Selected = false; result.Detail = ex.Message;
            }
        }
        WriteReport();
        undoButton.Enabled = lastApply.Count > 0;
        PopulateGrid(); UpdateSummary();
        MessageBox.Show(this, string.Format(T("Processo concluído. {0} tradução(ões) aplicada(s).", "Done. {0} translation(s) applied."), lastApply.Count), T("Concluído", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UndoLast()
    {
        if (lastApply.Count == 0) return;
        if (MessageBox.Show(this, T("Desfazer os arquivos aplicados na última operação?", "Undo the files applied in the last operation?"), T("Confirmar", "Confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            service.Undo(lastApply);
            lastApply = []; undoButton.Enabled = false;
            Analyze();
            MessageBox.Show(this, T("A última aplicação foi desfeita.", "The last application was undone."), T("Concluído", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, T("Não foi possível desfazer", "Could not undo"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void OpenConfigBackup()
    {
        if (!Directory.Exists(modsBox.Text))
        {
            MessageBox.Show(this, T("Escolha uma pasta Mods existente.", "Choose an existing Mods folder."), T("Pasta necessária", "Folder required"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            if (!string.IsNullOrWhiteSpace(configLibraryBox.Text) && !Directory.Exists(configLibraryBox.Text) &&
                Path.GetFullPath(configLibraryBox.Text).Equals(Path.GetFullPath(ConfigBackupService.DefaultStorageRoot(libraryBox.Text)), StringComparison.OrdinalIgnoreCase))
                Directory.CreateDirectory(configLibraryBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Não foi possível abrir a biblioteca de configurações", "Could not open settings library"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (!Directory.Exists(configLibraryBox.Text))
        {
            MessageBox.Show(this, T("Escolha uma pasta existente para a biblioteca de configurações.", "Choose an existing settings library folder."), T("Pasta necessária", "Folder required"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new ConfigBackupForm(configLibraryBox.Text, modsBox.Text, service);
        dialog.ShowDialog(this);
    }

    private void SearchNewMods()
    {
        if (!Directory.Exists(libraryBox.Text) || !Directory.Exists(modsBox.Text))
        {
            MessageBox.Show(this, T("Escolha primeiro a biblioteca de traduções e a pasta Mods.", "Choose the translation library and Mods folder first."), T("Pastas necessárias", "Folders required"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            var installed = service.ScanMods(modsBox.Text);
            var report = service.PrepareLibrary(libraryBox.Text, installed, SelectedLocale);
            var imported = service.ImportInstalledTranslations(libraryBox.Text, installed, SelectedLocale);
            Analyze();
            MessageBox.Show(this,
                string.Format(T("Busca concluída para {0}:\n\n• {1} pasta(s) nova(s) na biblioteca\n• {2} referência(s) i18n/default copiadas\n• {3} content.json copiado(s)\n• {4} tradução(ões) do idioma copiadas\n• {5} arquivo(s) inválido(s) ignorado(s)\n\nOs mods instalados não foram alterados.",
                    "Search finished for {0}:\n\n• {1} new library folder(s)\n• {2} i18n/default reference(s) copied\n• {3} content.json file(s) copied\n• {4} selected-language translation(s) copied\n• {5} invalid file(s) skipped\n\nInstalled mods were not changed."),
                    SelectedLocale, report.CreatedFolders, report.CopiedI18nReferences, report.CopiedContentReferences, imported.Imported, imported.Invalid),
                T("Busca concluída", "Search complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, T("Não foi possível procurar novos mods:\n\n", "Could not search for new mods:\n\n") + ex.Message, T("Erro", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private void ImportTranslationForMod(InstalledMod mod)
    {
        if (!Directory.Exists(libraryBox.Text) || !Directory.Exists(modsBox.Text))
        {
            MessageBox.Show(this, T("Escolha primeiro a biblioteca de traduções e a pasta Mods.", "Choose the translation library and Mods folder first."), T("Pastas necessárias", "Folders required"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var initialFolder = Path.Combine(mod.Directory, "i18n");
            var folderStyle = Directory.Exists(initialFolder) && Directory.EnumerateDirectories(initialFolder)
                .Any(path => TranslationLocales.SameTargetLanguage(Path.GetFileName(path), SelectedLocale));
            var chooseFolder = ChooseTranslationSource(folderStyle);
            if (chooseFolder == DialogResult.Cancel) return;
            if (chooseFolder == DialogResult.Yes)
            {
                using var folderDialog = new FolderBrowserDialog
                {
                    Description = string.Format(T("Escolha a pasta de idioma de {0}", "Choose {0}'s language folder"), mod.DisplayName),
                    InitialDirectory = Directory.Exists(initialFolder) ? initialFolder : mod.Directory,
                    ShowNewFolderButton = false
                };
                if (folderDialog.ShowDialog(this) != DialogResult.OK) return;
                var sourceFolder = folderDialog.SelectedPath;
                if (TranslationLocales.IsDefaultReference(sourceFolder))
                {
                    ImportDefaultForMod(mod, sourceFolder);
                    return;
                }
                var detectedFolder = TranslationLocales.Normalize(Path.GetFileName(sourceFolder));
                if (detectedFolder is not null && !TranslationLocales.SameTargetLanguage(detectedFolder, SelectedLocale))
                {
                    MessageBox.Show(this, string.Format(T("A pasta indica {0}, mas você selecionou {1}. Nada foi copiado.",
                        "The folder indicates {0}, but you selected {1}. Nothing was copied."), detectedFolder, SelectedLocale),
                        T("Idioma diferente", "Different language"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (detectedFolder is null && MessageBox.Show(this,
                    string.Format(T("O nome da pasta não identifica o idioma. Confirmar que ela contém a tradução em {0}?",
                        "The folder name does not identify a language. Confirm that it contains the translation in {0}?"), SelectedLocale),
                    T("Confirmar idioma", "Confirm language"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                var folderLocale = detectedFolder ?? SelectedLocale;
                if (MessageBox.Show(this,
                    string.Format(T("Guardar a pasta inteira na biblioteca?\n\nOrigem: {0}\nIdioma: {1}\n\nA pasta original não será alterada.",
                        "Save the whole folder in the library?\n\nSource: {0}\nLanguage: {1}\n\nThe original folder will not be changed."), sourceFolder, folderLocale),
                    T("Confirmar importação", "Confirm import"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                service.ImportI18nDirectory(libraryBox.Text, mod, sourceFolder, folderLocale);
                Analyze();
                MessageBox.Show(this, T("A pasta inteira foi salva na biblioteca. A original não foi alterada.",
                    "The whole folder was saved in the library. The original was not changed."),
                    T("Concluído", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var fileDialog = new OpenFileDialog
            {
                Title = string.Format(T("Escolha a tradução de {0} para {1}", "Choose {0}'s translation for {1}"), mod.DisplayName, SelectedLocale),
                InitialDirectory = Directory.Exists(initialFolder) ? initialFolder : mod.Directory,
                Filter = T("Arquivos JSON (*.json)|*.json|Todos os arquivos (*.*)|*.*", "JSON files (*.json)|*.json|All files (*.*)|*.*"),
                CheckFileExists = true
            };
            if (fileDialog.ShowDialog(this) != DialogResult.OK) return;
            var sourcePath = fileDialog.FileName;
            if (Path.GetFileName(sourcePath).StartsWith("content.", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, T("Arquivos content.<idioma>.json usam um formato especial. Esta importação manual é para arquivos i18n; não alterei nada.", "content.<language>.json files use a special format. Manual import is for i18n files; nothing was changed."), T("Formato diferente", "Different format"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (TranslationLocales.IsDefaultReference(sourcePath))
            {
                ImportDefaultForMod(mod, sourcePath);
                return;
            }

            var detected = TranslationLocales.FromFileName(sourcePath);
            if (detected is not null && !TranslationLocales.SameTargetLanguage(detected, SelectedLocale))
            {
                MessageBox.Show(this,
                    string.Format(T("O nome do arquivo indica {0}, mas você selecionou {1}. Escolha o idioma correto no topo ou procure outro arquivo; nada foi copiado.",
                        "The file name indicates {0}, but you selected {1}. Choose the correct language at the top or browse for another file; nothing was copied."), detected, SelectedLocale),
                    T("Idioma diferente", "Different language"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (detected is null && MessageBox.Show(this,
                string.Format(T("O nome do arquivo não identifica o idioma. Confirmar que este arquivo é uma tradução em {0}?", "The file name does not identify a language. Confirm that this is a translation in {0}?"), SelectedLocale),
                T("Confirmar idioma", "Confirm language"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var locale = detected ?? SelectedLocale;
            var destination = service.GetImportDestination(libraryBox.Text, mod, locale);
            var exists = File.Exists(destination);
            var prompt = string.Format(T("Guardar esta tradução na biblioteca?\n\nOrigem: {0}\nIdioma: {1}\nDestino: {2}{3}",
                "Save this translation in the library?\n\nSource: {0}\nLanguage: {1}\nDestination: {2}{3}"),
                sourcePath, locale, destination,
                exists ? T("\n\nJá existe uma cópia. Se substituir, a versão atual será guardada em backup.", "\n\nA copy already exists. If replaced, the current version will be backed up.") : "");
            if (MessageBox.Show(this, prompt, exists ? T("Confirmar substituição", "Confirm replacement") : T("Confirmar importação", "Confirm import"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            service.ImportI18nFile(libraryBox.Text, mod, sourcePath, locale, exists);
            Analyze();
            MessageBox.Show(this, T("A tradução foi salva na biblioteca. O arquivo original não foi alterado.", "The translation was saved in the library. The original file was not changed."), T("Concluído", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Não foi possível importar", "Could not import"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private DialogResult ChooseTranslationSource(bool folderStyle)
    {
        var theme = ThemeStore.Current;
        using var dialog = new Form
        {
            Text = T("Localizar tradução", "Find translation"),
            ClientSize = new Size(560, 174),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            Font = new Font("Segoe UI", 10),
            BackColor = theme.Background,
            ForeColor = theme.Text
        };
        var explanation = new Label
        {
            Text = T("Escolha onde está a tradução deste mod.\nO navegador começará na pasta i18n do mod, se ela existir.",
                "Choose where this mod's translation is stored.\nThe browser will start in the mod's i18n folder when available."),
            Location = new Point(22, 22),
            Size = new Size(516, 65),
            ForeColor = theme.Text
        };
        var file = new GlowButton { Text = T("Escolher arquivo JSON", "Choose JSON file"), Location = new Point(22, 108), Size = new Size(190, 40), DialogResult = DialogResult.No };
        var folder = new GlowButton { Text = T("Escolher pasta do idioma", "Choose language folder"), Location = new Point(218, 108), Size = new Size(202, 40), DialogResult = DialogResult.Yes };
        var cancel = new GlowButton { Text = T("Cancelar", "Cancel"), Location = new Point(426, 108), Size = new Size(112, 40), DialogResult = DialogResult.Cancel };
        file.ApplyTheme(theme, theme.IsLight ? theme.BorderDark : theme.BorderLight);
        folder.ApplyTheme(theme, theme.ScrollThumb);
        cancel.ApplyTheme(theme, theme.IsLight ? theme.BorderDark : theme.BorderLight);
        dialog.Paint += (_, e) => ThemeFramePainter.DrawFrame(e.Graphics, dialog.ClientRectangle, theme);
        dialog.Controls.AddRange([explanation, file, folder, cancel]);
        dialog.AcceptButton = folderStyle ? folder : file;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(this);
    }

    private void ImportDefaultForMod(InstalledMod mod, string sourcePath)
    {
        if (!TranslationLocales.SameTargetLanguage(SelectedLocale, "en"))
        {
            MessageBox.Show(this, T("default é a referência em inglês. Selecione en (default) para guardá-la; nada foi copiado.",
                "default is the English reference. Select en (default) to save it; nothing was copied."),
                T("Idioma diferente", "Different language"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show(this,
            string.Format(T("Guardar {0} como referência em inglês?\n\nEla não será aplicada por cima do default de um mod atualizado.",
                "Save {0} as an English reference?\n\nIt will not overwrite an updated mod's default file."), sourcePath),
            T("Confirmar importação", "Confirm import"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        service.ImportDefaultReference(libraryBox.Text, mod, sourcePath);
        Analyze();
        MessageBox.Show(this, T("Referência em inglês salva na biblioteca. A origem não foi alterada.",
            "English reference saved in the library. The source was not changed."),
            T("Concluído", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateSummary()
    {
        var localeResults = results.Where(p => p.Translation.Locale == SelectedLocale).ToList();
        int safe = localeResults.Count(p => p.State == MatchState.Safe);
        int existing = localeResults.Count(p => p.State == MatchState.AlreadyTranslated);
        int review = localeResults.Count(p => p.State == MatchState.NeedsConfirmation);
        int missing = localeResults.Count(p => p.State == MatchState.NoMatch);
        int missingFiles = localeResults.Count(p => p.State == MatchState.MissingTranslationFile);
        var filterSuffix = string.IsNullOrWhiteSpace(searchBox.Text) ? "" : string.Format(T(" • {0} exibidas", " • {0} shown"), FilteredResults().Count());
        summary.Text = localeResults.Count == 0 ? T("Nenhum resultado.", "No results.") : string.Format(
            T("{0} mods • {1} prontas • {2} já traduzidas • {3} para confirmar • {4} sem correspondência • {5} sem arquivo de tradução{6}",
                "{0} mods • {1} ready • {2} translated • {3} to review • {4} unmatched • {5} missing translation files{6}"),
            mods.Count, safe, existing, review, missing, missingFiles, filterSuffix);
        applyButton.Enabled = localeResults.Any(p => p.Selected && p.State == MatchState.Safe);
    }

    private IEnumerable<MatchResult> FilteredResults()
    {
        var query = searchBox.Text.Trim();
        var localeResults = results.Where(result => result.Translation.Locale == SelectedLocale);
        if (string.IsNullOrWhiteSpace(query)) return localeResults;
        return localeResults.Where(result =>
            DisplayTranslation(result.Translation).Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            result.Target?.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true ||
            result.Target?.UniqueId.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
    }

    private void LoadSavedMappings()
    {
        var path = MappingPath();
        if (!File.Exists(path)) return;
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
            foreach (var result in results.Where(p => p.State is MatchState.NeedsConfirmation or MatchState.NoMatch))
            {
                var key = MappingKey(result.Translation);
                if (!map.TryGetValue(key, out var uniqueId)) continue;
                var target = mods.FirstOrDefault(p => p.UniqueId.Equals(uniqueId, StringComparison.OrdinalIgnoreCase));
                if (target is null) continue;
                // A saved choice must not override an installed component at the exact source path.
                var exact = mods.FirstOrDefault(p => p.RelativeFolder.Equals(result.Translation.RelativeComponentPath, StringComparison.OrdinalIgnoreCase));
                if (exact is not null && !exact.UniqueId.Equals(target.UniqueId, StringComparison.OrdinalIgnoreCase)) continue;
                result.Target = target;
                result.State = service.HasTranslation(target, result.Translation) ? MatchState.AlreadyTranslated : MatchState.Safe;
                result.Selected = result.State == MatchState.Safe;
                result.Detail = result.Selected ? T("Correspondência lembrada de uma confirmação anterior.", "Match remembered from an earlier confirmation.") : T("O mod já possui tradução neste idioma.", "The mod already has a translation in this language.");
            }
        }
        catch { }
    }

    private void SaveMapping(MatchResult result)
    {
        if (result.Target is null || string.IsNullOrWhiteSpace(result.Target.UniqueId)) return;
        Directory.CreateDirectory(AppDataDirectory());
        var path = MappingPath();
        Dictionary<string, string> map = [];
        try { if (File.Exists(path)) map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? []; } catch { }
        map[MappingKey(result.Translation)] = result.Target.UniqueId;
        File.WriteAllText(path, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void WriteReport()
    {
        var reportDir = Path.Combine(AppDataDirectory(), "Relatorios");
        Directory.CreateDirectory(reportDir);
        var lines = new List<string> { T("Valley Keepsake — relatório", "Valley Keepsake — report"), DateTime.Now.ToString("F"), "" };
        lines.AddRange(results.Select(r => $"[{StatusText(r.State)}] {DisplayTranslation(r.Translation)} -> {DisplayTarget(r.Target)}: {r.Detail}"));
        File.WriteAllLines(Path.Combine(reportDir, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt"), lines);
    }

    private static string DisplayTranslation(TranslationUnit unit) => unit.IsContentTextMap
        ? unit.DisplayName + T(" › textos do content.json", " › content.json text")
        : unit.DisplayName;
    private static string DisplayTarget(InstalledMod? mod) => mod is null ? "—" : $"{mod.DisplayName} › {mod.RelativeFolder}";
    private static string MappingKey(TranslationUnit unit) => unit.LibraryPackage + "|" + unit.ComponentName + "|" + unit.TargetRelativePath;
    private static string StatusText(MatchState state) => state switch
    {
        MatchState.Safe => T("Pronta", "Ready"),
        MatchState.NeedsConfirmation => T("Confirmar destino", "Confirm target"),
        MatchState.AlreadyTranslated => T("Já traduzido", "Already translated"),
        MatchState.NoMatch => T("Não encontrada", "Not found"),
        MatchState.MissingTranslationFile => T("Arquivo de tradução ausente", "Missing translation file"),
        MatchState.Applied => T("Aplicada", "Applied"),
        MatchState.Failed => T("Falhou", "Failed"),
        _ => state.ToString()
    };
    private static string AppDataDirectory() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TradutorModsStardew");
    private static string MappingPath() => Path.Combine(AppDataDirectory(), "associacoes.json");
    private static string? GuessModsPath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Stardew Valley", "Mods"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Mods")
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }
}

internal sealed class TargetPickerForm : Form
{
    private readonly ListBox list = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true, HorizontalExtent = 1800 };
    public InstalledMod? SelectedMod => list.SelectedItem as InstalledMod;

    public TargetPickerForm(List<InstalledMod> mods, InstalledMod? current, TranslationUnit translation)
    {
        var theme = ThemeStore.Current;
        Text = UiLanguage.Text("Confirmar mod de destino", "Confirm target mod");
        Size = new Size(1000, 600);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        BackColor = theme.Background;
        ForeColor = theme.Text;
        list.BackColor = theme.Row;
        list.ForeColor = theme.Text;
        var filter = new TextBox { Dock = DockStyle.Top, PlaceholderText = UiLanguage.Text("Pesquisar mod…", "Search mods…"), Margin = new Padding(8), BackColor = theme.Field, ForeColor = theme.Text };
        var sourcePath = translation.SourcePath;
        var label = new Label { Dock = DockStyle.Top, Height = 72,
            Text = UiLanguage.Text("Origem da tradução: ", "Translation source: ") + sourcePath + "\n" +
                UiLanguage.Text("Escolha o componente de destino pelo caminho e UniqueID. A escolha será lembrada.",
                    "Choose the target component by its path and UniqueID. Your choice will be remembered."), ForeColor = theme.Text };
        var openSource = new GlowButton { Text = UiLanguage.Text("Abrir origem no Explorador", "Open source in Explorer"), AutoSize = true };
        openSource.Click += (_, _) =>
        {
            if (File.Exists(sourcePath) || Directory.Exists(sourcePath))
                Process.Start(new ProcessStartInfo("explorer.exe", File.Exists(sourcePath) ? $"/select,\"{sourcePath}\"" : $"\"{sourcePath}\"") { UseShellExecute = true });
        };
        var ok = new GlowButton { Text = UiLanguage.Text("Confirmar", "Confirm"), DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new GlowButton { Text = UiLanguage.Text("Cancelar", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
        openSource.ApplyTheme(theme, theme.IsLight ? theme.BorderDark : theme.BorderLight);
        ok.ApplyTheme(theme, theme.ScrollThumb);
        cancel.ApplyTheme(theme, theme.IsLight ? theme.BorderDark : theme.BorderLight);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), BackColor = theme.Background };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel); buttons.Controls.Add(openSource);
        void Fill(string query)
        {
            list.Items.Clear();
            foreach (var mod in mods.Where(p => string.IsNullOrWhiteSpace(query) || p.PickerName.Contains(query, StringComparison.CurrentCultureIgnoreCase))) list.Items.Add(mod);
            list.DisplayMember = nameof(InstalledMod.PickerName);
            if (current is not null) list.SelectedItem = current;
        }
        filter.TextChanged += (_, _) => Fill(filter.Text);
        list.DoubleClick += (_, _) => { if (SelectedMod is not null) { DialogResult = DialogResult.OK; Close(); } };
        Fill(translation.ComponentName);
        filter.Text = translation.ComponentName;
        Controls.Add(list); Controls.Add(filter); Controls.Add(label); Controls.Add(buttons);
        Paint += (_, e) => ThemeFramePainter.DrawFrame(e.Graphics, ClientRectangle, theme);
        AcceptButton = ok; CancelButton = cancel;
    }
}
