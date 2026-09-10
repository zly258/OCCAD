# 13 User Guide

This guide is for OCCAD initial-release users. It follows the real workflow from launch to drafting, editing, properties, history, and persistence. The authoritative support boundary is [14-FEATURE-MATRIX.md](14-FEATURE-MATRIX.md).

## 1. Launch and runtime

### Windows

Development build:

```powershell
.\build.ps1
.\run.ps1
```

When a flat Bridge SDK is used and the OCCT runtime is external:

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

The `run.ps1` included in a published package can launch OCCAD directly from that package directory.

### Linux

```bash
./build.sh
./run.sh
```

Linux uses the Bridge portable runtime. The application must keep its `runtime/` and `occt/` directories beside the executable output.

## 2. Main window

OCCAD uses one classic CAD shell and does not use a Ribbon:

```text
Menu
Toolbar Row 1
Toolbar Row 2

Model Tree | CAD Viewport | Layers / Properties

Fixed Tool Parameter Strip
Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

Toolbar row 1:

```text
Undo Redo | Current Layer | Top Front Right Iso Fit | Wireframe Shaded
```

Toolbar row 2:

```text
Line Polyline Rectangle Circle Arc Regular Polygon | Move Delete |
Box Cylinder Sphere Extrude Revolve
```

The toolbar contains only high-frequency commands. Full drafting, modeling, and view command sets remain in the Menu. File operations and language switching are not duplicated in the toolbar.

## 3. Standard operation loop

Example: create and edit a circle.

1. Choose Circle → Center + Radius.
2. Pick the center in the viewport.
3. Move the pointer and inspect Preview.
4. Pick the radius or enter an exact value.
5. The Tool completes and Preview/Snap/Tracking clear.
6. Select the circle.
7. Edit radius, layer, color, or Normal from Properties.
8. Use a Grip for geometry editing.
9. Use `Ctrl+Z` to undo and `Ctrl+Y` to redo.
10. Save `.ocad` and reopen it to verify formal state.

The initial product is designed around:

`Create → Preview → Exact Input → Commit → Property → Grip → Undo/Redo`

## 4. Tool controls

A Tool is a staged interaction state machine. The bottom Operation Prompt describes the required input for the current stage.

Common controls:

- `Esc`: cancel the active Tool and clear formal selection and Tool transient state;
- `Backspace`: step back one Tool stage;
- `Enter` / `Space`: accept the current stage or finish when allowed;
- right click: execute the Tool secondary action, typically accept, finish, or cancel according to the current stage;
- `Tab`: cycle to the next Snap candidate;
- `Shift+Tab`: cycle to the previous Snap candidate.

After Commit or Cancel, ToolPreview, Snap, Tracking, GripDrag, and other Tool-owned transient state must no longer remain visible.

## 5. Tool parameter strip

The parameter strip is fixed directly above the status strip. Its height remains stable so the viewport does not jump when tools start or stop.

Regular Polygon example:

```text
Regular Polygon: Sides [6]  Mode [Inscribed]
```

Editor mapping:

- Integer / Double / OptionalDouble → TextBox;
- Boolean → CheckBox;
- Choice → ComboBox;
- String → TextBox.

Editors update the active Tool through `TrySetParameter()`. Observable Preview dimensions can refresh displayed values, while a focused TextBox/ComboBox/CheckBox is not rebuilt or overwritten.

## 6. Exact input

OCCAD does not expose a permanent Command Line, but when the viewport is focused and an active Tool is waiting for input, values can be typed directly.

Common forms:

```text
100
100,50
100,50,20
@20,0
@50<30
L 100
A 45
ANGLE 45
F 2
Radius=50
```

Accepted forms depend on the current Tool and Stage.

- `X,Y[,Z]`: coordinate input;
- `@...`: relative coordinate input;
- polar-style point input can use `<`;
- `L` / `LENGTH`: length;
- `A` / `ANGLE`: angle;
- `F` / `FACTOR`: factor;
- `parameter=value`: set a Tool parameter directly;
- enter `O` during a point-input stage to begin offset-point input.

Rejected exact input must not create a history entry or mutate the formal Entity.

## 7. 2D drafting

The Draw menu currently provides:

- Point;
- Line;
- Polyline;
- Free Polygon;
- Regular Polygon;
- Rectangle;
- Circle;
- Arc;
- Ellipse;
- Spline.

### 7.1 Line

Pick the first and second point. Preview is visible before the second point is committed and can be combined with SNAP, ORTHO, POLAR, and exact length input.

### 7.2 Polyline

Pick consecutive points. Once the Tool can finish, use Enter/Space/right click to finish. Backspace removes the previous stage.

### 7.3 Free Polygon

Pick arbitrary vertices. Finish only after the Tool has the minimum valid vertex count.

### 7.4 Regular Polygon

Workflow:

1. choose `Sides`, range 3..360;
2. choose `Mode`: Inscribed / Circumscribed;
3. pick center;
4. pick radius.

Before picking the center, the side count can also be typed directly and confirmed with Enter.

### 7.5 Rectangle

Pick the first corner and opposite corner. Width/Height can synchronize to the parameter strip while preview is observable.

### 7.6 Circle

Supported methods:

- Center + Radius;
- Center + Diameter;
- Two Points;
- Three Points;
- Point + Center.

The toolbar Circle command uses Center + Radius by default. Other variants are available from the Draw menu.

### 7.7 Arc

Supported methods:

- Three Points;
- Center → Start → End;
- Start → Center → End;
- Start → End → Center;
- Start → End → Point;
- Start → End → Tangent.

The toolbar Arc command uses Three Points by default.

### 7.8 Ellipse

Supported methods:

- Center + Axes;
- Axis Endpoints + Minor Axis.

### 7.9 Spline

Pick fit points continuously. Finish with Enter/Space/right click after the Tool reaches a valid finish state.

## 8. 3D primitives and curve

The Model menu provides:

- Box;
- Cylinder;
- Cone;
- Frustum;
- Sphere;
- Ellipsoid;
- Torus;
- Helix.

These tools use staged base-point, size, direction, and height input, with exposed parameters shown in the fixed parameter strip.

The toolbar keeps only the frequent Box, Cylinder, and Sphere entries.

## 9. Modeling features

Current feature Tools:

- Extrude;
- Revolve;
- Sweep;
- Loft.

Features depend on existing Document entities/profiles. Follow the Operation Prompt for selection and geometry stages. Invalid source geometry must fail without polluting Document or History.

## 10. Selection

OCCAD distinguishes:

- Entity Selection: formal entity selection;
- Subobject Selection: Edge/Face/Vertex selection;
- Preselection: pointer hover;
- Window/Crossing: rectangle selection.

Formal selection supports Replace / Add / Remove / Toggle semantics.

The Core Selection state is authoritative. Viewer highlighting is derived presentation, not a separate source of truth.

## 11. Modify

The current Classic Shell officially exposes:

### Move

1. start Move;
2. select one or more Entities;
3. confirm selection with Enter/right click;
4. pick the base point;
5. move the pointer and inspect replacement Preview;
6. pick the target point;
7. the formal Entities commit atomically;
8. one Undo restores the complete move.

### Delete

Select Entities and execute Delete. Deletion is a formal undoable modification.

### Not exposed yet

Copy / Rotate / Scale / Mirror have transaction-level Core APIs but no completed and validated dedicated interactive Tools, so they are not part of the current Shell product surface.

## 12. Grip editing

Selecting an Entity that exposes Grips displays Grip markers.

Visual rules:

- normal Grip: filled rectangle;
- hot/moving state: circle;
- drag marker: circle.

Grip workflow:

1. click a Grip;
2. enter GripEditTool;
3. pointer movement changes only duplicate/preview state;
4. confirm to mutate the formal Entity;
5. invalid input must not pollute the formal Entity;
6. Commit/Cancel must clear GripDrag transient state.

Grip size and hit tolerance are configurable in Settings.

## 13. SNAP

SNAP resolves precise candidates from model geometry.

Current infrastructure supports:

- Endpoint;
- Midpoint;
- Center;
- Vertex;
- Quadrant;
- Nearest;
- Intersection;
- Perpendicular;
- Tangent;
- Apparent Intersection;
- Extension;
- Insertion;
- Node.

Keyboard:

- `F3`: toggle SNAP;
- `Tab`: next candidate;
- `Shift+Tab`: previous candidate.

Snap markers are transient and are not persisted or recorded in History.

## 14. ORTHO / POLAR

- `F8`: toggle ORTHO;
- `F10`: toggle POLAR.

ORTHO constrains orthogonal directions. POLAR tracks against the configured polar increment. Toggling either mode clears/recomputes the current Tracking transient state.

## 15. Work plane

The bottom XY / YZ / XZ controls represent the real drafting work plane, not a visual-only state.

Keyboard:

- `T`: XY;
- `S`: YZ;
- `F`: XZ.

If the current Tool stage allows a plane change, the WorkPlane changes immediately and Preview/Snap/Tracking refresh. If a fixed temporary ToolPlane owns the current stage, Core first attempts a safe StepBack. Previously accepted geometry is not reinterpreted onto the new plane.

## 16. Viewport navigation and views

Fixed views:

- Top / Bottom;
- Front / Back;
- Left / Right;
- Iso NE / NW / SE / SW;
- Fit;
- Wireframe;
- Shaded.

Common navigation behavior:

- middle-button navigation is handled by the viewport;
- `Shift + middle button`: rotate;
- middle-button double click: Fit All;
- ViewCube is disabled by default;
- lower-left Triedron remains visible.

## 17. Properties

Selecting an Entity shows Core descriptor-based properties in the right-side Properties panel.

Editor rules:

- Numeric: left-aligned TextBox;
- Layer: ComboBox;
- Boolean: CheckBox;
- Enum/Choice: ComboBox;
- Color: OCCAD CAD ColorTable;
- Measurement: read-only;
- Point / Vector / Normal: three-row X/Y/Z editor.

Editable planar `Normal` currently applies to Circle / Arc / Ellipse / Rectangle / Regular Polygon.

Property changes must use the unified transaction/history/presentation path; changing UI state alone is not a valid model edit.

## 18. Layers

The Layers panel supports:

- filtering;
- create;
- rename;
- delete non-default layers;
- set current layer;
- Visible;
- Locked;
- Color;
- LineStyle;
- LineWidth.

Rules:

- default Layer `0` cannot be removed or renamed;
- the top Current Layer ComboBox and Layers panel must stay synchronized;
- ByLayer entities resolve effective color/line style/line width from their Layer;
- Core/Document owns layer-to-presentation synchronization.

## 19. Undo / Redo

- `Ctrl+Z`: Undo;
- `Ctrl+Y` / `Ctrl+Shift+Z`: Redo;
- toolbar buttons provide the same operations.

History entry points go through `CadWorkspace.Undo()` / `CadWorkspace.Redo()`, which clears active Tool, Entity/Subobject/Preselection, and Grip state before changing history.

One normal user operation should produce one Undo unit.

## 20. Settings

Current scene/interaction settings include:

- Scene Background;
- Display Deviation Coefficient;
- Display Deviation Angle;
- Grip Size;
- Grip Tolerance;
- Snap Marker Size;
- Snap Marker Color;
- Snap Tolerance;
- Selection Tolerance;
- Zoom Sensitivity;
- Language;
- Snap Modes;
- ORTHO / POLAR;
- WorkPlane preset.

The settings UI must remain readable at 125% / 150% DPI and must not depend on fixed Chinese-label widths.

## 21. Save and Open

OCCAD's own document persistence stores formal state such as:

- Document Entities;
- Geometry / Placement;
- Layer;
- Appearance;
- Feature persistence data.

It does not store:

- Preview;
- Snap markers;
- Grip markers;
- Tracking guides;
- Selection Window;
- Preselection and other transient state.

Recommended release regression:

```text
Create mixed 2D/3D
→ change Layer/Properties
→ Save
→ New/close
→ Open
→ compare geometry/properties/presentation
```

## 22. Language

OCCAD supports explicit Chinese / English switching. Action/Tool/Entity identities use stable IDs; localized text is presentation only.

Menu, Toolbar, Properties, Tool parameter strip, Prompt, and Dialog surfaces should refresh together after language switching.

## 23. Shortcut reference

| Shortcut | Function |
|---|---|
| Esc | Cancel active Tool / clear current interaction |
| Backspace | Tool StepBack |
| Enter / Space | Accept stage or finish |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+Shift+Z | Redo |
| F3 | SNAP |
| F8 | ORTHO |
| F10 | POLAR |
| Tab / Shift+Tab | Cycle Snap candidates |
| T | XY WorkPlane |
| S | YZ WorkPlane |
| F | XZ WorkPlane |
| Shift+middle button | Rotate |
| Middle-button double click | Fit All |

## 24. Current product boundary

The initial product does not claim the following as completed user-facing features:

- interactive Copy / Rotate / Scale / Mirror Tools;
- Array;
- Offset;
- Trim / Extend;
- Fillet / Chamfer;
- Text / Dimension / Annotation;
- external-format Import/Export directions that have not been individually validated.

The existence of a geometry helper, Entity, or transaction API in source does not mean the feature is officially exposed.

## 25. Troubleshooting

### Preview residue

Press Esc first. If a visible object remains, record:

- Tool name;
- Stage;
- Commit or Cancel path;
- whether Snap/Grip/Tracking also remains;
- native cleanup diagnostics from the log.

### Tool does not finish

Check whether the Operation Prompt still requires a point, selection, or parameter. Try Enter, right click, or Backspace rather than repeatedly starting the same Tool.

### Layer/property presentation mismatch

Formal Entity/Layer Core state is authoritative. A mismatch between Properties and Viewport is a presentation synchronization defect, not a reason to create a second UI-side model state.

### Startup failure

Use `run.ps1` on Windows and `run.sh` on Linux. Verify Bridge SDK, portable runtime, OCCT resources, and logs first; do not solve startup issues by running OCCAD as Administrator/root.

## 26. Definition of a completed feature

A feature is product-complete only after the real chain succeeds:

`entry → parameters → prompt → preview → exact input → commit → property → grip/snap → undo/redo → save/open`

Commit/Cancel must return the interaction system to neutral state with no Preview or native transient residue.
