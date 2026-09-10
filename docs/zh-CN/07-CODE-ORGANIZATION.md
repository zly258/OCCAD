# 07 代码组织

## 1. 目的

本文描述当前仓库结构以及新增代码应放置的位置和 owner 规则。代码组织以本文为准，架构语义以 [04 系统架构](04-ARCHITECTURE.md) 为准。

## 2. Projects

### `src/OCCAD.Core`

包含 CAD 领域与交互逻辑，主要区域：

- `Actions/`：稳定命令入口和命令元数据；
- `Document/`：持久 Entity membership 与 presentation 同步；
- `Entities/`：持久 Entity 类型和几何语义；
- `Geometry/`：不拥有 UI 状态的可复用几何算法；
- `Grips/`：Grip 发现、Hot/Drag 状态和编辑交互；
- `History/`：Undo/Redo entry 与历史状态；
- `Interaction/`：Tool 生命周期、Preview/transient ownership、精确交互；
- `Layers/`：Layer 状态与当前层所有权；
- `Properties/`：Property descriptor、值语义和转换元数据；
- `Selection/`：Entity/Subobject/Preselection 状态；
- `Snapping/`：Snap 发现、排序、marker、cache/invalidation；
- persistence/exchange：文档持久化和外部格式边界。

`CadWorkspace` 组合一次 CAD Session。Core 不引用 Avalonia Control。

### `src/OCCAD.Avalonia`

只包含 Presentation 和 Adapter：

- `Application/`：Avalonia 应用启动与组合；
- `Shell/`：`MainWindow` partial、Menu、紧凑 Toolbar、Status、Tool 参数条；
- `Viewport/`：OCCT Viewport 适配与 Pointer 交互；
- `Input/`：直接键盘输入和 CAD Cursor；
- `Panels/Properties/`：PropertyGrid 展示与编辑器构造；
- `Panels/Layers/`：Layer Panel；
- `Panels/Model/`：Model Tree；
- `Dialogs/`：Dialog 和 Settings；
- `Localization/`：中英文资源；
- `Theming/`：CAD 专用布局指标和结构色；
- `Diagnostics/`：应用侧诊断。

当前产品不存在 Ribbon、永久 Command Line、浮动 Tool Panel 或独立 Dynamic HUD 子系统。除非产品方向明确改变，不重新引入这些已退出基线的目录和抽象。

## 3. 依赖方向

允许：

`OCCAD.Avalonia → OCCAD.Core → OcctNet/OcctCSharpBridge → OCCT`

禁止：

- Core → Avalonia；
- Entity/Document → UI Control；
- UI Controller 自己维护第二套 Document、Selection、Layer、Tool、History；
- 使用反射或 service locator 绕过明确 owner。

## 4. 文件落层规则

新增文件前先判断状态类别：

- 持久领域状态 → Document/Entity/Layer；
- 分阶段用户交互 → Tool/Interaction；
- 正式选择 → Selection；
- Snap/Grip 行为 → 对应 Core 子系统；
- 可撤销修改 → Transaction/History owner；
- 仅显示/输入适配 → Avalonia；
- 序列化兼容契约 → persistence/registry。

如果一个类无法归属唯一 owner，说明设计尚未收敛，不应直接编码。

## 5. 命名

原则上一个主要 public/internal 类型一个文件；紧密耦合的小 record/enum 可以与 owner 同文件。

使用稳定领域命名，例如 `CadEntity`、`CadWorkspace`、`CadToolManager`、`CadSelectionManager`、`CadPropertyTransaction`。

避免人为制造平行版本：`Advanced`、`Extended`、`Legacy`、`Compat`、`New`、`V1` / `V2`、`Helper2`，以及含义模糊的 `Common` / `Util`。

`Manager` 只表示真实生命周期/状态 owner。纯算法应按算法或变换职责命名。

## 6. 状态所有权

- Document 拥有持久 Entity membership；
- Entity 拥有正式 Geometry、Placement、Layer assignment、Appearance Override 和 Metadata；
- LayerManager 拥有 Layer 集合和 Current Layer；
- ToolManager 拥有 Active Tool 生命周期；
- Selection/SubobjectSelection/Preselection 各自拥有正式状态；
- PreviewManager 拥有 Tool Preview；
- SnapManager 拥有 Snap candidate/marker/current state；
- GripManager 拥有 Grip marker/hot/drag state；
- `CadTransientScene` 协调 transient channel ownership；
- History 拥有 Undo/Redo 状态；
- CadTheme 只拥有 Avalonia 公共视觉指标，不拥有业务状态。

