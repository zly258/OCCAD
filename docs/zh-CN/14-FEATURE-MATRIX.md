# 14 初版功能矩阵

## 1. 说明

本文以当前 `CadCoreRegistration`、Classic Avalonia Shell 和持久化注册为准。

状态判断必须区分：

- **Core**：是否真实进入当前 Core/Registry/基础设施；
- **UI**：是否在当前 Classic Shell 暴露；
- **参数**：Tool 参数是否有可见输入入口；
- **持久化**：是否有正式读写路径；
- **状态**：初版、基础设施、内部支持或非初版。

源码中存在历史类、实验类或未注册 Action，不等于产品支持。

## 2. 2D Entity / Tool

| 功能 | Core | UI | 参数入口 | 持久化 | 状态 |
|---|---|---|---|---|---|
| Point | 是 | 是 | Tool 阶段输入 | 是 | 初版 |
| Line | 是 | 是 | Tool 阶段输入 | 是 | 初版 |
| Polyline | 是 | 是 | Tool/精确输入 | 是 | 初版 |
| Free Polygon | 是 | 是 | Tool/精确输入 | 是 | 初版 |
| Regular Polygon | 是 | 是 | Sides / Mode 参数条 + 直接输入 | 是 | 初版 |
| Rectangle | 是 | 是 | Tool 参数/阶段输入 | 是 | 初版 |
| Circle | 是 | 是 | Method + Tool 输入 | 是 | 初版 |
| Arc | 是 | 是 | Method + Tool 输入 | 是 | 初版 |
| Ellipse | 是 | 是 | Method/参数条 | 是 | 初版 |
| Spline | 是 | 是 | Tool/精确输入 | 是 | 初版 |
| Path | Entity/Persistence | 否 | 否 | 是 | 内部 Feature 轮廓支持 |

## 3. 2D 绘制方式

### Circle

| Method | UI 入口 | Tool |
|---|---|---|
| Center + Radius | 是 | `CircleTool` |
| Center + Diameter | 是 | `CircleTool` |
| Two Points | 是 | `CircleTool` |
| Three Points | 是 | `CircleTool` |
| Point + Center | 是 | `CircleTool` |

### Arc

| Method | UI 入口 | Tool |
|---|---|---|
| Three Points | 是 | `ArcTool` |
| Center → Start → End | 是 | `ArcTool` |
| Start → Center → End | 是 | `ArcTool` |
| Start → End → Center | 是 | `ArcTool` |
| Start → End → Point | 是 | `ArcTool` |
| Start → End → Tangent | 是 | `ArcTool` |

### Ellipse

| Method | UI 入口 | Tool |
|---|---|---|
| Center + Axes | 是 | `EllipseTool` |
| Axis Endpoints + Minor Axis | 是 | `EllipseTool` |

### Regular Polygon

| Method | UI 入口 | 参数 | Tool |
|---|---|---|---|
| Inscribed | 是 | Sides 3~360 / Mode | `RegularPolygonTool` |
| Circumscribed | 是 | Sides 3~360 / Mode | `RegularPolygonTool` |

指定圆心前支持直接输入整数边数并回车。

## 4. 三维基本体 / 曲线

| 功能 | Core | UI | 参数条 | 持久化 | 状态 |
|---|---|---|---|---|---|
| Box | 是 | 是 | 是 | 是 | 初版 |
| Cylinder | 是 | 是 | 是 | 是 | 初版 |
| Cone | 是 | 是 | 是 | 是 | 初版 |
| Frustum | 是 | 是 | 是 | 是 | 初版 |
| Sphere | 是 | 是 | 是 | 是 | 初版 |
| Ellipsoid | 是 | 是 | 是 | 是 | 初版 |
| Torus | 是 | 是 | 是 | 是 | 初版 |
| Helix | 是 | 是 | 是 | 是 | 初版 |

## 5. Modeling Feature

| 功能 | Tool | UI | 参数/阶段输入 | Feature Persistence | 状态 |
|---|---|---|---|---|---|
| Extrude | 是 | 是 | 是 | 是 | 初版 |
| Revolve | 是 | 是 | 是 | 是 | 初版 |
| Sweep | 是 | 是 | 是 | 是 | 初版 |
| Loft | 是 | 是 | 是 | 是 | 初版 |

Feature Entity 需要 Document context，因此持久化和交互创建分别由 Registry/Tool 承担。

## 6. Classic Shell

| 功能 | 当前状态 |
|---|---|
| 原生 Avalonia FluentTheme | 是 |
| Classic Menu | 是 |
| Single-row Toolbar | 是 |
| Active Tool Parameter Strip | 是 |
| Ribbon | 否，已退出产品基线 |
| 三行分组 Toolbar | 否，已退出产品基线 |
| DensityStyle.Compact | 否 |
| 全局自定义 Button/TextBox/ComboBox 皮肤 | 否 |
| 自定义 CAD ColorTable | 是，业务控件保留 |
| ViewCube | 默认关闭 |
| Lower-left Triedron | 保留 |
| 永久 Bottom Command Input | 否 |
| 永久 Dynamic HUD | 否 |
| 当前操作提示 | 是，Status Strip |

