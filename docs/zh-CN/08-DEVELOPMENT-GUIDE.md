# 08 开发指南

## 1. 开发目标

OCCAD 的日常开发优先保证：

1. 交互正确；
2. 模型/显示一致；
3. Undo/Redo 原子；
4. Tool 退出后完全 neutral；
5. Native transient 无泄漏、无幽灵对象；
6. 扩展不破坏既有 owner 和依赖边界；
7. UI、文档、注册表和实际能力保持一致。

不要为了“代码看起来统一”而重构稳定路径；优先修复可复现问题和明确的架构债务。

## 2. 开发环境

仓库文件为权威来源：

- `.NET SDK`：`global.json`
- Solution：`OCCAD.sln`
- Core：`src/OCCAD.Core`
- Avalonia：`src/OCCAD.Avalonia`
- Bridge SDK：OcctCSharpBridge SDK 3.0
- Windows 默认 Bridge：`C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`
- Linux 默认 Bridge：`~/.local/share/OcctCSharpBridge/SDK/3.0/linux-x64`

可通过 `OCCTCSHARPBRIDGE_SDK` 覆盖 Bridge SDK。

Windows：

```powershell
git pull
.\build.ps1
.\run.ps1
```

flat runtime：

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

Linux：

```bash
git pull
./build.sh
./run.sh
```

发布：

```powershell
.\publish.ps1
```

或：

```bash
./publish.sh
```

## 3. 目录职责

### `src/OCCAD.Core`

核心业务和 CAD 状态：

```text
Actions/       Action / Command metadata
Document/      Document membership / persistence presentation
Entities/      2D / 3D / Feature entity
Events/        Workspace event projection
Exchange/      external-format boundary
Geometry/      reusable geometry algorithms
Grips/         Grip owner
History/       Undo/Redo
Interaction/   Tool / Preview / Precision / Tracking / Transient
Layers/        Layer model
Properties/    Property descriptors/value conversion
Selection/     Entity/Subobject/Preselection
Snapping/      Snap discovery/selection/marker
WorkPlane/     WorkPlane state
```

### `src/OCCAD.Avalonia`

```text
Application/   app startup/settings
Dialogs/       dialogs and CAD ColorTable
Input/         direct text input / cursor
Localization/  zh-CN/en-US resources
Panels/        Model/Layers/Properties controllers
Shell/         MainWindow partial responsibilities
Theming/       layout metrics/CAD visuals
Viewport/      input adaptation and viewport interaction
```

UI 目录不应该出现新的正式 Entity/History/Transaction owner。

## 4. 标准开发流程

### 4.1 先定位责任层

先回答：

- Domain？
- Interaction？
- Presentation？
- Persistence？

然后确定唯一 owner。不要直接从 Button.Click / PointerMoved 开始堆业务逻辑。

### 4.2 Core-first

业务行为优先进入 Core：

- Entity 语义 → `Entities`
- Document membership/presentation → `Document`
- Tool 生命周期 → `Interaction`
- Selection → `Selection`
- Snap → `Snapping`
- Grip → `Grips`
- History → `History`
- Layer → `Layers`
- Property transaction → `CadPropertyTransaction` / `CadTransaction`
- Action 注册 → `Actions` / `CadCoreRegistration`

Avalonia 最后只接入口和显示。

### 4.3 先定义失败语义

任何模型或 native mutation 编码前必须明确：

- 哪一步算 commit；
- 哪一步失败必须 rollback；
- History 在哪一步安装；
- Tool completion 是否属于 commit；
- transient 何时释放；
- notification observer 失败是否允许影响业务结果；
- fatal exception 是否会被错误吞掉。

### 4.4 实现最小闭环

新功能不要先做大量菜单、图标或参数，先完成：

`Core semantics → Tool → Preview → Commit → Undo → Cancel cleanup`

闭环通过后再接 Property、Grip、Snap、Persistence、UI 和文档。

### 4.5 最后接 UI

UI 只做：

- 参数收集；
- Core API 调用；
- notification 观察；
- visual state 显示；
- 本地化。

不在 ViewModel/Controller 中复制 transaction 或 geometry rule。

## 5. 新增 Entity 的标准步骤

假设新增 `CadXxxEntity`：

1. 在 `Entities` 建立类型，定义稳定 `EntityType`；
2. 参数校验必须在 Entity/Core 内完成；
3. 实现 geometry build / presentation 所需语义；
4. 实现 `Duplicate` / snapshot / `RestoreState`；
5. 定义 Snap/Grip geometry；
6. 在 `CadEntityRegistry` 注册 persistence；
7. 如需创建交互，增加 `XxxTool`；
8. 注册 Action；
9. 增加 Property descriptor；
10. 补中英文 localization；
11. 按需增加 Menu/Toolbar；
12. 完成 Save/Open、Undo/Redo、Cancel、Preview regression。

