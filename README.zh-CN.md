# OCCAD

OCCAD 是基于 **OcctCSharpBridge / OCCT** 构建的 Avalonia 桌面 CAD 应用与可扩展 CAD 框架。

[English](README.md)

## 项目定位

OCCAD 不是 OCCT API Demo，而是完整 CAD 产品层：领域模型、交互 Tool 框架、Avalonia 桌面壳、持久化、文档以及构建/运行/发布入口。

当前架构覆盖 Document/Entity/Layer、Selection/Preselection/Subobject、Grip、WorkPlane/Snap/Tracking/Precision、Preview/History/Action/Tool、二维/三维 Entity、Modify/Modeling、Annotation、Measurement、Persistence，以及 Avalonia 原生 Ribbon、Model/Layer/Property、Floating Tool Panel、Command Line 和 Status Bar。

## 目录结构

- `src/OCCAD.Core`：CAD Domain、Document、Entity、Layer、History、Action/Tool、Selection、Snap、Grip、Precision、Geometry、Persistence。
- `src/OCCAD.Avalonia`：Avalonia Ribbon-first Shell、OCCT Viewport、Model/Layer/Property/Floating Tool Panel、Command Line、Status Bar、本地化、Dialog。
- `docs/en-US` / `docs/zh-CN`：同步维护的长期设计与开发契约。
- `build.ps1`、`run.ps1`、`publish.ps1`：构建、运行、发布入口。
- `OCCAD.sln`：`OCCAD.Core` + `OCCAD.Avalonia`。

## 环境要求

- Windows x64
- `global.json` 指定的 .NET SDK
- NuGet 还原 Avalonia
- 已安装 OcctCSharpBridge SDK，默认 `C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`
- Bridge SDK 不含 portable runtime 时需要匹配的 OCCT runtime

可使用 `OCCTCSHARPBRIDGE_SDK` 覆盖 SDK 路径。

## 编译与运行

```powershell
cd D:\workspace\occt\OCCAD
git pull
.\build.ps1
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

Bridge SDK 自带 portable runtime 时 `run.ps1` 自动使用；否则传入 `-OcctRoot`，或设置 `OCCT_ROOT` / `CASROOT`。

```powershell
.\publish.ps1 -OcctRoot D:\tools\occt-vc144-64
```

OCCAD 日常构建不再 clone、build 或 sync OcctCSharpBridge。

## UI 基线

界面统一为紧凑工业 CAD 风格：顶部原生 Avalonia 自定义 Ribbon、克制灰色工具区、深色 Viewport、统一 11 px 字体、22 px 紧凑控件、低/无圆角、左侧 Model、右侧可调整 Layer/Property 双面板、Command Line 位于 Status Bar 上方、Viewport 内非模态 Floating Tool Panel。

Ribbon 只调用 Core 已注册 Action ID；Circle、Arc、正多边形、Ellipse、3D 基本实体使用一层下拉，不复制 Tool 业务逻辑。横向空间不足时 Ribbon 内滚动，避免 125%/150% DPI 下撑宽主窗口。

**Command Line 是唯一显示完整 Tool Prompt 的位置。** Floating Tool Panel 只显示当前 Step、精确坐标、Length/Angle/Factor、Tool 参数以及 Back/Accept/Finish/Cancel；Dynamic HUD 显示鼠标附近的精确/捕捉/追踪反馈；Status Bar 只保留 Selection、WorkPlane、SNAP/ORTHO/POLAR 和 XYZ 坐标。

## 属性与图层语义

普通 Entity 外观属性统一为 Layer、Color、LineStyle、LineWidth、Transparency、Visible。Color/LineStyle/LineWidth 在同一行内置 `随层(ByLayer)`。

PropertyGrid 使用 Core descriptor/editor 语义，支持分类、多选共同属性、Mixed Value、Numeric、Enum、Color、Layer/ByLayer，以及 Point/Vector X/Y/Z 分量编辑。

Layer 表格字段为 `当前 | 名称 | 显 | 色 | 线型 | 线宽 | 锁`。当前图层使用明确的 `●/○` 状态；只有“当前”列切换当前层，点击名称只检查图层。默认层不能重命名或删除。

Viewer handle、Id、内部 Selectable、Material/DisplayMode 实现细节、导入 BREP 字节数等不再作为普通 Property 行暴露。

直接修改 Color/LineStyle/LineWidth 时自动关闭对应 ByLayer；重新启用 ByLayer 时保留实体 override，只切换有效外观来源。

Entity 和 Layer 修改统一走 Core Transaction / History，保证 UI 修改、显示刷新、Undo/Redo 和失败回滚是一条链。

## 核心约束

- Document state 与 Entity geometry 是权威数据，Viewer object 是派生显示。
- Avalonia 只观察和调用 Core。
- Tool 是显式分阶段状态机。
- Ribbon、Command Line、Floating Tool Panel 共享同一 Action/Command/Tool 状态，不建立平行命令系统。
- `CadCommandManager.ForWorkspace()` 是每个 Workspace 唯一命令会话入口。
- Preview 不进入 Document / Selection / History。
- 真实模型和 History 成功前保留最后有效 Preview。
- Grip PointerMove 只编辑 Duplicate Preview。
- Snap 负责候选解析，Snap/Grip 语义属于 Entity。
- Layer/Property Controller 不自行实现平行业务事务。
- 禁止反射调度、重复 public API、migration/compatibility 层、过度 smoke/check、GitHub Actions，以及 `Advanced`、`Extended`、`V1`、`V2` 等人为后缀。

完整规范见 [docs/README.md](docs/README.md)。

## 应用首选项

`管理 → 首选项...` 保存应用级视图与交互偏好，配置文件位于 `%LOCALAPPDATA%\OCCAD\settings.json`。

当前可设置：场景背景、夹点大小、夹点命中容差、捕捉点大小、捕捉容差、选择容差、鼠标滚轮缩放灵敏度，以及 OCCT 场景显示离散精度。

其中“显示精度”只控制 OCCT Presentation 的 deviation/angle，不改变 BRep、曲线、曲面、尺寸或保存后的 CAD 几何数学精度。
