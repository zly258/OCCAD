# 08 界面细节规范

## 1. 唯一界面目标

OCCAD 采用原生 WPF 经典桌面 CAD 界面，不再恢复 Ribbon：

```text
Menu
ToolBar
────────────────────────────
Model Dock | Viewport | Layer Dock
                     | Property Dock
────────────────────────────
StatusBar
        + 非模态 ToolPanel
```

所有界面必须从这个基线继续，不允许同时维护 Classic Menu 与 Ribbon 两套实现。

## 2. Menu

顶级菜单固定为：文件、编辑、绘图、修改、视图、面板、设置。常用命令应直接位于顶级菜单下一层；只有真正的同族变体允许再有一层，例如临时捕捉类型。禁止三级以上命令路径。

菜单项文字必须完整本地化；快捷键显示与实际键位一致。菜单只启动 Action，不直接执行 Tool 内部逻辑。

## 3. ToolBar

ToolBar 常驻且高度稳定，不随 Tool 激活/结束跳动。推荐顺序：

`S F T | 当前层 | L [锁] | A [锁] | Finish | Cancel`

没有 Active Tool 时 L/A/Finish/Cancel 禁用但 ToolBar 不消失。S/F/T 始终表示工作平面，不表示相机视图。

Radius、Width、Height 等 Tool 专属参数不放 ToolBar。

## 4. ToolPanel

`CadToolPanel` 是唯一浮动面板基类。Panel 必须：非模态、不抢 Viewport 焦点、可拖动、关闭仅隐藏、Tool 结束自动隐藏、语言切换即时刷新、同一 Tool 更新不重建整个 Window。

Panel 行布局统一：左标签 110~120 DIP，右编辑器约 140~170 DIP；行高跟随系统控件；数字默认显示 3 位小数。需要的 Tool 才增加参数：Circle Radius/Diameter、Rectangle Width/Height、RegularPolygon Sides/Radius、Box Width/Depth/Height、Cylinder Radius/Height、Cone Radius/Height、Frustum BottomRadius/TopRadius/Height、Sphere Radius、Torus MajorRadius/MinorRadius。

## 5. Dock

左侧 Model，右上 Layer，右下 Property。Viewport 永远优先获得可用面积。Dock 标题栏使用普通背景、轻量文字和透明无边框 ×；Hover 只做轻微高亮。

Dock 切换只改变显示，不销毁业务对象。Dock 尺寸保存属于 UI 状态，不进入 Document。

## 6. Layer UI

列固定语义：名称、颜色、可见、锁定、线宽、线型。颜色按钮高度随行高，点击直接 ColorDialog。线宽和线型为单击可操作下拉，不要求先双击进入 DataGrid 编辑模式。

编辑非当前层的属性不得自动把该层设为 Current Layer。设为当前层必须是明确的名称点击/命令行为。

Layer 刷新不得通过 `ItemsSource=null` 重建整表；应使用稳定集合和局部更新。

## 7. Property UI

单选显示完整属性，多选显示共同属性。ColorByLayer、LineWidthByLayer、LineStyleByLayer 是独立状态；自定义颜色并不删除 ByLayer 能力。

Color 点击直接 ColorDialog。枚举使用本地化显示映射。double 显示 3 位小数，输入可接受更多位，内部值不 round。

## 8. Viewport

黑色背景；左下小型 trihedron；右上 ViewCube，不显示重复外部 XYZ 轴。Preview、Snap、Grip、Selection overlay 的颜色要在黑色背景上清晰但不过亮。

Viewport 不承担 Layer、Tool、History 业务规则。

## 9. StatusBar

只保留 Prompt/错误/结果、Snap、ORTHO、POLAR、Coordinate、后台进度。禁止把 Tool 稳定参数、长教程、重复工作平面控件塞入 StatusBar。

## 10. 尺寸与 DPI

不逐控件指定 FontFamily/FontSize。使用系统字体和原生控件度量。固定尺寸仅用于必要的工具按钮、输入框和 Dock 初始宽度；不得以 100% DPI 的截图像素作为布局依据。