Native Viewer Object 只是派生显示，不能成为独立业务真相。

## 7. 修改职责

Entity Property 修改使用 `CadPropertyTransaction` 或明确的 Core Transaction 路径。

Entity Appearance 语义：

- `ColorByLayer=true`、`LineStyleByLayer=true`、`LineWidthByLayer=true` 表示继承 Layer；
- 在 PropertyGrid 直接修改 Color/LineStyle/LineWidth 时，应在同一事务中关闭对应 ByLayer，形成 Entity **Override**；
- 重新启用 ByLayer 后恢复 Layer 继承；
- Viewer 刷新由最终 Core 状态派生。

Layer Color/Style/Width/Visibility/Locked 修改走 Core Layer/Workspace API；Tool Commit 走 Core Document/Transaction/History。Avalonia 可以解析输入和显示错误，但不实现独立 rollback。

## 8. MainWindow partial 与 Controller

`MainWindow` 按 Presentation 职责拆分。每个 partial 只适配或协调一种 UI 关注点，不成为 Core 行为的第二 owner。

当 partial 继续增大时，处理顺序是：

1. 先删除重复 refresh/subscription；
2. 可复用的纯 UI 行为拆成职责明确的小 Controller；
3. 业务行为移回真正的 Core owner；
4. 不为了缩短文件而建立一套平行框架。

“大文件”本身不一定是缺陷，重复 ownership 才是。

## 9. Dead Code 清理规则

只有同时满足以下条件时才删除：

1. 当前已注册产品面没有引用；
2. 不属于 persistence/registry 兼容；
3. 不是已文档化的 public extension contract；
4. 不参与当前 native/presentation 生命周期；
5. 删除后可通过真实 Build 验证。

不能仅因为某个 Entity/Geometry 类型没有 UI 入口就删除。Persistence、Feature Profile、Selection Filter 或旧文档数据仍可能依赖它。

发布收尾阶段优先删除 dead metadata、重复 adapter、过时文档、不可达 UI helper；最后才考虑删除 Core Geometry family。

## 10. 仓库卫生

根目录操作入口保持小而明确：

- `build.ps1` / `build.sh`；
- `run.ps1` / `run.sh`；
- `publish.ps1` / `publish.sh`；
- README / CHANGELOG / License；
- `docs/`；
- `src/`。

不提交 build output、本地日志、临时截图、编辑器缓存、本地 SDK 路径、一次性 migration script、临时 validation artifact。

OCCAD 正常构建只消费已安装 Bridge SDK，不自动 clone 或 rebuild Bridge。

## 11. 文档唯一归属

避免在多个文档重复整段契约：

- 产品边界 → [01 产品目标](01-PRODUCT-GOALS.md) + [14 功能矩阵](14-FEATURE-MATRIX.md)；
- UI 结构 → [02 UI 规范](02-UI-SPEC.md)；
- 交互 → [03 交互规范](03-INTERACTION-SPEC.md)；
- 架构/ownership → [04 系统架构](04-ARCHITECTURE.md)；
- Entity/Tool 契约 → [05 Entity / Tool 契约](05-ENTITY-TOOL-CONTRACT.md)；
- 开发流程 → [08 开发指南](08-DEVELOPMENT-GUIDE.md)；
- 扩展流程 → [09 扩展指南](09-EXTENSION-GUIDE.md)；
- Transaction/Native 不变量 → [10 事务与资源契约](10-TRANSACTION-RESOURCE-CONTRACT.md)；
- Build 验收 → [11 构建与验收](11-BUILD-VALIDATION.md)；
- 用户操作 → [13 用户指南](13-USER-GUIDE.md)；
- Command ID/Alias → [15 命令参考](15-COMMAND-REFERENCE.md)；
- Release Gate → [16 发布指南](16-RELEASE-GUIDE.md)。

其他文档需要引用这些规则时，只写本章节需要的结论，然后链接到唯一归属文档。
