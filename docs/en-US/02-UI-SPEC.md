# 02 UI Specification

## Goal

OCCAD uses `OCCTBIM-Source/release-1.0` as the CAD workflow reference without copying its Qt widgets or historical layout. The rule is: **preserve real CAD commands, reduce UI layers, prioritize viewport area, and keep one owner for each state**.

## Fixed workspace

The main window keeps only six persistent regions:

```text
┌──────────────────────────── Ribbon ────────────────────────────┐
├──────── Model ────────┬──────────── Viewport ────────────┬─────┤
│                       │                                   │Prop/│
│                       │                                   │Layer│
├───────────────────────┴───────────────────────────────────┴─────┤
│ Command Line                                                   │
├────────────────────────────────────────────────────────────────┤
│ Status                                                         │
└────────────────────────────────────────────────────────────────┘
```

- Ribbon: compact and low, rendering only actions backed by Core.
- Model: entity browsing and selection only; it does not duplicate Property editing.
- Viewport: the primary workspace with native navigation, ViewCube and Triedron.
- Right Inspector: `Properties / Layers` share one tabbed column instead of two permanent stacked docks.
- Command Line: the single complete prompt, command and exact-input entry point.
- Status: selection, current layer, Snap, Ortho, Polar, WorkPlane and coordinates only.

## UI that must stay removed until it has real product semantics

Do not add the following without a backed workflow:

- large brand/welcome/decorative header bands;
- a second view toolbar or duplicated selection/view controls;
- fake document tabs or a `+` tab without a real multi-document lifecycle;
- a floating Tool Panel duplicating Command Line prompts;
- permanent Log, Memory Monitor or Component docks;
- buttons without a Core Action/Transaction;
- placeholder commands, disabled fake menus or settings added only to resemble another CAD product.

## Ribbon

The ribbon follows Source command groups while deliberately reducing the surface:

- Home: Undo / Redo / Delete / Select / Measure;
- Draw: Line / Polyline / Circle / Arc / Ellipse / Rectangle / Polygon / Spline;
- 3D: Source primitives already implemented by OCCAD plus Extrude/Revolve/Sweep/Loft;
- Modify: Move / Copy / Rotate / Scale / Mirror / Array / Offset / Trim / Extend / Fillet / Chamfer;
- Annotate: CenterLine / Text / Dimension;
- View: Fit / Orientation / Display / Hide / Isolate / ShowAll.

A button may be rendered only when a real action exists in `CadActionManager`. Core capabilities that exist but are not part of the current Source primary workflow may remain available to automation without being exposed in the default UI.

## Property / Layer

Property rows are described by `CadPropertyService` and committed through Core transactions. Avalonia does not directly mutate Entity fields. Stable Entity IDs are data-layer information and are hidden from the default inspector.

Layer UI keeps only current layer, create, visibility, lock and necessary property editing. Entities reference layers by stable `LayerId`; names are editable display metadata.

ByLayer is expressed by an explicit toggle. Direct Color / LineStyle / LineWidth editing is enabled only when the corresponding ByLayer flag is off.

## Visual rules

- Use a compact light industrial shell and a dark viewport.
- Default font and control dimensions must remain usable at both 100% and 125% scaling.
- Do not depend on large icons to communicate commands; text remains readable and icons are optional when they add information.
- Borders, separators and headers use low contrast; selection and active state use one blue accent.
- Visual decoration must not reduce useful viewport area.

## Ownership boundary

Avalonia may render Core state, invoke Actions/Tools/Property/Layer transactions and forward input. It must not create a second Selection, Snap, Grip, WorkPlane, Preview, History, Layer or Entity state model.
