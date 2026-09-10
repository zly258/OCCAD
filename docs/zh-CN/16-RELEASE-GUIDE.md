# 16 发布与交付指南

本文定义 OCCAD 初版从源码状态到可发布交付物的标准流程。发布必须以**真实本机构建和实机交互回归**为依据，不能仅凭源码静态检查或文档完成度判断。

## 1. 发布目标

一个可发布版本至少应满足：

- `main` 是准备发布的单一真相；
- `OCCAD.Core` / `OCCAD.Avalonia` 无编译错误和 warning；
- Bridge SDK/runtime 布局通过脚本校验；
- 关键 CAD 交互闭环通过；
- Preview/Snap/Grip/Tracking 无明显 ghost；
- Undo/Redo 原子；
- Save/Open 基本回归通过；
- 中文/English 和 125%/150% DPI 可用；
- README、用户文档、功能矩阵、命令参考与源码一致；
- Windows/Linux 发布包包含正确 launcher/runtime/resources。

## 2. 发布前代码冻结

进入发布阶段后不再做：

- 新增 Entity 类型；
- 新增未验证修改 Tool；
- 大规模目录/命名重构；
- 更换 transaction/history 模型；
- 更换 Shell 结构；
- 顺带引入 Import/Export 大功能。

只允许：

- 编译修复；
- 可复现 blocker 修复；
- transient/transaction/selection 等稳定性修复；
- 本地化与文档纠正；
- build/run/publish 脚本修复。

## 3. 发布版本信息

OCCAD 当前仓库没有强制固定版本号策略。发布时建议使用 Semantic Versioning：

```text
MAJOR.MINOR.PATCH
```

首个正式稳定版本可以在完成全部 Release Gate 后选择 `1.0.0`；如果仍需要公开验证，可使用 `0.x` 或 prerelease tag。

版本号一旦进入安装包、Release、文件格式兼容说明，应在同一发布中保持一致。

## 4. 获取干净源码

Windows 示例：

```powershell
git status
git pull --ff-only
git rev-parse HEAD
```

要求：

- 工作区无未提交修改；
- HEAD 是准备发布的 commit；
- 构建日志记录该 commit SHA。

## 5. Bridge SDK 检查

Windows 默认：

```text
C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64
```

Linux 默认：

```text
~/.local/share/OcctCSharpBridge/SDK/3.0/linux-x64
```

可设置：

```text
OCCTCSHARPBRIDGE_SDK
```

SDK 根至少应提供 managed assemblies 和 Bridge metadata。portable SDK 还应提供：

```text
package-manifest.json
runtime/
occt/resources/
```

OCCAD 支持两种 portable root 形态：

```text
SDK/portable/...
```

或 SDK 根本身就是 portable root。

## 6. Windows 构建 Gate

执行：

```powershell
.\build.ps1
```

必须确认日志：

- `[build] OCCAD source` 是目标 HEAD；
- Bridge SDK/source 正确；
- runtime layout 正确；
- `OCCAD.Core` 成功；
- `OCCAD.Avalonia` 成功；
- `TreatWarningsAsErrors` 下没有 warning；
- 输出包含当前 Bridge runtime 所需文件。

**没有真实 `build.ps1` 成功输出，不得宣称 Windows 构建通过。**

## 7. Windows 启动 Gate

执行：

```powershell
.\run.ps1
```

flat Bridge + external OCCT：

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

通过条件：

- 进程正常启动；
- 主窗口可见；
- Viewport 首次显示正常，不需要移动鼠标才重绘；
- OCCT engine 初始化完成；
- 无 startup fatal error；
- 日志中没有持续 native resource failure。

## 8. Linux 构建与启动 Gate

```bash
./build.sh
./run.sh
```

检查：

- `OCCAD.dll`；
- `runtime/libOcctNative.so`；
- `occt/resources/`；
- `bridge-portable-manifest.json`；
- 应用能正常启动。

Linux 是跨平台验证，但不能替代 Windows 初版主验收。

## 9. Create 回归矩阵

至少覆盖：

### 2D

- Point；
- Line；
- Polyline；
- Polygon；
- Regular Polygon；
- Rectangle；
- Circle；
- Arc；
- Ellipse；
- Spline。

### 3D/Curve

- Box；
- Cylinder；
- Cone；
- Frustum；
- Sphere；
- Ellipsoid；
- Torus；
- Helix。

