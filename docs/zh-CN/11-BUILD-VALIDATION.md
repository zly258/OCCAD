# 11 构建与实机验收

## 1. 当前验证策略

OCCAD 当前不维护独立 Test 项目，也不使用 GitHub Actions 作为产品验收入口。仓库主解决方案只保留：

- `OCCAD.Core`
- `OCCAD.Avalonia`

当前权威验证顺序：

```text
静态检查
→ 本地 build.ps1
→ 启动真实程序
→ 手工/Native 交互回归
→ 文档与功能面核对
```

不得在没有真实 Windows `build.ps1` 输出时声称构建通过。

## 2. Windows 构建基线

Windows x64 是当前主要产品环境。权威构建入口：

```powershell
.\build.ps1
```

构建至少确认：

- 输出中 OCCAD source SHA 与待验证 main HEAD 一致；
- Bridge SDK 路径有效；
- Bridge SDK source/runtime 信息符合预期；
- `OCCAD.Core` 编译成功；
- `OCCAD.Avalonia` 编译成功；
- `TreatWarningsAsErrors` 下无 nullable/编译警告；
- 输出目录包含可运行 OCCAD 与所需 Bridge runtime。

默认 SDK：

```text
C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64
```

可用 `OCCTCSHARPBRIDGE_SDK` 覆盖。

## 3. Runtime

如果 Bridge SDK 是 flat runtime 且不携带完整 OCCT runtime，运行时需要：

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

或设置 `OCCT_ROOT` / `CASROOT`。

`OCCT resources: False` 并不自动表示 build 无效；要区分 compile/link contract 与最终 runtime resource 配置。

## 4. Linux 构建

Linux 使用：

```bash
./build.sh
```

Linux 用于跨平台编译/运行验证，不替代 Windows 初版产品验收。

## 5. 初版功能面

二维：Point、Line、Polyline、Polygon、Regular Polygon、Rectangle、Circle、Arc、Ellipse、Spline。

三维与曲线：Box、Cylinder、Cone、Frustum、Sphere、Ellipsoid、Torus、Helix。

建模：Extrude、Revolve、Sweep、Loft。

交互基础设施：Entity/Subobject Selection、Preselection、Window/Crossing、Snap、Grip、XY/YZ/XZ WorkPlane、ORTHO、POLAR、Preview、Tracking、Precision Input、Layer、Property、History/Transaction、Persistence/Exchange entry points。

完整矩阵见 [14-FEATURE-MATRIX.md](14-FEATURE-MATRIX.md)。

## 6. Create 闭环

每个当前创建 Tool 至少验证：

```text
Start Tool
→ Pointer / parameter path
→ Preview
→ Commit
→ Select result
→ Inspect/Edit Properties
→ Grip（如适用）
→ Undo/Redo（如该路径可达）
→ Cancel/exit
```

重点检查：最后一步不残留 Preview ghost；Tool 完成后不残留 Tool-owned transient；Snap/Tracking marker 能清理；Grip marker 与实体一致；WorkPlane / Drafting state 不被错误污染；model 与 native presentation 不分叉。

## 7. 多绘制方式

必须逐入口测试，而不是只测试默认 Method。

Circle：Center+Radius、Center+Diameter、Two Points、Three Points、Point+Center。

Arc：Three Points、Center→Start→End、Start→Center→End、Start→End→Center、Start→End→Point、Start→End→Tangent。

Ellipse：Center+Axes、Axis Endpoints+Minor Axis。

Regular Polygon：Inscribed、Circumscribed。

确认不同入口把初始参数传给同一 Tool，而不是出现入口可见但 Method 未生效。

## 8. Tool 参数条与实时联动

必须验证固定底部参数条，而不是只检查参数能否输入：

1. 启动带参数 Tool，参数条出现在状态栏正上方；
2. 切换到无参数 Tool 或取消 Tool，参数条固定行高度不变；
3. Viewport 不发生上下跳动；
4. 手工修改参数后 Preview 立即同步；
5. 鼠标移动时常用实际尺寸反向更新参数值；
6. 正在编辑的 TextBox/ComboBox/CheckBox 不被刷新重建；
7. ToolChanged / ToolUpdated / 语言切换后参数条仍只有一个 Visual Parent。

至少检查实时值：Circle Radius/Diameter、Rectangle Width/Height、Ellipse Major/Minor Radius、Box Length/Width/Height、Cylinder Radius/Height。

## 9. 工作平面切换

工作平面回归必须覆盖“绘图过程中切换”，不能只在 Idle 状态测试按钮。

二维 Tool 至少覆盖 Line、Polyline、Rectangle、Circle、Arc、Ellipse、Regular Polygon：

```text
Start Tool
→ 输入 0~1 个阶段点
→ 切 XY/YZ/XZ
→ 移动鼠标
→ 检查 Preview 平面
→ Commit
→ 检查实体 Normal/Axis
```

