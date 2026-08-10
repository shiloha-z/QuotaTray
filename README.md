# QuotaTray

Windows 托盘小工具：实时监控 AI 订阅用量 —— **ChatGPT Plus Codex 周配额** 与 **opencode Go 套餐用量**，鼠标悬停托盘图标即看。

## 功能

- 托盘常驻，悬停显示（数据与控制台官方口径一致）：
  - **ChatGPT Plus**：Codex 周限额剩余百分比 + 重置倒计时
  - **opencode Go**：5 小时滚动 / 每周 / 每月 剩余百分比 + 重置倒计时
- 图标颜色 = 各数据源最低余量：绿 >50% · 黄 20~50% · 红 <20% · 查询失败置灰
- 每 10 分钟自动刷新（托盘菜单可手动刷新）
- 登录态持久化：Windows 凭据管理器 + WebView2 配置，登录一次长期有效
- 开机自启（托盘菜单可开关）、单实例、纯托盘无主窗口

## 数据源与认证

| 数据 | 接口 | 认证 |
|---|---|---|
| ChatGPT Plus Codex 周配额 | `chatgpt.com/backend-api/wham/usage` | 浏览器 cookies + session access token |
| opencode Go 用量 | opencode.ai workspace `/go` 页 `lite.subscription.get`（SSR 内嵌数据） | WebView2 会话 cookies |

认证机制：**隐藏 WebView2 保活登录态 + 页面内 `fetch()`** —— 与真实浏览器同指纹，无 Cloudflare 风控问题；登录态过期时图标变灰，右键 → 重新登录即可。

## 使用

1. 运行 `QuotaTray.exe`（环境要求见下）
2. 首次运行自动弹出登录窗，按提示完成两个源的登录（ChatGPT 登录后点"完成并保存登录态"；Go 登录后进入 workspace 页面点保存）
3. 之后悬停托盘图标查看用量；登录失效时右键 → 重新登录

## 环境要求

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- WebView2 Runtime（Win11 自带，Win10 一般随 Edge 安装）

## 构建

```bash
dotnet publish src/QuotaTray/QuotaTray.csproj -c Release -r win-x64 \
  --self-contained false -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

输出：`src/QuotaTray/bin/Release/net8.0-windows/win-x64/publish/QuotaTray.exe`

## 已知限制

- 登录态过期需手动重新登录（右键菜单触发，WebView2 配置持久，一般只需点保存）
- Go 订阅为官方口径展示（`usagePercent`），不自行换算金额
