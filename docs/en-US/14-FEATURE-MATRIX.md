# 14 Initial Release Feature Matrix

## 1. Purpose

This document uses current `CadCoreRegistration`, the Classic Avalonia Shell, actual command registration, and persistence registration as the source of truth.

Status is split into:

- **Core**: real current Registry/infrastructure support;
- **UI**: exposure in the current Classic Shell;
- **Parameters**: whether required Tool parameters have a visible input surface;
- **Persistence**: formal read/write support;
- **Status**: initial release, infrastructure, internal support, or outside the release surface.

A historical/experimental class, geometry helper, transaction API, or unregistered Action in source does not imply product support.

For exact Action IDs and aliases, see [15-COMMAND-REFERENCE.md](15-COMMAND-REFERENCE.md). For release acceptance, see [16-RELEASE-GUIDE.md](16-RELEASE-GUIDE.md).

## 2. 2D Entity / Tool

| Feature | Core | UI | Parameter surface | Persistence | Status |
|---|---|---|---|---|---|
| Point | Yes | Yes | Tool stage input | Yes | Initial |
| Line | Yes | Yes | Tool stage input | Yes | Initial |
| Polyline | Yes | Yes | Tool/exact input | Yes | Initial |
| Free Polygon | Yes | Yes | Tool/exact input | Yes | Initial |
| Regular Polygon | Yes | Yes | Sides / Mode strip + direct input | Yes | Initial |
| Rectangle | Yes | Yes | Tool parameter/stage input | Yes | Initial |
| Circle | Yes | Yes | Method + Tool input | Yes | Initial |
| Arc | Yes | Yes | Method + Tool input | Yes | Initial |
| Ellipse | Yes | Yes | Method/parameter strip | Yes | Initial |
| Spline | Yes | Yes | Tool/exact input | Yes | Initial |
| Path | Entity/Persistence | No | No | Yes | Internal feature-profile support |

## 3. 2D construction methods

### Circle

| Method | UI entry | Tool |
|---|---|---|
| Center + Radius | Yes | `CircleTool` |
| Center + Diameter | Yes | `CircleTool` |
| Two Points | Yes | `CircleTool` |
| Three Points | Yes | `CircleTool` |
| Point + Center | Yes | `CircleTool` |

### Arc

| Method | UI entry | Tool |
|---|---|---|
| Three Points | Yes | `ArcTool` |
| Center → Start → End | Yes | `ArcTool` |
| Start → Center → End | Yes | `ArcTool` |
| Start → End → Center | Yes | `ArcTool` |
| Start → End → Point | Yes | `ArcTool` |
| Start → End → Tangent | Yes | `ArcTool` |

### Ellipse

| Method | UI entry | Tool |
|---|---|---|
| Center + Axes | Yes | `EllipseTool` |
| Axis Endpoints + Minor Axis | Yes | `EllipseTool` |

### Regular Polygon

| Method | UI entry | Parameters | Tool |
|---|---|---|---|
| Inscribed | Yes | Sides 3..360 / Mode | `RegularPolygonTool` |
| Circumscribed | Yes | Sides 3..360 / Mode | `RegularPolygonTool` |

Before center placement an integer side count may also be typed directly and confirmed with Enter.

## 4. 3D primitives / curves

| Feature | Core | UI | Parameter strip | Persistence | Status |
|---|---|---|---|---|---|
| Box | Yes | Yes | Yes | Yes | Initial |
| Cylinder | Yes | Yes | Yes | Yes | Initial |
| Cone | Yes | Yes | Yes | Yes | Initial |
| Frustum | Yes | Yes | Yes | Yes | Initial |
| Sphere | Yes | Yes | Yes | Yes | Initial |
| Ellipsoid | Yes | Yes | Yes | Yes | Initial |
| Torus | Yes | Yes | Yes | Yes | Initial |
| Helix | Yes | Yes | Yes | Yes | Initial |

## 5. Modeling features

| Feature | Tool | UI | Parameter/stage input | Feature persistence | Status |
|---|---|---|---|---|---|
| Extrude | Yes | Yes | Yes | Yes | Initial |
| Revolve | Yes | Yes | Yes | Yes | Initial |
| Sweep | Yes | Yes | Yes | Yes | Initial |
| Loft | Yes | Yes | Yes | Yes | Initial |

Feature entities require Document context, so persistence and interactive creation are owned by Registry/Tool respectively.

## 6. Classic Shell

| Surface | Current status |
|---|---|
| Native Avalonia FluentTheme | Yes |
| Classic Menu | Yes |
| Compact two-row Toolbar | Yes |
| Modify menu | Yes; Move / Delete |
| Active Tool Parameter Strip | Yes |
| Ribbon | No; retired from product baseline |
| Grouped three-row Toolbar | No; retired from product baseline |
| DensityStyle.Compact | No |
| Global custom Button/TextBox/ComboBox skin | No |
| Custom CAD ColorTable | Yes; retained business control |
| ViewCube | Disabled by default |
| Lower-left Triedron | Retained |
| Permanent Bottom Command Input | No |
| Permanent Dynamic HUD | No |
| Current operation prompt | Yes, Status Strip |

## 7. Tool parameter system

The current shell reads `CadTool.ParameterPanel` and creates editors automatically.

| Descriptor | UI |
|---|---|
| Integer | TextBox |
| Double | TextBox |
| OptionalDouble | TextBox |
| Boolean | CheckBox |
| Choice | ComboBox |
| String | TextBox |

The UI updates the Tool through `TrySetParameter()` and does not mutate Tool-private state directly.