### Feature

- Extrude；
- Revolve；
- Sweep；
- Loft。

每项最少执行：

```text
Start
→ Pointer/parameter input
→ Preview
→ Exact Input（适用时）
→ Commit
→ Select result
→ Property inspect/edit
→ Undo/Redo
→ Cancel second run
```

## 10. 多绘制方式 Gate

### Circle

全部验证：

- Center + Radius；
- Center + Diameter；
- Two Points；
- Three Points；
- Point + Center。

### Arc

全部验证：

- Three Points；
- Center → Start → End；
- Start → Center → End；
- Start → End → Center；
- Start → End → Point；
- Start → End → Tangent。

### Ellipse

- Center + Axes；
- Axis Endpoints + Minor Axis。

### Regular Polygon

- Inscribed；
- Circumscribed；
- Sides 直接输入。

## 11. 修改/历史 Gate

### Move

```text
Select multiple entities
→ confirm
→ base point
→ target point
→ Preview
→ Commit
→ Ctrl+Z
→ Ctrl+Y
```

检查：

- Preview 是 replacement，不产生重复正式对象；
- 一次移动只有一个 Undo；
- Undo 恢复全部 Entity；
- Redo 再次恢复移动结果。

### Delete

- 单实体删除；
- 多实体删除；
- Undo；
- Redo。

### 当前不作为发布 Gate

Copy / Rotate / Scale / Mirror 因未进入正式交互产品面，不作为初版用户验收功能，也不能出现在 Release Notes 的正式功能列表中。

## 12. Transient/Neutral Gate

对 Line、Circle、Arc、Rectangle、Box、Move、GripEdit 等重点路径执行 Commit 和 Cancel。

每次结束后检查：

- Active Tool = null；
- Preview 无残留；
- Snap marker 无残留；
- Tracking 无残留；
- GripDrag 无残留；
- Preselection 状态合理；
- WorkPlane temporary state 已释放；
- 下一个 Tool 能正常开始。

这是发布 blocker 级检查。

## 13. Selection / Snap / Grip Gate

Selection：

- Replace；
- Add；
- Remove；
- Toggle；
- Window；
- Crossing；
- Entity/Subobject。

Snap 至少：

- Endpoint；
- Midpoint；
- Center；
- Intersection；
- Perpendicular；
- Tangent；
- Tab cycle。

Grip：

- 普通填充矩形 marker；
- Hot 圆形 marker；
- Drag 圆形 marker；
- valid commit；
- invalid target；
- Esc cancel；
- cleanup。

## 14. WorkPlane / Drafting Gate

在 Line、Rectangle、Circle、Arc、Ellipse、RegularPolygon 中测试：

```text
Start Tool
→ 输入 0~1 个阶段点
→ T/S/F 或 XY/YZ/XZ
→ move pointer
→ inspect Preview
→ Commit
```

同时测试：

- F3 SNAP；
- F8 ORTHO；
- F10 POLAR；
- Tracking 重算；
- fixed temporary ToolPlane 的安全 StepBack。

## 15. Property / Layer Gate

至少检查：

- Numeric 左对齐；
- Layer ComboBox；
- ColorTable；
- ByLayer Color/LineStyle/LineWidth；
- Circle/Arc/Ellipse/Rectangle/RegularPolygon Normal；
- multi-selection common property；
- property Undo/Redo；
- Current Layer 顶部 ComboBox 与 Layers panel 同步；
- Layer 0 不能删除/重命名。

## 16. Save/Open Gate

执行：

```text
Create mixed 2D/3D/Feature
→ create/change Layers
→ change Properties
→ Save .ocad
→ New/close
→ Open .ocad
```

对比：

- Entity 数量和类型；
- geometry；
- placement；
- layer；
- appearance；
- feature references/parameters；
- presentation。

确认 Preview/Snap/Grip/Tracking/Selection transient 没有写入文件。

## 17. UI / Localization / DPI Gate

中文、English 各检查一遍：

- Menu；
- 两行 Toolbar；
- Tool 参数条；
- Operation Prompt；
- Properties；
- Layers；
- Settings；
- Message/Error Dialog。

DPI：

- 100%；
- 125%；
- 150%。

检查：

- 不裁切；
- 不重叠；
- Tool 参数条不导致 Viewport 跳动；
- ComboBox 内容可读；
- Dialog button 文本居中；
- 不恢复 Ribbon/永久 Command Line/Ready 噪声。

