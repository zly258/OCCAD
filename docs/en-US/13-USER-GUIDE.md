# 13 User Guide

## 1. Scope

This guide covers the OCCAD initial desktop release: build/run, shell, drafting, tool parameters, properties, layers, snap, grips, views, and persistence.

Use [14 Feature Matrix](14-FEATURE-MATRIX.md) as the exact support reference.

## 2. Build and run

Windows:

```powershell
.\build.ps1
```

Linux:

```bash
./build.sh
```

If the Bridge SDK does not provide a complete portable runtime, configure `OCCT_ROOT` / `CASROOT` or the OCCT path required by the run script.

## 3. Main shell

The application has one classic CAD shell:

```text
Menu
Single-row Toolbar
Active Tool Parameter Strip

Model Tree | Viewport | Layers / Properties

Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

Ribbon and grouped three-row command surfaces are not part of the product baseline.

## 4. Menu

### File

New, Open, Save, Save As, Settings, Exit.

### Draw

Point, Line, Polyline, Free Polygon, Regular Polygon, Rectangle, Circle, Arc, Ellipse, Spline.

Circle / Arc / Ellipse / Regular Polygon construction variants are selected from submenus.

### Model

Box, Cylinder, Cone, Frustum, Sphere, Ellipsoid, Torus, Helix, Extrude, Revolve, Sweep, Loft.

### View

Top / Bottom / Front / Back / Left / Right, Iso NE/NW/SE/SW, Fit, Wireframe, Shaded.

### Window

Controls visibility of Model Tree, Layers, and Properties.

### Language

Switches `中文 / English`. Menu, Toolbar, parameter strip, panels, and prompts refresh and the preference is persisted.

## 5. Toolbar

The Toolbar is a single row of frequent commands. It may scroll horizontally when width is insufficient.

The current Layer ComboBox is available from the top command area for choosing the creation layer.

## 6. Tool parameter strip

A Tool with `ParameterPanel` descriptors gets a visible parameter strip below the Toolbar. Tools without parameters hide the strip completely.

Regular Polygon example:

```text
Regular Polygon: Sides [6]  Mode [Inscribed]
```

Sides accepts 3..360.

Before placing the center, a side count can also be entered directly in the viewport and confirmed with Enter:

```text
Start Regular Polygon
→ type 8
→ Enter
→ place center
→ place radius
```

This creates an octagon.

Existing parameterized tools such as Box, Cylinder, Cone, Frustum, Sphere, Ellipsoid, Torus, Helix, Ellipse, and Extrude use the same mechanism.

## 7. Free Polygon vs Regular Polygon

Free Polygon is built by placing arbitrary vertices.

Regular Polygon uses:

1. side count;
2. Inscribed/Circumscribed mode;
3. center;
4. radius.

They are intentionally different tool semantics.

## 8. Circle

Supported construction methods:

- Center + Radius;
- Center + Diameter;
- Two Points;
- Three Points;
- Point + Center.

These entries reuse one Circle Tool.

## 9. Arc

Supported methods:

- Three Points;
- Center → Start → End;
- Start → Center → End;
- Start → End → Center;
- Start → End → Point;
- Start → End → Tangent.

## 10. Ellipse

Supported methods:

- Center + Axes;
- Axis Endpoints + Minor Axis.

## 11. Exact input

Exact input is interpreted by the active Tool stage. Numeric meaning is not globally fixed; it follows the current Tool prompt/precision state.

For example, Regular Polygon accepts an integer side count before center placement, then uses numeric input for radius/length-angle geometry after center placement.

## 12. PropertyGrid

Selecting entities populates Properties from Core descriptors.

Rules:

- numeric values are left aligned;
- Layer uses a drop-down;
- Boolean uses CheckBox;
- Enum/Choice uses ComboBox;
- Color uses the CAD ColorTable;
- Measurement remains read-only;
- categories are collapsible.

Point / Vector / Normal use vertical component editing:

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

`Normal` is editable for Circle / Arc / Ellipse / Rectangle / Regular Polygon through the real geometry and Undo/Redo transaction path.

## 13. Layers

Layers supports filtering, creation, rename, removal of non-default layers, current layer, visibility, color, line style, line width, and lock state.

Default Layer `0` cannot be renamed or removed.

## 14. Work plane

The bottom strip provides XY / YZ / XZ work-plane switches.

If an active Tool does not allow a plane change, OCCAD rejects the switch rather than changing geometric interpretation mid-tool.

## 15. SNAP / ORTHO / POLAR

The status strip provides real interaction toggles:

- SNAP: object snap;
- ORTHO: orthogonal tracking;
- POLAR: polar tracking.

SNAP configuration includes Endpoint, Midpoint, Intersection, Center, Perpendicular, Tangent, Quadrant, Extension, Insertion, Node, Apparent Intersection, and Nearest.

## 16. Grip and selection

Initial infrastructure includes Replace / Add / Remove / Toggle, Window / Crossing Selection, Entity/Subobject Selection, Preselection, Grip / Hot Grip, and Grip Edit Preview/Commit.

Grip, Snap, Preview, Tracking, and Selection Window graphics are transient and do not enter Document/History.

## 17. Status strip

The left side shows the current operation prompt; the right side contains XY/YZ/XZ and SNAP/ORTHO/POLAR.

The prompt region may be blank while idle.

OCCAD does not permanently show Ready, Command: Ready, version text, XYZ coordinates, or a second command input.

## 18. Save/Open

Save and reopen should restore formal Document/Entity/Layer/Feature state.

Preview, snap markers, grip markers, and tracking guides must never be persisted.

## 19. Definition of complete

A feature is not complete merely because an Entity/Tool/Action exists in source. Validate the real path:

`Entry → Parameters → Prompt → Preview → Exact Input → Commit → Property → Grip/Snap → Save/Open`

Commit/Cancel must also leave no preview or native transient residue.
