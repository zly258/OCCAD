# 02 界面规范

## 1. 最终界面基线

OCCAD 使用 Avalonia 12 原生紧凑型 CAD 桌面界面：

```text
Menu：文件 编辑 绘图 修改 建模 标注 视图 面板 设置
ToolBar：工作平面 | 当前层 | 捕捉 | 正交 | 极轴 | 完成 | 取消
Model Dock | Viewport | Layer / Property Tabs
Command Line
StatusBar：Prompt | Selection | History | Snap | Precision | Work Plane | Coordinate
+ 非模态 ToolPanel
```

Viewport 始终是最大区域。界面使用 Fluent Compact 密度和克制的工业软件配色，不再使用 Ribbon、图标堆叠或第三方壳层/主题框架。

## 2. Menu

菜单只调用已经注册的 Action ID。Circle、Arc、Ellipse、Boolean、Array 等真正的命令族允许一层二级菜单；未注册 Action 不显示为可用命令。对象捕捉设置只暴露已经完成运行时契约的类型。

## 3. ToolBar / ToolPanel

ToolBar 只放全局和当前阶段状态。实体/工具专属参数统一进入 Avalonia 原生 `CadToolPanel`。标题栏 × 只隐藏面板，不取消 Tool；启动新 Tool 时重新显示。参数修改实时驱动 Preview；Finish/Cancel 走统一 Tool 生命周期。

## 4. Dock / Layer / Property

Model 默认左侧。Layer 与 Property 合并为右侧紧凑 Tab，不再固定上下堆叠占用视口。

Layer 使用 Avalonia 原生控件编辑当前层、颜色、可见、锁定、线宽和线型。Property 消费 Core 的 `CadPropertyDescriptor`/`CadValueDescriptor` 元数据，支持单选/多选共同属性、Layer/Enum/Bool/Color、简洁数值显示、History 和失败回滚；不再嵌 WinForms PropertyGrid。

## 5. Viewport / HUD / Cursor

唯一 OCCT Host 是 `OcctAvaloniaViewport`。Drawing 使用中心留空的自定义十字光标；中键导航使用导航光标；普通/选择阶段使用正常指针。Snap Aperture 和 Dynamic HUD 根据 Avalonia `RenderScaling` 换算，125%/150% DPI 下与 native viewport input 保持对齐。

S/F/T 只切工作平面，不切相机。右键执行 CAD secondary action。F3/F8/F10 对应 Snap/Ortho/Polar。Tab 轮换捕捉候选。ViewCube 隐藏，保留左下角 triedron。

## 6. Localization

持久 UI 文本统一来自中英文资源。Tool Prompt、Command Result、Action、History、Layer/Property 和面板标题在语言切换后即时刷新。
