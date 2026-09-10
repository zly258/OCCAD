# 15 命令与功能参考

本文给出 OCCAD 初版的稳定 Action ID、Command alias、默认绘制方式、Tool ID 和产品暴露状态。

> 当前 Classic Shell **没有永久 Command Line**。普通用户主要通过 Menu/Toolbar 操作；Command ID/Alias 是 Core 的稳定命令元数据，可供 UI、自动化、MCP/脚本集成或后续命令输入面复用。活动 Tool 的精确输入仍可直接在 Viewport 键入。

## 1. 命名规则

稳定 ID 使用分层形式：

```text
draw.*       二维绘图
solid.*      三维基本体
curve.*      三维/空间曲线
feature.*    Modeling Feature
edit.*       修改/编辑
view.*       视图
display.*    显示模式
```

规则：

- Action ID 是稳定机器标识，不本地化；
- 中文/English 只影响 Caption；
- Alias 不应指向未注册 Action；
- 一个 Tool 的多种绘制方式使用不同 Action ID + initial parameter；
- 源码 helper/API 存在不代表有正式 Command。

## 2. 二维绘图命令

| 功能 | Action ID | Alias | Tool | 当前状态 |
|---|---|---|---|---|
| Point | `draw.point` | `POINT`, `PO` | `point` | 初版 |
| Line | `draw.line` | `LINE`, `L` | `line` | 初版 |
| Polyline | `draw.polyline` | `POLYLINE`, `PL` | `polyline` | 初版 |
| Free Polygon | `draw.polygon` | `POLYGON`, `PG` | `polygon` | 初版 |
| Rectangle | `draw.rectangle` | `RECTANGLE`, `REC` | `rectangle` | 初版 |
| Spline | `draw.spline` | `SPLINE`, `SPL` | `spline` | 初版 |

## 3. Regular Polygon

| 方式 | Action ID | Alias | Initial Parameter |
|---|---|---|---|
| Inscribed | `draw.regularpolygon.inscribed` | `REGULARPOLYGON`, `RPOLY` | `Mode=Inscribed` |
| Circumscribed | `draw.regularpolygon.circumscribed` | — | `Mode=Circumscribed` |

两者都进入 `regularpolygon` Tool。

参数：

- `Sides`: 3~360；
- `Mode`: Inscribed / Circumscribed。

默认传统命令 `REGULARPOLYGON` 对应 Inscribed。

## 4. Circle

所有方式共用 `circle` Tool。

| 方式 | Action ID | Alias | Initial Parameter |
|---|---|---|---|
| Center + Radius | `draw.circle.centerradius` | `CIRCLE`, `C` | `Method=CenterRadius` |
| Center + Diameter | `draw.circle.centerdiameter` | `CIRCLEDIAMETER` | `Method=CenterDiameter` |
| Two Points | `draw.circle.twopoints` | `CIRCLE2P` | `Method=TwoPoints` |
| Three Points | `draw.circle.threepoints` | `CIRCLE3P` | `Method=ThreePoints` |
| Point + Center | `draw.circle.pointcenter` | `CIRCLEPC` | `Method=PointCenter` |

Toolbar 的“圆”使用 `draw.circle.centerradius`。

## 5. Arc

所有方式共用 `arc` Tool。

| 方式 | Action ID | Alias |
|---|---|---|
| Three Points | `draw.arc.threepoints` | `ARC`, `A` |
| Center → Start → End | `draw.arc.centerstartend` | — |
| Start → Center → End | `draw.arc.startcenterend` | — |
| Start → End → Center | `draw.arc.startendcenter` | — |
| Start → End → Point | `draw.arc.startendpoint` | — |
| Start → End → Tangent | `draw.arc.startendtangent` | — |

Toolbar 的“圆弧”使用 `draw.arc.threepoints`。

## 6. Ellipse

所有方式共用 `ellipse` Tool。

