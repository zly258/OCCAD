# 02 UI 规范

## 总体方向
OCCAD 使用紧凑工业 CAD 桌面壳：Menu + 紧凑 ToolBar + 左侧 Model + 中央 Viewport + 右侧可调整 Layer/Property + Command Line + StatusBar + 非模态 ToolPanel。Viewport 始终占最大区域。

## Theme
`CadTheme` 是桌面 UI 视觉指标唯一来源。当前基线：主 UI 11 px；次要文字 10/10.5 px；紧凑控件 22 px；Header 约 23–24 px；浅灰工具/面板区域；深色 Viewport；蓝色仅用于 Current/Active；低/无圆角；细分隔线；Segoe UI / Microsoft YaHei UI / CJK fallback 字体栈。

## Menu / ToolBar / ToolPanel
Menu 只调用已注册 Action ID。真正命令族才使用一层二级菜单。ToolBar 只放全局/当前绘图状态；Radius/Width/Height 等稳定 Tool 参数进入 ToolPanel。ToolPanel 不重复完整 Prompt。

## Model / Layer / Property
Model 位于左侧。Layer 与 Property 是右侧两个独立、可调整高度的面板，不是 Tab。Layer UI 只调用 `CadWorkspace`；事务、回滚、History 在 Core。

Property 多选只显示共同属性。外观行统一为：
```text
颜色      [随层] [颜色值]
线型      [随层] [线型]
线宽      [随层] [数值]
透明度
可见
```
三个 ByLayer bool 保留在 Core，但不单独占行。

## Prompt
Command Line 唯一显示完整 Tool Prompt；ToolPanel 显示参数/精确输入/WorkPlane/Accept/Finish/Cancel；StatusBar 显示应用/Tool 状态、Selection、History、Snap、Precision、WorkPlane、坐标，不重复下一步说明。

## Viewport / DPI
唯一 OCCT Host 是 `OcctAvaloniaViewport`。Drawing 使用中心留空十字。Bridge 已 requestRedraw 的操作不再重复 Redraw。125%/150% 缩放下 RenderScaling 与 native viewport input 坐标保持一致。