不要仅新增 Entity 类就把功能写进 README。

## 6. 新增 Tool 的标准步骤

Tool 必须有明确 stage machine。

建议先写出状态图：

```text
Activate
→ Stage 0
→ accept point/selection/parameter
→ Stage 1
→ Preview
→ CanCommit / CanFinish
→ Commit
→ Deactivate
```

实现时检查：

- `CurrentStep` 与 Prompt 一致；
- `CanStepBack` 正确；
- `CanFinish` 正确；
- pointer move 只更新 Preview；
- exact input 与 pointer path 进入同一几何逻辑；
- Cancel 不修改正式 Entity；
- Tool-owned transient 全部由 `CadTransientScene` 追踪；
- complete/cancel 后满足 `NeutralStateViolations.Count == 0`。

## 7. 新增修改命令

修改命令必须先定义 selection contract：

- 是否预先选择；
- 是否允许 Tool 内追加选择；
- Entity/Subobject scope；
- selection 确认方式；
- preview 是否 replacement；
- commit 是修改原实体还是创建副本。

推荐链路：

```text
Selection
→ capture source state
→ base/reference input
→ replacement Preview
→ formal mutation
→ Tool completion
→ history
```

Move 已经是当前参考实现。Copy/Rotate/Scale/Mirror 在完整 Tool lifecycle 完成前不要只加按钮。

## 8. Action / Command 注册

正式产品入口应通过 `CadCoreRegistration` 注册。

保持三者一致：

1. Action ID；
2. `CadCommandCatalog` alias/caption；
3. Menu/Toolbar 入口。

例如 Move 当前稳定 ID 是：

```text
edit.move
```

不要同时保留旧 `modify.move` 之类失效 ID。

对于一个 Tool 的多个绘制方式，使用不同 Action ID + initial parameter 指向同一个 Tool，不复制 Tool：

```text
draw.circle.centerradius
→ CircleTool + Method=CenterRadius
```

## 9. Property 开发

新增可编辑属性：

1. descriptor 定义在 Core；
2. parse/validation 在 Core value converter/metadata；
3. mutation 使用 `CadPropertyTransaction` / `ApplyEntities`；
4. Document presentation 必须同步；
5. History 必须可 Undo/Redo；
6. UI editor 只负责输入。

禁止在 PropertyGrid controller 中直接改 native shape 或绕开 history。

## 10. Snap 开发

Snap 类型开发应拆分：

- Entity/geometry 提供 candidate semantics；
- `CadSnapManager` 负责过滤、排序、hysteresis、cycle 和 marker；
- pointer path 只消费最终 resolved point。

新增 Snap 后至少验证：

- 单候选；
- 多候选；
- Tab cycle；
- temporary modes；
- Tool 切换；
- Commit/Cancel cleanup；
- Engine recreation。

## 11. Grip 开发

Grip 数据来自正式 Entity geometry，但 drag 过程中不能直接持续修改正式 Entity。

标准模式：

```text
selected Entity
→ GripPoint
→ GripEditTool
→ duplicate/preview
→ validate target
→ ApplyEntities
→ history
```

普通 Grip 使用填充矩形，Hot/Drag 使用圆形。marker size 和 hit tolerance 必须使用 Settings，不写死 UI 像素。

## 12. WorkPlane 与精确输入

新的 2D Tool 必须显式考虑 XY/YZ/XZ，而不是默认 XY。

精确输入和 pointer 输入必须共享同一 stage semantics。不能出现：

- 鼠标路径正确、文本输入路径几何不同；
- WorkPlane 切换后继续使用旧 stage point frame；
- ORTHO/POLAR 只影响视觉不影响 resolved point。

## 13. Persistence 开发

持久化只保存正式模型状态。

可以进入 schema：

- Entity identity/type；
- geometry；
- placement；
- layer；
- appearance；
- feature references/parameters。

不得保存：

- Preview；
- Snap marker；
- Grip marker；
- Tracking；
- Preselection；
- Selection rectangle。

Registry ID 一旦进入发布格式就视为协议，不随类名重构随意更改。

## 14. Event 规则

新增 event 必须分类：

- veto/pre-state；
- strict internal propagation；
- post-state notification。

公共 notification 默认逐 observer 隔离。除非明确属于 strict/veto，不直接 multicast invocation。

observer recoverable failure 只能写诊断，不能让已经提交的模型 API 返回失败。

## 15. Exception 规则

参数错误：

- `ArgumentNullException`
- `ArgumentOutOfRangeException`
- `ArgumentException`

状态错误：

- `InvalidOperationException`

Recoverable native failure 只在明确 presentation/transient cleanup 边界吞掉并记录诊断。

