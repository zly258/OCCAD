# 05 Entity and Tool Contract

## Entity

Each Entity provides stable type/identity, valid geometry, presentation construction, Duplicate/Restore, transforms, Snap/Grip semantics, change notifications, and persistence state.

Geometry setters validate finite values, range/positive constraints, directions, and indices before mutation.

## Property exposure

Browsable properties must have ordinary CAD meaning: Type/Name/Layer, meaningful geometry, useful measurements, and effective appearance controls.

Hide viewer handles, object IDs, internal selectable/display/material state, feature plumbing, raw serialized BREP size, and similar implementation details.

Color/LineStyle/LineWidth use integrated ByLayer editors. The bool flags remain Core state but are hidden as standalone rows.

## Snap / Grip

Entity defines snap and grip semantics. SnapManager only selects candidates. GripManager only displays/hit-tests grips.

`MoveGrip` accepts a resolved world point and never processes raw pointer/Snap/Ortho UI input.

Grip editing uses a duplicate preview throughout pointer movement.

## Tool

Every Tool defines stable ID/name, State/Stage, InputKind, Prompt, PrecisionInputs, InteractionPolicy, SnapResolvePolicy, precision reference, WorkPlane behavior, parameters, Preview, Finish/Cancel/StepBack.

UI code does not interpret raw Stage integers to create parallel business behavior.

## Preview

Preview uses the same Entity geometry model as the final result where practical. Failed updates retain the last valid preview.

## Normal commit

```text
validate
→ finalize Entity
→ apply layer/appearance semantics
→ Document mutation / History
→ clear Preview
→ complete Tool
```

Do not clear Preview before a mutation that can fail. `CadToolContext.AddEntity` performs only the model add.

## Replacement commit

For Trim/Extend/Fillet/Chamfer-style replacement, keep valid replacement preview and source suppression during mutation. After success, clear transient state and restore source presentation safely. On mutation failure, keep the Tool usable.

## Property transaction

`Capture → Validate → Apply → Presentation/Dependency Refresh → History`.

Explicit Color/LineStyle/LineWidth edits automatically set the corresponding ByLayer flag false. Toggling ByLayer is itself history-recorded.

## Completion

A new Entity/Tool is complete only when geometry, registration, stages, prompt, Preview, Snap, Grip, Precision, Property, Layer/ByLayer, Undo/Redo, persistence, localization, invalid-input handling, Cancel/StepBack/right-click behavior, and navigation coexist correctly.
