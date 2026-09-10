# 03 Interaction Specification

## 1. Core rule

CAD interaction is a predictable state machine. Mouse, keyboard, precision input, Snap, and Grip input first pass through the active Tool/interaction context and only then update Preview or commit Document state. MainWindow event handlers must not directly mutate entity geometry.

## 2. Tool lifecycle

```text
Idle
  ↓ Activate
Drawing / WaitForSelection
  ↓ Pointer / Keyboard / Parameter
Preview Updated
  ↓ Left Click / Enter / Finish
Commit Stage
  ├─ Next Stage → Drawing
  └─ Complete → Commit Document → Idle

Esc / Right Click / Cancel
  ↓
Cancel or Finish according to Tool contract
  ↓
Clear transient state → Idle
```

Right-click first submits the current non-pointer Tool step when that step can be accepted; otherwise it finishes a finishable Tool, and only then falls back to cancel. Enter and the ToolPanel Accept/Finish control use the same Core submission rule. Backspace means Tool StepBack. Delete means delete selection and is not a second StepBack key.

StepBack restores both Tool-owned stage data and the temporary WorkPlane frame/origin for the preceding stage. An exact point API reports success only when that point was actually accepted; pointer handlers may still consume an invalid click while keeping the Tool active.

A blank Command Line Enter uses the same `SubmitCurrent()` rule. Point steps never reuse a previously observed pointer position as implicit input.

## 3. Point-resolution pipeline

All drawing Tools, Grip Edit, Move, and Copy use one ResolvePoint pipeline:

```text
Screen point
→ View ray / screen-to-world
→ Active WorkPlane intersection
→ Object Snap candidate
→ Tracking candidate (Ortho/Polar/Axis/Angle)
→ Length constraint
→ Final world point
```

Individual Tools do not duplicate XY projection, polar math, or angle-lock logic.

Command-line coordinates follow the same work-plane contract. `x,y` and `x,y,z` are absolute coordinates in the persistent user work-plane frame. `@x,y` and `@x,y,z` are relative to the current Tool precision reference and use the effective Tool/Grip frame axes. Two- and three-component coordinates therefore remain consistent on XY, YZ, XZ, and custom planes.

## 4. Work plane

S/F/T map to S→YZ, F→XZ, T→XY. They change the drawing plane, not the camera view.

A Tool may temporarily lock a work plane. User switching is then rejected with an explicit StatusBar message. Tool completion restores the persistent user drawing plane rather than writing transient Tool state back to global state.

GripPoint may carry its own WorkPlane, ConstraintOrigin, and LockedAngle so the Entity describes the correct edit space.

## 5. Snap

Baseline modes: Endpoint, Midpoint, Center, Vertex, Quadrant, Nearest, Intersection, Perpendicular, Tangent. Snap resolution uses explicit priority plus screen-pixel tolerance.

Snap markers must be screen-size stable, type-specific, reliably re-show after being hidden, avoid per-frame object recreation, and keep temporary snap overrides separate from persistent modes.

## 6. Direction and length constraints

Ortho, Polar, Angle Lock, and Axis Lock are direction constraints that produce a common tracking result rather than overwriting coordinates independently.

- ORTHO: automatic local X/Y directions;
- POLAR: angular increments;
- Angle Lock: explicit arbitrary user angle;
- Axis Lock: explicit local axis;
- Length Lock: distance applied after direction resolution.

Explicit locks take precedence over automatic tracking. For example, enabling explicit Angle Lock disables Axis Lock.

## 7. Dynamic input

ToolBar L/A values are current-stage precision values. When unlocked they show live telemetry; Enter or a lock control converts the current numeric value into a constraint.

Tool Panel values such as Radius, Width, Height, and Sides are stable Tool parameters. Editing them updates Preview immediately without committing.

## 8. Selection

Support Replace, Add, Remove, and Toggle. Point and box selection use the same SelectionManager.

Box direction:

- left-to-right: Window, full containment required;
- right-to-left: Crossing, intersection is sufficient.

Selection rectangle is transient presentation. Preselection is separate from committed Selection. Hidden, locked-layer, or non-selectable entities cannot enter formal Selection.

## 9. Grips

Entity supplies GripPoints; GripManager renders, hit-tests, and manages hot state but does not infer geometry semantics.

Suggested marker semantics:

- Vertex/Control: compact filled square or diamond;
- Midpoint: triangle;
- Center: circle/center marker;
- Radius: compact radial-oriented marker;
- Axis/Height: directional marker.

Hot state is expressed by color and slight size change only.

### Grip edit transaction

```text
Capture original entity
→ Duplicate edit preview
→ MoveGrip(preview)
→ Validate + rebuild preview
→ success: keep preview
→ failure: rollback preview; keep Tool active
→ Accept: apply preview geometry to real entity
→ record one History entry
```

Recoverable geometry errors never terminate the process. The real Entity is not modified on every pointer move.

## 10. Preview

Preview objects do not enter Document, Selection, or History. Preview geometry and appearance match the final Entity as closely as possible.

Preview replacement is atomic: successfully create/configure the new presentation before deleting the old one. Failure keeps the previous valid preview visible.

## 11. Navigation during Tools

Pan/Rotate/Zoom remain available while a Tool is active. Navigation must neither commit nor cancel the Tool, and preview continues afterward.

Right-click cannot simultaneously mean camera rotation and Tool Finish; navigation uses a distinct mouse contract.

Leaving the viewport invalidates the last observed pointer and clears preselection, Snap/Tracking candidates, hot Grip state, dynamic input, and the snap aperture. A later shortcut or Tool refresh must not reuse an off-viewport coordinate.

## 12. Property edit

Property changes use: Capture State → Validate → Apply → update geometry/appearance → Record History. Failure restores the prior state.

Multi-selection exposes common properties only. ByLayer flags are explicitly editable.

## 13. Exception boundaries

Three layers:

1. Local Tool/Grip/Property transaction boundary catches recoverable input/geometry/presentation errors and rolls back;
2. UI interaction boundary prevents ordinary exceptions from escaping the Avalonia Dispatcher, reports the error, and clears inconsistent transient state;
3. Fatal failures such as OutOfMemory, StackOverflow, and AccessViolation are not falsely reported as safely recovered.

Every failure path must leave Tool, Preview, Grip, Selection, and WorkPlane in a coherent state.
