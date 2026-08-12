# ADR-002: CredentialStore 降级为登录标记，不再持久化 cookie 内容

- 状态：已接受（2026-08-12，架构复盘质询轮 2）
- 关联：[[0001-hidden-webview2-on-demand]]、[[glossary]]

## 背景

架构复盘审计 CredentialStore 全部使用点，发现：

| 条目 | 写入内容 | 读取处 |
|---|---|---|
| `ChatGptCookies` | 登录时从 WebView2 导出的完整 cookie 串 | 仅 `Read() != null` 当"是否登过"的布尔标记；**内容从未被回读** |
| `ZenJwt` | `"ok:" + workspaceId`（名不副实，非 JWT） | 同样仅当布尔标记；workspaceId 已另存 settings.json |

真实认证会话由 WebView2 的 UDP（见 ADR-001）承载并自动轮换，凭据管理器中的拷贝与认证机制完全脱钩：
cookie 明文以机器级（`PersistLocalMachine`）长期留存、永不轮换，形成只进不出的安全隐患。
条目由来已不可考（遗留），无任何使用者。

## 决策

- **停止写入凭据内容**：登录成功只写入标记 `"ok"`，不再导出/保存 cookie 串，不再向凭据管理器冗余写 workspaceId。
- **标记语义保留**：首登判定（`Read() != null`）与升级路径不变——`Save()` 现有清理逻辑（删除 `#0..#63` 与 `#count` 旧段）在下次登录时自然清掉存量数据。
- **存量清理**：版本上线时对旧条目做一次性删除（`CredentialStore.Delete` 后写入 `"ok"`），覆盖从不重新登录的用户。
- **登录成功判定重构**：`SaveChatGptAsync` 原先用"cookie 数量 > 0"判定登录成功，现改为校验页面会话信号
  （如 `/api/auth/session` 是否返回 `accessToken`），不再依赖导出 cookie。
- `ZenJwt` 目标名保留（历史兼容，条目内只存 `"ok"`）；`ChatGptCookies` 目标名同样保留。

## 影响

- 正向：凭据管理器不再留存明文 cookie；凭据内容与命名错位问题消除；安全面收窄。
- 负向：放弃"未来脱离 WebView2 直接注入 cookie 发请求"的可能性（该路线本就不可行——
  cookie 从不轮换，到启用时必然过期，见 ADR-001 的 UDP 结论）。
- 无功能回归：首登判定、重新登录、401 检测路径均不变。

## 验收标准

- 登录后凭据管理器内仅存 `"ok"` 标记（单段），无 `#n` 分段。
- 首次运行 / 重新登录的判定行为与现状一致。
- 存量多段 cookie 条目在升级后被清理。
