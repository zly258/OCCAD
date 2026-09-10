# OCCAD 文档索引

这里维护 OCCAD 的长期产品、用户、架构、开发、扩展、构建、质量和维护文档。

## 阅读顺序

### 使用 OCCAD

1. [13 用户指南](13-USER-GUIDE.md)
2. [14 初版功能矩阵](14-FEATURE-MATRIX.md)
3. [02 UI 规范](02-UI-SPEC.md)
4. [03 交互规范](03-INTERACTION-SPEC.md)

### 新开发者

1. [01 产品目标](01-PRODUCT-GOALS.md)
2. [04 系统架构](04-ARCHITECTURE.md)
3. [05 Entity / Tool 契约](05-ENTITY-TOOL-CONTRACT.md)
4. [07 代码组织](07-CODE-ORGANIZATION.md)
5. [08 开发指南](08-DEVELOPMENT-GUIDE.md)
6. [09 扩展指南](09-EXTENSION-GUIDE.md)
7. [10 事务、事件与 Native 资源契约](10-TRANSACTION-RESOURCE-CONTRACT.md)

### 构建、验收与维护

- [06 质量与数据](06-QUALITY-AND-DATA.md)
- [11 构建与验收](11-BUILD-VALIDATION.md)
- [12 维护与故障排查](12-MAINTENANCE-TROUBLESHOOTING.md)
- [ADR 索引](../adr/README.md)

## 主题列表

1. `01-PRODUCT-GOALS.md`：产品定位、边界、目标与非目标。
2. `02-UI-SPEC.md`：极简 Fluent 紧凑 CAD 壳、Panel、Toolbar、Status Strip 规范。
3. `03-INTERACTION-SPEC.md`：Pointer、Selection、Snap、Grip、WorkPlane、Preview 行为。
4. `04-ARCHITECTURE.md`：分层、依赖、权威状态、组件职责与事件边界。
5. `05-ENTITY-TOOL-CONTRACT.md`：Entity 与 Tool 的实现契约。
6. `06-QUALITY-AND-DATA.md`：数据、精度、本地化、性能与质量约束。
7. `07-CODE-ORGANIZATION.md`：目录、命名、代码卫生和仓库规则。
8. `08-DEVELOPMENT-GUIDE.md`：环境、开发流程、编码与提交前检查。
9. `09-EXTENSION-GUIDE.md`：新增 Entity、Tool、Action、属性、Snap/Grip、UI Adapter 的标准方法。
10. `10-TRANSACTION-RESOURCE-CONTRACT.md`：Transaction/History/Event/Preview/Native ownership 不变量。
11. `11-BUILD-VALIDATION.md`：本地构建、Native 验证和发布前回归矩阵。
12. `12-MAINTENANCE-TROUBLESHOOTING.md`：常见故障定位、日志、Native 资源与维护策略。
13. `13-USER-GUIDE.md`：安装、运行、界面、绘图、属性、捕捉、夹点、保存与常见操作。
14. `14-FEATURE-MATRIX.md`：初版实际注册功能、绘制方式、支持面和暂不暴露功能。

## 文档治理

- `docs/zh-CN` 与 `docs/en-US` 文件编号和主题保持一致。
- 文档写稳定契约和当前真实产品面，不写逐 commit 历史。
- 修改架构、生命周期、公共 API、事务、持久化或扩展点时，同一变更必须同步更新对应文档。
- 跨模块、长期有效且有明显取舍的架构决策，应新增 ADR。
- 源码与文档不一致时，以源码为事实，但该不一致本身视为缺陷。
