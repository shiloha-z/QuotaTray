using QuotaTray.Infra;
using QuotaTray.Model;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace QuotaTray.Tray;

internal sealed class TooltipForm : Form
{
    private static readonly Font TitleFont = new("Microsoft YaHei UI", 11f, FontStyle.Bold);
    private static readonly Font DataFont = new("Microsoft YaHei UI", 10f);
    private static readonly string[] GoNames = { "5h 滚动", "每周", "每月" };

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    private readonly TableLayoutPanel _table;
    private readonly Label _chatLabel = MakeData(Formatting.TextLabel);
    private readonly Label _chatValue = MakeData(Formatting.TextLabel);
    private readonly Label _chatReset = MakeData(Formatting.TextReset);
    private readonly Label[,] _goLabels = new Label[3, 3];

    private readonly System.Windows.Forms.Timer _countdownTimer;
    private UsageSnapshot _snapshot = new();
    private Settings _settings = new();

    public TooltipForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;

        _table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 9,
            BackColor = Color.White,
        };
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 9; i++) _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _table.RowStyles[2] = new RowStyle(SizeType.Absolute, 8);

        var chatTitle = MakeTitle("ChatGPT Plus");
        _table.Controls.Add(chatTitle, 0, 0);
        _table.SetColumnSpan(chatTitle, 3);

        _table.Controls.Add(_chatLabel, 0, 1);
        _table.Controls.Add(_chatValue, 1, 1);
        _table.Controls.Add(_chatReset, 2, 1);

        var goTitle = MakeTitle("opencode Go");
        _table.Controls.Add(goTitle, 0, 3);
        _table.SetColumnSpan(goTitle, 3);

        string[] goNames = { "5h 滚动", "每周", "每月" };
        for (int i = 0; i < 3; i++)
        {
            _goLabels[i, 0] = MakeData(Formatting.TextLabel);
            _goLabels[i, 0].Text = GoNames[i];
            _goLabels[i, 1] = MakeData(Formatting.TextLabel);
            _goLabels[i, 2] = MakeData(Formatting.TextReset);
            _table.Controls.Add(_goLabels[i, 0], 0, 4 + i);
            _table.Controls.Add(_goLabels[i, 1], 1, 4 + i);
            _table.Controls.Add(_goLabels[i, 2], 2, 4 + i);
        }

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(16, 14, 16, 14),
        };
        content.Controls.Add(_table);
        Controls.Add(content);

        _countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _countdownTimer.Tick += (_, _) => UpdateLabels();

        UpdateLabels(); // 初始计算大小，避免 ShowAt 时尺寸为 0
    }

    private static Label MakeTitle(string text) => new()
    {
        Text = text,
        Font = TitleFont,
        ForeColor = Formatting.TextTitle,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 6),
    };

    private static Label MakeData(Color color) => new()
    {
        Font = DataFont,
        ForeColor = color,
        AutoSize = true,
        Margin = new Padding(0, 0, 18, 2),
    };

    private void FitToContent()
    {
        // 手动适配大小：table 内容 + content Panel 的 Padding
        var pref = _table.PreferredSize;
        ClientSize = new Size(pref.Width + 32, pref.Height + 28);
        SetRoundedRegion();
    }

    private void SetRoundedRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        const int r = 10;
        var path = new GraphicsPath();
        path.AddArc(0, 0, r, r, 180, 90);
        path.AddArc(Width - r, 0, r, r, 270, 90);
        path.AddArc(Width - r, Height - r, r, r, 0, 90);
        path.AddArc(0, Height - r, r, r, 90, 90);
        path.CloseFigure();
        Region = new Region(path);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        SetRoundedRegion();
    }

    public void UpdateData(UsageSnapshot snapshot, Settings settings)
    {
        _snapshot = snapshot;
        _settings = settings;
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        var elapsed = DateTime.Now - _snapshot.RefreshedAt;

        if (_snapshot.ChatGptPercent.HasValue && _snapshot.ChatGptResetSec.HasValue)
        {
            var remaining = _snapshot.ChatGptResetSec.Value - (long)elapsed.TotalSeconds;
            _chatLabel.Text = "周限额";
            _chatValue.Text = $"剩余 {_snapshot.ChatGptPercent.Value:0}%";
            _chatValue.ForeColor = Formatting.PercentColor(_snapshot.ChatGptPercent, _settings);
            _chatReset.Text = $"重置于 {Formatting.FormatReset(remaining)}";
        }
        else
        {
            _chatLabel.Text = "";
            _chatValue.Text = _snapshot.ChatGptDetail;
            _chatValue.ForeColor = Formatting.TextReset;
            _chatReset.Text = "";
        }

        if (_snapshot.GoStatus == SourceStatus.Ok &&
            _snapshot.Go5hPercent.HasValue && _snapshot.GoWeekPercent.HasValue && _snapshot.GoMonthPercent.HasValue)
        {
            _goLabels[0, 0].Text = GoNames[0];
            _goLabels[1, 0].Text = GoNames[1];
            _goLabels[2, 0].Text = GoNames[2];
            SetGoRow(0, _snapshot.Go5hPercent.Value, _snapshot.GoReset5hSec - (long)elapsed.TotalSeconds);
            SetGoRow(1, _snapshot.GoWeekPercent.Value, _snapshot.GoResetWeekSec - (long)elapsed.TotalSeconds);
            SetGoRow(2, _snapshot.GoMonthPercent.Value, _snapshot.GoResetMonthSec - (long)elapsed.TotalSeconds);
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                _goLabels[i, 0].Text = "";
                _goLabels[i, 1].Text = i == 0 ? _snapshot.GoDetail : "";
                _goLabels[i, 2].Text = "";
            }
        }

        FitToContent();
    }

    private void SetGoRow(int row, double usagePercent, long? resetSec)
    {
        var remaining = 100 - usagePercent;
        _goLabels[row, 1].Text = $"剩余 {remaining:0}%";
        _goLabels[row, 1].ForeColor = Formatting.PercentColor(remaining, _settings);
        _goLabels[row, 2].Text = $"重置于 {Formatting.FormatReset(resetSec)}";
    }

    public void ShowAt(Point screenPosition)
    {
        var working = Screen.FromPoint(screenPosition).WorkingArea;
        var x = Math.Min(screenPosition.X + 12, working.Right - Width);
        var y = Math.Min(screenPosition.Y + 14, working.Bottom - Height);
        Location = new Point(x, y);

        Show();
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        _countdownTimer.Start();
    }

    public new void Hide()
    {
        _countdownTimer.Stop();
        base.Hide();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
            return cp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _countdownTimer.Stop();
            _countdownTimer.Dispose();
            TitleFont.Dispose();
            DataFont.Dispose();
        }
        base.Dispose(disposing);
    }
}