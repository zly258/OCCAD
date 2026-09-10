# 04 Architecture

## Layers

```text
Presentation — OCCAD.Avalonia
  MainWindow / Menu / Toolbar / Panels / Dialogs / CommandLine / StatusBar
        ↓
Application & Interaction — OCCAD.Core
  Workspace / ActionManager / ToolManager / Selection / Snap / Tracking / Grip / Preview
        ↓
Domain — OCCAD.Core
  Document / Entity / Layer / History / Registry / Persistence
        ↓
Geometry & Viewer Bridge
  OcctNet / OcctNet.Avalonia / OcctCSharpBridge / OCCT
```

Dependency direction is `Avalonia → Core → OcctNet`.

## Ownership

`CadWorkspace` is the session composition root. Document is the sole persistent Entity owner and final appearance resolver.

Entity owns stable identity/type, valid geometry, layer, appearance override values + ByLayer flags, placement, Snap/Grip semantics, duplicate/restore/transform behavior, and persistence state.

Viewer objects are derived presentation only.

## Appearance

Document resolves effective Color, LineStyle, LineWidth, visibility, and selectability from Entity + Layer state.

## Property / Layer transaction

`CadPropertyCatalog` / `CadPropertyDescriptor` / `CadValueDescriptor` define semantic property metadata. `TypeDescriptor` is only a CLR adapter.

`CadPropertyTransaction` applies Entity edits atomically and handles explicit appearance override semantics. Layer mutations are centralized on `CadWorkspace`; Avalonia does not implement a second rollback/history path.

## Tool / Action

Action is the stable instantaneous command entry. Tool owns Step/Stage, Prompt, InputKind, InteractionPolicy, precision reference/input, WorkPlane strategy, parameters, Preview, Finish/Cancel/StepBack.

`CadToolContext` exposes Core services but does not hide Preview cleanup inside model mutation methods.

## Transient state / redraw

Selection, Preselection, SubobjectSelection, Grip, Snap, Tracking, Preview, selection rectangle, and HUD have separate lifecycles.

Transient preview/source suppression is cleaned after successful model/history mutation or Tool cancellation/deactivation.

Viewer batching/redraw follows the Bridge display-batch contract; OCCAD does not add redundant Redraw calls when Bridge already schedules one.

`OCCTBIM-Source` is used to validate responsibility boundaries and behavior, not copied structurally.
