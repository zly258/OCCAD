# OCCAD

OCCAD 是基于 OcctCSharpBridge 构建的 Avalonia CAD 应用与可扩展 CAD 框架。

[English](README.md)

## 定位

本仓库只维护 OCCAD 产品本身，包括 CAD Core、Avalonia 桌面应用、文档和产品构建入口。OCCAD 直接消费系统安装的 OcctCSharpBridge Binary SDK；默认路径为 `C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`，可通过 `OCCTCSHARPBRIDGE_SDK` 覆盖。Bridge wrapper 源码、Demo 和 Tests 继续由 OcctCSharpBridge 仓库维护。

当前范围包括 Document/Entity、Registry、Layer、Selection/Preselection/Subobject、Grip、Work Plane、Object Snap、Tracking、Precision Input、Preview、History、Action/Tool、2D/3D Entity、Modify/Modeling、Annotation、Measurement、Group/Block、文档持久化以及 Avalonia Shell。

## 目录结构

- `src/OCCAD.Core`：Document、Entity、Action/Tool、Selection、Precision、History、Persistence 等核心框架。
- `src/OCCAD.Avalonia`：Avalonia Shell、菜单/工具栏、OCCT Viewport、Dynamic Input、原生 Layer/Property 面板和 Command Line。
- `docs/README.md`：文档入口。
- `docs/en-US` / `docs/zh-CN`：设计与开发基线。
- `OCCAD.sln`：OCCAD solution。

## 环境要求

- 当前发布目标 Windows x64
- `global.json` 指定的 .NET SDK
- NuGet 还原 Avalonia 12.1.0
- 已安装且包含 `OcctNet.Avalonia.dll` 的 OcctCSharpBridge SDK
- 匹配的 OCCT runtime

## 编译与运行

Bridge SDK 更新后，OCCAD 日常只需要：

```powershell
.\build.ps1
.\run.ps1 -OcctRoot "D:\tools\occt-vc144-64"
```

发布：

```powershell
.\publish.ps1 -OcctRoot "D:\tools\occt-vc144-64"
```

不再 clone、build 或 sync Bridge。

## 设计约束

Entity geometry 和 Document state 是权威数据，Viewer object 只作为派生显示状态。Tool 是显式交互状态机；Preview 与 Commit 使用同一个 resolved-point contract。Avalonia 仅负责呈现和适配，不维护第二份业务状态。

禁止反射调用、重复 public API、migration/compatibility 层、过度 smoke/check、GitHub Actions；不使用 `Advanced`、`Extended`、`V1`、`V2` 等人为后缀。
