# 11 Code Organization and Cleanup

Project structure should expose responsibilities directly. Do not hide duplicate implementations behind suffixes such as Advanced, Extended, V2, or Compat, and do not retain two long-lived entry paths after a replacement is complete.

Core keeps clear areas for Actions, Document, Entities/TwoD, Entities/ThreeD, Grips, History, Interaction, Layers, Selection, Serialization, Snapping, and Tools/TwoD/ThreeD/Modify. A basic Entity normally owns one file and a primary Tool one file. Shared behavior belongs in real base classes/services, not copied across Tools.

WPF owns UI and adaptation only: Localization, MainWindow XAML and responsibility-based partials, CadToolPanel, Property helpers, Converters, and Dialogs. Long-lived partial names such as UiFix, Refinement, Patch, or migration helpers are not acceptable architecture.

When a new path becomes authoritative, remove obsolete controls, handlers, styles, converters, hidden compatibility hosts, and resources. Current cleanup targets include historical Ribbon naming, hidden ToolParameterHost/FactorInput, old layer-column installers, duplicate color palettes, and runtime visual-tree patches.

The dependency direction is `WPF → Core → OcctNet`. Core never references WPF; Entity never references MainWindow; Tool never manipulates WPF controls directly. UI observes Workspace/manager events.

Entity, Tool, and Action use explicit registries with stable unique IDs. Adding a type must not require large MainWindow switches.

Long-lived event subscriptions have explicit unsubscription. Viewer overlays have clear owners and separate lifecycles.

Root scripts remain simple: build.ps1 compiles by default; run.ps1 runs; publish.ps1 publishes; Bridge refresh uses build.ps1 -SyncBridge. Do not restore excessive smoke/check PowerShell frameworks.

One development stage should end as one coherent commit. Temporary file-by-file commits are squashed. `docs/zh-CN` and `en-US` are the only CAD design baseline; duplicate historical architecture/interaction/roadmap files are removed.
