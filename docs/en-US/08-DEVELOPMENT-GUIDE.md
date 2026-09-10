# 08 Development Guide

## 1. Development priorities

Daily OCCAD development prioritizes:

1. correct interaction behavior;
2. model/presentation coherence;
3. atomic Undo/Redo;
4. fully neutral Tool teardown;
5. no native/transient ghost state;
6. extensions that preserve existing ownership and dependency boundaries;
7. consistency between UI, documentation, registration, and actual capability.

Do not refactor stable paths merely for stylistic uniformity. Prefer reproducible defects and explicit architectural debt.

## 2. Development environment

Repository files are authoritative:

- .NET SDK: `global.json`
- Solution: `OCCAD.sln`
- Core: `src/OCCAD.Core`
- Avalonia: `src/OCCAD.Avalonia`
- Bridge SDK: OcctCSharpBridge SDK 3.0
- Windows default: `C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`
- Linux default: `~/.local/share/OcctCSharpBridge/SDK/3.0/linux-x64`

Override with `OCCTCSHARPBRIDGE_SDK`.

Windows:

```powershell
git pull
.\build.ps1
.\run.ps1
```

Flat runtime:

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

Linux:

```bash
git pull
./build.sh
./run.sh
```

Publish:

```powershell
.\publish.ps1
```

or:

```bash
./publish.sh
```

## 3. Directory responsibilities

### `src/OCCAD.Core`

```text
Actions/       Action / command metadata
Document/      Document membership / persistence presentation
Entities/      2D / 3D / Feature entities
Events/        Workspace event projection
Exchange/      external-format boundary
Geometry/      reusable geometry algorithms
Grips/         Grip owner
History/       Undo/Redo
Interaction/   Tool / Preview / Precision / Tracking / Transient
Layers/        Layer model
Properties/    Property descriptors/value conversion
Selection/     Entity/Subobject/Preselection
Snapping/      Snap discovery/selection/marker
WorkPlane/     WorkPlane state
```

### `src/OCCAD.Avalonia`

```text
Application/   startup/settings
Dialogs/       dialogs and CAD ColorTable
Input/         direct input / cursor
Localization/  zh-CN/en-US resources
Panels/        Model/Layers/Properties controllers
Shell/         MainWindow partial responsibilities
Theming/       layout metrics/CAD visuals
Viewport/      input adaptation and viewport interaction
```

UI code must not introduce new formal Entity/History/Transaction owners.

## 4. Standard development workflow

### 4.1 Identify the responsibility layer first

Classify the work as Domain, Interaction, Presentation, or Persistence, then identify the single owner. Do not begin by accumulating business logic in Button.Click or PointerMoved handlers.

### 4.2 Core first

Business behavior belongs in Core:

- Entity semantics → `Entities`
- Document membership/presentation → `Document`
- Tool lifecycle → `Interaction`
- Selection → `Selection`
- Snap → `Snapping`
- Grip → `Grips`
- History → `History`
- Layers → `Layers`
- Property transactions → `CadPropertyTransaction` / `CadTransaction`
- Actions → `Actions` / `CadCoreRegistration`

Avalonia is added last as entry and presentation.

### 4.3 Define failure semantics first

Before implementing any model or native mutation, define:

- commit point;
- rollback conditions;
- history-installation point;
- whether Tool completion is part of commit;
- transient release timing;
- whether notification observer failure may affect the business result;
- whether fatal exceptions could be incorrectly suppressed.

### 4.4 Build the smallest complete loop

Do not start with a broad menu surface. First complete:

`Core semantics → Tool → Preview → Commit → Undo → Cancel cleanup`

Then add Property, Grip, Snap, Persistence, UI, and documentation.

### 4.5 Add UI last

UI code only:

- collects parameters;
- calls Core APIs;
- observes notifications;
- renders visual state;
- localizes presentation.

It must not duplicate transaction or geometry rules.

## 5. Adding an Entity

For a new `CadXxxEntity`:

1. add the type under `Entities` with a stable `EntityType`;
2. validate parameters in Entity/Core;
3. implement geometry/presentation semantics;
4. implement `Duplicate`, snapshots, and `RestoreState`;
5. define Snap/Grip geometry where applicable;
6. register persistence in `CadEntityRegistry`;
7. add `XxxTool` if interactive creation is required;
8. register an Action;
9. add property descriptors;
10. add zh-CN/en-US localization;
11. expose Menu/Toolbar only if product-facing;
12. validate Save/Open, Undo/Redo, Cancel, and Preview cleanup.

