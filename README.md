# OCCAD

OCCAD is an Avalonia desktop CAD application and extensible CAD kernel built on **OcctCSharpBridge / Open CASCADE Technology (OCCT)**.

[中文说明](README.zh-CN.md)

## Project scope

The current milestone is an initial CAD baseline with an explicit lifecycle for:

`Create → Preview → Exact Input → Commit → Property → Grip → Undo/Redo`

Core layers include Document / Entity / Layer, Tool / Action / Transaction / History, Selection / Preselection / Subobject Selection, WorkPlane / Snap / Tracking / Precision Input, Grip / Preview / transient ownership, 2D drafting, 3D primitives, modeling features, Model Tree / Layers / Properties, localization, and persistence.

## Technical baseline

- .NET 10
- Avalonia 12 native `FluentTheme`
- OcctCSharpBridge SDK 3.0 / ABI 5
- Open CASCADE Technology 7.9.0
- Windows x64 / Linux x64

## UI baseline

OCCAD has one classic CAD shell:

```text
Menu: File | Draw | Model | Modify | View | Window | Language

Toolbar Row 1
Undo Redo | Current Layer | Top Front Right Iso Fill | Wireframe Shaded

Toolbar Row 2
Line Polyline Rectangle Circle Arc Polygon | Move Delete | Box Cylinder Sphere Extrude Revolve

Model Tree | CAD Viewport | Layers / Properties

Fixed Tool Parameter Strip
Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

Rules:

- no Ribbon;
- the toolbar contains only frequent commands and may use up to two compact rows;
- file operations, language switching, and the full command set remain in Menu;
- Button / TextBox / ComboBox / CheckBox / Menu use native Avalonia Fluent styling;
- `CadTheme` contains layout metrics and CAD-specific viewport/overlay visuals only;
- the custom CAD ColorTable is retained;
- the Tool parameter strip is fixed immediately above the status strip so the viewport does not jump when tools change;
- dark viewport, lower-left triedron, ViewCube disabled by default;
- no permanent bottom command bar, Ready text, version text, or permanent coordinate noise.

## Tool parameters

Parameterized tools expose their `ParameterPanel` through the fixed bottom parameter strip.

Regular Polygon example:

```text
Regular Polygon: Sides [6]  Mode [Inscribed]
```

- `Sides`: 3..360;
- `Mode`: Inscribed / Circumscribed;
- before center placement, the side count may also be typed directly and confirmed with Enter;
- editor changes are written back to the active Tool;
- observable preview dimensions may refresh the displayed values;
- a focused editor is not overwritten by live refresh.

The same mechanism is shared by Box, Cylinder, Cone, Frustum, Sphere, Ellipsoid, Torus, Helix, Ellipse, Extrude, and other parameterized tools.

## PropertyGrid

PropertyGrid is driven by Core descriptors:

- Property / Value are left aligned;
- numeric editors are left aligned;
- Point / Vector / Normal use vertical engineering input:

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

- Layer uses a ComboBox;
- Color / LineStyle / LineWidth support ByLayer;
- Color editing keeps OCCAD's custom CAD ColorTable;
- planar `Normal` is editable for Circle / Arc / Ellipse / Rectangle / Regular Polygon.

## Interaction baseline

- `Esc`: cancel the active Tool and clear Entity / Subobject / Preselection state;
- `Backspace`: step back the active Tool stage;
- `Enter/Space`: submit the current valid stage or finish;
- XY / YZ / XZ switch the drafting work plane; a fixed temporary ToolPlane is first stepped back to a safe stage;
- `Ctrl+Z`: Undo;
- `Ctrl+Y` / `Ctrl+Shift+Z`: Redo;
- Preview/Snap/Tracking/Grip are transient and must clear after Commit/Cancel.

## Initial feature surface

2D: Point, Line, Polyline, Free Polygon, Regular Polygon, Rectangle, Circle, Arc, Ellipse, Spline.

Circle methods: Center+Radius, Center+Diameter, Two Points, Three Points, Point+Center.

Arc methods: Three Points, Center→Start→End, Start→Center→End, Start→End→Center, Start→End→Point, Start→End→Tangent.

Ellipse methods: Center+Axes, Axis Endpoints+Minor Axis.

3D/curves: Box, Cylinder, Cone, Frustum, Sphere, Ellipsoid, Torus, Helix.

Features: Extrude, Revolve, Sweep, Loft.

Modify: Move and Delete are exposed in the current shell. Copy / Rotate / Scale / Mirror remain transaction-level Core capabilities until dedicated interactive Tools are completed and validated.

Views: Top / Bottom / Front / Back / Left / Right, Iso NE/NW/SE/SW, Fill (`view.fit`), Wireframe, Shaded.

## Architecture

```text
OCCAD.Avalonia
      ↓
OCCAD.Core
      ↓
OcctNet / OcctCSharpBridge
      ↓
OCCT
```

Core does not depend on Avalonia. The UI adapts input and presents state; it does not duplicate Document, Selection, History, Layer, Geometry, or Tool business state.

## Build

Windows:

```powershell
.\build.ps1
```

Linux:

```bash
./build.sh
```

Linux validation uses the current portable Bridge SDK layout (`portable/runtime` + `portable/occt`). Windows accepts the installed SDK layout supported by `build.ps1`.

The repository currently does not maintain a separate Test project. Validation order is:

`Core build → Avalonia build → real interactive regression → native/transient cleanup checks`

Source existence or Action registration alone does not prove product completion.

## Documentation

Start at [docs/README.md](docs/README.md). Key contracts include UI, interaction, architecture, entity/tool, transaction/resource, build validation, user guide, and feature matrix documents.

## License

Original OCCAD code uses **OCCAD Non-Commercial License 1.0** from the repository root. Third-party dependencies remain under their own licenses; see `THIRD_PARTY_NOTICES.md`.