以下 fatal failure 不得当成普通业务失败吞掉：

- `OutOfMemoryException`
- `StackOverflowException`
- `AccessViolationException`

Aggregate/InnerException 也必须递归分类。

## 16. Transaction / History 规则

- 单实体/少量实体高频编辑优先 lightweight history entry；
- 多步骤批量修改使用 `CadTransaction`；
- 外层 transaction 内不安装嵌套 Undo；
- no-op 不污染 History/Modified；
- Tool completion 如果属于 commit contract，必须放在 atomic boundary 内；
- Undo/Redo 自身也要考虑中途失败恢复。

UI 不直接调用 `CadHistory.Undo/Redo`，统一调用 `CadWorkspace.Undo/Redo`。

## 17. Native API 与资源所有权

每个 native object 创建后立即有 owner。

代码必须能回答：

- handle 存在哪里；
- 谁删除；
- engine 重建时怎么办；
- delete 失败时是否还保留 handle；
- 失败后对象是否仍 selectable/visible；
- retry cleanup 从哪里触发。

临时 native object 不依赖 GC/finalizer 保证正确性。

## 18. UI 开发规则

### Shell

保持：

- Menu + 最多两行 Toolbar；
- 深色 Viewport；
- 固定 Tool 参数条；
- Status Strip；
- Model/Layers/Properties。

不恢复：

- Ribbon；
- 三行 grouped toolbar；
- permanent command line；
- floating tool panel；
- Ready/version/永久坐标噪声。

### DPI

至少考虑 100% / 125% / 150%。不要根据中文字数写死 Label 宽度；优先 Grid/Auto/* 布局。

### Localization

任何新增用户可见文字必须同时进入 zh-CN / en-US。稳定 Action/Tool/Entity ID 不使用本地化文字。

## 19. 性能规则

高频 pointer path 避免：

- 每次 MouseMove 全量 Document snapshot；
- 不必要的大量 LINQ 分配；
- 重复 BuildPresentation；
- 重复 redraw；
- 同一位置重复计算昂贵 Snap；
- 每次 ToolUpdated 重建整套 Shell。

优先：

- pointer scheduler；
- native display batch；
- replacement preview；
- snap cache + 正确 invalidation；
- focused editor 不重建。

## 20. 构建与运行

Windows 权威构建：

```powershell
.\build.ps1
```

Linux：

```bash
./build.sh
```

build script 会校验 Bridge managed assembly、metadata、portable/flat native layout。不要在项目文件里临时写死个人 SDK 路径。

真实运行：

```powershell
.\run.ps1
```

或：

```bash
./run.sh
```

只看编译成功不等于 Native interaction 通过。

## 21. 发布构建

Windows：

```powershell
.\publish.ps1
```

Linux：

```bash
./publish.sh
```

发布脚本必须验证：

- `OCCAD.dll`；
- Bridge managed assemblies/metadata；
- portable runtime（如使用）；
- OCCT resources（portable）；
- launcher：Windows `run.ps1` / Linux `run.sh`。

完整步骤见 [16-RELEASE-GUIDE.md](16-RELEASE-GUIDE.md)。

## 22. 提交前检查

Core：

- [ ] ownership 明确；
- [ ] no-op 不产 history；
- [ ] rollback 路径明确；
- [ ] Undo/Redo 对称；
- [ ] observer 语义正确；
- [ ] Tool Cancel/Deactivate neutral；
- [ ] transient 不进 Document/History；
- [ ] native handle 不丢失；
- [ ] fatal exception 不被吞；
- [ ] 新增扩展点有文档。

UI：

- [ ] 没有复制 Core business logic；
- [ ] 125%/150% DPI 不破布局；
- [ ] 没有重复 Prompt/状态；
- [ ] 本地化资源完整；
- [ ] Menu/Toolbar 与 Action registration 一致。

## 23. 功能完成 Definition of Done

新增功能至少要通过：

```text
Action/Menu entry
→ Tool activation
→ Prompt
→ pointer path
→ exact input path
→ Preview
→ Commit
→ Property
→ Snap/Grip if applicable
→ Undo/Redo
→ Cancel
→ Save/Open if persistent
```

并确认：

- no ghost Preview；
- no stale Snap/Grip/Tracking；
- neutral state；
- 一次用户操作一个 Undo；
- model/native presentation 一致。

## 24. 不做什么

- 默认不引入 GitHub Actions；
- 不通过反射实现命令分发；
- 不建立 compatibility/migration 层保留错误 API；
- 不为一个 bug 增加一套平行框架；
- 不在核心交互未稳定时横向扩大量 Entity；
- 不因为 Core API 已存在就把未完成 Tool 暴露到产品 UI；
- 不用管理员/root 运行来掩盖 SDK/runtime 配置问题。
