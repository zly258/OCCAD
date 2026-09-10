# OCCAD

OCCAD is an Avalonia desktop CAD application and CAD Core built on **OcctCSharpBridge / OCCT**.

[中文说明](README.zh-CN.md)

## Current goal

This branch prioritizes a **usable, verifiable and ownership-correct** CAD baseline instead of adding demo-oriented UI surfaces.

`OCCTBIM-Source/release-1.0` is the behavioral reference for Document / Entity / Layer / Property / Tool / Grip / Snap / WorkPlane / Viewport interaction. OCCAD keeps its C# / .NET / Avalonia / OcctCSharpBridge architecture and does not copy Qt widgets, singleton patterns or legacy implementation details.

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

`CadApplicationCore` is the desktop composition root. Avalonia does not construct a second Workspace and does not mirror CAD state.

## Minimal UI

The desktop shell has six long-lived areas only:

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

- Ribbon: text-first, low-height, and limited to registered Core actions.
- Model: browses and selects document entities.
- Viewport: the primary scene with dark background, ViewCube, triedron, Window/Crossing selection, Grip, Snap and Preselection.
- Inspector: Properties and Layers share one right-side tab surface.
- Command Line: the single entry point for commands, coordinates, exact values and the full Tool prompt.
- Status: selection, current layer, SNAP / ORTHO / POLAR, work plane and coordinates.

Large branding bars, fake document tabs, duplicate view toolbars, Floating Tool Panel, Dynamic HUD, permanent log panels, and buttons without real Core behavior are intentionally excluded.

## Interaction baseline

- Middle mouse: pan; `Shift + middle mouse`: rotate; middle double-click: Fit.
- `F3`: Snap; `F8`: Ortho; `F10`: Polar.
- During drawing, `T / F / S`: XY / XZ / YZ work plane.
- Drawing uses a center-gap CAD cross cursor so snap markers remain visible.
- Left-to-right rectangle is Window; right-to-left is Crossing.
- Idle viewport context actions follow the Source baseline: ShowAll / Hide / Isolate / Select / Move / Copy / Delete / Property.
- Typing a letter while idle enters the shared Command Line; Space repeats the previous command; numeric input during drawing enters the same exact-input path.
- Esc / Backspace / Enter / Space / right-click all flow through the shared ToolManager lifecycle; the UI does not mutate geometry directly.

## State and transactions

- Document state and Entity geometry are authoritative; Viewer objects are derived presentation.
- Preview, Snap markers, Tracking, Grip drag markers and other temporary graphics belong to the Transient Scene and never enter Document / Selection / History.
- Tool completion and cancellation return to a neutral state that clears Preview, Snap, Tracking, WorkPlane, Preselection and pointer transients.
- Grip pointer movement edits a duplicate preview and writes back to the real Entity only on accept.
- Property and Layer edits use Core transaction/history paths.
- Entity-to-layer references use stable `LayerId`; layer names are editable metadata only.

## Current command surface

The default Ribbon exposes the stabilized registered baseline:

- New, Undo / Redo, Delete, Select All, Invert Selection, Distance Measure;
- Point / Line / Polyline / Circle / Arc / Rectangle / Polygon / RegularPolygon / Ellipse / Spline;
- Box / Cylinder / Cone / Sphere / Ellipsoid / Torus;
- Extrude / Revolve / Sweep / Loft;
- Move / Copy / Rotate / Scale / Mirror / Array / Offset / Trim / Extend / Fillet / Chamfer;
- CenterLine / Text / Length / Angle / Radius / Diameter;
- Fit / Isometric / Top / Front / Right / Wireframe / Shaded / Hide / Isolate / ShowAll.

Core capabilities that do not yet form a stable product workflow may remain implemented without being exposed as default UI placeholders.

## Settings

Application settings are owned by `CadSettingsStore` and saved on exit to:

`%LOCALAPPDATA%\OCCAD\settings.json`

There is intentionally no separate fake Preferences surface. A setting enters the UI only when it has a clear Core owner and a real effect path.

## Build and run

Requirements are Windows x64, the .NET SDK pinned by `global.json`, and an installed OcctCSharpBridge SDK at the default location:

`C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`

```powershell
cd D:\workspace\occt\OCCAD
git pull
.\build.ps1
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

When the Bridge SDK provides a portable runtime, `run.ps1` uses it automatically. Otherwise pass `-OcctRoot`, or set `OCCT_ROOT` / `CASROOT`.

See [docs/README.md](docs/README.md) for the long-lived contracts.