## 8. Selection / interaction

| Feature | Core | UI/Viewport | Status |
|---|---|---|---|
| Entity Selection | Yes | Yes | Initial infrastructure |
| Subobject Selection | Yes | Yes | Initial infrastructure |
| Preselection | Yes | Yes | Initial infrastructure |
| Replace / Add / Remove / Toggle | Yes | Yes | Initial infrastructure |
| Window Selection | Yes | Yes | Initial infrastructure |
| Crossing Selection | Yes | Yes | Initial infrastructure |
| Grip / Hot Grip | Yes | Yes | Initial infrastructure |
| Grip Edit Preview/Commit | Yes | Yes | Initial infrastructure |
| WorkPlane XY/YZ/XZ | Yes | Yes | Initial infrastructure |
| ORTHO | Yes | Yes | Initial infrastructure |
| POLAR | Yes | Yes | Initial infrastructure |
| Precision Input | Yes | Tool dependent | Core/Tool infrastructure |
| Preview | Yes | Yes | transient |
| Tracking | Yes | Yes | transient |
| Selection Window | Yes | Yes | transient |

## 9. Snap

| Snap Type | Core | UI configuration |
|---|---|---|
| Endpoint | Yes | Yes |
| Midpoint | Yes | Yes |
| Intersection | Yes | Yes |
| Center | Yes | Yes |
| Perpendicular | Yes | Yes |
| Tangent | Yes | Yes |
| Quadrant | Yes | Yes |
| Extension | Yes | Yes |
| Insertion | Yes | Yes |
| Node | Yes | Yes |
| Apparent Intersection | Yes | Yes |
| Nearest | Yes | Yes |

## 10. Property / Layer

| Feature | Core | UI | Status |
|---|---|---|---|
| Categorized PropertyGrid | Descriptor | Yes | Initial |
| Multi-selection common properties | Yes | Yes | Initial |
| Mixed Value | Yes | Yes | Initial |
| Numeric / Enum / Choice | Yes | Yes | Initial |
| Left-aligned numeric editors | — | Yes | UI baseline |
| Layer | Yes | ComboBox | Initial |
| ByLayer appearance | Yes | Yes | Initial |
| Color | Yes | Custom ColorTable | Initial |
| Point / Vector | Yes | vertical X/Y/Z | Initial |
| Editable planar Normal | Yes | vertical X/Y/Z | Circle/Arc/Ellipse/Rectangle/Regular Polygon |
| Measurement readonly | Yes | Yes | Initial |
| Layer manager | Yes | Yes | Initial |
| Current Layer | Yes | top ComboBox | Initial |

## 11. Edit / History / Transaction

| Feature | Core | Current Shell | Status |
|---|---|---|---|
| Move | `MoveTool` + atomic transaction | Modify menu + Toolbar | Initial |
| Delete | Yes | Modify menu + Toolbar | Initial |
| Copy | Transaction-level API | No interactive Tool | Infrastructure only |
| Rotate | Transaction-level API | No interactive Tool | Infrastructure only |
| Scale | Transaction-level API | No interactive Tool | Infrastructure only |
| Mirror | Transaction-level API | No interactive Tool | Infrastructure only |
| CadTransaction | Yes | Indirect | Infrastructure |
| CadHistory | Yes | Indirect | Infrastructure |
| Undo | Yes | Dedicated Toolbar button + Ctrl+Z | Initial |
| Redo | Yes | Dedicated Toolbar button + Ctrl+Y / Ctrl+Shift+Z | Initial |
| Property atomic history | Yes | Yes | Initial infrastructure |

Undo/Redo UI entry points must call `CadWorkspace.Undo()` / `CadWorkspace.Redo()` rather than invoking `CadHistory` directly, so Tool, Selection, Subobject, Preselection, and Grip cleanup remains consistent.

## 12. Persistence / exchange

Formal Document/Entity/Layer/Feature state may be persisted. Preview, snap markers, grip markers, tracking guides, and other transient objects are never persisted.

STEP / IGES / BREP / STL / OBJ / glTF directions must be validated independently against actual Import/Export implementation. A file-picker extension does not prove bidirectional exchange support.

## 13. Outside the current initial product surface

Unless explicitly brought back into scope, the current Classic Shell does not expose:

- Copy / Rotate / Scale / Mirror;
- Array;
- Offset;
- Trim / Extend;
- Fillet / Chamfer;
- Text;
- Dimension / Annotation;
- advanced types that merely exist in source but are not registered for the current product surface.

## 14. Definition of complete

Validate every product feature through:

`Entry → Parameters → Prompt → Preview → Exact Input → Commit → Property → Grip/Snap → Undo/Redo → Save/Open`

Also verify that Commit/Cancel restores neutral state and leaves no Preview/native transient residue.

The repository currently has no independent Test project; actual builds plus manual/native regression are the current acceptance path.

## 15. Release classification

Use these labels consistently in README, documentation, and Release Notes:

- **Initial** — formally exposed and part of the initial release claim, pending/subject to the normal release gates.
- **Initial infrastructure** — required platform behavior used by initial features; not normally a standalone command.
- **Infrastructure only** — Core API/geometry/transaction support exists but the user-facing interaction loop is incomplete.
- **Internal** — persistence/geometry/helper capability used by other features and not a public user entry.
- **Outside release surface** — intentionally not claimed in the current release.

A feature may move to **Initial** only when registration, UI entry, prompt/parameters, preview, exact input where applicable, commit, cleanup, Undo/Redo, and persistence expectations are all coherent and the release regression is defined.
