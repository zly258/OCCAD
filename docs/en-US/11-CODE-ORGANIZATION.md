# 11 Code Organization and Cleanup

Project structure exposes responsibilities directly. Do not retain parallel UI entry paths or suffix duplicates such as Advanced, Extended, V2 or Compat.

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
    Dialogs / helpers
```

Avalonia owns UI/adaptation only. Core does not reference Avalonia. Entity and Tool do not manipulate controls. Explicit registries remain the extension mechanism.

After the Avalonia migration, the WPF/WinForms UI path is removed rather than retained as compatibility code. The official Bridge `OcctNet.Avalonia` host is used directly.

Long-lived event subscriptions have explicit release paths. Transient viewer overlays have single owners. Root scripts remain `build.ps1`, `run.ps1`, and `publish.ps1`; normal builds do not resync Bridge or run migration/check frameworks.
