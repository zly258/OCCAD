# 04 系统架构

## 1. 目标

OCCAD 的架构目标不是“封装 OCCT API”，而是提供一个可维护、可扩展的 CAD 产品内核。核心原则是：**领域状态与交互状态必须有明确 owner；Native Viewer 只做派生显示；UI 不重复实现业务规则。**

## 2. 分层与依赖

```text
┌──────────────────────────────────────────────┐
│ OCCAD.Avalonia                               │
│ Shell / Toolbar / Panels / Viewport Adapter │
└──────────────────────┬───────────────────────┘
                       │ calls / observes
┌──────────────────────▼───────────────────────┐
│ OCCAD.Core                                   │
│ Workspace / Document / Entity / Tool         │
│ Selection / Snap / Grip / History / Layer    │
│ Geometry / Persistence / Actions             │
└──────────────────────┬───────────────────────┘
                       │ native geometry/view
┌──────────────────────▼───────────────────────┐
│ OcctNet / OcctCSharpBridge / OCCT            │
└──────────────────────────────────────────────┘
```

允许依赖方向：

`OCCAD.Avalonia → OCCAD.Core → OcctNet`

禁止：

- Core 引用 Avalonia；
- Entity/Document 依赖 UI Control；
- Avalonia 自己维护第二套 Document、Selection、History、Layer 或 Tool 状态；
- 通过反射、全局 singleton 或 service locator 绕过明确依赖。

## 3. Composition Root

`CadWorkspace` 是一次 CAD 会话的 composition root，负责组合：

- `CadDocument`
- `CadLayerManager`
- `CadSelectionManager`
- `CadSubobjectSelectionManager`
- `CadPreselectionManager`
- `CadWorkPlane`
- `CadDraftingSettings`
- `CadSnapManager`
- `CadTrackingManager`
- `CadPrecisionInputManager`
- `CadPreviewManager`
- `CadGripManager`
- `CadTransientScene`
- `CadHistory`
- `CadToolManager`
- `CadActionManager`
- `CadWorkspaceEvents`

业务逻辑优先进入这些 Core owner，而不是进入 MainWindow、Panel Controller 或 Viewport Controller。

## 4. 权威状态与派生显示

### 4.1 权威状态

- Document 中的 Entity 集合；
- Entity 的 Geometry、Placement、Layer、Appearance、Metadata；
- Layer 集合和当前层；
- History 当前状态与 Undo/Redo 栈；
- Entity / Subobject Selection；
- Tool 当前 Stage、Parameters、Precision、WorkPlane 语义。

### 4.2 派生/Presentation 状态

- `ViewerObject`
- Selection highlight
- Snap marker
- Grip marker
- Tracking guide
- Preview presentation
- Preselection highlight
- Selection rectangle
- Subobject marker
- Toolbar / Panel / Dialog / Status Strip 文本和控件状态

初版 Shell 不显示永久 Command Line 或 Dynamic HUD，但相应输入/精度基础设施仍属于 Presentation/Interaction 适配层，而不是权威 Document 数据。

派生显示 observer 失败不能在权威状态已经提交后反向制造“操作失败”；但**内部严格同步失败**必须能够阻止并回滚权威状态。

## 5. 事件三分法

### 5.1 Pre-state / veto

例如 `Changing`：

- 字段修改前执行；
- 允许验证或拒绝操作；
- 异常向上传播。

### 5.2 Strict internal state propagation

例如 Entity/Layer 内部 `StateChanged`：

- 用于 Document、LayerManager、native presentation 等必须同步成功的内部链；
- 失败向上传播；
- mutation owner 负责 rollback；
- 不作为普通 UI observer 通知接口。

### 5.3 Post-state notification

例如公共 `Changed`、`ToolChanged`、`History.Changed`、`Snap.CurrentChanged`、`Grip.HotChanged`：

- 状态已经成立；
- observer 独立调用；
- recoverable observer 异常只进入诊断，不得让 API 伪装成失败，也不得阻断后续 observer；
- `OutOfMemoryException`、`StackOverflowException`、`AccessViolationException` 等 fatal 异常不吞。

新增事件必须先明确属于哪一类。

## 6. Document / Entity

`CadDocument` 是持久 Entity 的唯一 owner，并负责：

- 添加/删除 Entity；
- ViewerObject 映射；
- attach/detach engine；
- presentation build/rebuild/delete；
- effective appearance resolve；
- Entity strict state propagation；
- Layer 改动后的 appearance 同步；
- Document change-set 聚合。

`CadEntity` 负责：

- Id / EntityType；
- 合法 Geometry；
- Placement；
- Layer 与外观 override；
- `Duplicate` / `RestoreState` / geometry snapshot；
- Snap/Grip 几何语义；
- persistence geometry。

Entity 不负责 UI，不直接创建 Toolbar/Panel，也不拥有 History。

## 7. Tool / Action / Shell

