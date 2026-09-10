# 08 Development Guide

## 1. Purpose

This document describes the normal OCCAD development workflow. It does not duplicate the full architecture, transaction, build, or release contracts; those remain authoritative in `04`, `10`, `11`, and `16`.

Daily priorities are:

1. correct interaction behavior;
2. model/presentation coherence;
3. atomic Undo/Redo;
4. neutral Tool teardown;
5. no native/transient ghost state;
6. one clear owner for every stateful concern;
7. agreement between registration, UI, documentation, and actual behavior.

## 2. Environment

Repository files are authoritative:

- .NET SDK: `global.json`;
- solution: `OCCAD.sln`;
- Core: `src/OCCAD.Core`;
- Avalonia: `src/OCCAD.Avalonia`;
- Bridge: OcctCSharpBridge SDK 3.0;
- Windows default SDK: `C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`;
- Linux default SDK: `~/.local/share/OcctCSharpBridge/SDK/3.0/linux-x64`.

Override the SDK location with `OCCTCSHARPBRIDGE_SDK`.

Windows:

```powershell
git pull --ff-only
.\build.ps1
.\run.ps1
```

If a flat Bridge SDK uses an external OCCT runtime:

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

Linux:

```bash
git pull --ff-only
./build.sh
./run.sh
```

Normal OCCAD builds consume the installed Bridge SDK. They do not clone, rebuild, or synchronize Bridge source.

## 3. Standard workflow

### 3.1 Classify the change

Before editing code, classify the concern:

- persistent domain state;
- interaction state;
- presentation/input adaptation;
- persistence/exchange;
- build/runtime/release infrastructure.

Then identify the single owner. See [07 Code Organization](07-CODE-ORGANIZATION.md).

### 3.2 Change Core first when behavior is authoritative

Typical ownership:

- Entity semantics → `Entities/`;
- Document membership/presentation synchronization → `Document/`;
- Tool lifecycle and transient ownership → `Interaction/`;
- selection → `Selection/`;
- Snap → `Snapping/`;
- Grip → `Grips/`;
- layers → `Layers/`;
- property semantics → `Properties/` + `CadPropertyTransaction`;
- Action registration → `Actions/` + `CadCoreRegistration`;
- Avalonia controls/layout only → `OCCAD.Avalonia`.

Do not start from a UI handler when the requested behavior changes authoritative CAD state.

### 3.3 Define mutation semantics

For every state-changing operation decide before implementation:

- what the authoritative state is;
- where commit occurs;
- which failure must roll back;
- when History is installed;
- whether Tool completion belongs to commit;
- what transient state must be released;
- which events are veto, strict propagation, or post-state notification.

Use [10 Transaction, Event, and Native Resource Contract](10-TRANSACTION-RESOURCE-CONTRACT.md) for the full rules.

### 3.4 Implement the smallest complete path

Prefer extending the existing owner instead of adding a parallel framework.

Examples:

- a new Circle construction method should activate the existing `CircleTool` with initial parameters;
- an Entity appearance edit should use `CadPropertyTransaction`, not direct UI mutation;
- a new formal edit operation needs a real interactive Tool before it is exposed in the product shell;
- a new marker must have explicit transient/native ownership.

### 3.5 Adapt Avalonia last

Avalonia is responsible for:

- collecting input;
- calling Core APIs/Actions/Tools;
- observing Core notifications;
- showing validation feedback;
- building controls and layout;
- localization and DPI-safe presentation.

Avalonia must not duplicate transaction, geometry, selection, layer, or history semantics.

## 4. Property development

Property descriptors and value semantics belong to Core.

Entity property edits use `CadPropertyTransaction` so multi-selection changes are atomic and undoable.

Appearance rules:

- `ColorByLayer`, `LineStyleByLayer`, and `LineWidthByLayer` represent Layer inheritance;
- directly editing Color/LineStyle/LineWidth creates an Entity **Override** by disabling the matching ByLayer flag in the same transaction;
- re-enabling ByLayer restores inheritance;
- presentation refresh follows authoritative Core state.

The PropertyGrid may choose TextBox, ComboBox, CheckBox, ColorTable, or compound ByLayer editors, but editor selection is a presentation concern only.

## 5. Tool development

A Tool is a staged state machine. A new Tool must define:

