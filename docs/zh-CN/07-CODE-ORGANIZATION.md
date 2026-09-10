# 07 代码组织

## Projects
`src/OCCAD.Core` 包含 Actions、Document、Entities、Geometry、Grips、History、Interaction、Layers、Properties、Selection、Snapping、WorkPlane/Drafting/Precision、Persistence。

`src/OCCAD.Avalonia` 只包含 Presentation/Adapter：Program/Application/Theme、MainWindow partial、Viewport interaction、ToolPanel、Layer/Property Controller、Command Line、Localization、Dialog、UI helper。

## 依赖
Avalonia 可以引用 Core，Core 不引用 Avalonia。MainWindow/Controller 不复制 Geometry 算法、Transaction 语义、Layer/Property 业务逻辑。

## 命名
原则上一个主要 Entity/Tool 一个文件。共享 helper 只有在有真实复用职责时才存在。禁止 Advanced、Extended、Extension、Compat、Legacy、V1、V2 平行 API。

## 状态所有权
Document 拥有持久 Entity；Workspace 组合 Session Service；LayerManager 管 Layer；ToolManager 管 Active Tool；Selection/Subobject/Preselection 各自拥有状态；PreviewManager 管 transient preview；GripManager 管 Grip；SnapManager 管 Snap；CadTheme 管公共 Avalonia 视觉指标；Localization resource 管 UI 文案。

## 修改职责
Entity Property 走 `CadPropertyTransaction`；Layer Color/Style/Width/Visible/Locked 走 `CadWorkspace`；Tool Commit 走 Core Document/History。UI 可校验输入格式，但权威验证和回滚在 Core。

## 仓库卫生
根构建入口保持 `build.ps1`、`run.ps1`、`publish.ps1`。日常 Build 不 Build/Sync Bridge，不默认跑大型 smoke/check。除非策略改变，不加 GitHub Actions。不提交 build output、本地日志、临时截图、migration script、动态 icon 生成链和一次性 validation artifact。文档只描述长期契约。
