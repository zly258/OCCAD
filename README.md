# OCCAD

OCCAD is an Avalonia desktop CAD application and extensible CAD kernel built on **OcctCSharpBridge / Open CASCADE Technology (OCCT)**.

[中文说明](README.zh-CN.md)

## Project scope

OCCAD is not an OCCT API demo or just a model viewer. The current milestone is an initial CAD baseline with explicit lifecycle and ownership rules for:

`Create → Preview → Exact Input → Commit → Property → Grip → Undo/Redo`

Core product layers include Document / Entity / Layer, Tool / Action / Transaction / History, Selection / Preselection / Subobject Selection, WorkPlane / Snap / Tracking / Precision Input, Grip / Preview / transient scene ownership, 2D drafting, 3D primitives, modeling features, Model Tree / Layers / Properties, localization, persistence, exchange, build, and publish.

## Technical baseline

- .NET 10
- Avalonia 12 native `FluentTheme`
- OcctCSharpBridge SDK 3.0 / ABI 5
- Open CASCADE Technology 7.9.0
- Windows x64 / Linux x64

## UI baseline

The product has one **classic CAD shell**:

```text
Menu: File | Draw | Model | View | Window | Language
Single-row Toolbar
Active Tool Parameter Strip (visible only for tools with parameters)

Model Tree | CAD Viewport | Layers / Properties

Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

Rules:

- no Ribbon;
- no grouped three-row command surface;
- no `DensityStyle.Compact` or custom global control skin;
- Button / TextBox / ComboBox / CheckBox / Menu use native Avalonia Fluent styling;
- `CadTheme` contains only CAD layout metrics and product-specific viewport/overlay visuals;
- the custom CAD ColorTable is retained;
- dark viewport, lower-left triedron, ViewCube disabled by default;
- no permanent bottom command input, `Ready`, version text, or permanent coordinate noise;
- the current operation prompt remains visible in the status strip.

## Tool parameters

A Tool `ParameterPanel` is rendered automatically in the active-tool parameter strip, avoiding the previous state where Core had parameters but the UI exposed no editor.

Regular Polygon example:

```text
Regular Polygon: Sides [6]  Mode [Inscribed]
```

- `Sides`: 3..360;
- `Mode`: Inscribed / Circumscribed;
- before the center is specified, a side count can also be typed directly and confirmed with Enter.

The same parameter surface is used by existing parameterized tools such as Box, Cylinder, Cone, Frustum, Sphere, Ellipsoid, Torus, Helix, Ellipse, and Extrude.

## PropertyGrid

PropertyGrid is driven by Core descriptors rather than a second UI property model.

- Property and Value are left aligned;
- numeric values are not right aligned;
- Point / Vector / Normal use vertical engineering input:

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

- planar `Normal` is editable for Circle / Arc / Ellipse / Rectangle / Regular Polygon;
- Layer uses a drop-down selector;
- Color / LineStyle / LineWidth support ByLayer;
- color editing keeps OCCAD's custom CAD ColorTable.

## Initial feature surface

2D: Point, Line, Polyline, Free Polygon, Regular Polygon, Rectangle, Circle, Arc, Ellipse, Spline.

Circle methods: Center+Radius, Center+Diameter, Two Points, Three Points, Point+Center.

Arc methods: Three Points, Center→Start→End, Start→Center→End, Start→End→Center, Start→End→Point, Start→End→Tangent.

Ellipse methods: Center+Axes, Axis Endpoints+Minor Axis.

3D/curves: Box, Cylinder, Cone, Frustum, Sphere, Ellipsoid, Torus, Helix.

Features: Extrude, Revolve, Sweep, Loft.

Views: Top / Bottom / Front / Back / Left / Right, Iso NE/NW/SE/SW, Fit, Wireframe, Shaded.

Interaction foundation: Window/Crossing Selection, Subobject Selection, Preselection, Snap, Grip, XY/YZ/XZ WorkPlane, ORTHO, POLAR, Preview, Tracking, Precision Input.

See [Initial Release Feature Matrix](docs/en-US/14-FEATURE-MATRIX.md) for exact status.

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

## Repository layout

```text
OCCAD/
├─ src/
│  ├─ OCCAD.Core/
│  └─ OCCAD.Avalonia/
├─ docs/
│  ├─ en-US/
│  ├─ zh-CN/
│  └─ adr/
├─ build.ps1 / build.sh
├─ run.ps1 / run.sh
├─ publish.ps1 / publish.sh
└─ OCCAD.sln
```

## Build

Windows:

```powershell
.\build.ps1
```

Linux:

```bash
./build.sh
```

If the Bridge SDK does not include a complete portable runtime, configure `OCCT_ROOT` / `CASROOT` or provide the OCCT path through the run script.

The project currently does not maintain a separate Test project. Validation order is:

`Core build → Avalonia build → real interactive regression → native/transient cleanup checks`

A source file or registered Action alone does not prove product support.

## Documentation

Start at [docs/README.md](docs/README.md). Key contracts include UI, interaction, architecture, entity/tool, extension, transaction/resource, build validation, user guide, and feature matrix documents.

## License

Original OCCAD code uses **OCCAD Non-Commercial License 1.0** from the repository root. Non-commercial use is permitted under its terms; commercial products, paid engineering delivery, services, and use inside a for-profit production/design/engineering workflow require separate written authorization.

OCCT, OcctCSharpBridge, Avalonia, .NET, and other dependencies remain under their own licenses. See `THIRD_PARTY_NOTICES.md`.