- stable Tool ID;
- stages and prompts;
- valid exact-input forms;
- Preview behavior;
- Backspace/StepBack behavior;
- Enter/Space/right-click completion behavior;
- Cancel/deactivate cleanup;
- transaction/history boundary;
- Snap/Tracking/WorkPlane policy;
- parameter descriptors when applicable.

Commit and Cancel must both return the interaction system to neutral.

## 6. Action and command development

Actions are stable command entry points.

When registering a new Action:

1. choose a stable hierarchical ID;
2. register it in `CadCoreRegistration`;
3. add aliases only if the Action is truly registered;
4. map localized captions separately from the stable ID;
5. expose it in Menu/Toolbar only when the complete workflow is ready;
6. update [15 Command Reference](15-COMMAND-REFERENCE.md) and [14 Feature Matrix](14-FEATURE-MATRIX.md).

Do not use localized text as an Action/Tool/Entity ID.

## 7. Entity development

A persistent Entity must define the geometry and state needed by its product contract, including as applicable:

- geometry validation;
- duplicate/snapshot/restore behavior;
- transform behavior;
- Snap points/curves;
- Grip points/editing;
- presentation construction;
- persistence read/write;
- property descriptors.

Adding an Entity class alone does not make it a user-facing feature.

## 8. Native and transient development

Every native temporary object must have an owner immediately after creation.

Code must be able to answer:

- where the handle is stored;
- who deletes it;
- what happens if deletion fails;
- whether failed cleanup leaves it visible/selectable;
- how engine recreation is handled;
- how a later cleanup retries retained state.

Do not rely on GC/finalizers for correctness of temporary native presentation.

## 9. Exception policy

Use normal argument exceptions for invalid API input and `InvalidOperationException` for broken runtime invariants.

Recoverable failures may be contained only at an explicit presentation/input/cleanup boundary and must remain diagnosable.

At minimum, these are not ordinary recoverable errors:

- `OutOfMemoryException`;
- `StackOverflowException`;
- `AccessViolationException`.

Do not hide a fatal failure behind a user-facing “invalid input” result.

## 10. Performance

High-frequency pointer paths should avoid:

- full Document snapshots per move;
- repeated native presentation rebuilds;
- redundant redraws;
- repeated expensive Snap geometry evaluation at the same state;
- large avoidable allocations.

Correct invalidation and ownership are more important than speculative caching.

## 11. Validation

The repository currently does **not** maintain a separate Test project. Do not write documentation or review checklists that imply one exists.

For each change, use the narrowest real validation available:

- `build.ps1` / `build.sh` for compile/runtime-layout validation;
- focused manual interaction regression for Tool/Selection/Snap/Grip/Property changes;
- native object/transient observation when Viewer state is involved;
- Save/Open regression for persisted changes;
- 125%/150% DPI and Chinese/English checks for UI changes.

The complete build matrix is in [11 Build and Validation](11-BUILD-VALIDATION.md); the release matrix is in [16 Release Guide](16-RELEASE-GUIDE.md).

If validation was not executed, state that explicitly. Static review is not a passing build.

## 12. Pre-commit checklist

- [ ] Change belongs to one clear owner.
- [ ] No duplicate Core/UI state model was introduced.
- [ ] No-op does not create History or Modified state.
- [ ] Rollback and Undo/Redo semantics are defined.
- [ ] Tool Commit/Cancel returns neutral when applicable.
- [ ] Preview/Snap/Tracking/Grip transient state is cleaned.
- [ ] Native handles are not forgotten after failed cleanup.
- [ ] ByLayer/Override semantics remain correct for appearance edits.
- [ ] Stable IDs and registrations match documentation.
- [ ] Chinese and English resources/docs are synchronized where applicable.
- [ ] Required build/manual/native validation was actually executed or explicitly marked pending.

## 13. Release stabilization rules

During release preparation do not add broad new feature families or perform aesthetic architecture rewrites.

Allowed work is limited to:

- compile/runtime fixes;
- reproducible release blockers;
- transaction/transient/selection/native cleanup fixes;
- localization/DPI corrections;
- documentation corrections;
- build/run/publish fixes;
- removal of code proven dead without breaking persistence or documented contracts.

See [16 Release Guide](16-RELEASE-GUIDE.md).
