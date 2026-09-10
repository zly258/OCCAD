# 04 系统架构

## 1. 总体分层

```text
Presentation (Avalonia)
  MainWindow / Menu / ToolBar / Dock / ToolPanel / CommandLine / StatusBar
        ↓ observes + invokes
Application / Interaction
  Workspace / ActionManager / ToolManager / Selection / Snap / Tracking / Grip / Preview
        ↓
Domain
  Document / Entity / Layer / History / Registries / Serialization
        ↓
Geometry & Presentation Bridge
  OcctNet / OcctNet.Avalonia / OcctCSharpBridge / OCCT
```

依赖方向固定为 `Avalonia → Core → OcctNet`。Core 不引用 Avalonia；Entity 不引用 MainWindow；Tool 不直接操作 UI 控件。

`CadWorkspace` 继续作为会话组合根。Document 是持久 Entity 唯一所有者和最终外观解析器；Entity 负责几何、身份、外观、Transform、Snap/Grip 语义和持久化。Entity/Tool/Action 使用显式 Registry 和稳定 ID。

Action 是瞬时命令入口，Tool 是分阶段交互状态机。稳定 Tool 参数由 Core Descriptor 描述，统一由 Avalonia `CadToolPanel` 呈现；Core 不知道具体 Window。

Selection、Preselection、SubobjectSelection 相互独立。点解析继续统一为 screen/ray → WorkPlane → Snap → tracking/constraint → final point；用户持久工作平面、Tool 临时平面和 Grip 平面分离。

Preview、Grip、Snap、Preselection、Subobject overlay 分别管理 transient 生命周期。GripEdit 在 Duplicate Preview 上编辑，Accept 后只产生一次真实状态修改和 History。

Layer/Property 是 Core 状态的 Avalonia 视图。Property 语义由 Core 的 `CadPropertyDescriptor`/`CadValueDescriptor` 提供，`TypeDescriptor` 只作为 CLR 属性适配层，不是 UI 业务模型。所有修改路径遵循 Capture → Validate/Build → Apply → History，失败回滚。

Avalonia Viewport 直接消费安装 SDK 提供的官方 `OcctAvaloniaViewport`；OCCAD 不再自建第二层 HWND/NativeControlHost 包装。
