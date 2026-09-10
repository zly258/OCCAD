# 04 系统架构

## 1. 目标

OCCAD 的架构目标不是“封装 OCCT API”，而是提供一个可维护、可扩展的 CAD 产品内核。核心原则是：**领域状态与交互状态必须有明确 owner；Native Viewer 只做派生显示；UI 不重复实现业务规则。**

架构设计优先保证：

1. Document/Entity 是模型真相；
2. ViewerObject 是派生显示；
3. Tool 生命周期可结束、可取消、可回退；
4. Undo/Redo 与模型 mutation 原子；
5. Preview/Snap/Grip/Tracking 有显式 transient owner；
6. UI 只做适配，不形成第二套 CAD 内核。

## 2. 分层与依赖

```text
┌───────────────────────────────────────────────────────────┐
│ OCCAD.Avalonia                                            │
│ Shell / Menu / Toolbar / Panels / Dialogs / Viewport     │
│ Input Adapter / Localization / Settings Presentation      │
└───────────────────────────┬───────────────────────────────┘
                            │ calls / observes
┌───────────────────────────▼───────────────────────────────┐
│ OCCAD.Core                                                │
│ Workspace / Document / Entity / Layer                     │
│ Action / Command / Tool / Transaction / History           │
│ Selection / Snap / Grip / WorkPlane / Precision           │
│ Preview / Tracking / Geometry / Persistence / Exchange    │
└───────────────────────────┬───────────────────────────────┘
                            │ managed/native bridge
┌───────────────────────────▼───────────────────────────────┐
│ OcctNet / OcctCSharpBridge                                │
└───────────────────────────┬───────────────────────────────┘
                            │ OCCT API
┌───────────────────────────▼───────────────────────────────┐
│ Open CASCADE Technology                                   │
└───────────────────────────────────────────────────────────┘
```

允许依赖方向：

`OCCAD.Avalonia → OCCAD.Core → OcctNet → OcctCSharpBridge/OCCT`

禁止：

- Core 引用 Avalonia；
- Entity/Document 依赖 UI Control；
- Avalonia 自己维护第二套 Document、Selection、History、Layer 或 Tool 状态；
- 通过反射、全局 singleton 或 service locator 绕过明确依赖；
- UI Controller 直接保存 native transient handle 并绕过既有 owner；
- 本地化字符串充当业务 ID。

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

一个 `CadWorkspace` 对应一个独立 CAD 会话；Workspace 内 owner 可以互相协作，但不通过全局静态状态共享正式 Document。

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

## 7. Tool / Action / Command / Shell

四者职责不同：

- `CadAction`：稳定、瞬时的命令入口；
- `CadCommandDescriptor` / `CadCommandManager`：Action/Tool 的统一命令元数据与文本输入适配；
- `CadTool`：有状态的 staged interaction state machine；
- Shell：把 Menu/Toolbar/Input 映射到同一套 Core Action/Tool。

Circle/Arc/Ellipse/RegularPolygon 的多个绘制入口只通过初始参数激活同一个 Tool，不复制几何实现。

标准 Tool 生命周期：

