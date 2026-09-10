# 02 UI 规范

## 总体方向

OCCAD 使用紧凑工业 CAD 桌面壳：自定义 Ribbon + 左侧 Model + 中央 Viewport + 右侧 Layer/Property + 底部 Command Line + StatusBar + Viewport 内非模态浮动 Tool Panel。Viewport 始终占最大区域。

```text
Ribbon
┌──────────────┬──────────────────────────────┬─────────────────────┐
│ Model        │          Viewport            │ Layer               │
│              │   Floating Tool Panel        │ splitter            │
│              │                              │ Property            │
└──────────────┴──────────────────────────────┴─────────────────────┘
Command Line
Selection | Work Plane | SNAP ORTHO POLAR | XYZ
```

不再构造旧 Menu、旧 Toolbar 或启动后的 UI refinement 壳层。

## Theme

`CadTheme` 是桌面 UI 视觉指标唯一来源。当前基线：主 UI 11 px；次要文字 10/10.5 px；紧凑控件 22 px；Header 约 23–24 px；浅灰工具/面板区域；深色 Viewport；蓝色仅用于 Current/Active；低/无圆角；细分隔线；Segoe UI / Microsoft YaHei UI / CJK fallback 字体栈。

Ribbon 使用原生 Avalonia 控件，不依赖第三方 Ribbon。每个 Group 固定三行紧凑排列，横向空间不足时滚动，不允许在 125%/150% DPI 下把主窗口撑宽。

## Ribbon / Action / Tool Panel

Ribbon 只调用已注册 Action ID，不复制业务逻辑。真正的命令族使用一层下拉，例如 Circle、Arc、Regular Polygon、Ellipse 和 3D Primitives。

`CadActionManager`、`CadToolManager`、`CadCommandManager.ForWorkspace()` 分别作为 Action、Tool、命令会话的唯一状态来源。Ribbon、Command Line 和 Floating Tool Panel 只是同一 Core 状态机的不同输入表面。

稳定的 Tool 参数（Radius、Width、Height、Angle、Factor 等）进入 Floating Tool Panel。Tool Panel 不重复完整 Prompt；参数、精确输入、Back/Accept/Finish/Cancel 必须直接调用当前 Tool，不维护第二份参数状态。

## Model / Layer / Property

Model 位于左侧。Layer 与 Property 是右侧两个独立、可调整高度的面板，不是 Tab。

Layer 表格字段为：

```text
当前 | 名称 | 显 | 色 | 线型 | 线宽 | 锁
```

当前图层使用明确的 `●/○` 状态；只有“当前”列切换当前图层，点击名称只检查图层。默认层不能重命名或删除。Layer UI 只调用 `CadWorkspace`；事务、回滚、History 在 Core。

Property 使用 Core descriptor/editor 语义，支持分类、多选共同属性、Mixed Value、Layer、ByLayer、Color、Enum、Numeric、Point/Vector X/Y/Z 编辑，并通过 `CadPropertyTransaction` 提交修改。

外观行统一为：

```text
颜色      [随层] [颜色值]
线型      [随层] [线型]
线宽      [随层] [数值]
透明度
可见
```

三个 ByLayer bool 保留在 Core，但不单独占行。

## Prompt 与状态职责

- Command Line：唯一完整 Tool Prompt、命令输入、历史/补全、执行结果和错误。
- Floating Tool Panel：当前步骤、精确坐标、Length/Angle/Factor、Tool 参数、Back/Accept/Finish/Cancel。
- Dynamic HUD：鼠标附近当前 Length/Angle、SNAP、ORTHO/POLAR tracking。
- StatusBar：Selection、Work Plane、SNAP/ORTHO/POLAR、XYZ 坐标。

禁止把下一步 Tool Prompt、History 或 Precision 文本再复制到 StatusBar。

## Viewport / DPI

唯一 OCCT Host 是 `OcctAvaloniaViewport`。Drawing 使用中心留空十字；Snap aperture 与 Dynamic HUD 保持屏幕像素稳定。ViewCube 隐藏，左下角 Triedron 保留。

Bridge 已 requestRedraw 的操作不再重复 Redraw。125%/150% 缩放下 RenderScaling、Overlay 与 native viewport input 坐标必须保持一致。

## 应用首选项

长期用户视图/交互偏好属于 Application State，不属于 Document State。`管理 → 首选项...` 统一编辑并持久化场景背景、Grip/Snap Marker 大小、Grip/Snap/Selection 像素容差、鼠标滚轮缩放灵敏度和 Viewer deviation/angle。

设置修改后立即应用到当前 Viewport。“场景显示精度”只表示 Presentation 离散质量，不能解释为修改 CAD 模型数学精度。
