# OCCAD

OCCAD is an Avalonia desktop CAD application and extensible CAD framework built on **OcctCSharpBridge / OCCT**.

[中文说明](README.zh-CN.md)

## What OCCAD is

OCCAD is a product repository, not an OCCT wrapper demo. It contains the CAD domain model, interactive Tool framework, Avalonia desktop shell, persistence, documentation, and product build/run/publish entry points.

The current architecture includes Document/Entity/Layer ownership, Selection/Preselection/Subobject selection, Grip editing, WorkPlane/Snap/Tracking/Precision, Preview/History/Action/Tool, 2D/3D entities, modify/modeling, annotations, measurement, persistence, and native Avalonia Model/Layer/Property/Tool panels plus Command Line.

## Repository layout

- `src/OCCAD.Core` — CAD domain, document, entities, layers, history, actions/tools, selection, snap, grip, precision, geometry, persistence.
- `src/OCCAD.Avalonia` — Avalonia shell, menu/toolbar/status bar, OCCT viewport integration, panels, command line, localization, dialogs.
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

The shell uses a compact industrial CAD style: restrained gray tool surfaces, dark viewport, consistent 11 px UI typography, compact 22 px controls, low/no corner radius, left Model panel, resizable right Layer/Property panels, Command Line above StatusBar, and a non-modal ToolPanel.

The Command Line is the only surface that shows the full active Tool prompt. StatusBar shows application/tool state, selection, history, snap, precision, work plane, and coordinates without duplicating the full prompt.

## Property and layer semantics

Normal Entity appearance properties are Layer, Color, LineStyle, LineWidth, Transparency, and Visible. Color/LineStyle/LineWidth each have an integrated `ByLayer` control.

Internal viewer handles, IDs, selectable implementation state, material/display-mode details, imported BREP byte counts, and similar implementation fields are hidden from the normal Property panel.

Editing explicit Color/LineStyle/LineWidth automatically leaves its corresponding ByLayer mode. Re-enabling ByLayer preserves the stored override and changes only the effective source.

Entity and Layer edits use Core transaction/history paths so UI edits, Undo/Redo, presentation updates, and rollback share one contract.

## Core design rules

- Document state and Entity geometry are authoritative; Viewer objects are derived presentation.
- Avalonia adapts Core state only.
- Tool is an explicit staged state machine.
- Preview never enters Document, Selection, or History.
- Commit keeps the last valid Preview until model/history mutation succeeds.
- Grip pointer movement edits a duplicate preview only.
- Snap owns candidate resolution; Entity owns Snap/Grip semantics.
- Layer and Property controllers call Core business APIs instead of implementing parallel transactions.
- Avoid reflection dispatch, duplicate public APIs, compatibility/migration layers, excessive smoke/check frameworks, GitHub Actions, and artificial suffixes such as `Advanced`, `Extended`, `V1`, or `V2`.

See [docs/README.md](docs/README.md).