## 7. Tool 参数系统

当前 Shell 会读取 `CadTool.ParameterPanel` 并自动建立编辑控件。

| Descriptor | UI |
|---|---|
| Integer | TextBox |
| Double | TextBox |
| OptionalDouble | TextBox |
| Boolean | CheckBox |
| Choice | ComboBox |
| String | TextBox |

UI 通过 `TrySetParameter()` 更新 Tool，不直接修改 Tool 私有状态。

## 8. Selection / Interaction

| 功能 | Core | UI/Viewport | 状态 |
|---|---|---|---|
| Entity Selection | 是 | 是 | 初版基础设施 |
| Subobject Selection | 是 | 是 | 初版基础设施 |
| Preselection | 是 | 是 | 初版基础设施 |
| Replace / Add / Remove / Toggle | 是 | 是 | 初版基础设施 |
| Window Selection | 是 | 是 | 初版基础设施 |
| Crossing Selection | 是 | 是 | 初版基础设施 |
| Grip / Hot Grip | 是 | 是 | 初版基础设施 |
| Grip Edit Preview/Commit | 是 | 是 | 初版基础设施 |
| WorkPlane XY/YZ/XZ | 是 | 是 | 初版基础设施 |
| ORTHO | 是 | 是 | 初版基础设施 |
| POLAR | 是 | 是 | 初版基础设施 |
| Precision Input | 是 | Tool dependent | Core/Tool 基础设施 |
| Preview | 是 | 是 | transient |
| Tracking | 是 | 是 | transient |
| Selection Window | 是 | 是 | transient |

## 9. Snap

| Snap Type | Core | UI 配置 |
|---|---|---|
| Endpoint | 是 | 是 |
| Midpoint | 是 | 是 |
| Intersection | 是 | 是 |
| Center | 是 | 是 |
| Perpendicular | 是 | 是 |
| Tangent | 是 | 是 |
| Quadrant | 是 | 是 |
| Extension | 是 | 是 |
| Insertion | 是 | 是 |
| Node | 是 | 是 |
| Apparent Intersection | 是 | 是 |
| Nearest | 是 | 是 |

## 10. Property / Layer

| 功能 | Core | UI | 状态 |
|---|---|---|---|
| Categorized PropertyGrid | Descriptor | 是 | 初版 |
| Multi-selection common properties | 是 | 是 | 初版 |
| Mixed Value | 是 | 是 | 初版 |
| Numeric / Enum / Choice | 是 | 是 | 初版 |
| Numeric 左对齐 | — | 是 | 初版 UI 规则 |
| Layer | 是 | ComboBox | 初版 |
| ByLayer appearance | 是 | 是 | 初版 |
| Color | 是 | 自定义 ColorTable | 初版 |
| Point / Vector | 是 | X/Y/Z 三行 | 初版 |
| Editable planar Normal | 是 | X/Y/Z 三行 | Circle/Arc/Ellipse/Rectangle/Regular Polygon |
| Measurement readonly | 是 | 是 | 初版 |
| Layer manager | 是 | 是 | 初版 |
| Current Layer | 是 | 顶部 ComboBox | 初版 |

## 11. History / Transaction

| 功能 | Core | 当前 Shell | 状态 |
|---|---|---|---|
| CadTransaction | 是 | 间接使用 | 基础设施 |
| CadHistory | 是 | 间接使用 | 基础设施 |
| Undo | 是 | 无独立顶部按钮 | Core 支持 |
| Redo | 是 | 无独立顶部按钮 | Core 支持 |
| Property atomic history | 是 | 是 | 初版基础设施 |

## 12. Persistence / Exchange

正式 Document/Entity/Layer/Feature 状态可进入持久化；Preview、Snap marker、Grip marker、Tracking guide 等 transient 不持久化。

STEP / IGES / BREP / STL / OBJ / glTF 等方向必须按实际 Import/Export 实现分别验收。文件选择器出现扩展名不等于双向交换能力完成。

## 13. 不属于当前初版产品面

除非重新明确进入范围，否则不在当前 Classic Shell 暴露：

- Move / Copy / Rotate / Scale / Mirror；
- Array；
- Offset；
- Trim / Extend；
- Fillet / Chamfer；
- Text；
- Dimension / Annotation；
- 仅源码存在但未注册的高级类型。

## 14. 完成判定

每个产品功能按以下链路验收：

`入口 → 参数 → Prompt → Preview → Exact Input → Commit → Property → Grip/Snap → Save/Open`

并检查 Cancel/Commit 后是否恢复 neutral、是否存在 Preview/Native transient 残留。

当前仓库不维护独立 Test 项目；实际 build 和手工/native 回归是当前验收依据。
