# OCCAD CAD 开发规范

本目录只维护长期有效的产品与 Core 实现契约，不记录临时计划、差距矩阵、提交记录、本地验证日志或 UI 试验。

- [01 产品目标](01-PRODUCT-GOALS.md)
- [03 交互规范](03-INTERACTION-SPEC.md)
- [04 系统架构](04-ARCHITECTURE.md)
- [05 Entity 与 Tool 契约](05-ENTITY-TOOL-CONTRACT.md)
- [06 质量与数据](06-QUALITY-AND-DATA.md)
- [07 代码组织](07-CODE-ORGANIZATION.md)

当前阶段不定义 UI 规范。`OCCAD.Avalonia` 仅保留空应用壳，待 Core 架构稳定后再重新设计 UI。

`OCCTBIM-Source/release-1.0` 是 Document / Entity / Layer / Property / Tool / Grip / Snap / Settings 的行为与职责边界基准；OCCAD 保留 C#/.NET/OcctCSharpBridge 的技术架构，不照搬 Qt 控件、单例、数据库实现或具体类层次。

核心原则：Entity geometry 与 Document/Model state 是权威数据；Layer 使用稳定 ID，名称只是可编辑元数据；Property 与 Tool 参数是 UI 无关的 Core schema；Selection、Snap、Grip、Preview、WorkPlane、History、Settings 各自只有一个 Core owner；Viewer/presentation 是派生状态；Avalonia 只能呈现和调用 Core，不能建立第二套 CAD 状态。
