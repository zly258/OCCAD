# 09 扩展指南

## 1. 扩展原则

扩展 OCCAD 时优先复用现有 owner、Registry 和生命周期，不创建第二套系统。新增功能前先阅读：

- [04 系统架构](04-ARCHITECTURE.md)
- [05 Entity / Tool 契约](05-ENTITY-TOOL-CONTRACT.md)
- [10 事务、事件与 Native 资源契约](10-TRANSACTION-RESOURCE-CONTRACT.md)
- [14 初版功能矩阵](14-FEATURE-MATRIX.md)

一个功能进入产品面必须形成：

```text
Entity/Geometry
  ↓
Registry
  ↓
Tool / Action
  ↓
Preview / Commit / Cancel
  ↓
Property / Grip / Selection / Snap
  ↓
History / Persistence / Presentation
  ↓
Toolbar / Panel Adapter
  ↓
Local Build + Manual/Native Validation
```

不能从“先加按钮”开始扩展。

## 2. 新增 Entity

一个新 Entity 至少需要回答：

- 几何数据是什么？
- 哪些字段属于 Placement，哪些属于本体 Geometry？
- 如何 `Duplicate()`？
- 如何 restore state / geometry snapshot？
- 如何 BuildPresentation？
- 有哪些 Snap 点/曲线？
- 有哪些 Grip？
- 是否可持久化？
- 哪些属性暴露到 PropertyGrid？
- 是否需要独立 Entity Registry ID？

标准步骤：

1. 在 `src/OCCAD.Core/Entities` 增加 Entity；
2. 实现输入合法性和 geometry invariants；
3. 实现 duplicate/snapshot/restore；
4. 实现 native presentation；
5. 实现 Snap/Grip 语义；
6. 在 `CadCoreRegistration` / `CadEntityRegistry` 注册；
7. 如需持久化，实现 write/read geometry；
8. 验证 create → property → grip → save/open → history/presentation 闭环。

禁止：

- Entity 引用 Avalonia；
- Entity 自己操作 History；
- Entity 自己保存正式 Selection；
- Entity 在 pointer move 中直接修改正式 Document model。

## 3. 新增绘图 Tool

Tool 是显式 staged state machine。

标准步骤：

1. 定义稳定 Tool ID；
2. 明确每个 Stage 的 `InputKind`、Prompt、pointer/parameter/precision 行为；
3. exact/parameter input 与 pointer input 共享几何求解路径；
4. Preview 只使用 transient presentation/duplicate；
5. Commit 前验证几何；
6. 使用 Workspace/Transaction 的正式 create mutation 路径；
7. Tool completion、History 安装和失败 rollback 遵循统一 atomic contract；
8. StepBack 只回退一个语义步骤；
9. Cancel/Deactivate 清理 ToolPreview/Snap/Tracking/GripDrag 等 owner state；
10. 最终返回 neutral。

Tool 不应：

- 把 Preview Entity 加入 Document；
- pointer move 产生 History；
- UI Controller 持有 Tool 几何状态；
- 为不同 UI 入口复制同一几何算法。

Circle/Arc/Ellipse/RegularPolygon 已展示推荐做法：多个绘制方式由不同 Action 初始参数激活同一个 Tool。

## 4. 新增 Modify Tool

Move/Rotate/Scale/Mirror/Grip 等修改类 Tool 推荐模式：

```text
Capture source state
  ↓
Create duplicate preview
  ↓
Pointer/parameter updates preview only
  ↓
Commit real entities atomically
  ↓
Complete Tool
  ↓
Install one Undo entry
```

如果 completion/history 安装前失败，模型必须 rollback。

注意：这些通用 Modify Tool 当前不属于初版注册产品面。恢复时必须按完整闭环重新验证，而不是直接恢复历史按钮。

## 5. 新增 Action

Action 用于稳定的瞬时命令入口，不承载长生命周期交互状态。

标准步骤：

1. 定义稳定 Action ID；
2. 注册到 `CadActionManager` / `CadCoreRegistration`；
3. Action 调用已有 Workspace/Core API 或激活 Tool；
4. Toolbar/Menu/快捷键只引用 Action ID；
5. UI 根据 `CanExecute()` 更新可用状态。

禁止 UI 根据字符串反射调用方法，也不能使用中文/英文显示文本作为业务 ID。

## 6. 新增属性

属性应由 Core descriptor 语义驱动。

需要考虑：

- 数据类型/semantic；
- 是否支持多选共同属性；
- Mixed Value；
- 是否影响 Geometry、Appearance 或 Metadata；
- no-op 判断；
- 是否需要 ByLayer；
- 修改后是否需要 Grip/Presentation refresh；
- 是否只读 Measurement。

