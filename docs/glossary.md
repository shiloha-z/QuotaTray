# QuotaTray 术语表

> 架构复盘过程中沉淀的概念定义。条目按字母序/首字母归类。

## 核心概念

- **隐藏 WebView2（HiddenFetchWebView）**：不可见的 WebView2 实例，在页面上下文中执行
  `fetch()`，从而携带与真实浏览器一致的会话与指纹（cookie、session token），规避站点风控。
  数据源抓取的唯一手段。

- **UDP（User Data Folder）**：WebView2 的用户数据目录。持久化 cookie / localStorage /
  站点会话。本项目路径：`%APPDATA%\AgentUsageChecker\webview\<profile>`（chatgpt / zen）。
  登录态的持久化载体——**进程销毁后登录态仍在**（见 ADR-001）。

- **保活（Keepalive）**：以常驻进程维持登录会话的旧设计。经实测仅对延迟有益、对正确性非必需，
  且常驻代价约 1 GB 内存，已被 ADR-001 废弃（改为按需创建）。

- **登录态（Login state）**：站点的认证会话。ChatGPT 侧 = cookie + `/api/auth/session`
  的 accessToken；Go 侧 = opencode.ai 会话 cookie（SSR 内嵌 `lite.subscription.get` 数据）。
  失效时 fetch 返回 401/403 → 数据源状态 AuthFailed → 图标置灰 → 手动重新登录。

- **剩余百分比语义（Remaining-percent semantics）**：全应用统一为"剩余"口径——
  ChatGPT 源的 usagePercent 本就是剩余；Go 源的 `usagePercent` 是已用，展示时换算为
  `100 - usagePercent`。整体余量 = 各窗口剩余百分比取最小值，语义为"距下一次封顶的最紧
  闸门"而非整体健康度（ADR-004）。
- **登录失效通知（Auth-expiry notification）**：数据源从正常转为 401/403 时弹一次系统通知，
  失效段内只弹一次、重新登录成功即重置，按源独立标记（ADR-003）。

## 应用结构

- **单实例（Single instance）**：以命名 Mutex（`Local\QuotaTray.SingleInstance`）保证
  只允许一个进程运行，重复启动弹提示后退出。

- **刷新周期（Refresh interval）**：定时轮询（默认 10 分钟，可配置）。每次并行抓取两个
  数据源，45 秒取消超时兜底。

- **告警档位（Alert thresholds）**：整体余量首次跌破 50% / 30% / 10% 时各弹一次系统通知；
  余量回升超过 +5 缓冲后重置标记，允许再次触发。

## 凭据与安全

- **凭据管理器（CredentialStore）**：Windows 凭据管理器的封装（多段分片存储）。历史版本曾
  持久化 cookie 串与 workspaceId；审计确认内容从未被回读，仅作"是否登录过"的布尔标记。
  已按 ADR-002 降级为纯标记（只存 `"ok"`）。

## 数据源

- **ChatGPT Plus Codex 周配额**：`chatgpt.com/backend-api/wham/usage` 返回的周限额
  已用百分比，经 JSONPath（`rate_limit.primary_window.used_percent`）取值。

- **opencode Go 套餐用量**：`opencode.ai/workspace/<id>/go` 页面 SSR 内嵌的
  `rollingUsage` / `weeklyUsage` / `monthlyUsage` 三窗口用量，正则解析
  `resetInSec` + `usagePercent`。
