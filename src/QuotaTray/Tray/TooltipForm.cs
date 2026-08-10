namespace QuotaTray.Tray;

internal sealed class TooltipForm : Form
{
    private readonly Label _label;

    public TooltipForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10, 8, 10, 8);
        BackColor = Color.FromArgb(36, 36, 36);
        _label = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9f),
            MaximumSize = new Size(420, 0),
        };
        Controls.Add(_label);
    }

    public void ShowAt(Point screenPosition, string text)
    {
        _label.Text = text;
        var size = _label.GetPreferredSize(new Size(420, 0));
        ClientSize = new Size(size.Width + 20, size.Height + 16);

        var working = Screen.FromPoint(screenPosition).WorkingArea;
        var x = Math.Min(screenPosition.X + 12, working.Right - Width);
        var y = Math.Min(screenPosition.Y + 14, working.Bottom - Height);
        Location = new Point(x, y);

        Show();
        BringToFront();
    }

    protected override bool ShowWithoutActivation => true;
}
