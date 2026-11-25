using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace TradutorModsStardew;

// Keeps the native Windows selector for Classic and draws the themed selectors itself.
internal sealed class ThemeSelector : UserControl
{
    private readonly ComboBox native = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private AppTheme theme = ThemeStore.Current;
    private ToolStripDropDown? dropDown;

    public ThemeSelector()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.Selectable, true);
        Height = 27;
        MinimumSize = new Size(80, 27);
        native.SelectedIndexChanged += (_, _) =>
        {
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        };
        Controls.Add(native);
        ApplyTheme(theme);
    }

    public ComboBox.ObjectCollection Items => native.Items;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex { get => native.SelectedIndex; set => native.SelectedIndex = value; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem { get => native.SelectedItem; set => native.SelectedItem = value; }
    public event EventHandler? SelectedIndexChanged;

    public void ShowDropDown(Control anchor) => OpenDropDownAt(anchor);

    public void ApplyTheme(AppTheme value)
    {
        theme = value;
        bool classic = value.Kind == ThemeKind.Classic;
        native.Visible = classic;
        native.BackColor = value.Field;
        native.ForeColor = value.Text;
        BackColor = value.Field;
        TabStop = !classic;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (theme.Kind == ThemeKind.Classic) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        var bounds = ClientRectangle;
        using var shadow = new SolidBrush(theme.BorderDark);
        using var face = new SolidBrush(theme.Field);
        using var light = new SolidBrush(theme.BorderLight);
        using var mid = new SolidBrush(AppTheme.Blend(theme.BorderDark, theme.BorderLight, .56));
        g.FillRectangle(shadow, bounds);
        g.FillRectangle(mid, 2, 1, Width - 4, Height - 3);
        g.FillRectangle(face, 4, 3, Width - 8, Height - 7);
        g.FillRectangle(light, 3, 2, Width - 6, 2);
        g.FillRectangle(light, 2, 3, 2, Height - 7);
        g.FillRectangle(shadow, 3, Height - 4, Width - 6, 2);
        g.FillRectangle(shadow, Width - 4, 3, 2, Height - 7);
        // Square cut corners keep the border consistent with the pixel frames.
        g.FillRectangle(face, 0, 0, 2, 2);
        g.FillRectangle(face, Width - 2, 0, 2, 2);
        g.FillRectangle(face, 0, Height - 2, 2, 2);
        g.FillRectangle(face, Width - 2, Height - 2, 2, 2);

        int arrowCenterX = Width - 13;
        g.FillRectangle(mid, Width - 25, 4, 2, Height - 9);
        using var arrow = new SolidBrush(theme.Text);
        for (int row = 0; row < 3; row++)
            g.FillRectangle(arrow, arrowCenterX - 5 + row * 2, (Height - 6) / 2 + row * 2,
                11 - row * 4, 2);
        TextRenderer.DrawText(g, SelectedItem?.ToString() ?? "", Font,
            new Rectangle(9, 3, Math.Max(0, Width - 38), Height - 6), theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && theme.Kind != ThemeKind.Classic) OpenDropDown();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (theme.Kind == ThemeKind.Classic) return;
        if (e.KeyCode is Keys.Enter or Keys.Space or Keys.F4 || (e.Alt && e.KeyCode == Keys.Down))
        {
            OpenDropDown();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down && SelectedIndex < Items.Count - 1)
        {
            SelectedIndex++;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up && SelectedIndex > 0)
        {
            SelectedIndex--;
            e.Handled = true;
        }
    }

    private void OpenDropDown() => OpenDropDownAt(null);

    private void OpenDropDownAt(Control? anchor)
    {
        if (dropDown is not null || Items.Count == 0) return;
        if (anchor is null) Focus();
        int viewportHeight = Math.Min(Items.Count, 10) * 27;
        bool needsScroll = Items.Count > 10;
        var viewport = new Panel
        {
            Size = new Size(Width - 4, viewportHeight),
            BackColor = theme.Field
        };
        var list = new ListBox
        {
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 27,
            BackColor = theme.Field,
            ForeColor = theme.Text,
            Font = Font,
            IntegralHeight = false,
            Width = viewport.Width - (needsScroll ? 18 : 0),
            Height = Items.Count * 27,
            Location = Point.Empty
        };
        foreach (var item in Items) list.Items.Add(item);
        viewport.Controls.Add(list);
        ThemedScrollBar? scroll = null;
        if (needsScroll)
        {
            scroll = new ThemedScrollBar(Orientation.Vertical)
            {
                Dock = DockStyle.Right,
                Width = 18
            };
            scroll.ApplyTheme(theme);
            scroll.SetMetrics(list.Height, viewportHeight, 0);
            scroll.ValueChanged += (_, _) => list.Top = -scroll.Value;
            viewport.Controls.Add(scroll);
            scroll.BringToFront();
            void ScrollWheel(object? _, MouseEventArgs e) =>
                scroll.Value -= Math.Sign(e.Delta) * list.ItemHeight * 3;
            list.MouseWheel += ScrollWheel;
            viewport.MouseWheel += ScrollWheel;
        }
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            using var background = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? theme.Selection : theme.Field);
            e.Graphics.FillRectangle(background, e.Bounds);
            TextRenderer.DrawText(e.Graphics, list.Items[e.Index]?.ToString() ?? "", list.Font,
                new Rectangle(e.Bounds.X + 7, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height), theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };
        var host = new ToolStripControlHost(viewport)
        {
            AutoSize = false,
            Size = viewport.Size,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        var menu = new ToolStripDropDown
        {
            AutoSize = false,
            Size = new Size(Width, viewportHeight + 4),
            Padding = new Padding(2),
            BackColor = theme.BorderLight,
            DropShadowEnabled = true
        };
        menu.Items.Add(host);
        dropDown = menu;
        list.SelectedIndex = SelectedIndex;
        if (scroll is not null && list.SelectedIndex >= 10)
            scroll.Value = Math.Min(list.SelectedIndex * list.ItemHeight, list.Height - viewportHeight);
        list.SelectedIndexChanged += (_, _) =>
        {
            if (scroll is null || list.SelectedIndex < 0) return;
            int top = list.SelectedIndex * list.ItemHeight;
            if (top < scroll.Value) scroll.Value = top;
            else if (top + list.ItemHeight > scroll.Value + viewportHeight)
                scroll.Value = top + list.ItemHeight - viewportHeight;
        };
        list.MouseClick += (_, _) => Choose();
        list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { Choose(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { menu.Close(); e.Handled = true; }
        };
        void Choose()
        {
            if (list.SelectedIndex >= 0) SelectedIndex = list.SelectedIndex;
            menu.Close();
        }
        var owner = anchor ?? this;
        menu.Closed += (_, _) =>
        {
            dropDown = null;
            if (owner.IsHandleCreated) owner.BeginInvoke(() => { menu.Dispose(); owner.Focus(); });
            else menu.Dispose();
        };
        menu.Show(owner, new Point(anchor is null ? 0 : owner.Width - Width, owner.Height));
        list.Focus();
    }
}
