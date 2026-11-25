namespace TradutorModsStardew;

internal static class ThemedMessageBox
{
    public static DialogResult Show(IWin32Window owner, string message, string caption,
        MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        var theme = ThemeStore.Current;
        using var dialog = new Form
        {
            Text = caption,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            Font = new Font("Segoe UI", 10),
            BackColor = theme.Background,
            ForeColor = theme.Text
        };
        WindowIcon.Apply(dialog);
        dialog.HandleCreated += (_, _) => WindowsTitleBar.Apply(dialog, theme);

        const int textWidth = 548;
        var measured = TextRenderer.MeasureText(message, dialog.Font,
            new Size(textWidth, 10000),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        int messageHeight = Math.Max(56, measured.Height + 12);
        int viewportHeight = Math.Min(messageHeight, 320);
        dialog.ClientSize = new Size(620, viewportHeight + 102);

        var surface = new Panel
        {
            Location = new Point(12, 12),
            Size = new Size(dialog.ClientSize.Width - 24, dialog.ClientSize.Height - 24),
            BackColor = theme.Panel
        };
        surface.Paint += (_, e) => ThemeFramePainter.DrawFrame(e.Graphics, surface.ClientRectangle, theme);
        var messageHost = new Panel
        {
            Location = new Point(20, 18),
            Size = new Size(surface.Width - 40, viewportHeight),
            AutoScroll = true,
            BackColor = theme.Panel
        };
        var text = new Label
        {
            Text = message,
            Location = Point.Empty,
            Size = new Size(textWidth, messageHeight),
            ForeColor = theme.Text,
            BackColor = theme.Panel
        };
        messageHost.Controls.Add(text);
        surface.Controls.Add(messageHost);

        var choices = buttons switch
        {
            MessageBoxButtons.OK => new[] { (UiLanguage.Text("OK", "OK"), DialogResult.OK) },
            MessageBoxButtons.YesNo => new[]
            {
                (UiLanguage.Text("Sim", "Yes"), DialogResult.Yes),
                (UiLanguage.Text("Não", "No"), DialogResult.No)
            },
            MessageBoxButtons.YesNoCancel => new[]
            {
                (UiLanguage.Text("Sim", "Yes"), DialogResult.Yes),
                (UiLanguage.Text("Não", "No"), DialogResult.No),
                (UiLanguage.Text("Cancelar", "Cancel"), DialogResult.Cancel)
            },
            _ => throw new NotSupportedException($"Unsupported dialog buttons: {buttons}")
        };
        var accent = icon switch
        {
            MessageBoxIcon.Error => Color.FromArgb(208, 83, 91),
            MessageBoxIcon.Warning => Color.FromArgb(239, 187, 74),
            _ => theme.ScrollThumb
        };
        int left = surface.Width - 20 - choices.Length * 112 - (choices.Length - 1) * 8;
        for (int index = 0; index < choices.Length; index++)
        {
            var (label, result) = choices[index];
            var button = new GlowButton
            {
                Text = label,
                DialogResult = result,
                AutoSize = false,
                Size = new Size(112, 40),
                Location = new Point(left + index * 120, surface.Height - 58)
            };
            button.ApplyTheme(theme, index == 0 ? accent : theme.BorderLight);
            surface.Controls.Add(button);
            if (index == 0) dialog.AcceptButton = button;
            if (result == DialogResult.Cancel || result == DialogResult.No)
                dialog.CancelButton = button;
        }
        dialog.Controls.Add(surface);
        return dialog.ShowDialog(owner);
    }
}
