# 04 Architecture

## Layers

```text
Presentation (Avalonia)
  MainWindow / Menu / ToolBar / Docks / ToolPanel / CommandLine / StatusBar
        ↓ observes + invokes
Application / Interaction
  Workspace / ActionManager / ToolManager / Selection / Snap / Tracking / Grip / Preview
        ↓
Domain
  Document / Entity / Layer / History / Registries / Serialization
        ↓
Geometry & Presentation Bridge
  OcctNet / OcctNet.Avalonia / OcctCSharpBridge / OCCT
```

Dependency direction is `Avalonia → Core → OcctNet`. Core never references Avalonia; Entity never references MainWindow; Tool never manipulates UI controls directly.

`CadWorkspace` remains the session composition root. Document is the sole persistent Entity owner and final appearance resolver. Entity owns geometry, identity, appearance, transforms, Snap/Grip semantics and persistence. Entity/Tool/Action use explicit registries with stable IDs.

Action is the instantaneous command entry. Tool is the staged interaction state machine. Stable Tool parameters are described by Core descriptors and rendered by the single Avalonia `CadToolPanel`; Core knows nothing about the concrete Window.

Selection, Preselection and SubobjectSelection remain separate. Point resolution remains screen/ray → WorkPlane → Snap → tracking/constraints → final point. Persistent drawing plane, transient Tool plane and Grip plane stay distinct.

Preview, Grip, Snap, Preselection and subobject overlays have separate lifecycles. GripEdit works on a duplicate preview and creates one real state/history change on Accept.

Layer and Property panels are Avalonia views over Core state. Property semantics come from Core `CadPropertyDescriptor`/`CadValueDescriptor`; `TypeDescriptor` is only the Core adapter for CLR properties, not the UI business model. All modifying paths follow Capture → Validate/Build → Apply → History with rollback on failure.

The Avalonia viewport adapter consumes the official `OcctAvaloniaViewport` from the installed Bridge SDK; OCCAD does not create a second HWND/NativeControlHost wrapper.
