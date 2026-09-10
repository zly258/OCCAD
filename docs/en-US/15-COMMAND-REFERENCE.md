# 15 Command and Feature Reference

This document defines the initial-release stable Action IDs, command aliases, default drawing methods, Tool IDs, and product exposure status.

> The current Classic Shell has **no permanent Command Line**. End users mainly work through Menu/Toolbar. Command IDs and aliases are stable Core metadata for UI, automation, MCP/script integration, and future command surfaces. Exact Tool input can still be typed directly while an active Tool is waiting for input.

## 1. Naming convention

Stable IDs use namespaces:

```text
draw.*       2D drafting
solid.*      3D primitives
curve.*      3D/spatial curves
feature.*    modeling features
edit.*       modify/edit
view.*       view commands
display.*    display modes
```

Rules:

- Action IDs are stable machine identifiers and are never localized;
- Chinese/English affects captions only;
- aliases must never point to an unregistered Action;
- multiple methods of one Tool use separate Action IDs plus initial parameters;
- the existence of a helper/API in source does not imply a formal command.

## 2. 2D drafting commands

| Feature | Action ID | Alias | Tool | Status |
|---|---|---|---|---|
| Point | `draw.point` | `POINT`, `PO` | `point` | Initial |
| Line | `draw.line` | `LINE`, `L` | `line` | Initial |
| Polyline | `draw.polyline` | `POLYLINE`, `PL` | `polyline` | Initial |
| Free Polygon | `draw.polygon` | `POLYGON`, `PG` | `polygon` | Initial |
| Rectangle | `draw.rectangle` | `RECTANGLE`, `REC` | `rectangle` | Initial |
| Spline | `draw.spline` | `SPLINE`, `SPL` | `spline` | Initial |

## 3. Regular Polygon

| Method | Action ID | Alias | Initial parameter |
|---|---|---|---|
| Inscribed | `draw.regularpolygon.inscribed` | `REGULARPOLYGON`, `RPOLY` | `Mode=Inscribed` |
| Circumscribed | `draw.regularpolygon.circumscribed` | — | `Mode=Circumscribed` |

Both activate the `regularpolygon` Tool.

Parameters:

- `Sides`: 3..360;
- `Mode`: Inscribed / Circumscribed.

The traditional `REGULARPOLYGON` alias maps to Inscribed.

## 4. Circle

All methods activate the `circle` Tool.

| Method | Action ID | Alias | Initial parameter |
|---|---|---|---|
| Center + Radius | `draw.circle.centerradius` | `CIRCLE`, `C` | `Method=CenterRadius` |
| Center + Diameter | `draw.circle.centerdiameter` | `CIRCLEDIAMETER` | `Method=CenterDiameter` |
| Two Points | `draw.circle.twopoints` | `CIRCLE2P` | `Method=TwoPoints` |
| Three Points | `draw.circle.threepoints` | `CIRCLE3P` | `Method=ThreePoints` |
| Point + Center | `draw.circle.pointcenter` | `CIRCLEPC` | `Method=PointCenter` |

The toolbar Circle command uses `draw.circle.centerradius`.

## 5. Arc

All methods activate the `arc` Tool.

| Method | Action ID | Alias |
|---|---|---|
| Three Points | `draw.arc.threepoints` | `ARC`, `A` |
| Center → Start → End | `draw.arc.centerstartend` | — |
| Start → Center → End | `draw.arc.startcenterend` | — |
| Start → End → Center | `draw.arc.startendcenter` | — |
| Start → End → Point | `draw.arc.startendpoint` | — |
| Start → End → Tangent | `draw.arc.startendtangent` | — |

The toolbar Arc command uses `draw.arc.threepoints`.

## 6. Ellipse

All methods activate the `ellipse` Tool.

| Method | Action ID | Alias |
|---|---|---|
| Center + Axes | `draw.ellipse.centermajor` | `ELLIPSE`, `EL` |
| Axis Endpoints + Minor Axis | `draw.ellipse.axisendpoints` | — |

## 7. 3D primitives and curve

| Feature | Action ID | Alias | Tool |
|---|---|---|---|
| Box | `solid.box` | `BOX`, `B` | `box` |
| Cylinder | `solid.cylinder` | `CYLINDER`, `CYL` | `cylinder` |
| Cone | `solid.cone` | `CONE`, `CN` | `cone` |
| Frustum | `solid.frustum` | `FRUSTUM`, `FRU` | `frustum` |
| Sphere | `solid.sphere` | `SPHERE`, `SPH` | `sphere` |
| Ellipsoid | `solid.ellipsoid` | `ELLIPSOID`, `ELLIP` | `ellipsoid` |
| Torus | `solid.torus` | `TORUS`, `TOR` | `torus` |
| Helix | `curve.helix` | `HELIX`, `HX` | `helix` |

## 8. Modeling features

| Feature | Action ID | Alias | Tool |
|---|---|---|---|
| Extrude | `feature.extrude` | `EXTRUDE`, `EXT` | `extrude` |
| Revolve | `feature.revolve` | `REVOLVE`, `REV` | `revolve` |
| Sweep | `feature.sweep` | `SWEEP`, `SW` | `sweep` |
| Loft | `feature.loft` | `LOFT` | `loft` |

Feature Tools require compatible existing Document entities/profiles.

## 9. Modify commands

| Feature | Action ID | Alias | Tool/Action | Product status |
|---|---|---|---|---|
| Move | `edit.move` | `MOVE`, `M` | `move` Tool | Initial |
| Delete | `edit.delete` | `DELETE`, `ERASE` | Action | Initial |

### Move stages

