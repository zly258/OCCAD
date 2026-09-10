# 07 代码组织

## 职责

src/OCCAD.Core 包含 Actions、Document、Entities、Geometry、Grips、History、Interaction、Layers、Properties、Selection、Snapping、WorkPlane。

src/OCCAD.Avalonia 包含本地化、Program、Application/Theme、MainWindow partial、Viewport interaction、ToolPanel、Layer/Property panel、Command Line 和少量 UI helper。

Avalonia 只负责 UI/适配。Core 不引用 Avalonia。Entity 和 Tool 不直接操作控件。显式 Registry 继续作为扩展机制。

## 命名与文件

原则上一个主要 Entity 或 Tool 一个文件，共享几何算法进入职责明确的 helper。不要为了绕开正确契约而创建 Advanced、Extended、Extension、Compat、V1、V2 等平行 API。

小 helper 文件必须有独立职责；只有一个方法且没有复用价值的 helper 应并回所属对象。

## 状态所有权

Document 拥有持久 Entity。Workspace 组合会话服务。ToolManager 管理活动 Tool 生命周期。Selection、Subobject、Grip、Snap、Preview 各自只有一个 Core owner。MainWindow 和 Controller 只观察并调用这些服务，不复制业务状态。

## 仓库卫生

根脚本只保留 build.ps1、run.ps1、publish.ps1。日常 build 不重建、不 sync Bridge，也不运行大规模 migration/check/smoke 框架。不要求 GitHub Actions。

仓库不保留提交流水账、本地验证日志、过期 UI 迁移文档、旧 gap analysis 和生成物。文档只描述稳定契约，不描述某个阶段临时进度。