```text
Action.Execute
  ↓
CadToolManager.Activate
  ↓
CadTransientScene.BeginToolSession
  ↓
Tool.Activate
  ↓
Pointer / Selection / Parameter / Exact Input
  ↓
Preview
  ↓
Commit mutation
  ↓
Tool completion / Deactivate
  ↓
Install History
  ↓
Reset neutral state
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
6. Preview 不进入 History；
7. no-op 不产生 History，也不错误改变 Modified state。

### 8.1 Create 提交

```text
Build candidate Entity
→ Add to Document
→ complete Tool / release transient
→ install AddEntities history
```

如果 Tool completion 失败且 History 尚未安装，创建的 Entity 必须从 Document 回滚。

### 8.2 Edit 提交

```text
Capture before state
→ mutate formal Entity
→ complete Tool
→ capture after state
→ install history
```

如果 History 尚未建立时 mutation/cleanup 失败，则恢复 before state。

完整规则见 [10-TRANSACTION-RESOURCE-CONTRACT.md](10-TRANSACTION-RESOURCE-CONTRACT.md)。

## 9. Transient 与 Native Ownership

`CadTransientScene` 协调：

- ToolPreview；
- Snap；
- Tracking；
- GripDrag；
- Preselection；
- SelectionWindow；
- SelectionMarkers。

原则：

- Tool lifetime cleanup 严格；
- Workspace/UI dispose 对 recoverable native cleanup failure 使用 best-effort，不能截断整个 teardown；
- Native handle 只有确认删除成功后才能忘记；
- 删除失败时优先隐藏 + non-selectable，并保留 handle 供后续重试；
- Preview/marker 不进入 Document/History；
- Tool owner 只有在所属 channel 实际无残留后才能释放。

### 9.1 为什么要保留 owner

如果 native 删除失败后立即把 owner 清零，会出现：

```text
ghost object still visible
+ old owner lost
+ next Tool starts new owner
= leaked presentation without lifecycle
```

因此当前设计要求残留状态继续归属于原 Tool session，显式通道清理成功后再释放 owner。

## 10. Selection / Snap / Grip

### Selection

- Entity Selection 是正式选择；
- Subobject Selection 单独管理；
- Preselection 只表示 hover；
- Window/Crossing 通过 Viewport Controller 转成 Core selection operation；
- Viewer highlight 不是权威 Selection。

### Snap

Snap owner 负责：

- candidate discovery；
- priority；
- screen/depth ordering；
- hysteresis；
- cycle；
- temporary modes；
- marker；
- cache/invalidation。

Entity 只负责声明可捕捉几何语义。

### Grip

Grip pointer movement只修改 duplicate/preview；最终确认才进入正式 Entity transaction。

视觉约定：普通 Grip 为填充矩形，Hot/Drag 为圆形。

## 11. WorkPlane / Drafting / Precision

`CadWorkPlane` 是工作平面 owner；`CadDraftingSettings` 管 ORTHO/POLAR 等 drafting state；`CadPrecisionInputManager` 负责长度、角度、比例和 offset point 等精确输入。

Tool 可以申请临时 ToolPlane，但不能把 WorkPlane 状态复制到自己的永久并行状态。

切换 XY/YZ/XZ 时：

1. ToolManager 判断当前 stage 是否允许；
2. 必要时 StepBack 到兼容 stage；
3. WorkPlane 更新；
4. Snap/Tracking 清理；
5. Preview 按新平面重新解析。

## 12. Property / Layer

- Property descriptor 与值语义属于 Core；
- `CadPropertyTransaction` / `CadTransaction.ApplyEntities` 负责正式 property mutation；
- PropertyGrid Controller 只构造 editor、转换输入和刷新 UI；
- LayerManager 是 Layer 集合/current-layer 的唯一 owner；
- ByLayer effective appearance 在 Core/Document 链中解析；
- 平面 Entity 的 editable `Normal` 通过 descriptor metadata 暴露，修改仍进入统一 Entity mutation/history/presentation 链。

## 13. Persistence / Exchange

- Entity Registry ID 是持久化协议的一部分，不能随意重命名；
- Geometry/Placement/Appearance 数据与 transient presentation 分离；
- Feature Entity 可以只注册 persistence，而由 Tool 负责需要 Document context 的创建；
- Exchange service 是外部 CAD 格式边界，不应把第三方格式对象直接当成 UI 真相；
- 破坏性 schema 变化需要 ADR 和明确兼容策略。

当前初版对外功能声明以自有 Document 持久化为主。外部格式只有逐方向验证后才能写入正式 feature matrix。

## 14. Avalonia 边界

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

## 15. MainWindow 内部职责

MainWindow 使用 partial files 拆分 Shell 职责：

- `MainWindow.cs`：基础窗口/主要 UI 组成；
- `MainWindow.ClassicShell.cs`：经典 Menu/Toolbar/参数条；
- `MainWindow.ClassicStability.cs`：稳定布局、Esc、live parameter；
- `MainWindow.Actions.cs`：Action/快捷键入口；
- `MainWindow.Events.cs`：Workspace event 投影；
- `MainWindow.Files.cs`：New/Open/Save；
- `MainWindow.Settings.cs`：设置应用；
- `MainWindow.OperationStatus.cs` / `CommandStatus.cs`：操作提示。

这些 partial 文件只是 UI responsibility split，不是业务 owner。跨文件共享字段不能变成另一套 Core 状态机。

## 16. 一次 Pointer Move 的数据流

典型绘图移动链：

```text
OcctAvaloniaViewport.PreviewPointerInput
→ CadPointerMoveScheduler
→ CadViewportInteractionController.ProcessPointer
→ CadToolManager.HandlePointer
→ active CadTool
→ CadWorkspace.ResolvePoint
   ├─ WorkPlane
   ├─ Snap
   ├─ Drafting / Tracking
   └─ Precision context
