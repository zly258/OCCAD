# OCCAD

OCCAD 是基于 OcctCSharpBridge 构建的 WPF CAD 应用与可扩展 CAD 框架。

[English](README.md)

## 定位

本仓库只维护 OCCAD 产品本身，包括 CAD Core、WPF 应用、文档和产品构建入口。OCCAD 通过 `external/OcctCSharpBridge/win-x64` 消费 OcctCSharpBridge binary SDK；Bridge wrapper 源码、Bridge demo 和 Bridge tests 继续由 OcctCSharpBridge 仓库维护。

当前框架范围包括 Document/Entity 所有权、稳定 Entity Registry、Layer、Selection/Preselection、持久化子对象选择、Grip、Work Plane、Object Snap、Tracking、Precision Input、Preview、History、Action/Tool、基础 2D/3D Entity、基础 Modify、Group 所有权、Block Definition/Reference、文档持久化以及 WPF Shell。

## 目录结构

- `src/OCCAD.Core`：Document、Entity、Action/Tool、Selection、Precision、History、Persistence 等核心框架。
- `src/OCCAD.Wpf`：WPF Shell、菜单/工具栏、Viewport 集成、Dynamic Input 和 Property UI。
- `docs/README.md`：文档入口。
- `docs/en-US`：英文设计与开发基线。
- `docs/zh-CN`：中文设计与开发基线。
- `OCCAD.sln`：OCCAD solution。

## 环境要求

- Windows x64
- `global.json` 指定的 .NET SDK
- 与当前 OcctCSharpBridge binary SDK 匹配的 OCCT runtime

## 编译与运行

仅在本地 binary SDK 缺失或 Bridge contract 发生变化时刷新一次：

```powershell
.\build.ps1 -SyncBridge -BridgeBranch main -OcctRoot "D:\tools\occt-vc144-64"
```

日常编译：

```powershell
.\build.ps1
```

运行：

```powershell
.\run.ps1 -OcctRoot "D:\tools\occt-vc144-64"
```

发布：

```powershell
.\publish.ps1 -OcctRoot "D:\tools\occt-vc144-64"
```

日常 `build.ps1` 不重新构建或同步 OcctCSharpBridge。

## 设计约束

Entity geometry 和 Document state 是权威数据，Viewer object 只作为派生显示状态。Tool 是显式交互状态机；Preview 与 Commit 必须使用同一个 resolved-point contract。新增 Entity/Tool 原则上按一个具体类型一个文件组织，并使用稳定 ID、Registry 和明确 ownership。

禁止反射调用、重复 public API、migration/compatibility 层、smoke/check 框架和 GitHub Actions；不使用 `Advanced`、`Extended`、`V1`、`V2` 等人为后缀。