属性编辑必须走 `CadPropertyTransaction` / `CadTransaction.ApplyEntities` 等 Core 路径，不在 PropertyGrid Controller 中自行实现 Undo。

如果 PropertyDescriptor 动态注入属性，例如平面 `Normal`，必须处理 TypeDescriptor nullable contract、稳定初始化和真实 Entity mutation，不能只生成显示字段。

## 7. 新增 Snap 语义

优先让 Entity 声明自身确定的 Snap geometry；`CadSnapManager` 统一负责：

- candidate 收集；
- priority；
- screen/depth 排序；
- hysteresis；
- cycle；
- temporary modes；
- marker；
- cache/invalidation。

新增 Snap 类型时必须定义：

- runtime mode；
- priority group；
- geometry source；
- WorkPlane/reference point 关系；
- marker representation；
- cache invalidation。

不要把 Entity-specific Snap 几何散落到 Viewport/UI。

## 8. 新增 Grip 语义

Entity 提供 Grip 的：

- Kind；
- world position；
- edit geometry semantics。

`CadGripManager` 负责 marker、hot、hit、native ownership；Grip Tool 负责 preview/commit。

至少验证：

- 初始位置；
- drag preview；
- invalid target 不提交；
- valid target 一次逻辑 History；
- Undo/Redo geometry 恢复；
- marker/GripDrag cleanup。

## 9. 新增 Selection 行为

正式选择语义必须进入 Core Selection owner。UI 只把输入转换成：

- Replace
- Add
- Remove
- Toggle
- Window
- Crossing
- Entity/Subobject scope

新增过滤条件时同时考虑：

- viewer sync；
- existing selection validity；
- Grip visibility；
- Preselection validity。

## 10. 新增 Transient Presentation

任何临时 native 显示必须先定义：

- channel；
- Tool/Workspace lifetime；
- owner；
- `HasState`；
- `Clear`；
- engine recreation；
- delete failure strategy。

优先注册进 `CadTransientScene`。

recoverable native delete failure 时：

- 不得直接忘记 handle；
- 尽量 hidden + non-selectable；
- 保留 handle 后续 retry。

## 11. 新增 Avalonia Panel / Dialog / Toolbar 入口

UI 扩展保持 adapter-only：

- input → Core API；
- Core notification → UI refresh；
- 不保存第二套 business state。

Panel 如果需要 Selection/Document state，观察统一 Core notification，不把 raw viewer state 当作业务真相。

Toolbar 入口必须在 Action/Tool 注册之后增加；初版 UI 采用 grouped three-row text buttons，不通过功能图标或重复 Tool 实现扩展命令族。

## 12. 新增本地化文本

- ID 保持英文稳定标识；
- `zh-CN` / `en-US` 只影响 display text；
- Tool Prompt/Property label/Action caption 使用 localization key；
- 两种语言资源必须同步；
- 语言切换不改变 model/history/persistence 数据。

## 13. 新增 Persistence 字段或格式

需要明确：

- schema 是否向后兼容；
- 缺失字段默认值；
- 非法值处理；
- Entity Registry ID 是否稳定；
- Geometry 与 Presentation 数据是否分离；
- Feature Entity 是否需要 Document context。

破坏性 schema 改动应写 ADR。

## 14. 新增 Exchange Format

不要只在 FilePicker 增加扩展名。

一个 Exchange format 至少需要明确：

1. Import、Export 或双向；
2. Bridge/OCCT API owner；
3. 文件失败/unsupported entity 的错误语义；
4. 导入后如何进入正式 Document；
5. 导出使用 Selection 还是全部 Document；
6. runtime/native dependency；
7. 手工验证样本。

## 15. 扩展验证

当前仓库不把 GitHub Actions 或自动 smoke framework 作为权威验收入口。扩展至少完成：

```powershell
.\build.ps1
```

并按功能类型进行本地 Windows manual/native 回归。

重点检查：

- Create/Preview/Commit/Cancel；
- exact/parameter path 与 pointer path；
- transient cleanup；
- Property/Grip；
- Undo/Redo；
- Save/Open；
- language switch；
- native presentation 与 authoritative model 一致。

## 16. 扩展完成标准

一个扩展只有同时满足以下条件才算完成：

- Core owner 明确；
- Registry/ID 明确；
- UI 无平行业务逻辑；
- parameter/precision 与 pointer 几何路径一致；
- Preview 不污染 Document/History；
- Cancel neutral；
- 一次用户提交形成一次逻辑 history；
- Undo/Redo 对称；
- observer failure 不破坏 authoritative state；
- native object ownership 可解释；
- persistence/schema 行为明确；
- 本地 build 和 manual/native validation 完成；
- 中英文文档同步更新。