The existence of an Entity class alone does not justify adding the feature to README.

## 6. Adding a Tool

A Tool must have an explicit stage machine.

Write the state flow first:

```text
Activate
→ Stage 0
→ accept point/selection/parameter
→ Stage 1
→ Preview
→ CanCommit / CanFinish
→ Commit
→ Deactivate
```

Verify:

- `CurrentStep` matches the prompt;
- `CanStepBack` is correct;
- `CanFinish` is correct;
- pointer movement updates Preview only;
- exact input and pointer input share geometry semantics;
- Cancel never mutates the formal Entity;
- Tool-owned transient state is registered through `CadTransientScene`;
- completion/cancel returns `NeutralStateViolations.Count == 0`.

## 7. Adding a modify command

Define the selection contract first:

- preselection allowed or not;
- selecting inside the Tool or not;
- Entity/Subobject scope;
- selection confirmation;
- replacement-preview strategy;
- mutate original Entities or create duplicates.

Recommended flow:

```text
Selection
→ capture source state
→ base/reference input
→ replacement Preview
→ formal mutation
→ Tool completion
→ history
```

Move is the current reference implementation. Do not expose Copy/Rotate/Scale/Mirror as buttons before their complete Tool lifecycle exists.

## 8. Action and command registration

Product-facing entries are registered through `CadCoreRegistration`.

Keep these synchronized:

1. Action ID;
2. `CadCommandCatalog` alias/caption;
3. Menu/Toolbar entry.

Move currently uses the stable ID:

```text
edit.move
```

Do not retain dead aliases such as an obsolete `modify.move`.

For multiple drawing methods of one Tool, register multiple Actions with initial parameters rather than duplicating the Tool:

```text
draw.circle.centerradius
→ CircleTool + Method=CenterRadius
```

## 9. Property development

For an editable property:

1. define the descriptor in Core;
2. parse/validate in Core value conversion/metadata;
3. mutate through `CadPropertyTransaction` / `ApplyEntities`;
4. keep Document presentation synchronized;
5. ensure Undo/Redo works;
6. keep UI editor logic presentation-only.

Never mutate native shapes directly from the PropertyGrid controller or bypass History.

## 10. Snap development

Separate responsibilities:

- Entity/geometry supplies candidate semantics;
- `CadSnapManager` owns filtering, ranking, hysteresis, cycling, and markers;
- pointer processing consumes the final resolved point.

Regression for a new Snap type includes single candidate, multiple candidates, Tab cycling, temporary modes, Tool switching, Commit/Cancel cleanup, and engine recreation.

## 11. Grip development

Grip data comes from formal Entity geometry, but dragging must not repeatedly mutate the formal Entity.

Standard flow:

```text
selected Entity
→ GripPoint
→ GripEditTool
→ duplicate/preview
→ validate target
→ ApplyEntities
→ history
```

Normal Grips are filled rectangles; Hot/Drag markers are circular. Marker size and hit tolerance come from Settings rather than hard-coded UI pixels.

## 12. WorkPlane and exact input

Every new 2D Tool must explicitly support XY/YZ/XZ rather than assuming XY.

Exact and pointer input must share stage semantics. Avoid:

- correct pointer geometry with different text-input geometry;
- retaining points from an incompatible frame after WorkPlane switching;
- ORTHO/POLAR affecting visuals without affecting the resolved point.

## 13. Persistence development

Persist formal model state only.

Schema may include:

- Entity identity/type;
- geometry;
- placement;
- layer;
- appearance;
- feature references/parameters.

Never persist Preview, Snap markers, Grip markers, Tracking, Preselection, or SelectionWindow.

A Registry ID that ships in a released file format is a protocol identifier and must not be renamed casually with a class refactor.

## 14. Event rules

Every new event must be classified as:

- veto/pre-state;
- strict internal propagation;
- post-state notification.

Public notifications normally isolate observers. Direct multicast invocation is reserved for intentional strict/veto contracts.

Recoverable observer failure is diagnostic only and must not make an already-committed model API report failure.

## 15. Exception rules

Argument errors use `ArgumentNullException`, `ArgumentOutOfRangeException`, or `ArgumentException`.

Broken runtime invariants use `InvalidOperationException`.

Recoverable native failures may be suppressed only at explicit presentation/transient-cleanup boundaries and remain diagnosable.

