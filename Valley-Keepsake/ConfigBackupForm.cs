namespace TradutorModsStardew;

internal sealed class ConfigBackupForm : Form
{
    private static string T(string pt, string en) => UiLanguage.Text(pt, en);
    private readonly string libraryRoot;
    private readonly string modsRoot;
    private readonly TranslationService modService;
    private readonly ConfigBackupService configService = new();
    private readonly DataGridView grid = new();
    private readonly ThemedScrollBar verticalScroll = new(Orientation.Vertical) { Dock = DockStyle.Right, Width = 18, Visible = false };
    private readonly ThemedScrollBar horizontalScroll = new(Orientation.Horizontal) { Dock = DockStyle.Bottom, Height = 18, Visible = false };
    private readonly Label summary = new() { AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly ToolTip tips = new() { AutoPopDelay = 12000, InitialDelay = 450, ReshowDelay = 150 };
    private List<ConfigResult> results = [];
    private bool updatingScrollBars;

    public ConfigBackupForm(string libraryRoot, string modsRoot, TranslationService modService)
    {
        this.libraryRoot = libraryRoot;
        this.modsRoot = modsRoot;
        this.modService = modService;
        Text = T("Backup de configurações dos mods", "Mod settings backup");
        HandleCreated += (_, _) => WindowsTitleBar.Apply(this, ThemeStore.Current);
        Size = ThemeStore.CurrentKind switch
        {
            ThemeKind.Parchment => new Size(1200, 780),
            ThemeKind.Wood => new Size(1120, 740),
            ThemeKind.Mine => new Size(1120, 740),
            ThemeKind.Garden => new Size(1200, 780),
            _ => new Size(1020, 650)
        };
        MinimumSize = ThemeStore.CurrentKind switch
        {
            ThemeKind.Parchment => new Size(1040, 700),
            ThemeKind.Wood => new Size(980, 640),
            ThemeKind.Mine => new Size(980, 640),
            ThemeKind.Garden => new Size(1040, 700),
            _ => new Size(800, 500)
        };
        StartPosition = FormStartPosition.CenterParent;
        ResizeRedraw = true;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(10, 31, 53);
        if (ThemeStore.CurrentKind != ThemeKind.Classic)
            Padding = new Padding(ThemeStore.CurrentKind switch
            {
                ThemeKind.Parchment => 84,
                ThemeKind.Wood => 64,
                ThemeKind.Mine => 56,
                ThemeKind.Garden => 72,
                _ => 12
            });
        Paint += (_, e) => ThemeFramePainter.DrawFrame(e.Graphics, ClientRectangle, ThemeStore.Current, true);

        var title = new Label { Dock = DockStyle.Top, Height = 68, Padding = new Padding(18, 14, 18, 0), Text = T("Backup de configurações dos mods\nSalve seus config.json antes de atualizar. A versão atual é guardada antes de cada restauração.", "Mod settings backup\nSave config.json files before updating. Current settings are backed up before each restore."), Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(194, 239, 235) };
        ConfigureGrid();
        var export = new GlowButton { Text = T("Exportar todos os configs", "Back up all configs"), AccentColor = Color.FromArgb(108, 224, 230), AutoSize = true };
        var import = new GlowButton { Text = T("Restaurar selecionados", "Restore selected"), AccentColor = Color.FromArgb(161, 232, 74), AutoSize = true };
        var restoreAll = new GlowButton { Text = T("Restaurar tudo", "Restore all"), AccentColor = Color.FromArgb(161, 232, 74), AutoSize = true };
        var refresh = new GlowButton { Text = T("Procurar novas configs", "Find new configs"), AccentColor = Color.FromArgb(130, 114, 210), AutoSize = true };
        tips.SetToolTip(export, T("Guarda uma cópia dos config.json dos mods na biblioteca.", "Saves copies of the mods' config.json files in the library."));
        tips.SetToolTip(import, T("Restaura os configs marcados, pedindo confirmação para cada mod.", "Restores checked configs, asking for confirmation for each mod."));
        tips.SetToolTip(restoreAll, T("Restaura todos os configs ausentes ou diferentes, com uma confirmação única. Os arquivos atuais substituídos são guardados antes.", "Restores all missing or different configs with one confirmation. Existing files are backed up first."));
        tips.SetToolTip(refresh, T("Procura config.json novos nos mods e atualiza a lista; não cria backup automaticamente.", "Finds new mod config.json files and refreshes the list; it does not create backups automatically."));
        export.Click += (_, _) => ExportAll();
        import.Click += (_, _) => RestoreSelected();
        restoreAll.Click += (_, _) => RestoreAll();
        refresh.Click += (_, _) => LoadResults();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(18, 6, 18, 8), BackColor = BackColor };
        actions.Controls.Add(restoreAll); actions.Controls.Add(import); actions.Controls.Add(export); actions.Controls.Add(refresh);
        summary.ForeColor = Color.FromArgb(174, 223, 231);
        summary.Dock = DockStyle.Top;
        summary.Padding = new Padding(20, 5, 0, 0);
        summary.AutoSize = false;
        summary.Height = 28;
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 82, BackColor = BackColor };
        footer.Controls.Add(actions); footer.Controls.Add(summary);
        var gridHost = new BufferedPanel { Dock = DockStyle.Fill, BackColor = BackColor };
        gridHost.Controls.Add(grid);
        gridHost.Controls.Add(verticalScroll);
        gridHost.Controls.Add(horizontalScroll);
        gridHost.Paint += (_, e) => ThemeFramePainter.DrawFrame(e.Graphics, gridHost.ClientRectangle, ThemeStore.Current);
        Controls.Add(gridHost); Controls.Add(footer); Controls.Add(title);
        var theme = ThemeStore.Current;
        BackColor = title.BackColor = footer.BackColor = actions.BackColor = gridHost.BackColor = theme.Background;
        if (theme.Kind is ThemeKind.Parchment or ThemeKind.Wood or ThemeKind.Garden)
            title.BackColor = footer.BackColor = actions.BackColor = Color.Transparent;
        gridHost.Padding = theme.Kind == ThemeKind.Classic ? Padding.Empty : new Padding(7);
        title.ForeColor = summary.ForeColor = theme.Text;
        export.ApplyTheme(theme, theme.IsLight ? theme.BorderDark : theme.BorderLight);
        import.ApplyTheme(theme, theme.ScrollThumb);
        restoreAll.ApplyTheme(theme, theme.ScrollThumb);
        refresh.ApplyTheme(theme, theme.IsLight ? theme.BorderDark : theme.BorderLight);
        if (theme.Kind == ThemeKind.Garden)
        {
            var rose = Color.FromArgb(243, 145, 182);
            foreach (var button in new[] { refresh, export, import, restoreAll })
                button.ApplyTheme(theme, rose);
        }
        verticalScroll.ApplyTheme(theme);
        horizontalScroll.ApplyTheme(theme);
        ApplyGridTheme();
        Shown += (_, _) => UpdateScrollBars();
        LoadResults();
    }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill;
        grid.BackgroundColor = Color.FromArgb(16, 49, 72);
        grid.BorderStyle = BorderStyle.None;
        grid.ScrollBars = ScrollBars.None;
        grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false; grid.AutoGenerateColumns = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(26, 97, 125), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9.5f), Padding = new Padding(5, 2, 5, 2) };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(19, 58, 83), ForeColor = Color.FromArgb(231, 249, 247), SelectionBackColor = Color.FromArgb(31, 112, 132), SelectionForeColor = Color.White, Padding = new Padding(4, 3, 4, 3) };
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Use", HeaderText = T("Restaurar", "Restore"), Width = 78 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mod", HeaderText = "Mod", Width = 280, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UniqueId", HeaderText = "UniqueID", Width = 260, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = T("Situação", "Status"), Width = 160, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Detail", HeaderText = T("Detalhes", "Details"), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 360, ReadOnly = true });
        grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Use" && grid.Rows[e.RowIndex].Tag is ConfigResult result)
                result.Selected = Convert.ToBoolean(grid.Rows[e.RowIndex].Cells["Use"].Value);
        };
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        verticalScroll.ValueChanged += (_, _) =>
        {
            if (grid.Rows.Count == 0) return;
            try { grid.FirstDisplayedScrollingRowIndex = Math.Min(verticalScroll.Value, grid.Rows.Count - 1); }
            catch (InvalidOperationException) { }
        };
        horizontalScroll.ValueChanged += (_, _) => grid.HorizontalScrollingOffset = horizontalScroll.Value;
        grid.Scroll += (_, _) => UpdateScrollBars();
        grid.SizeChanged += (_, _) => UpdateScrollBars();
        grid.ColumnWidthChanged += (_, _) => UpdateScrollBars();
        grid.MouseWheel += (_, e) =>
        {
            if (!verticalScroll.Visible) return;
            int lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
            int notches = Math.Max(1, Math.Abs(e.Delta) / 120);
            verticalScroll.Value -= Math.Sign(e.Delta) * lines * notches;
        };
    }

    private void ApplyGridTheme()
    {
        var theme = ThemeStore.Current;
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
    }

    private void UpdateScrollBars()
    {
        if (updatingScrollBars || !grid.IsHandleCreated) return;
        updatingScrollBars = true;
        try
        {
            int visibleRows = grid.DisplayedRowCount(false);
            verticalScroll.Visible = grid.Rows.Count > visibleRows;
            verticalScroll.SetMetrics(grid.Rows.Count, Math.Max(1, visibleRows),
                grid.Rows.Count == 0 ? 0 : Math.Max(0, grid.FirstDisplayedScrollingRowIndex));
            int contentWidth = grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible);
            int viewportWidth = Math.Max(1, grid.ClientSize.Width);
            horizontalScroll.Visible = contentWidth > viewportWidth;
            horizontalScroll.SetMetrics(contentWidth, viewportWidth, grid.HorizontalScrollingOffset);
        }
        finally { updatingScrollBars = false; }
    }

    private void LoadResults()
    {
        try
        {
            results = configService.Compare(libraryRoot, modService.ScanMods(modsRoot));
            grid.Rows.Clear();
            foreach (var result in results)
            {
                var row = grid.Rows[grid.Rows.Add(result.Selected, result.Mod.DisplayName, result.Mod.UniqueId, StatusText(result.State), result.Detail)];
                row.Tag = result;
                row.Cells["Use"].ReadOnly = result.State != ConfigState.ReadyToRestore;
                row.DefaultCellStyle.BackColor = ThemeStore.Current.ConfigRowFor(result.State);
                row.DefaultCellStyle.ForeColor = ThemeStore.Current.Text;
                row.DefaultCellStyle.SelectionBackColor = ThemeStore.Current.GridSelection;
                row.DefaultCellStyle.SelectionForeColor = ThemeStore.Current.GridSelectionText;
            }
            summary.Text = string.Format(T("{0} mods encontrados • {1} configuração(ões) prontas para restaurar", "{0} mods found • {1} config(s) ready to restore"), results.Count, results.Count(x => x.State == ConfigState.ReadyToRestore));
            UpdateScrollBars();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, T("Não foi possível ler as configurações", "Could not read settings"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ExportAll()
    {
        try
        {
            var count = configService.ExportAll(libraryRoot, modService.ScanMods(modsRoot));
            LoadResults();
            MessageBox.Show(this, string.Format(T("{0} config.json salvo(s) em:\n{1}", "{0} config.json file(s) saved to:\n{1}"), count, configService.GetStorageRoot(libraryRoot)), T("Backup concluído", "Backup complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, T("Não foi possível exportar", "Could not export"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void RestoreSelected()
    {
        var selected = results.Where(x => x.Selected && x.State == ConfigState.ReadyToRestore).ToList();
        if (selected.Count == 0) return;
        var restored = 0;
        foreach (var result in selected)
        {
            var answer = MessageBox.Show(this, string.Format(T("Restaurar a configuração salva de:\n{0}?\n\nA configuração atual será guardada em um backup datado antes da substituição.", "Restore the saved configuration for:\n{0}?\n\nThe current configuration will be backed up before replacement."), result.Mod.DisplayName), T("Confirmar restauração", "Confirm restore"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) break;
            if (answer != DialogResult.Yes) continue;
            try { configService.Restore(libraryRoot, result); restored++; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, T("Não foi possível restaurar ", "Could not restore ") + result.Mod.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        LoadResults();
        if (restored > 0) MessageBox.Show(this, string.Format(T("{0} configuração(ões) restaurada(s).", "{0} configuration(s) restored."), restored), T("Concluído", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RestoreAll()
    {
        LoadResults();
        var pending = results.Where(x => x.State == ConfigState.ReadyToRestore).ToList();
        if (pending.Count == 0)
        {
            MessageBox.Show(this, T("Não há configurações diferentes ou ausentes para restaurar.", "There are no missing or different settings to restore."), T("Nada para restaurar", "Nothing to restore"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var existing = pending.Count(x => File.Exists(Path.Combine(x.Mod.Directory, "config.json")));
        var answer = MessageBox.Show(this,
            string.Format(T("Restaurar {0} configuração(ões) salva(s)?\n\n{1} arquivo(s) atual(is) diferente(s) serão guardados em backup datado antes da substituição. Os demais configs ausentes serão criados.",
                "Restore {0} saved configuration(s)?\n\n{1} different existing file(s) will be saved in a dated backup before replacement. Missing configs will be created."), pending.Count, existing),
            T("Confirmar restauração de tudo", "Confirm restore all"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

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
        MessageBox.Show(this, message, T("Restauração concluída", "Restore complete"), MessageBoxButtons.OK,
            failures.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private static string StatusText(ConfigState state) => state switch
    {
        ConfigState.ReadyToRestore => T("Confirmar restauração", "Confirm restore"),
        ConfigState.SameAsSaved => T("Já está salva", "Already backed up"),
        ConfigState.NoSavedCopy => T("Sem cópia salva", "No backup"),
        _ => T("Falhou", "Failed")
    };
}
