# OCCAD CAD 开发规范

本目录只维护长期有效的产品与实现契约，不记录临时计划、提交流水、当前缺口或本地验证日志。

- [01 产品目标](01-PRODUCT-GOALS.md)
- [02 UI 规范](02-UI-SPEC.md)
- [03 交互规范](03-INTERACTION-SPEC.md)
- [04 系统架构](04-ARCHITECTURE.md)
- [05 Entity 与 Tool 契约](05-ENTITY-TOOL-CONTRACT.md)
- [06 质量与数据](06-QUALITY-AND-DATA.md)
- [07 代码组织](07-CODE-ORGANIZATION.md)

核心原则：Entity geometry 与 Document state 是权威数据，Viewer state 是派生状态；Tool 是阶段式状态机；Preview、Grip、Snap、Property、History、Selection、Layer 各自只有一条 Core 权威链；Avalonia 只负责呈现和调用，不建立第二套业务模型。

OCCAD 直接消费已安装的 OcctCSharpBridge SDK，不携带 Bridge 源码、兼容包装、反射调度、migration 框架，也不增加 V1/V2/Advanced/Extended 等重复 API。