| 方式 | Action ID | Alias |
|---|---|---|
| Center + Axes | `draw.ellipse.centermajor` | `ELLIPSE`, `EL` |
| Axis Endpoints + Minor Axis | `draw.ellipse.axisendpoints` | — |

## 7. 三维基本体与曲线

| 功能 | Action ID | Alias | Tool |
|---|---|---|---|
| Box | `solid.box` | `BOX`, `B` | `box` |
| Cylinder | `solid.cylinder` | `CYLINDER`, `CYL` | `cylinder` |
| Cone | `solid.cone` | `CONE`, `CN` | `cone` |
| Frustum | `solid.frustum` | `FRUSTUM`, `FRU` | `frustum` |
| Sphere | `solid.sphere` | `SPHERE`, `SPH` | `sphere` |
| Ellipsoid | `solid.ellipsoid` | `ELLIPSOID`, `ELLIP` | `ellipsoid` |
| Torus | `solid.torus` | `TORUS`, `TOR` | `torus` |
| Helix | `curve.helix` | `HELIX`, `HX` | `helix` |

## 8. Modeling Feature

| 功能 | Action ID | Alias | Tool |
|---|---|---|---|
| Extrude | `feature.extrude` | `EXTRUDE`, `EXT` | `extrude` |
| Revolve | `feature.revolve` | `REVOLVE`, `REV` | `revolve` |
| Sweep | `feature.sweep` | `SWEEP`, `SW` | `sweep` |
| Loft | `feature.loft` | `LOFT` | `loft` |

Feature Tool 需要符合要求的已有 Entity/轮廓输入。

## 9. 修改命令

| 功能 | Action ID | Alias | Tool/Action | 产品状态 |
|---|---|---|---|---|
| Move | `edit.move` | `MOVE`, `M` | `move` Tool | 初版 |
| Delete | `edit.delete` | `DELETE`, `ERASE` | Action | 初版 |

### Move 阶段

```text
Select Entities
→ Confirm Selection
→ Base Point
→ Target Point / replacement Preview
→ Commit
```

Move 是当前修改 Tool 的参考实现。

### 未开放修改能力

以下只存在部分 Core geometry/transaction 能力，不属于正式 Command surface：

- Copy；
- Rotate；
- Scale；
- Mirror；
- Array；
- Offset；
- Trim / Extend；
- Fillet / Chamfer。

不要为这些能力建立 alias 或 UI 入口，除非完整 Tool lifecycle 已实现并验收。

## 10. 视图命令

| 功能 | Action ID | Alias |
|---|---|---|
| Fit | `view.fit` | `FIT`, `ZE` |
| Top | `view.top` | `TOP` |
| Bottom | `view.bottom` | `BOTTOM` |
| Front | `view.front` | `FRONT` |
| Back | `view.back` | `BACK` |
| Left | `view.left` | `LEFT` |
| Right | `view.right` | `RIGHT` |
| Iso NE | `view.iso.ne` | `ISO`, `ISONE` |
| Iso NW | `view.iso.nw` | `ISONW` |
| Iso SE | `view.iso.se` | `ISOSE` |
| Iso SW | `view.iso.sw` | `ISOSW` |
| Wireframe | `display.wireframe` | `WIREFRAME`, `WF` |
| Shaded | `display.shaded` | `SHADED`, `SHADE` |

## 11. History 快捷键

History 的 UI 入口不是 CommandCatalog Action，而由 Workspace 统一处理：

