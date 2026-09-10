# OCCAD CAD 开发规范

本文档集是 `cad` 分支唯一的产品、界面、交互和技术规范。第二轮审查同时对照 `OCCTBIM-Source` 的真实模块边界与当前 OCCAD 代码，不照搬 Qt，也不再保留与当前方向冲突的历史说明。

## 唯一产品界面目标

Avalonia 原生紧凑型 CAD 界面：`Menu + 常驻 ToolBar + Viewport + Dock + StatusBar + 非模态 ToolPanel`。不再恢复 Ribbon，不维护双 UI 壳。

## 文档索引

- [01 产品定位与开发目标](01-PRODUCT-GOALS.md)
- [02 界面规范](02-UI-SPEC.md)
- [03 交互规范](03-INTERACTION-SPEC.md)
- [04 系统架构](04-ARCHITECTURE.md)
- [05 Entity 与 Tool 开发契约](05-ENTITY-TOOL-CONTRACT.md)
- [06 开发路线](06-DEVELOPMENT-PLAN.md)
- [07 当前实现差距分析](07-CURRENT-GAP-ANALYSIS.md)
- [08 界面细节规范](08-UI-DETAILS.md)
- [09 稳定性与性能规范](09-ROBUSTNESS-PERFORMANCE.md)
- [10 本地化、数值与数据表达规范](10-LOCALIZATION-AND-DATA.md)
- [11 代码组织与项目清理规范](11-CODE-ORGANIZATION.md)
- [12 验收清单](12-ACCEPTANCE-CHECKLIST.md)

英文文档位于 `../en-US/`，两个目录保持相同文件结构和语义。

## 参考来源

主要参考 `OCCTBIM-Source` 的 system-architecture、tool、entity、action-system、dock-system、property-editor。借鉴的是 Document/Entity/Tool/Action/Dock/Property/Grip/Snap 的职责边界，而不是 Qt 控件、单例写法或具体类名。

## 最高优先级原则

正确交互优先于实体数量；Entity 是业务对象而不是 Shape 包装；Tool 是阶段式状态机；Action 是统一命令入口；Preview 不污染 Document/History；Grip/Property/Tool 参数修改必须可回滚；Snap/Tracking/WorkPlane/Precision 共用一套点解析；UI 不复制业务状态；ByLayer 与自定义外观必须同时存在；中文/英文完整覆盖；UI 只格式化数值而不降低模型精度；可恢复输入和几何错误不得导致应用退出。

## 如何使用

开发前先查 07 的差距和 06 的阶段；实现一个 Entity/Tool 时按 05；UI 改动按 02/08；交互按 03；异常/性能按 09；本地化和 Property 按 10；阶段结束按 12 验收并按 11 清理代码与提交。
