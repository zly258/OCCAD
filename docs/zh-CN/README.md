# OCCAD CAD 核心开发规范

当前阶段只维护稳定的领域与实现契约，UI 已主动清空，不再把旧界面规范当作实现基线。

- [01 产品目标](01-PRODUCT-GOALS.md)
- [03 交互规范](03-INTERACTION-SPEC.md)
- [04 系统架构](04-ARCHITECTURE.md)
- [05 Entity 与 Tool 契约](05-ENTITY-TOOL-CONTRACT.md)
- [06 质量与数据](06-QUALITY-AND-DATA.md)
- [07 代码组织](07-CODE-ORGANIZATION.md)

核心原则：Entity geometry 与 Document state 是权威数据，Viewer state 是派生状态；Tool 是阶段式状态机；Preview、Grip、Snap、Property、History、Selection、Layer、Settings 各自只有一条 Core 权威链。

旧 UI 规范已随旧 UI 实现一起删除。只有核心架构完成与 `OCCTBIM-Source/release-1.0` 的语义对齐并通过测试后，才重新设计 Avalonia UI。
