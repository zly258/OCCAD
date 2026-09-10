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

The product keeps one **classic CAD shell**:

```text
Menu: File | Draw | Model | View | Window | Language
Single-row Toolbar

Model Tree | CAD Viewport | Layers / Properties

Fixed Tool Parameter Strip
Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

Rules:

- no Ribbon and no legacy CleanShell/Ribbon compatibility layer;
- no grouped three-row command surface;
- Button / TextBox / ComboBox / CheckBox / Menu use native Avalonia Fluent styling;
- `CadTheme` contains layout metrics and CAD product colors/sizes only, not a second global control skin;
- the Tool parameter strip is fixed directly above the status bar, so parameter visibility does not resize the viewport;
- Settings label columns use shared content-based sizing while value columns consume remaining width;
- dialog action captions are horizontally and vertically centered;
- the custom CAD ColorTable is retained;
- dark viewport, lower-left triedron, ViewCube disabled by default;
- no permanent bottom command input, `Ready`, version text, or permanent coordinate noise;
- the current operation prompt remains visible in the status strip.

## Tool parameters and live values

A Tool `ParameterPanel` is rendered automatically in the **fixed bottom parameter strip**. The strip is a projection of current Tool state and does not maintain a second business-state model.

Regular Polygon example:

```text
Regular Polygon: Sides [6]  Mode [Inscribed]
```

- `Sides`: 3..360;
- `Mode`: Inscribed / Circumscribed;
- before the center is specified, a side count can also be typed directly and confirmed with Enter.

The same parameter surface is used by parameterized tools such as Box, Cylinder, Cone, Frustum, Sphere, Ellipsoid, Torus, Helix, Ellipse, and Extrude.

Parameter editors are bidirectional: user edits are committed to the active Tool, while common dimensional previews feed their current values back to the strip. Current live dimensions include Circle Radius/Diameter, Rectangle Width/Height, Ellipse Major/Minor Radius, Box Length/Width/Height, and Cylinder Radius/Height. A focused editor is never overwritten by live refresh.

## Escape and work planes

`Esc` is the unified CAD cancel operation:

`cancel active Tool → clear Entity/Subobject/Preselection → clear Snap/Tracking → return focus to Viewport`

It is intended to work whether focus is in the viewport or in a Tool parameter editor.

XY / YZ / XZ represent the current drawing work plane. When a plane change is requested while drawing:

- if the current stage permits the change, the new preset is applied immediately and preview is refreshed;
- if the Tool is in a fixed temporary construction-plane stage, Core first steps back to the nearest stage that permits a plane change, then applies the requested preset;
- a user-plane lock or fixed Grip plane still blocks the change;
- planar Tools that cache basis vectors, including Circle, Arc, Rectangle, Ellipse, and Regular Polygon, must refresh that cache so UI state and generated geometry cannot diverge.

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

Core does not depend on Avalonia. The UI adapts input and presents state; it does not duplicate Document, Selection, History, Layer, Geometry, or Tool business state. Safe work-plane rollback, Tool lifecycle, transactions, and resource ownership belong in Core rather than being reimplemented independently by each UI entry point.

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

## Build and validation

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

`static inspection → Core build → Avalonia build → real interactive regression → native/transient cleanup checks`

Manual regression must cover at least:

- no viewport height jump when parameterized Tools start or cancel;
- parameter edits and live preview values remain synchronized without overwriting an active editor;
- XY/YZ/XZ behaves consistently in 2D Tools and staged 3D Tools;
- `Esc` cancels the command and clears all formal selection state;
- long Chinese/English Settings labels are not clipped and dialog button captions are centered;
- 125% / 150% DPI layouts remain usable;
- preview, snap, tracking, and other transient state is gone after Tool completion/cancelation.

A source file or registered Action alone does not prove product support, and build success must not be claimed without real build output.

## Documentation

Start at [docs/README.md](docs/README.md). Key contracts include UI, interaction, architecture, entity/tool, extension, transaction/resource, build validation, user guide, and feature matrix documents.

## License

Original OCCAD code uses **OCCAD Non-Commercial License 1.0** from the repository root. Non-commercial use is permitted under its terms; commercial products, paid engineering delivery, services, and use inside a for-profit production/design/engineering workflow require separate written authorization.

OCCT, OcctCSharpBridge, Avalonia, .NET, and other dependencies remain under their own licenses. See `THIRD_PARTY_NOTICES.md`.
