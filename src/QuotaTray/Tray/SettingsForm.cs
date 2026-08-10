using QuotaTray.Infra;

namespace QuotaTray.Tray;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown _interval = new();
    private readonly NumericUpDown _warningThreshold = new();
    private readonly NumericUpDown _greenThreshold = new();
    private readonly CheckBox _autostart = new();
    private readonly Button _save = new();
    private readonly Button _cancel = new();
    private readonly Settings _settings;

    public SettingsForm(Settings settings)
    {
        _settings = settings;
        Text = "设置 - QuotaTray";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 260);
        Font = new Font("Segoe UI", 9f);

        var intervalLabel = new Label
        {
            Text = "刷新间隔（分钟）",
            Location = new Point(20, 25),
            AutoSize = true,
        };
        SetupNumeric(_interval, 1, 60, _settings.RefreshIntervalMinutes, 130, 22);

        var warnLabel = new Label
        {
            Text = "告警阈值（%，低于则通知）",
            Location = new Point(20, 65),
            AutoSize = true,
        };
        SetupNumeric(_warningThreshold, 0, 100, _settings.WarningThresholdPercent, 130, 62);

        var greenLabel = new Label
        {
            Text = "绿色阈值（%，高于则显示绿色）",
            Location = new Point(20, 105),
            AutoSize = true,
        };
        SetupNumeric(_greenThreshold, 0, 100, _settings.GreenThresholdPercent, 130, 102);

        _autostart.Text = "开机自启";
        _autostart.Location = new Point(20, 142);
        _autostart.AutoSize = true;
        _autostart.Checked = Autostart.IsEnabled();

        _save.Text = "保存";
        _save.Location = new Point(200, 190);
        _save.Size = new Size(90, 32);
        _save.Click += OnSave;

        _cancel.Text = "取消";
        _cancel.Location = new Point(300, 190);
        _cancel.Size = new Size(90, 32);
        _cancel.DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[]
        {
            intervalLabel, _interval,
            warnLabel, _warningThreshold,
            greenLabel, _greenThreshold,
            _autostart,
            _save, _cancel,
        });

        AcceptButton = _save;
        CancelButton = _cancel;
    }

    private static void SetupNumeric(NumericUpDown n, int min, int max, int value, int x, int y)
    {
        n.Minimum = min;
        n.Maximum = max;
        n.Value = Math.Clamp(value, min, max);
        n.Location = new Point(x, y);
        n.Size = new Size(80, 22);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _settings.RefreshIntervalMinutes = (int)_interval.Value;
        _settings.WarningThresholdPercent = (int)_warningThreshold.Value;
        _settings.GreenThresholdPercent = (int)_greenThreshold.Value;

        var newAutostart = _autostart.Checked;
        if (Autostart.IsEnabled() != newAutostart)
        {
            Autostart.Set(newAutostart);
        }

        _settings.Save();
        DialogResult = DialogResult.OK;
        Close();
    }
}
