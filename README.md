# OCCAD

OCCAD is an Avalonia desktop CAD application and CAD Core built on **OcctCSharpBridge / OCCT**.

[中文说明](README.zh-CN.md)

## Current goal

This branch focuses on a **usable, verifiable and ownership-correct** CAD baseline. Document / Entity / Layer / Property / Tool / Selection / Snap / Grip / Precision / WorkPlane / History correctness takes priority over adding demo-oriented features.

`OCCTBIM-Source/release-1.0` is the behavioral and responsibility reference. OCCAD keeps its C# / .NET / Avalonia / OcctCSharpBridge architecture and does not copy Qt widgets, singleton patterns or legacy implementation details.

## Architecture

```text
Avalonia Shell
    ↓
CadApplicationCore
    ├─ CadSettingsStore
    └─ CadWorkspace
         ├─ Document / Entity / Layer
         ├─ Action / Command / Tool
         ├─ Selection / Preselection / Subobject
         ├─ Snap / Tracking / Precision / WorkPlane
         ├─ Grip / Preview / Transient Scene
         └─ History / Property Transaction
              ↓
        OcctCSharpBridge / OCCT
```

Core rules:

- Entity owns CAD semantics, geometry, presentation properties and stable `LayerId`; viewer objects are derived presentation only.
- Tool owns the interaction state machine; Action is the user-operation entry point; Command Line, Ribbon and shortcuts delegate to Core.
- Preview / Snap markers / Tracking / Grip drag markers belong only to the Transient Scene and never enter Document / Selection / History.
- Property and Layer edits always use Core transaction/history paths; Avalonia does not mutate model state directly.
- Avalonia does not own a second Workspace, Selection, Layer, Tool or Property state model.

## Minimal UI

The desktop shell keeps six long-lived areas only:

```text
┌──────────────────── Ribbon ────────────────────┐
├──── Model ────┬──────── Viewport ────────┬─────┤
│               │                           │Prop/│
│               │                           │Layer│
├───────────────┴───────────────────────────┴─────┤
│ Command Line                                    │
├─────────────────────────────────────────────────┤
│ Status                                          │
└─────────────────────────────────────────────────┘
```

- Ribbon: follows ModelScript's compact three-row Avalonia Ribbon approach, text-first and limited to real Core actions.
- Model: browses and selects document entities.
- Viewport: the single primary scene with dark background, triedron, Window/Crossing selection, Grip, Snap and Preselection; **ViewCube is disabled**.
- Inspector: Properties and Layers share the right-side tab surface without duplicating model state.
- Command Line: unified commands, coordinates, exact values and Tool prompt entry.
- Status: selection, current layer, SNAP / ORTHO / POLAR, work plane and coordinates.

Large branding bars, fake document tabs, duplicate view toolbars, Floating Tool Panel, Dynamic HUD, permanent log panels and buttons without real Core behavior are intentionally excluded.

## Current product surface

The executable product surface contains common capabilities only.

**2D entities**: Point, Line, Polyline, Rectangle, Circle, Arc, Ellipse, Spline, Polygon, RegularPolygon, CenterLine, CenterMark.

**3D entities**: Box, Cylinder, Cone, Sphere.

**Edit**: Move, Copy, Rotate, Scale, Mirror, Array.

**Utility**: Undo / Redo, Delete, Select All / Invert, Distance Measure, Fit, standard orientations, Wireframe / Shaded, Hide / Isolate / ShowAll.

Offset / Trim / Extend / Fillet / Chamfer, experimental Features, Annotation, extra 3D primitives, ImportedShape and CAD Exchange are currently outside the product surface and must not leak through Registry / Action / Ribbon discovery.

## Precise drawing

Exact input uses one path:

```text
Viewport / Command Line
    ↓
CadCommandManager
    ↓
Coordinate / Precision parser
    ↓
Effective WorkPlane
    ↓
Snap + Tracking + Ortho/Polar
    ↓
Tool state machine
    ↓
Preview
    ↓
Transaction / History
    ↓
Entity
```

Absolute coordinates, relative coordinates, polar coordinates and Tool length/angle/factor inputs are supported. Drawing input must use the current Effective WorkPlane instead of silently falling back to world XY.

## Interaction baseline

- Middle mouse: pan; `Shift + middle mouse`: rotate; middle double-click: Fit.
- `F3`: Snap; `F8`: Ortho; `F10`: Polar.
- During drawing, `T / F / S`: XY / XZ / YZ work plane.
- Drawing uses a center-gap CAD cross cursor so snap markers remain visible.
- Left-to-right rectangle is Window; right-to-left is Crossing.
- Idle viewport context actions are limited to ShowAll / Hide / Isolate / Move / Copy / Delete / Property.
- Esc / Backspace / Enter / Space / right-click all flow through the shared ToolManager lifecycle.

## Build and run

Requirements are Windows x64, the .NET SDK pinned by `global.json`, and an installed OcctCSharpBridge SDK at the default location:

`C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`

```powershell
cd D:\workspace\occt\OCCAD
git pull
.\build.ps1 -Configuration Release
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

When the Bridge SDK provides a portable runtime, `run.ps1` uses it automatically. Otherwise pass `-OcctRoot`, or set `OCCT_ROOT` / `CASROOT`.

See [docs/README.md](docs/README.md) for the long-lived contracts.
