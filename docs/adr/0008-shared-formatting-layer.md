# ADR-008: 渲染格式化收敛为共享层，删除 TooltipText 死代码

- 状态：已接受（2026-08-12，架构复盘质询轮 4）
- 关联：[[0006-ui-context-capture-fix]]、[[0004-overall-percent-min-semantics]]、[[glossary]]

## 背景

渲染层存在三重复制且已开始漂移：

- `FormatReset`（剩余时间文案）在 `UsageSnapshot` / `DetailForm` / `TooltipForm` 各有一份，
  行为已有差异（snapshot 版无秒级，tooltip 版有秒级倒计时）；
- 百分比配色逻辑（`GetColorForPercent`）与调色板常数在 DetailForm / TooltipForm（及图标工厂）
  重复；
- `UsageSnapshot.TooltipText` 自 v1.1.0 UI 重写后无任何调用者（全树 grep 仅自身引用），
  为死代码。

## 决策

- **收敛共享格式化层**：`FormatReset`、百分比配色、调色板常数收敛到单一静态类
  （如 `Model/Formatting`），两个窗体（tooltip / 详情）与图标工厂统一引用。
- **删除死代码** `UsageSnapshot.TooltipText`（连同其私有 `FormatReset`）。
- **布局代码不合并**：tooltip（无边框、TopMost、非激活、透明）与详情窗（对话框、带按钮、
  居中）是两种不同的表面，各自保留独立布局实现——共享的是"值如何格式化"，不是"如何摆放"。

## 影响

- 正向：格式化行为单一来源，漂移根除；删除死代码降低误读成本。
- 负向：无——纯重构，行为零变化（共享层实现按现状逐字搬运，含 tooltip 的秒级格式）。

## 验收标准

- 三处 `FormatReset` 收敛为一处；编译零警告。
- tooltip / 详情窗 / 图标的文案与颜色与现状逐字一致。
- `TooltipText` 及其私有辅助删除后无编译错误。
