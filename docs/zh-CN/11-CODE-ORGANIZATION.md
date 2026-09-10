# 11 代码组织与项目清理规范

项目结构直接表达职责，不长期保留双 UI 入口，也不使用 Advanced、Extended、V2、Compat 等后缀掩盖重复实现。

```text
src/
  OCCAD.Core/
    Actions/
    Document/
    Entities/
    Grips/
    History/
    Interaction/
    Layers/
    Selection/
    Snapping/
  OCCAD.Avalonia/
    Localization/
    Program.cs
    CadApplication.cs
    CadTheme.cs
    MainWindow.cs
    MainWindow.Menu.cs
    MainWindow.State.cs
    MainWindow.Files.cs
    CadViewportInteractionController.cs
    CadToolPanel.cs
    CadLayerPanelController.cs
    CadPropertyInspectorController.cs
    CadCommandLineController.cs
    Dialog / helper
```

Avalonia 只负责 UI/适配。Core 不引用 Avalonia，Entity/Tool 不直接操作控件，扩展继续依赖稳定 Entity/Tool/Action Registry。

Avalonia 迁移完成后删除 WPF/WinForms UI 路径，不保留兼容壳。Viewport 直接使用 Bridge 官方 `OcctNet.Avalonia` Host。

长期事件订阅必须有解除位置；transient viewer overlay 各自拥有唯一 owner。根脚本只保留 `build.ps1`、`run.ps1`、`publish.ps1`；日常 build 不 sync Bridge，不恢复 migration/check 脚本体系。
