# OCCAD

OCCAD 是基于 **OcctCSharpBridge / OCCT** 构建的 Avalonia 桌面 CAD 应用与可扩展 CAD 框架。

[English](README.md)

## 项目定位

OCCAD 不是 OCCT API Demo，而是完整 CAD 产品层：领域模型、交互 Tool 框架、Avalonia 桌面壳、持久化、文档以及构建/运行/发布入口。

当前架构覆盖 Document/Entity/Layer、Selection/Preselection/Subobject、Grip、WorkPlane/Snap/Tracking/Precision、Preview/History/Action/Tool、二维/三维 Entity、Modify/Modeling、Annotation、Measurement、Persistence，以及 Avalonia 原生 Model/Layer/Property/Tool 面板和 Command Line。

## 目录结构

- `src/OCCAD.Core`：CAD Domain、Document、Entity、Layer、History、Action/Tool、Selection、Snap、Grip、Precision、Geometry、Persistence。
- `src/OCCAD.Avalonia`：Avalonia Shell、菜单/工具栏/状态栏、OCCT Viewport、面板、命令行、本地化、Dialog。
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

界面统一为紧凑工业 CAD 风格：克制灰色工具区、深色 Viewport、统一 11 px 字体、22 px 紧凑控件、低/无圆角、左侧 Model、右侧可调整 Layer/Property 双面板、Command Line 位于 StatusBar 上方、非模态 ToolPanel。

**Command Line 是唯一显示完整 Tool Prompt 的位置。** StatusBar 只显示应用/Tool 状态、Selection、History、Snap、Precision、WorkPlane 和坐标，不重复完整绘图提示。

## 属性与图层语义

普通 Entity 外观属性统一为 Layer、Color、LineStyle、LineWidth、Transparency、Visible。Color/LineStyle/LineWidth 在同一行内置 `随层(ByLayer)`。

Viewer handle、Id、内部 Selectable、Material/DisplayMode 实现细节、导入 BREP 字节数等不再作为普通 Property 行暴露。

直接修改 Color/LineStyle/LineWidth 时自动关闭对应 ByLayer；重新启用 ByLayer 时保留实体 override，只切换有效外观来源。

Entity 和 Layer 修改统一走 Core Transaction / History，保证 UI 修改、显示刷新、Undo/Redo 和失败回滚是一条链。

## 核心约束

- Document state 与 Entity geometry 是权威数据，Viewer object 是派生显示。
- Avalonia 只观察和调用 Core。
- Tool 是显式分阶段状态机。
- Preview 不进入 Document / Selection / History。
- 真实模型和 History 成功前保留最后有效 Preview。
- Grip PointerMove 只编辑 Duplicate Preview。
- Snap 负责候选解析，Snap/Grip 语义属于 Entity。
- Layer/Property Controller 不自行实现平行业务事务。
- 禁止反射调度、重复 public API、migration/compatibility 层、过度 smoke/check、GitHub Actions，以及 `Advanced`、`Extended`、`V1`、`V2` 等人为后缀。

完整规范见 [docs/README.md](docs/README.md)。
