# OCCAD

OCCAD is an Avalonia desktop CAD application and extensible CAD framework built on **OcctCSharpBridge / OCCT**.

[中文说明](README.zh-CN.md)

## What OCCAD is

OCCAD is a product repository, not an OCCT wrapper demo. It contains the CAD domain model, interactive Tool framework, Avalonia desktop shell, persistence, documentation, and product build/run/publish entry points.

The current architecture includes Document/Entity/Layer ownership, Selection/Preselection/Subobject selection, Grip editing, WorkPlane/Snap/Tracking/Precision, Preview/History/Action/Tool, 2D/3D entities, modify/modeling, annotations, measurement, persistence, and native Avalonia Ribbon, Model/Layer/Property panels, Floating Tool Panel, Command Line, and Status Bar.

## Repository layout

- `src/OCCAD.Core` — CAD domain, document, entities, layers, history, actions/tools, selection, snap, grip, precision, geometry, persistence.
- `src/OCCAD.Avalonia` — Ribbon-first Avalonia shell, OCCT viewport integration, Model/Layer/Property/Floating Tool panels, Command Line, Status Bar, localization, dialogs.
- `docs/en-US` / `docs/zh-CN` — synchronized long-lived design and implementation contracts.
- `build.ps1`, `run.ps1`, `publish.ps1` — product build/run/publish entry points.
- `OCCAD.sln` — `OCCAD.Core` + `OCCAD.Avalonia`.

## Requirements

- Windows x64
- .NET SDK from `global.json`
- Avalonia restored by NuGet
- installed OcctCSharpBridge SDK, default `C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`
- compatible OCCT runtime when the installed Bridge SDK does not provide a portable runtime

Override the SDK path with `OCCTCSHARPBRIDGE_SDK`.

## Build and run

```powershell
cd D:\workspace\occt\OCCAD
git pull
.\build.ps1
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

When the Bridge SDK contains a portable runtime, `run.ps1` uses it automatically. Otherwise pass `-OcctRoot` or set `OCCT_ROOT` / `CASROOT`.

```powershell
.\publish.ps1 -OcctRoot D:\tools\occt-vc144-64
```

Normal OCCAD builds do not clone, rebuild, or sync OcctCSharpBridge.

## UI baseline

The shell uses a compact industrial CAD style: a native Avalonia custom Ribbon, restrained gray tool surfaces, dark viewport, consistent 11 px UI typography, compact 22 px controls, low/no corner radius, left Model panel, resizable right Layer/Property panels, Command Line above Status Bar, and a non-modal Floating Tool Panel inside the viewport.

Ribbon controls invoke registered Core Action IDs only. Circle, Arc, Regular Polygon, Ellipse, and 3D Primitives use one command-family drop-down layer instead of duplicating Tool logic. Ribbon content scrolls horizontally when required so common 125%/150% Windows scaling does not force the main window wider.

The Command Line is the only surface that shows the full active Tool prompt. Floating Tool Panel shows current Step, exact coordinates, Length/Angle/Factor, Tool parameters, and Back/Accept/Finish/Cancel. Dynamic HUD shows pointer-adjacent precision/snap/tracking feedback. Status Bar is limited to Selection, WorkPlane, SNAP/ORTHO/POLAR, and XYZ coordinates.

## Property and layer semantics

Normal Entity appearance properties are Layer, Color, LineStyle, LineWidth, Transparency, and Visible. Color/LineStyle/LineWidth each have an integrated `ByLayer` control.

PropertyGrid is descriptor-driven and supports categories, common-property multi-selection, mixed values, Numeric, Enum, Color, Layer/ByLayer, and Point/Vector X/Y/Z component editing.

Layer columns are `Current | Name | V | C | Style | Width | L`. Current layer uses an explicit `●/○` state. Only the Current column changes current layer; clicking the name only inspects the layer. The default layer cannot be renamed or removed.

Internal viewer handles, IDs, selectable implementation state, material/display-mode details, imported BREP byte counts, and similar implementation fields are hidden from the normal Property panel.

Editing explicit Color/LineStyle/LineWidth automatically leaves its corresponding ByLayer mode. Re-enabling ByLayer preserves the stored override and changes only the effective source.

Entity and Layer edits use Core transaction/history paths so UI edits, Undo/Redo, presentation updates, and rollback share one contract.

## Core design rules

- Document state and Entity geometry are authoritative; Viewer objects are derived presentation.
- Avalonia adapts Core state only.
- Tool is an explicit staged state machine.
- Ribbon, Command Line, and Floating Tool Panel share the same Action/Command/Tool state and never create a parallel command system.
- `CadCommandManager.ForWorkspace()` is the single command-session entry point per Workspace.
- Preview never enters Document, Selection, or History.
- Commit keeps the last valid Preview until model/history mutation succeeds.
- Grip pointer movement edits a duplicate preview only.
- Snap owns candidate resolution; Entity owns Snap/Grip semantics.
- Layer and Property controllers call Core business APIs instead of implementing parallel transactions.
- Avoid reflection dispatch, duplicate public APIs, compatibility/migration layers, excessive smoke/check frameworks, GitHub Actions, and artificial suffixes such as `Advanced`, `Extended`, `V1`, or `V2`.

See [docs/README.md](docs/README.md).

## Application preferences

`Manage → Preferences...` stores application-level viewport and interaction preferences in `%LOCALAPPDATA%\OCCAD\settings.json`.

Current preferences include scene background, grip marker size and hit tolerance, snap marker size and snap tolerance, selection tolerance, mouse-wheel zoom sensitivity, and OCCT display tessellation precision.

Display precision controls OCCT presentation deviation/angle only. It does **not** change the mathematical precision of BRep, curves, surfaces, dimensions, or saved CAD geometry.
