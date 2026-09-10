# 11 构建与实机验收

## 1. 当前验证策略

OCCAD 当前不维护独立 Test 项目，也不使用 GitHub Actions 作为产品验收入口。仓库主解决方案只保留：

- `OCCAD.Core`
- `OCCAD.Avalonia`

当前权威验证顺序是：

```text
静态检查
→ 本地 build.ps1
→ 启动真实程序
→ 手工/Native 交互回归
→ 文档与功能面核对
```

不得在没有真实 Windows `build.ps1` 结果时声称构建通过。

## 2. Windows 构建基线

Windows x64 是当前主要产品环境。权威构建入口：

```powershell
.\build.ps1
```

构建必须至少确认：

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

或设置：

```text
OCCT_ROOT
CASROOT
```

`OCCT resources: False` 并不自动表示 build 无效；要区分 compile/link contract 与最终 runtime resource 配置。

## 4. Linux 构建

Linux 使用：

```bash
./build.sh
```

Linux 用于跨平台编译/运行验证，不替代 Windows 初版产品验收。

## 5. 初版功能面

### 二维

- Point
- Line
- Polyline
- Polygon
- Regular Polygon：Inscribed / Circumscribed
- Rectangle
- Circle：5 种 Method
- Arc：6 种 Method
- Ellipse：2 种 Method
- Spline

### 三维与曲线

- Box
- Cylinder
- Cone
- Frustum
- Sphere
- Ellipsoid
- Torus
- Helix

### 建模

- Extrude
- Revolve
- Sweep
- Loft

### 交互基础设施

- Entity/Subobject Selection
- Preselection
- Window/Crossing
- Snap
- Grip / Grip Edit
- WorkPlane XY/YZ/XZ
- ORTHO / POLAR
- Preview / Tracking / Transient cleanup
- Layer / Property inspection/editing
- History / Transaction infrastructure
- persistence / exchange entry points

完整矩阵见 [14-FEATURE-MATRIX.md](14-FEATURE-MATRIX.md)。

## 6. 必须手测的 Create 闭环

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

重点检查：

- 最后一步不残留 Preview ghost；
- Tool 完成后不残留 Tool-owned transient；
- Snap marker 能清理；
- Grip marker 与实体一致；
- work plane / drafting state 不被错误污染；
- model 与 native presentation 不分叉。

## 7. 多绘制方式验收

必须逐入口测试，而不是只测试默认 Method。

Circle：

1. Center + Radius
2. Center + Diameter
3. Two Points
4. Three Points
5. Point + Center

Arc：

1. Three Points
2. Center → Start → End
3. Start → Center → End
4. Start → End → Center
5. Start → End → Point
6. Start → End → Tangent

Ellipse：

1. Center + Axes
2. Axis Endpoints + Minor Axis

Regular Polygon：

1. Inscribed
2. Circumscribed

验证不同入口是否真正把初始参数传给同一 Tool，而不是出现入口可见但 Method 未生效。

## 8. Property / Normal / Layer 验收

至少验证：

- 数值 editor 左对齐；
- Label/Value 不出现异常右对齐；
- Circle/Arc/Ellipse/Rectangle/RegularPolygon 的 `Normal` 可编辑；
- `Normal` 拒绝零向量和非法值；
- Normal 改动后几何和 Viewer 同步；
- Property 修改形成正确 history；
- Layer ComboBox 与 Layer panel 的 current-layer 状态一致；
- ByLayer appearance 正确解析。

## 9. Selection / Snap / Grip 验收

Selection：

- left→right Window；
- right→left Crossing；
- Replace / Add / Remove / Toggle；
- Entity / Subobject scope。

Snap 至少覆盖高频类型：

- Endpoint
- Midpoint
- Center
- Intersection
- Perpendicular
- Tangent

并检查 candidate/current marker 在 Tool 完成/Cancel 后清理。

Grip 至少验证：

- marker 显示；
- hot 状态；
- drag preview；
- valid commit；
- invalid input 不污染实体；
- cleanup。

## 10. View 验收

所有固定视图按钮必须是真实 Action：

- 6 个正交视图；
- 4 个 Corner Isometric；
- Fit；
- Wireframe；
- Shaded。

同时确认：

- ViewCube 始终隐藏；
- Engine recreation 后仍隐藏 ViewCube；
- 左下角 Triedron 正常。

## 11. UI 验收

检查：

- FluentTheme 使用 Compact Density；
- 顶部只有一套 grouped three-row Toolbar；
- 不出现大型 Ribbon、Reference Shell 或 Floating Tool Panel；
- 不显示底部 Command Input；
- 不显示 `命令：就绪` / `Command: Ready` / `Ready - OCCT`；
- Layer ComboBox 和中文/English 切换位于顶部；
- 底部只保留 XY/YZ/XZ、SNAP、ORTHO、POLAR；
- Model / Properties / Layers 布局稳定；
- 125% / 150% DPI 下无明显错位、裁切或模糊；
- 错误仍能通过 Dialog/Error Window 明确反馈。

## 12. 保存/打开验收

至少执行一次：

```text
Create mixed 2D/3D entities
→ change Layer/Properties
→ Save .ocad
→ close/new
→ Open .ocad
→ compare geometry/properties/presentation
```

Preview/Snap/Grip/Selection 等 transient 不应进入持久化数据。

## 13. Exchange 验收

文件选择器出现扩展名不代表双向完整支持。每种实际对外声明的格式都应分别验证 Import/Export 方向，并记录 Bridge/OCCT runtime 条件。

## 14. 构建失败处理顺序

如果 `build.ps1` 失败：

1. 先修第一个真实编译错误；
2. 不通过 `!`、空实现或兼容空 API 隐藏真实 nullable/contract 问题；
3. 判断错误来自初版产品面还是无用残留；
4. 初版功能依赖必须修复；
5. 真正无用的历史残留才删除；
6. 构建成功后再做实机回归。

## 15. 完成定义

一次初版变更完成至少要求：

- 当前两个项目本地 Windows 编译成功；
- 对应真实用户流程可执行；
- 不新增假按钮或占位功能；
- 不新增 Preview/Snap/Grip/SelectionWindow 残留；
- model / history / native presentation 一致；
- UI 与文档描述一致；
- 中文/English 均正常；
- 新代码有清晰 owner 和生命周期。