→ CadPreviewManager
→ OcctNet Viewer presentation
```

性能优化优先在 scheduler、snap cache、preview replacement、redraw batching 上做，不得把几何真相移到 UI。

## 17. 一次 Property Edit 的数据流

```text
Property editor
→ Descriptor convert/validate
→ CadPropertyTransaction / ApplyEntities
→ Entity mutation
→ Document strict propagation
→ presentation rebuild/update
→ History install
→ public Changed notification
→ Properties/Model/Layer UI refresh
```

如果 presentation strict propagation 在 History 安装前失败，应回滚 Entity；如果只是 post-state UI observer 失败，正式 mutation 仍然成立。

## 18. Undo/Redo 数据流

UI 不直接调用 `CadHistory.Undo/Redo`，而使用：

```text
CadWorkspace.Undo / Redo
→ cancel active Tool
→ clear Selection/Subobject/Preselection/Grip
→ Document change set
→ CadHistory Undo/Redo
→ presentation/state notifications
```

这样 History 不会与活动 Tool 或旧 Grip transient 并存。

## 19. Engine 生命周期

### Attach

Engine 初始化后：

- Document 建立正式 presentation；
- Selection/Snap/Grip manager 绑定 Engine；
- Viewport interaction 同步 automatic highlight；
- subobject marker 等 Workspace transient 可以重新建立。

### Engine recreation

Engine recreation 不允许继续持有旧 native handle。UI transient controller 必须丢弃旧 marker handle 并由正式 Core state重新派生需要的显示。

### Dispose

Workspace/UI dispose 必须：

- 取消 Tool；
- 清 transient；
- 删除/释放 native presentation；
- 解除 event subscription；
- recoverable cleanup failure 记录诊断但不中断剩余 teardown。

## 20. 扩展决策规则

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

## 21. 新功能落层示例

### 新增可绘制 Entity

```text
Entity geometry/state
→ Registry persistence
→ Snap/Grip semantics
→ Tool
→ Action registration
→ Menu/Toolbar if product-facing
→ Property descriptors
→ localization
→ save/open regression
```

### 新增修改命令

```text
Define selection contract
→ preview strategy
→ formal mutation API
→ atomic history
→ Tool lifecycle
→ Action
→ UI entry
→ cancel/undo/native cleanup regression
```

### 新增 UI Panel

Panel 只能观察/调用已有 Core owner。若 Panel 需要自己保存正式 CAD 状态，说明责任层设计错误，应先回到 Core 建模。

## 22. 架构级改动

以下变化必须写 ADR：

- 新增项目/程序集或改变依赖方向；
- 引入新的 transaction/history 模型；
- 改变 Tool 生命周期或 command session 模型；
- 改变 Document/Entity ownership；
- 引入全局服务、插件容器、线程模型或异步 native 调度；
- 改变 persistence schema 的兼容策略。

ADR 模板见 `docs/adr/0000-template.md`。

## 23. 当前发布架构边界

初版发布不扩展以下架构面：

- 不新增插件容器；
- 不新增第三个业务程序集；
- 不建立第二套命令系统；
- 不新增 parallel ViewModel domain state；
- 不为了 Copy/Rotate/Scale/Mirror 只做 UI 按钮而跳过完整 Tool lifecycle；
- 不把 Import/Export 当成当前核心闭环的前置条件。

发布后的功能扩展继续沿用当前 owner、transaction、transient、history 契约演进。