分阶段三维 Tool（如 Box/Cylinder）还要验证：

- 普通阶段立即切换；
- fixed temporary ToolPlane 阶段请求切换时，Core 自动 StepBack 到最近兼容阶段；
- 不混用旧阶段已接受点和新 Plane；
- User Plane Lock / fixed Grip Plane 时请求被拒绝；
- 切换后 Snap/Tracking 被清理并重新计算。

## 10. Esc 验收

至少在 Viewport、Tool 参数 TextBox、主窗口其他普通控件三种焦点状态按 `Esc`。

每次都检查 Active Tool 为空、Entity Selection 为空、Subobject Selection 为空、Preselection 为空、Snap / Tracking 临时状态清理、Viewport 恢复焦点、Preview 不残留。

## 11. Property / Normal / Layer

至少验证：

- 数值 editor 左对齐；
- Label/Value 不异常右对齐；
- Circle/Arc/Ellipse/Rectangle/RegularPolygon 的 `Normal` 可编辑；
- `Normal` 拒绝零向量和非法值；
- Normal 改动后几何和 Viewer 同步；
- Property 修改形成正确 history；
- Layer ComboBox 与 Layer panel 的 current-layer 状态一致；
- Layer 面板“线型/线宽”ComboBox 有足够列宽，不能只露一个字符；
- ByLayer appearance 正确解析。

## 12. Selection / Snap / Grip

Selection：Window、Crossing、Replace/Add/Remove/Toggle、Entity/Subobject scope。

Snap 至少覆盖 Endpoint、Midpoint、Center、Intersection、Perpendicular、Tangent，并检查 candidate/current marker 在 Tool 完成/Cancel 后清理。

Grip 至少验证 marker、hot、drag preview、valid commit、invalid input 不污染实体、cleanup。

## 13. View

所有固定视图入口必须是真实 Action：6 个正交视图、4 个 Corner Isometric、充满（内部 Action `view.fit`）、Wireframe、Shaded。

同时确认 ViewCube 始终隐藏、Engine recreation 后仍隐藏、左下角 Triedron 正常。

## 14. UI / Dialog / DPI

当前 UI 基线必须检查：

- Avalonia 原生 FluentTheme；
- 顶部只保留 Menu + 最多两行高频 Toolbar；
- Toolbar 第一行包含撤销/重做、当前图层、常用视图、充满和显示模式；
- Toolbar 第二行只保留常用 2D / 3D / Feature；
- New/Open/Save 和语言切换不重复占用 Toolbar，仍通过 Menu 使用；
- 不出现 Ribbon、旧 CleanShell、三行分组命令区或 Floating Tool Panel；
- Tool 参数条固定在底部状态栏上方；
- 不显示永久 Command Input、`命令：就绪`、`Command: Ready`、`Ready - OCCT`；
- 状态栏只保留当前操作提示、XY/YZ/XZ、SNAP、ORTHO、POLAR；
- Model / Properties / Layers 布局稳定；
- Settings 标签列按最长当前语言标签自适应，Value 列占剩余空间；
- Message/Layer/Color/Error/Settings 等 Dialog 操作按钮文字水平和垂直居中；
- 125% / 150% DPI 下无明显错位、裁切或模糊；
- 错误仍能通过 Dialog/Error Window 明确反馈。

## 15. 保存/打开

至少执行一次：

```text
Create mixed 2D/3D entities
→ change Layer/Properties
→ Save .ocad
→ close/new
→ Open .ocad
→ compare geometry/properties/presentation
```

Preview/Snap/Grip/Selection 等 transient 不进入持久化数据。

## 16. Exchange

文件选择器出现扩展名不代表双向完整支持。每种实际对外声明的格式都应分别验证 Import/Export 方向，并记录 Bridge/OCCT runtime 条件。

## 17. 构建失败处理顺序

如果 `build.ps1` 失败：

1. 先确认输出中的 OCCAD source SHA；
2. 修第一个真实编译错误；
3. 不通过 `!`、空实现或兼容空 API 隐藏 nullable/contract 问题；
4. 判断错误来自当前产品面还是历史残留；
5. 产品功能依赖修复，真正无用残留删除；
6. 再次 build；
7. 编译成功后才进行实机交互回归。

## 18. 完成定义

一次初版变更完成至少要求：当前两个项目本地 Windows 编译成功；对应真实用户流程可执行；不新增假按钮或占位功能；不新增 Preview/Snap/Grip/SelectionWindow 残留；model / history / native presentation 一致；UI 与文档描述一致；中文/English 均正常；新代码有清晰 owner 和生命周期。

在真实构建和实机验收之前只能描述“已修改/待验证”，不能描述为“已通过”。