- `Action` 是稳定、瞬时的命令入口；
- `Tool` 是有状态的 staged interaction state machine；
- `CadToolManager` 是 Tool 生命周期 owner；
- 顶部 grouped Toolbar、Panel、Viewport input adapter 只调用或观察同一套 Core Action/Tool 状态；
- Circle/Arc/Ellipse/RegularPolygon 的多个绘制入口只通过初始参数激活同一个 Tool，不复制几何实现；
- 初版 Shell 不展示永久 Command Line、Floating Tool Panel 或大型 Ribbon。

标准 Tool 生命周期：

```text
Activate
  ↓
Begin Tool transient owner
  ↓
Pointer / parameter / precision / staged state
  ↓
Preview
  ↓
Commit model mutation
  ↓
Complete Tool
  ↓
Install History
  ↓
Return Neutral
```

Cancel/Deactivate 必须释放 Tool-owned transient，并最终满足 neutral contract。

## 8. Transaction / History

高频操作优先使用 operation-specific lightweight history；批量复合操作使用显式 `CadTransaction` snapshot。

关键不变量：

1. History 只有在模型操作真正成为 authoritative 后才能前移；
2. History 未安装时模型失败必须 rollback；
3. History 已安装后，即使 notification observer 失败，也不得回滚模型；
4. Tool create/feature commit 的 Tool completion 属于 commit contract；
5. 外层 transaction 负责 history 时，内部 lightweight mutation 不建立嵌套 Undo；
6. Preview 不进入 History。

完整规则见 [10-TRANSACTION-RESOURCE-CONTRACT.md](10-TRANSACTION-RESOURCE-CONTRACT.md)。

## 9. Transient 与 Native Ownership

`CadTransientScene` 协调 ToolPreview、Snap、Tracking、GripDrag、Preselection、SelectionWindow、SelectionMarkers 等通道。

原则：

- Tool lifetime cleanup 严格；
- Workspace/UI dispose 对 recoverable native cleanup failure 使用 best-effort，不能截断整个 teardown；
- Native handle 只有确认删除成功后才能忘记；
- 删除失败时优先隐藏 + non-selectable，并保留 handle 供后续重试；
- Preview/marker 不进入 Document/History。

## 10. Selection / Snap / Grip

- Selection 是正式 Entity Selection owner；
- Subobject Selection 单独管理；
- Preselection 只表示 hover；
- Snap 负责候选发现、priority、screen/depth 排序、hysteresis、cycle、temporary modes、marker 和 cache；
- Entity 负责声明 Snap/Grip 几何语义；
- Grip pointer movement 只改 duplicate/preview；最终提交才修改真实 Entity。

## 11. Property / Layer

- Property descriptor 与值语义属于 Core；
- `CadPropertyTransaction` / `CadTransaction.ApplyEntities` 负责正式 property mutation；
- PropertyGrid Controller 只构造 editor、转换输入和刷新 UI；
- LayerManager 是 Layer 集合/current-layer 的唯一 owner；
- ByLayer effective appearance 在 Core/Document 链中解析；
- 平面 Entity 的 editable `Normal` 通过 descriptor metadata 暴露，修改仍进入统一 Entity mutation/history/presentation 链。

## 12. Persistence / Exchange

- Entity Registry ID 是持久化协议的一部分，不能随意重命名；
- Geometry/Placement/Appearance 数据与 transient presentation 分离；
- Feature Entity 可以只注册 persistence，而由 Tool 负责需要 Document context 的创建；
- Exchange service 是外部 CAD 格式边界，不应把第三方格式对象直接当成 UI 真相；
- 破坏性 schema 变化需要 ADR 和明确兼容策略。

## 13. Avalonia 边界

Avalonia 可以：

- 调用 Core Action/Tool/API；
- 观察 Core notification；
- 负责 Viewport input adapter、布局、控件、Dialog、本地化显示；
- 把用户输入转换成 Core 接受的 property/selection/tool 参数。

Avalonia 不可以：

- 自己做 Entity transaction/rollback；
- 自己建立第二套 Undo/Redo；
- 绕过 Selection manager 直接改正式选择语义；
- 在 UI Controller 中复制 Tool 几何算法；
- 把 native marker 生命周期散落到多个无 owner 的 List；
- 使用本地化文本充当 Action/Tool/Entity 稳定 ID。

## 14. 扩展决策规则

新增功能前依次判断：

1. 这是 Domain state、Interaction state 还是 Presentation？
2. 谁是唯一 owner？
3. 是否需要 Undo/Redo？
4. 是否进入 Document？
5. 是否属于 transient？
6. native object 的 handle 谁持有、何时删除？
7. 失败发生时 authoritative state 在哪里？
8. 事件属于 veto、strict propagation 还是 notification？
9. persistence ID/schema 是否受影响？
10. 最小本地 build + manual/native validation 是什么？

如果这些问题无法明确回答，不应直接开始编码。

## 15. 架构级改动

以下变化必须写 ADR：

- 新增项目/程序集或改变依赖方向；
- 引入新的 transaction/history 模型；
- 改变 Tool 生命周期或 command session 模型；
- 改变 Document/Entity ownership；
- 引入全局服务、插件容器、线程模型或异步 native 调度；
- 改变 persistence schema 的兼容策略。

ADR 模板见 `docs/adr/0000-template.md`。
