# OCCAD CAD 开发规范

本目录只保留长期有效的产品与实现契约，不记录临时开发计划、当前缺口、某次提交状态、本地验证流水账或阶段性验收表。

## 文档

- [01 产品目标](01-PRODUCT-GOALS.md)
- [02 UI 规范](02-UI-SPEC.md)
- [03 交互规范](03-INTERACTION-SPEC.md)
- [04 架构](04-ARCHITECTURE.md)
- [05 Entity 与 Tool 契约](05-ENTITY-TOOL-CONTRACT.md)
- [06 质量与数据](06-QUALITY-AND-DATA.md)
- [07 代码组织](07-CODE-ORGANIZATION.md)

## 基线原则

正确交互优先于命令数量。Entity geometry 与 Document state 是权威数据，Viewer object 只是派生显示状态。Tool 是阶段式状态机，Preview 在 Commit 前不得进入持久状态。Grip、Snap、Property、History、Selection 各自只有一条 Core 权威路径。Avalonia 只适配 Core，不维护第二套业务模型。

OCCAD 直接消费已安装的 OcctCSharpBridge SDK，不携带 Bridge 源码、兼容包装、反射调度、迁移框架，也不增加 V1/V2/Advanced/Extended 等重复 API。
