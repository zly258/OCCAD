# 05 Entity and Tool Development Contract

## 1. Why this contract exists

OCCTBIM-Source gives Entity a complete role—shape, visual state, properties, grips, snaps, edit copies, and serialization—while Tool owns staged input and preview. OCCAD fixes these responsibilities as a stable extension contract so each new entity does not reinvent CAD interaction.

## 2. Minimum Entity contract

Every Entity provides:

- stable type Id;
- valid geometric fields;
- `BuildShape(OcctEngine)`;
- `Duplicate()`;
- `RestoreGeometry(snapshot)`;
- `Translate/Rotate/Scale`;
- `GetSnapPoints()`;
- `GetGripPoints()`;
- `MoveGrip(index, targetPoint)`;
- change events;
- serializable business state.

Geometry setters validate finite/positive/range constraints before mutating fields. NaN, Infinity, negative radius, and zero direction vectors must not be stored and deferred to BuildShape for failure.

## 3. File organization

Prefer one entity and one principal tool per file:

```text
Entities/
  CadLine.cs
  CadCircle.cs
  CadArc.cs
  CadRectangle.cs
  CadPolyline.cs
  CadBox.cs
  CadCylinder.cs
Tools/
  LineTool.cs
  CircleTool.cs
  ArcTool.cs
  RectangleTool.cs
  BoxTool.cs
  CylinderTool.cs
```

Shared geometry algorithms belong in explicit helpers/services, not copied across Tools.

## 4. Duplicate and restore

`Duplicate()` copies complete business state into a new object instance. Identity semantics must be explicit: edit snapshots are not distinguished by identity, while a true Copy operation creates a new Entity Id.

`RestoreGeometry()` restores geometry only. `RestoreState()` restores full business state including metadata and appearance.

## 5. Snap contract

Entity defines semantic points/curves. Examples:

- Line: endpoints, midpoint;
- Circle: center, quadrants, nearest/tangent curve;
- Arc: endpoints, center, applicable midpoint/quadrants;
- Polyline: vertices and segment midpoints;
- 3D primitives: semantically useful points as required.

SnapManager performs candidate collection, screen-distance comparison, priority, and policy filtering only.

## 6. Grip contract

Every Grip describes:

- stable `Index`;
- `Kind`: Control/Vertex/Midpoint/Center/Radius/Axis/Height;
- `Position`;
- `ConstraintOrigin`;
- `WorkPlane`;
- `PrecisionInputs`.

`MoveGrip` receives the already-resolved world-space target. It does not implement mouse handling, Snap, or Ortho itself.

Examples:

### Line

Start = Vertex; midpoint = whole-line move; end = Vertex.

### Circle

Center = move center; Radius grip = change radius in the defined work plane.

### Box

Corner/base grips control footprint; Height grip follows the height axis; optional move/center grip moves the whole primitive.

The real Entity is never modified continuously during grip drag.

## 7. Minimum Tool contract

Every Tool declares:

- Id;
- localized display name;
- State;
- stage count and stage prompts;
- pointer left/right/move behavior;
- Enter/Esc/Backspace behavior;
- `CanFinish`;
- `PrecisionInputs`;
- `PrecisionReferencePoint`;
- work-plane lock policy;
- `ParameterPanel`;
- preview construction/update;
- commit;
- cancel cleanup.

## 8. Stage values versus stable Tool parameters

### Stage/dynamic values

Pointer position, current length, angle, displacement vector, and similar values produced by pointer + precision. These appear in ToolBar/HUD.

### Stable Tool parameters

Radius, Width, Height, Sides, Mode, etc. These appear in the floating Tool Panel and can update Preview immediately.

If one value can be established by either pointer or panel, both paths update the same Tool field rather than maintaining duplicate states.

## 9. Preview contract

Preview uses the same Entity type and parameter model as the final result. Do not maintain simplified preview geometry that produces a visible jump at commit.

Tool supplies preview business entities; PreviewManager owns presentation.

On preview failure:

- retain the last valid preview;
- keep the Tool active;
- allow further pointer/parameter input;
- report a meaningful status message.

## 10. Commit contract

```text
Validate current state
→ create/finalize Entity
→ apply current layer/appearance semantics
→ Document add/update in a change set
→ record one History entry
→ clear transient presentation
→ complete Tool
```

Do not delete valid preview/original state before final geometry creation succeeds.

## 11. Cancel and StepBack

Cancel clears every transient state created by the Tool and restores temporary WorkPlane/locks. It never deletes pre-existing user entities.

StepBack returns one stage and is not equivalent to canceling the Tool. Multi-stage Tools such as Polyline must support staged rollback.

## 12. Property contract

Core properties expose stable business meaning. UI DisplayName/Category/Description comes from localization descriptors rather than Chinese strings embedded in Core attributes.

Double values display 3 decimals but setters and persistence use full double precision. Color uses ColorDialog. Enums display localized labels while storing the original enum value.

## 13. Validation and errors

All Entity/Tool implementations follow the same rules:

- validate input before state mutation;
- recoverable failure affects only the current operation;
- capture before mutating real objects;
- record History only after success;
- catch only when a coherent rollback/recovery is performed.

## 14. Completion checklist for a new Entity/Tool

A feature is complete only when it covers Entity geometry/properties, registration, Tool stages/prompts, Preview, Snap, Grip, Precision, PropertyGrid, Layer/ByLayer, Undo/Redo, serialization, Chinese/English localization, degenerate input, cancel/step-back/right-click finish, and navigation while the Tool is active.