```text
Select Entities
→ Confirm Selection
→ Base Point
→ Target Point / replacement Preview
→ Commit
```

Move is the current reference modify Tool.

### Modify capabilities not exposed

The following may have partial Core geometry/transaction infrastructure but are not part of the formal command surface:

- Copy;
- Rotate;
- Scale;
- Mirror;
- Array;
- Offset;
- Trim / Extend;
- Fillet / Chamfer.

Do not add aliases or UI entries for these until a complete interactive Tool lifecycle is implemented and validated.

## 10. View commands

| Feature | Action ID | Alias |
|---|---|---|
| Fit | `view.fit` | `FIT`, `ZE` |
| Top | `view.top` | `TOP` |
| Bottom | `view.bottom` | `BOTTOM` |
| Front | `view.front` | `FRONT` |
| Back | `view.back` | `BACK` |
| Left | `view.left` | `LEFT` |
| Right | `view.right` | `RIGHT` |
| Iso NE | `view.iso.ne` | `ISO`, `ISONE` |
| Iso NW | `view.iso.nw` | `ISONW` |
| Iso SE | `view.iso.se` | `ISOSE` |
| Iso SW | `view.iso.sw` | `ISOSW` |
| Wireframe | `display.wireframe` | `WIREFRAME`, `WF` |
| Shaded | `display.shaded` | `SHADED`, `SHADE` |

## 11. History shortcuts

History UI entry points are handled by Workspace rather than the CommandCatalog:

| Input | Behavior |
|---|---|
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Ctrl+Shift+Z` | Redo |

UI must use `CadWorkspace.Undo()` / `CadWorkspace.Redo()` instead of calling `CadHistory` directly.

## 12. Drafting shortcuts

| Input | Behavior |
|---|---|
| `F3` | SNAP on/off |
| `F8` | ORTHO on/off |
| `F10` | POLAR on/off |
| `T` | XY WorkPlane |
| `S` | YZ WorkPlane |
| `F` | XZ WorkPlane |
| `Tab` | next Snap candidate |
| `Shift+Tab` | previous Snap candidate |

WorkPlane shortcuts do not force a change when the active Tool stage cannot safely change planes.

## 13. Tool control input

| Input | Behavior |
|---|---|
| `Esc` | Cancel active Tool |
| `Backspace` | StepBack |
| `Enter` / `Space` | Submit/Finish |
| right click | Secondary action |
| `O` | begin Offset Point during point-input stage |

`CadCommandManager` also recognizes textual control forms:

- `ESC`, `CANCEL`;
- `FINISH`, `DONE`;
- `U`, `BACK`, `STEPBACK`.

These textual semantics primarily support unified command-input and automation interfaces.

## 14. Exact input syntax

When supported by the active Tool stage:

```text
100                scalar Length/Angle/Factor depending on stage
L 100              Length
LENGTH 100
A 45               Angle
ANGLE 45
F 2                Factor
FACTOR 2
100,50             2D point
100,50,20          3D point
@20,0              relative point
@50<30             relative polar-style point
Radius=50          Tool parameter
```

Constraints:

- numeric values must be finite;
- relative points require a reference point in the current stage;
- requested precision kind must be allowed by the Tool `PrecisionInputs`;
- unsupported parameters fail without mutating the model;
- invalid input creates no History entry.

## 15. Snap types

Current Snap Core/UI configuration includes:

- Endpoint;
- Midpoint;
- Center;
- Vertex;
- Quadrant;
- Nearest;
- Intersection;
- Perpendicular;
- Tangent;
- Apparent Intersection;
- Extension;
- Insertion;
- Node.

Snap is drafting infrastructure, not persistent Entity state.

## 16. UI exposure rules

Action registration does not automatically imply toolbar exposure.

### Toolbar

Only high-frequency functions remain:

```text
Row 1: Undo Redo | Layer | View | Display
Row 2: frequent 2D | Move Delete | frequent 3D/Feature
```

### Menu

The Menu contains the complete current product Action surface, including all Circle/Arc/Ellipse methods and lower-frequency 3D/Feature commands.

### Never expose merely because source exists

- unregistered Actions;
- an Entity class without a complete user flow;
- geometry helpers;
- transaction-only modify APIs without an interactive Tool.

## 17. Action executability

`CadToolAction.CanExecute()` at minimum requires:

- an initialized Workspace Engine;
- the target Tool to be registered.

A command can therefore exist in metadata but be unavailable before the engine is initialized.

Non-Tool Actions such as Delete may additionally gate execution on formal Selection state.

## 18. Repeat and history concepts

Tool Actions are repeatable commands. `CadActionManager` stores recent repeatable Action IDs, while `CadCommandManager` maintains textual command-input history.

These are different from model Undo/Redo History:

- Action/Command history: what command the user invoked;
- `CadHistory`: formal model-state Undo/Redo.

They must not be merged into one concept.

## 19. Stability requirements

When adding or changing a command, verify:

- Action ID matches `CadCoreRegistration`;
- aliases point only to actually registered Actions;
- caption keys exist in zh-CN and en-US;
- method variants share one geometry implementation;
- initial parameters are accepted by the Tool;
- UI entries invoke Actions rather than constructing Tools directly;
- command failure does not swallow fatal exceptions;
- documentation and Feature Matrix are updated together.

## 20. Initial-release command surface

The initial release only guarantees commands listed here and features marked Initial in [14-FEATURE-MATRIX.md](14-FEATURE-MATRIX.md).

A new command should update together:

1. `CadCoreRegistration`;
2. `CadCommandCatalog`;
3. Menu/Toolbar when applicable;
4. zh-CN/en-US localization;
5. this reference;
6. Feature Matrix;
7. interaction/user documentation.
