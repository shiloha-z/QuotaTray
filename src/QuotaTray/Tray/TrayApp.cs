using QuotaTray.Auth;
using QuotaTray.Infra;
using QuotaTray.Model;
using QuotaTray.Sources;

namespace QuotaTray.Tray;

internal sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _icon = new();
    private readonly TooltipForm _tip = new();
    private readonly ContextMenuStrip _menu = new();
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly System.Windows.Forms.Timer _hoverTimer;
    private readonly System.Windows.Forms.Timer _hoverWatchTimer;
    private readonly ChatGptUsage _chat = new();
    private readonly GoUsage _go = new();
    private Settings _settings = Settings.Load();
    private ToolStripMenuItem? _autostartItem;

    private UsageSnapshot _snapshot = new();
    private bool _refreshing;
    private Point _lastIconPos = Point.Empty;
    private bool _notified50;
    private bool _notified30;
    private bool _notified10;
    private DetailForm? _detailForm;

    public TrayApp()
    {
        Autostart.CleanupLegacy();
        if (!Autostart.IsEnabled())
        {
            Autostart.Set(true);
        }

        BuildMenu();

        _icon.Icon = IconFactory.Get(IconFactory.Gray, null);
        _icon.Visible = true;
        _icon.Text = "QuotaTray";

        _hoverTimer = new System.Windows.Forms.Timer { Interval = 600 };
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            _tip.UpdateData(_snapshot, _settings);
            _tip.ShowAt(Cursor.Position);
            _hoverWatchTimer?.Start();
        };

        _hoverWatchTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _hoverWatchTimer.Tick += (_, _) =>
        {
            var pos = Cursor.Position;
            var insideTip = _tip.Visible && _tip.Bounds.Contains(pos);
            var nearIcon = Math.Abs(pos.X - _lastIconPos.X) <= 60 && Math.Abs(pos.Y - _lastIconPos.Y) <= 60;
            if (!insideTip && !nearIcon)
            {
                _hoverWatchTimer.Stop();
                _tip.Hide();
            }
        };

        _icon.MouseMove += (_, _) =>
        {
            _lastIconPos = Cursor.Position;
            if (!_tip.Visible)
            {
                _hoverTimer.Stop();
                _hoverTimer.Start();
            }
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _hoverTimer.Stop();
                _tip.Hide();
                ShowDetail();
            }
        };

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(1, _settings.RefreshIntervalMinutes) * 60 * 1000,
        };
        _refreshTimer.Tick += async (_, _) => await RefreshNowAsync();
        _refreshTimer.Start();

        // 首刷与首登都延迟到 UI 消息循环启动后执行：
        // 构造器中 SynchronizationContext 尚未就绪（创建控件句柄后才安装），
        // 此时启动刷新会让 HiddenFetchWebView 拿到 "no UI context" 而失败（见 ADR-006）。
        // 首刷先于首登注册：登录弹窗 ShowDialog 期间（嵌套消息循环）刷新可正常完成。
        Application.Idle += FirstRefreshOnIdle;
        Application.Idle += FirstLoginOnIdle;
    }

    private void FirstRefreshOnIdle(object? sender, EventArgs e)
    {
        Application.Idle -= FirstRefreshOnIdle;
        _ = RefreshNowAsync();
    }

    private void FirstLoginOnIdle(object? sender, EventArgs e)
    {
        Application.Idle -= FirstLoginOnIdle;
        RunFirstLoginIfNeeded();
    }

    private void BuildMenu()
    {
        var refresh = new ToolStripMenuItem("立即刷新");
        refresh.Click += async (_, _) => await RefreshNowAsync();

        var detail = new ToolStripMenuItem("查看详情...");
        detail.Click += (_, _) => ShowDetail();

        var loginChat = new ToolStripMenuItem("重新登录 ChatGPT...");
        loginChat.Click += (_, _) => ShowLogin(LoginKind.ChatGpt);

        var loginZen = new ToolStripMenuItem("重新登录 Go...");
        loginZen.Click += (_, _) => ShowLogin(LoginKind.Zen);

        var settings = new ToolStripMenuItem("设置...");
        settings.Click += (_, _) => ShowSettings();

        _autostartItem = new ToolStripMenuItem("开机自启") { Checked = Autostart.IsEnabled() };
        _autostartItem.Click += (_, _) =>
        {
            Autostart.Set(!_autostartItem.Checked);
            _autostartItem.Checked = Autostart.IsEnabled();
        };

        var openLog = new ToolStripMenuItem("查看日志");
        openLog.Click += (_, _) => OpenLog();

        var openData = new ToolStripMenuItem("打开数据目录");
        openData.Click += (_, _) => System.Diagnostics.Process.Start("explorer.exe", AppPaths.DataDir);

        var exit = new ToolStripMenuItem("退出");
        exit.Click += (_, _) => Exit();

        _menu.Items.AddRange(new ToolStripItem[]
        {
            refresh, detail, new ToolStripSeparator(),
            loginChat, loginZen, new ToolStripSeparator(),
            settings, _autostartItem, openLog, openData, new ToolStripSeparator(),
            exit,
        });

        _icon.ContextMenuStrip = _menu;
    }

    private void ShowDetail()
    {
        if (_detailForm is null || _detailForm.IsDisposed)
        {
            _detailForm = new DetailForm { OnRefresh = RefreshNowAsync };
            _detailForm.FormClosed += (_, _) => _detailForm = null;
        }

        _detailForm.UpdateData(_snapshot, _settings);
        _detailForm.Show();
        _detailForm.BringToFront();
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            // 设置已保存到 _settings 实例 + 文件，重新加载并应用
            _settings = Settings.Load();
            ApplySettings();
        }
    }

    private void ApplySettings()
    {
        _refreshTimer.Stop();
        _refreshTimer.Interval = Math.Max(1, _settings.RefreshIntervalMinutes) * 60 * 1000;
        _refreshTimer.Start();

        if (_autostartItem is not null)
        {
            _autostartItem.Checked = Autostart.IsEnabled();
        }

        // 阈值变更后立即刷新 UI 颜色
        UpdateUi();
    }

    private static void OpenLog()
    {
        try
        {
            var file = Path.Combine(AppPaths.LogDir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            if (!File.Exists(file))
            {
                File.WriteAllText(file, "");
            }

            System.Diagnostics.Process.Start("explorer.exe", file);
        }
        catch (Exception ex)
        {
            Logger.Log("open log error: " + ex.Message);
        }
    }

    private async void ShowLogin(LoginKind kind)
    {
        if (kind == LoginKind.ChatGpt)
        {
            _chat.ResetSession();
        }
        else
        {
            _go.ResetSession();
        }

        using (var window = new LoginWindow(kind))
        {
            window.ShowDialog();
        }

        await Task.Delay(300);
        await RefreshNowAsync();
    }

    private void RunFirstLoginIfNeeded()
    {
        var hasChatGpt = CredentialStore.Read(CredentialTargets.ChatGptCookies) != null;
        var hasZen = CredentialStore.Read(CredentialTargets.ZenJwt) != null;

        if (!hasChatGpt || !hasZen)
        {
            _icon.ShowBalloonTip(8000, "QuotaTray",
                "首次使用，请完成登录：\nChatGPT（用量查询）" + (hasZen ? "" : "\nopencode Go（用量查询）"),
                ToolTipIcon.Info);
        }

        if (!hasChatGpt)
        {
            ShowLogin(LoginKind.ChatGpt);
        }

        if (!hasZen && CredentialStore.Read(CredentialTargets.ChatGptCookies) != null)
        {
            ShowLogin(LoginKind.Zen);
        }
    }

    internal async Task RefreshNowAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var settings = _settings;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            var snapshot = new UsageSnapshot { RefreshedAt = DateTime.Now };

            var chatTask = _chat.FetchAsync(settings.ChatGptEndpoint ?? "", settings.ChatGptJsonPath,
                settings.ChatGptMaxValue, settings.ChatGptValueIsRemaining, cts.Token);

            var goTask = _go.FetchAsync(settings.GoWorkspaceId ?? "", cts.Token);

            await Task.WhenAll(chatTask, goTask);

            (snapshot.ChatGptStatus, snapshot.ChatGptDetail, snapshot.ChatGptPercent,
                snapshot.ChatGptResetSec) = chatTask.Result;
            (snapshot.GoStatus, snapshot.GoDetail, snapshot.Go5hPercent, snapshot.GoWeekPercent,
                snapshot.GoMonthPercent, snapshot.GoReset5hSec, snapshot.GoResetWeekSec,
                snapshot.GoResetMonthSec) = goTask.Result;

            _snapshot = snapshot;
            Logger.Log($"REFRESH ok: chat={snapshot.ChatGptStatus}/{snapshot.ChatGptDetail} go={snapshot.GoStatus}/{snapshot.GoDetail}");
            // 所有调用点都在 UI 线程（定时器/菜单/详情窗/重登），await 续体由 WinForms
            // 同步上下文自动回到 UI 线程，无需手动 marshal（见 ADR-006）
            UpdateUi();
        }
        catch (Exception ex)
        {
            Logger.Log("refresh error: " + ex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void UpdateUi()
    {
        var s = _snapshot;
        double? percent = s.HasError ? null : s.OverallPercent;

        Color color;
        if (s.HasError || percent is null)
        {
            color = IconFactory.Gray;
        }
        else if (percent < _settings.WarningThresholdPercent)
        {
            color = IconFactory.Red;
        }
        else if (percent < _settings.GreenThresholdPercent)
        {
            color = IconFactory.Yellow;
        }
        else
        {
            color = IconFactory.Green;
        }

        _icon.Icon = IconFactory.Get(color, percent);

        // 更新 tooltip 数据
        _tip.UpdateData(s, _settings);
        if (_detailForm is not null && !_detailForm.IsDisposed)
        {
            _detailForm.UpdateData(s, _settings);
        }

        // 低余量通知：仅在降到 50% / 30% / 10% 时各通知一次
        CheckLowQuotaNotification(percent);
    }

    private void CheckLowQuotaNotification(double? percent)
    {
        if (!percent.HasValue)
        {
            return;
        }

        var current = percent.Value;

        // 余量回升超过档位 +5 缓冲时重置标记，避免在阈值附近抖动反复通知，并允许下次再触发
        if (current > 55) _notified50 = false;
        if (current > 35) _notified30 = false;
        if (current > 15) _notified10 = false;

        // 降到各档位时各通知一次（独立判断，一次跨多档可连续触发）
        if (current <= 50 && !_notified50)
        {
            _notified50 = true;
            NotifyLowQuota(current);
        }
        if (current <= 30 && !_notified30)
        {
            _notified30 = true;
            NotifyLowQuota(current);
        }
        if (current <= 10 && !_notified10)
        {
            _notified10 = true;
            NotifyLowQuota(current);
        }
    }

    private void NotifyLowQuota(double current)
    {
        _icon.ShowBalloonTip(8000, "QuotaTray 余量告警",
            $"当前最低余量仅剩 {current:0}%，请注意用量。",
            ToolTipIcon.Warning);
        Logger.Log($"NOTIFY low quota: {current:0.##}%");
    }

    private void Exit()
    {
        _hoverTimer.Stop();
        _hoverWatchTimer.Stop();
        _refreshTimer.Stop();
        _tip.Hide();
        _tip.Dispose();
        _detailForm?.Close();
        _detailForm?.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        ExitThread();
    }
}