## 18. Windows 发布包

执行：

```powershell
.\publish.ps1
```

默认输出：

```text
artifacts\publish\OCCAD
```

至少检查：

- `OCCAD.exe` / `OCCAD.dll`；
- `OcctNet.dll`；
- `OcctNet.Avalonia.dll`；
- `bridge-contract.json`；
- `bridge-manifest.json`；
- portable 时 `runtime/`；
- portable 时 `occt/resources/`；
- portable 时 `bridge-portable-manifest.json`；
- `run.ps1`。

在**发布目录本身**执行：

```powershell
.\run.ps1
```

确保 launcher 不依赖仓库源码目录。

## 19. Linux 发布包

执行：

```bash
./publish.sh
```

默认生成：

```text
artifacts/publish/OCCAD-linux-x64/
artifacts/publish/OCCAD-linux-x64.tar.gz
```

检查目录内：

- OCCAD apphost/dll；
- `runtime/`；
- `occt/resources/`；
- Bridge manifest；
- `run.sh`。

解压 tar.gz 到新的目录，再使用包内 `run.sh` 启动一次。

## 20. Release Notes 内容

Release Notes 只写当前真实产品面。

建议结构：

```text
OCCAD <version>

Highlights
- Classic Avalonia CAD shell
- 2D drafting
- 3D primitives
- Extrude/Revolve/Sweep/Loft
- Move/Delete
- Selection/Snap/Grip
- WorkPlane/ORTHO/POLAR
- Property/Layer
- Undo/Redo
- .ocad persistence
- Chinese/English

Runtime
- .NET 10
- Avalonia 12
- OcctCSharpBridge SDK 3.0 / ABI5
- OCCT 7.9.0
- Windows x64 / Linux x64

Known boundaries
- Copy/Rotate/Scale/Mirror not exposed yet
- no Array/Trim/Fillet/Annotation initial surface
- external exchange directions only when separately validated
```

不要把“Core API exists”写成用户功能。

## 21. Tag / Release 建议

发布 commit 应满足：

- 文档已同步；
- build/interaction gate 已记录；
- 无发布后临时修复未提交；
- Release Notes 对应同一 HEAD。

推荐：

```text
git tag -a v<version> <release-commit>
git push origin v<version>
```

GitHub Release 使用该 tag，并上传经过验证的 Windows/Linux 交付物。

## 22. 发布物校验记录

建议保存一份本地/Release 附件外的验收记录：

```text
Version:
Commit:
Bridge source:
Windows build:
Windows package launch:
Linux build:
Linux package launch:
Create regression:
Move/Delete:
Undo/Redo:
Snap/Grip:
Save/Open:
zh-CN/en-US:
125%/150% DPI:
Known issues:
```

这比只写“测试通过”更可追溯。

## 23. 发布 blocker

出现以下任一问题不应正式发布：

- Windows 主构建失败；
- 应用启动失败；
- 普通 Create 最终 Preview ghost；
- Move/Delete 造成模型/显示分叉；
- Undo/Redo 破坏 Document；
- Save/Open 丢失基本 Entity；
- Grip commit 污染未选实体；
- Tool Cancel 后无法进入下一个命令；
- 中文/English 关键 UI 大量缺失；
- 发布包依赖开发机源码目录才能运行。

## 24. 非 blocker / 可作为 Known Issue

在不影响基本闭环时，可以作为已知限制记录：

- 某个低频 Snap 的边界场景；
- 某个低频 Feature 的复杂几何失败；
- Linux 与 Windows 的细微字体/布局差异；
- 尚未进入产品面的高级修改命令；
- 未正式声明的外部格式方向。

必须在 Release Notes 中准确描述，不得写成已完成。

## 25. 发布后维护

发布后修复遵循：

1. 先复现；
2. 判断是否回归发布契约；
3. 最小修复；
4. 更新对应 regression；
5. Patch 版本发布。

不要在 Patch release 中混入大架构改造。

## 26. Definition of Release Ready

只有满足以下条件才标记 Release Ready：

```text
Clean main HEAD
+ real Windows build
+ real application startup
+ core interaction regression
+ neutral/transient cleanup
+ Undo/Redo
+ Save/Open
+ localization/DPI
+ package launch from clean directory
+ documentation matches product surface
```

文档完成只是其中一项，不替代真实构建和实机验收。
