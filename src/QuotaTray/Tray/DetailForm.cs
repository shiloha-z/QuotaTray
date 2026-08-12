using QuotaTray.Infra;
using QuotaTray.Model;

namespace QuotaTray.Tray;

internal sealed class DetailForm : Form
{
    private static readonly Color ColorGreen = Color.FromArgb(46, 160, 67);
    private static readonly Color ColorYellow = Color.FromArgb(220, 130, 30);
    private static readonly Color ColorRed = Color.FromArgb(210, 50, 45);
    private static readonly Color TextTitle = Color.FromArgb(40, 40, 40);
    private static readonly Color TextLabel = Color.FromArgb(110, 110, 110);
    private static readonly Color TextReset = Color.FromArgb(150, 150, 150);
    private static readonly Color TextDim = Color.FromArgb(170, 170, 170);

    private static readonly Font TitleFont = new("Microsoft YaHei UI", 11f, FontStyle.Bold);
    private static readonly Font DataFont = new("Microsoft YaHei UI", 10f);
    private static readonly string[] GoNames = { "5h 滚动", "每周", "每月" };

    private readonly Label _chatLabel = MakeData(TextLabel);
    private readonly Label _chatValue = MakeData(TextLabel);
    private readonly Label _chatReset = MakeData(TextReset);
    private readonly Label[,] _goLabels = new Label[3, 3];

    private readonly Label _updatedLabel = new()
    {
        AutoSize = true,
        ForeColor = TextDim,
        Font = new Font("Microsoft YaHei UI", 9f),
    };
    private readonly Button _refreshButton = new()
    {
        Text = "立即刷新",
        Width = 100,
        Height = 32,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowOnly, // 高 DPI 下文字变宽时自动增大，避免按钮文字被裁剪
        Font = new Font("Microsoft YaHei UI", 9.5f),
    };
    private readonly Button _closeButton = new()
    {
        Text = "关闭",
        Width = 80,
        Height = 32,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowOnly,
        Font = new Font("Microsoft YaHei UI", 9.5f),
    };

    private readonly TableLayoutPanel _table;
    private readonly System.Windows.Forms.Timer _countdownTimer;
    private UsageSnapshot _snapshot = new();
    private Settings _settings = new();

    public Func<Task>? OnRefresh { get; set; }

    public DetailForm()
    {
        Text = "用量详情 - QuotaTray";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 360);
        BackColor = Color.White;
        Font = DataFont;

        _table = new TableLayoutPanel
        {
            Location = new Point(20, 18),
            Size = new Size(420, 280),
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
            _goLabels[i, 0] = MakeData(TextLabel);
            _goLabels[i, 0].Text = GoNames[i];
            _goLabels[i, 1] = MakeData(TextLabel);
            _goLabels[i, 2] = MakeData(TextReset);
            _table.Controls.Add(_goLabels[i, 0], 0, 4 + i);
            _table.Controls.Add(_goLabels[i, 1], 1, 4 + i);
            _table.Controls.Add(_goLabels[i, 2], 2, 4 + i);
        }

        _refreshButton.Click += async (_, _) =>
        {
            _refreshButton.Enabled = false;
            try { if (OnRefresh is not null) await OnRefresh(); }
            finally { _refreshButton.Enabled = true; }
        };

        _closeButton.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { _table, _refreshButton, _closeButton, _updatedLabel });

        _countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _countdownTimer.Tick += (_, _) => UpdateLabels();

        FitToContent(); // 初始按内容确定窗体尺寸，避免出现裁剪
    }

    /// <summary>按表格内容自适应尺寸：表格可能比预设宽度更宽（高 DPI 或文字较长时），
    /// 固定大小会导致右侧文字被裁剪，故按 PreferredSize 重新摆放并调整窗体。</summary>
    private void FitToContent()
    {
        var pref = _table.PreferredSize; // 触发表格按当前文本重新计算
        _table.Size = pref;
        _table.PerformLayout();

        // 按钮按内容取尺寸（高 DPI 下文字变宽/变高时同步放大，初始尺寸为下限）
        var btnH = Math.Max(32, Math.Max(_refreshButton.PreferredSize.Height, _closeButton.PreferredSize.Height));
        _refreshButton.Size = new Size(Math.Max(100, _refreshButton.PreferredSize.Width), btnH);
        _closeButton.Size = new Size(Math.Max(80, _closeButton.PreferredSize.Width), btnH);

        var y = _table.Bottom + 14; // 按钮行
        var w = Math.Max(460, _table.Right + 20);
        _closeButton.Location = new Point(w - 20 - _closeButton.Width, y);
        _refreshButton.Location = new Point(_closeButton.Left - 10 - _refreshButton.Width, y);
        _updatedLabel.Location = new Point(20, y + 8);

        ClientSize = new Size(w, Math.Max(360, y + btnH + 18));
    }

    private static Label MakeTitle(string text) => new()
    {
        Text = text,
        Font = TitleFont,
        ForeColor = TextTitle,
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

    public void UpdateData(UsageSnapshot snapshot, Settings settings)
    {
        _snapshot = snapshot;
        _settings = settings;
        _updatedLabel.Text = "最后刷新: " + snapshot.RefreshedAt.ToString("HH:mm:ss");
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
            _chatValue.ForeColor = GetColorForPercent(_snapshot.ChatGptPercent);
            _chatReset.Text = $"重置于 {FormatReset(remaining)}";
        }
        else
        {
            _chatLabel.Text = "";
            _chatValue.Text = _snapshot.ChatGptDetail;
            _chatValue.ForeColor = TextReset;
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

        FitToContent(); // 文本可能变化，重新按内容布局
    }

    private void SetGoRow(int row, double usagePercent, long? resetSec)
    {
        var remaining = 100 - usagePercent;
        _goLabels[row, 1].Text = $"剩余 {remaining:0}%";
        _goLabels[row, 1].ForeColor = GetColorForPercent(remaining);
        _goLabels[row, 2].Text = $"重置于 {FormatReset(resetSec)}";
    }

    private Color GetColorForPercent(double? percent)
    {
        if (!percent.HasValue) return TextReset;
        if (percent.Value < _settings.WarningThresholdPercent) return ColorRed;
        if (percent.Value < _settings.GreenThresholdPercent) return ColorYellow;
        return ColorGreen;
    }

    private static string FormatReset(long? seconds)
    {
        if (!seconds.HasValue || seconds.Value <= 0) return "未知";
        var t = TimeSpan.FromSeconds(seconds.Value);
        if (t.TotalDays >= 1) return $"{t.Days} 天 {t.Hours} 小时";
        if (t.TotalHours >= 1) return $"{t.Hours} 小时 {t.Minutes} 分";
        if (t.TotalMinutes >= 1) return $"{t.Minutes} 分 {t.Seconds} 秒";
        return $"{t.Seconds} 秒";
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) _countdownTimer.Start();
        else _countdownTimer.Stop();
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