| 输入 | 行为 |
|---|---|
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Ctrl+Shift+Z` | Redo |

必须经过 `CadWorkspace.Undo()` / `CadWorkspace.Redo()`，不能从 UI 直接调用 `CadHistory`。

## 12. Drafting 快捷键

| 输入 | 行为 |
|---|---|
| `F3` | SNAP on/off |
| `F8` | ORTHO on/off |
| `F10` | POLAR on/off |
| `T` | XY WorkPlane |
| `S` | YZ WorkPlane |
| `F` | XZ WorkPlane |
| `Tab` | 下一 Snap candidate |
| `Shift+Tab` | 上一 Snap candidate |

WorkPlane 快捷键在 Tool stage 不允许切换时不会强行破坏已确认几何。

## 13. Tool 控制输入

| 输入 | 行为 |
|---|---|
| `Esc` | Cancel 当前 Tool |
| `Backspace` | StepBack |
| `Enter` / `Space` | Submit/Finish |
| 右键 | Secondary action |
| `O` | Point stage 下进入 Offset Point |

CommandManager 还理解：

- `ESC`, `CANCEL`；
- `FINISH`, `DONE`；
- `U`, `BACK`, `STEPBACK`。

这些文本语义主要用于统一命令输入/自动化接口。

## 14. 精确输入语法

当前 Tool stage 支持时，可以使用：

```text
100                单值 Length/Angle/Factor（取决于 Stage）
L 100              Length
LENGTH 100
A 45               Angle
ANGLE 45
F 2                Factor
FACTOR 2
100,50             2D point
100,50,20          3D point
@20,0              relative point
@50<30             relative polar-style point
Radius=50          Tool parameter
```

约束：

- 数值必须有限；
- relative point 需要当前 Stage 有 reference point；
- 输入类型必须属于当前 Tool `PrecisionInputs`；
- unsupported parameter 返回失败，不应修改模型；
- invalid input 不建立 History。

## 15. Snap 类型

当前 Snap Core/UI 配置面包含：

- Endpoint；
- Midpoint；
- Center；
- Vertex；
- Quadrant；
- Nearest；
- Intersection；
- Perpendicular；
- Tangent；
- Apparent Intersection；
- Extension；
- Insertion；
- Node。

Snap 是 drafting infrastructure，不是持久 Entity。

## 16. UI 暴露规则

Action 注册后仍不自动意味着要放进 Toolbar。

### Toolbar

只保留高频：

```text
Row 1: Undo Redo | Layer | View | Display
Row 2: frequent 2D | Move Delete | frequent 3D/Feature
```

### Menu

保留完整当前产品 Action surface，包括所有 Circle/Arc/Ellipse 方法和低频三维/Feature。

### 不暴露

- 未注册 Action；
- 只有 Entity class 的能力；
- 只有 geometry helper 的能力；
- 只有 transaction API、没有交互 Tool 的修改能力。

## 17. Action 可执行条件

`CadToolAction.CanExecute()` 至少要求：

- Workspace Engine 已初始化；
- 对应 Tool 已注册。

因此一个命令可以存在于元数据中，但在 Engine 未初始化时不可执行。

Delete 等非 Tool Action 还可以根据 Selection 等正式状态定义自己的 `CanExecute()`。

## 18. Repeat 与历史

Tool Action 是 repeatable command。`CadActionManager` 保存最近的可重复 Action ID；`CadCommandManager` 另外维护文本命令输入历史。

两者与模型 Undo/Redo History 不同：

- Action/Command history：用户最近调用过什么命令；
- `CadHistory`：正式模型状态的 Undo/Redo。

不能混为一套。

## 19. 稳定性要求

增加/修改命令时必须检查：

- Action ID 与 `CadCoreRegistration` 一致；
- Alias 只指向真实注册 Action；
- Caption key 在中英文资源中存在；
- 多方法 Tool 不复制 geometry implementation；
- Tool Action 的 initial parameter 可被 Tool 接受；
- UI 入口调用 Action，不直接 new Tool；
- Command failure 不吞 fatal exception；
- 文档与 Feature Matrix 同步。

## 20. 当前发布命令面

初版发布只承诺本文列出的已注册命令和 [14-FEATURE-MATRIX.md](14-FEATURE-MATRIX.md) 标记为初版的功能。

后续新增 Command 应同时更新：

1. `CadCoreRegistration`；
2. `CadCommandCatalog`；
3. Menu/Toolbar（如需要）；
4. zh-CN/en-US localization；
5. 本文；
6. Feature Matrix；
7. 对应交互/用户文档。
