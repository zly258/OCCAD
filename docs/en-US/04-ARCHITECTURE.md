# 04 Architecture

## Layers

```text
Presentation — OCCAD.Avalonia
        ↓
Application Core — CadApplicationCore
  Settings / Command Session / Document Session
        ↓
CAD Workspace — OCCAD.Core
  Document / Entity / Layer / Action / Tool / Selection / Snap / Grip / Preview / History
        ↓
OcctNet / OcctNet.Avalonia / OcctCSharpBridge / OCCT
```

Dependency direction is `Avalonia → Core → OcctNet`. Avalonia does not create a parallel CAD session or state container.

## Ownership

`CadApplicationCore` is the application composition root. It uniquely owns `CadSettingsStore`, `CadCommandManager`, `CadDocumentSession`, and one `CadWorkspace`. `CadWorkspace` composes CAD model and interaction services; it is not a global service locator.

Document is the sole persistent Entity owner and final appearance resolver. Entity owns stable identity/type, valid geometry, `LayerId`, appearance override values + ByLayer flags, placement, Snap/Grip semantics, duplicate/restore/transform behavior, and persistence state. Viewer objects are derived presentation only.

## Property / Layer transaction

`CadPropertyCatalog` / `CadPropertyDescriptor` / `CadValueDescriptor` define semantic property metadata. `TypeDescriptor` is only a CLR adapter. `CadPropertyTransaction` applies Entity edits atomically. Layer mutations stay in Core transaction/history; Avalonia does not implement a second rollback/history path.

## Command / Action / Tool

`CadCommandManager` is one application-owned command-line session and no longer uses a static Workspace lookup table. Action is the stable user-operation entry. Tool owns Step/Stage, Prompt, InputKind, InteractionPolicy, precision input, WorkPlane strategy, parameters, Preview, Finish/Cancel/StepBack. Frontends submit commands/input without owning a second command history or Tool state.

## Transient state / redraw

Native presentation for Selection, Preselection, SubobjectSelection, Grip, Snap, Tracking, Preview, and Window/Crossing selection rectangles is managed by Core owners and participates in the `CadTransientScene` lifecycle. Tool completion, cancellation, switching, or failure must return to neutral state; viewer/presentation state is never authoritative model data.

Viewer batching/redraw follows the Bridge display-batch contract; OCCAD does not add redundant Redraw calls when Bridge already schedules one. `OCCTBIM-Source/release-1.0` validates responsibility boundaries and behavior and is not copied structurally, including its Qt, singleton, or database implementation.
