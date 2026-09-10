# 11 代码组织与项目清理规范

## 1. 原则

项目结构要让职责从目录和文件名就能看懂。不要通过 `Advanced`、`Extended`、`V2`、`Compat` 等后缀掩盖重复实现；不要为过渡代码长期保留第二套入口。

## 2. Core 目录职责

建议长期保持：

```text
OCCAD.Core/
  Actions/
  Document/
  Entities/
    TwoD/
    ThreeD/
  Grips/
  History/
  Interaction/
  Layers/
  Selection/
  Serialization/
  Snapping/
  Tools/
    TwoD/
    ThreeD/
    Modify/
```

一个基础 Entity 一个文件，一个主要 Tool 一个文件。公共逻辑进入真正的基类/服务，不复制到多个 Tool。

## 3. WPF 目录职责

WPF 层只负责界面与适配：

```text
OCCAD.Wpf/
  Localization/
  MainWindow.xaml
  MainWindow.*.cs
  CadToolPanel.cs
  Property/
  Converters/
  Dialogs/
```

`MainWindow` partial 仅按真实职责拆分，例如 Document、Layer、Selection、Viewport、Dock；不要出现 `UiFix`、`Refinement`、`Patch` 等长期文件。

## 4. 删除旧路径

当新实现已经成为唯一入口时，必须删除旧控件、旧 handler、旧 style、旧 converter、隐藏兼容 Host 和无用资源。典型待清理项包括历史 Ribbon 命名、隐藏 ToolParameterHost、无用 FactorInput、旧 Layer column installer、重复颜色 palette、runtime visual-tree patch。

## 5. 第三方 UI

核心界面使用原生 WPF/WinForms 必要互操作，不再引入 Ribbon/主题类第三方 UI 包。几何能力依赖 OcctCSharpBridge/OcctNet；UI 库不得反向进入 Core。

## 6. 注册

Entity、Tool、Action 都通过显式 registry 统一注册。新增类型不应要求在 MainWindow 写大型 switch。Action ID、Tool ID、EntityType 稳定、唯一、可序列化。

## 7. 依赖方向

```text
WPF → Core → OcctNet
```

Core 不引用 WPF。Entity 不引用 MainWindow。Tool 不直接操作 WPF 控件。UI 通过 Workspace/Manager event 观察状态。

## 8. 事件与生命周期

订阅必须有明确解除位置。Window/Tool/Document Dispose 或 Close 时解除长期事件，避免 MainWindow/Workspace 被静态事件持有。

Viewer object、Preview、Grip、Snap overlay 都有拥有者；禁止“谁创建都可以、谁看到都删除”的共享生命周期。

## 9. 构建脚本

根入口保持简单：`build.ps1` 默认只编译；`run.ps1` 运行；`publish.ps1` 发布；需要 Bridge SDK 更新时使用 `build.ps1 -SyncBridge`。不恢复过度的 smoke/check/test PowerShell 体系。

## 10. 提交

一个开发阶段形成一个 coherent commit，避免一文件一提交。临时修补提交在阶段结束前 squash。提交说明使用简洁英文，描述真实变更，不带无意义前缀。

## 11. 文档

`docs/zh-CN` 与 `en-US` 是唯一 CAD 规范。旧的重复架构/交互/路线文档删除，不保留两套“都可能正确”的说明。代码行为变更若修改了核心契约，同一阶段更新对应文档。
