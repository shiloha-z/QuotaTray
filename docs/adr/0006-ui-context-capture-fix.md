# ADR-006: 删除 UI 线程手动 marshal（_postCtx），首刷移至 Application.Idle

- 状态：已接受（2026-08-12，架构复盘质询轮 4）
- 关联：[[0004-overall-percent-min-semantics]]、[[0005-per-source-degradation]]、[[glossary]]

## 背景

复盘发现 `TrayApp` 的图标颜色更新路径是死代码。证据链（探针实测 + 代码确认）：

1. `Application.Run(new TrayApp())` 之前 `SynchronizationContext.Current == NULL`
   （探针在 Main、Initialize() 后、ApplicationContext 构造器内三处实测均为 NULL）；
2. 上下文在**创建控件句柄时**才被自动安装（探针：`NotifyIcon.Visible` 后即变为
   `WindowsFormsSynchronizationContext`）；
3. `_postCtx` 在构造器第 32 行捕获，早于第一个句柄创建（第 42 行 `_icon.Visible = true`），
   **捕获值恒为 NULL**；
4. 唯一刷新图标颜色的路径 `_postCtx?.Post(_ => UpdateUi(), null)` 因此从不执行；
   tooltip / 详情窗直接读 `_snapshot` 故正常——数据全在动、唯独图标永远灰色，
   仅当用户在设置里点"确定"时（`ApplySettings` 顺带调 `UpdateUi`）才会偶发更新。

连带问题：构造器里 `_ = RefreshNowAsync()` 的首刷在上下文就绪前启动，
`HiddenFetchWebView` 检测到 "no UI context" 直接返回 status=0，首刷必然失败一次。

## 决策

- **删除 `_postCtx` 字段与手动 marshal**：`RefreshNowAsync` 的所有调用点
  （定时器 / 右键菜单 / 详情窗刷新回调 / 重登后）都在 UI 线程，且其 await 续体
  由 WinForms 同步上下文自动回到 UI 线程——直接调用 `UpdateUi()` 即可。
- **首刷从构造器移至 `Application.Idle`**：与 `FirstLoginOnIdle` 同款时机
  （消息循环就绪后再执行），顺带消除首刷 "no UI context" 失败。

## 影响

- 正向：图标颜色按余量正常更新；首刷成功；整个间接层删除，此类 bug 不再可能发生。
- 负向：无。行为变化即修复本身。

## 验收标准

- 首次刷新完成后图标即为对应余量颜色（绿/黄/红/灰），无需任何用户操作。
- 日志无 "no UI context" 记录。
- tooltip / 详情窗行为不变。