The following are not ordinary recoverable failures:

- `OutOfMemoryException`
- `StackOverflowException`
- `AccessViolationException`

Aggregate/inner exceptions must be classified recursively.

## 16. Transaction and History rules

- prefer lightweight history entries for small frequent edits;
- use `CadTransaction` for compound work;
- do not install nested Undo inside an outer transaction;
- no-op must not change History/Modified state;
- Tool completion belongs inside the atomic boundary when it is part of commit semantics;
- Undo/Redo implementations must consider partial failure and recovery.

UI calls `CadWorkspace.Undo/Redo`, not `CadHistory.Undo/Redo` directly.

## 17. Native resource ownership

Every native object receives an owner immediately.

Code must answer:

- where the handle is stored;
- who deletes it;
- what happens after engine recreation;
- whether a failed delete retains the handle;
- whether a failed cleanup can leave it visible/selectable;
- where retry cleanup occurs.

Temporary native objects must not rely on GC/finalizers for correctness.

## 18. UI development rules

### Shell

Keep:

- Menu + no more than two toolbar rows;
- dark Viewport;
- fixed Tool parameter strip;
- Status Strip;
- Model/Layers/Properties panels.

Do not restore:

- Ribbon;
- grouped three-row toolbar;
- permanent command line;
- floating tool panel;
- Ready/version/permanent coordinate noise.

### DPI

Design for at least 100%, 125%, and 150%. Do not hard-code label widths based on Chinese text length; prefer Grid Auto/* layout.

### Localization

Every new user-visible string must be added to zh-CN and en-US. Stable Action/Tool/Entity IDs are never localized.

## 19. Performance rules

High-frequency pointer paths should avoid:

- full Document snapshots on every MouseMove;
- unnecessary large LINQ allocations;
- repeated BuildPresentation;
- redundant redraw;
- repeated expensive Snap geometry evaluation at the same location;
- rebuilding the entire shell on every ToolUpdated event.

Prefer pointer scheduling, native display batches, replacement preview, Snap cache with correct invalidation, and non-destructive refresh of focused editors.

## 20. Build and run

Authoritative Windows build:

```powershell
.\build.ps1
```

Linux:

```bash
./build.sh
```

Build scripts validate Bridge managed assemblies, metadata, and portable/flat native layout. Do not hard-code personal SDK paths into project files.

Run the real app with:

```powershell
.\run.ps1
```

or:

```bash
./run.sh
```

Compilation success alone does not validate native interaction behavior.

## 21. Publishing

Windows:

```powershell
.\publish.ps1
```

Linux:

```bash
./publish.sh
```

Publishing must verify:

- `OCCAD.dll`;
- Bridge managed assemblies/metadata;
- portable runtime when used;
- portable OCCT resources;
- launcher: Windows `run.ps1`, Linux `run.sh`.

See [16-RELEASE-GUIDE.md](16-RELEASE-GUIDE.md).

## 22. Pre-commit checklist

Core:

- [ ] ownership is explicit;
- [ ] no-op does not create History;
- [ ] rollback is defined;
- [ ] Undo/Redo are symmetric;
- [ ] observer semantics are correct;
- [ ] Tool Cancel/Deactivate returns neutral;
- [ ] transient state does not enter Document/History;
- [ ] native handles are not forgotten;
- [ ] fatal exceptions are not swallowed;
- [ ] new extension points are documented.

UI:

- [ ] no duplicated Core business logic;
- [ ] layout survives 125%/150% DPI;
- [ ] no duplicated prompt/status surface;
- [ ] localization is complete;
- [ ] Menu/Toolbar matches Action registration.

## 23. Feature Definition of Done

A new feature should pass:

```text
Action/Menu entry
→ Tool activation
→ Prompt
→ pointer path
→ exact input path
→ Preview
→ Commit
→ Property
→ Snap/Grip if applicable
→ Undo/Redo
→ Cancel
→ Save/Open if persistent
```

Verify no ghost Preview, no stale Snap/Grip/Tracking, neutral state, one Undo per user operation, and coherent model/native presentation.

## 24. Avoid

- GitHub Actions by default;
- reflection-based command dispatch;
- compatibility layers that preserve incorrect APIs;
- parallel frameworks added to solve one bug;
- broad entity expansion before core interaction loops are stable;
- exposing incomplete Tools merely because transaction APIs already exist;
- running as Administrator/root to hide SDK/runtime configuration defects.
