# 13 User Guide

## 1. Main window

OCCAD uses a classic CAD shell:

```text
Menu
Toolbar Row 1
Toolbar Row 2

Model Tree | Viewport | Layers / Properties

Fixed Tool Parameter Strip
Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

The toolbar uses no more than two compact rows and contains only frequent commands. The complete command set remains available from Menu.

### Row 1

```text
Undo Redo | Current Layer | Top Front Right Iso Fill | Wireframe Shaded
```

### Row 2

```text
Line Polyline Rectangle Circle Arc Polygon | Box Cylinder Sphere Extrude Revolve
```

File operations and language switching stay in Menu.

## 2. Undo and Redo

- Toolbar: Undo / Redo;
- `Ctrl+Z`: Undo;
- `Ctrl+Y` or `Ctrl+Shift+Z`: Redo;
- while a Tool is active, history commands are disabled until the command is completed or cancelled.

Button state follows `CadHistory.CanUndo/CanRedo`.

## 3. Views

Frequent toolbar entries are Top, Front, Right, Iso, Fill, Wireframe, and Shaded.

`Fill` maps to internal action `view.fit` and fits the current model to the visible viewport. The complete orthographic and four-corner isometric set remains in the View menu.

## 4. Tool parameter strip

The parameter strip is fixed immediately above the status strip and keeps stable height, so starting or leaving a Tool never changes viewport height.

When a Tool exposes parameters, real editors appear. With no parameters, the row stays present but empty.

Regular Polygon example:

```text
Regular Polygon: Sides [6]  Mode [Inscribed]
```

Sides range is 3..360. Before center placement, the side count may also be typed directly and confirmed with Enter.

Live synchronization rules:

- parameter editor changes write back to the active Tool;
- observable Tool/Preview dimensions may refresh displayed values;
- focused TextBox/ComboBox/CheckBox editors are not overwritten by live refresh.

## 5. 2D drafting

Menu keeps the complete initial 2D set: Point, Line, Polyline, Free Polygon, Regular Polygon, Rectangle, Circle, Arc, Ellipse, and Spline.

Toolbar keeps frequent 2D commands only: Line, Polyline, Rectangle, Circle, Arc, Regular Polygon.

### Free Polygon

Specify arbitrary vertices point by point.

### Regular Polygon

Specify:

1. side count;
2. Inscribed/Circumscribed mode;
3. center;
4. radius.

## 6. 3D and Features

Menu keeps Box, Cylinder, Cone, Frustum, Sphere, Ellipsoid, Torus, Helix, Extrude, Revolve, Sweep, and Loft.

Toolbar keeps frequent commands only: Box, Cylinder, Sphere, Extrude, Revolve.

## 7. Work plane

Bottom XY / YZ / XZ controls the real drafting work plane.

When switching during a Tool:

- if the current stage allows it, switch immediately and refresh Snap/Tracking/Preview;
- if the Tool owns a fixed temporary ToolPlane, step back to the nearest safe stage first, then switch;
- already confirmed geometry is never reinterpreted onto a different plane.

## 8. Escape / Backspace / Enter

- `Esc`: cancel the active Tool and clear Entity / Subobject / Preselection state;
- `Backspace`: step back the active Tool stage;
- `Enter/Space`: submit the current valid stage or finish;
- Preview, Snap, Tracking, and other transient state must clear after Commit/Cancel.

## 9. PropertyGrid

Properties are driven by Core descriptors.

- numeric editors are left aligned;
- Layer uses ComboBox;
- Boolean uses CheckBox;
- Enum/Choice use ComboBox;
- Color uses OCCAD's custom ColorTable;
- Measurement is read-only.

Point / Vector / Normal use vertical input:

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

## 10. Layers

The Layers panel supports filtering, add, rename, removal of non-default layers, current layer, visibility, color, line style, line width, and locking.

Line Style and Line Width columns are widened so their ComboBox values remain readable; the right engineering panel is wider than the old layout.

Default Layer `0` cannot be renamed or removed.

## 11. SNAP / ORTHO / POLAR

- SNAP: object snap;
- ORTHO: orthogonal tracking;
- POLAR: polar tracking.

Snap, Grip, Preview, Tracking, and Selection Window are transient and are not serialized into Document/History.

## 12. Save and Open

Save/Open restores formal Document / Entity / Layer / Feature state. Preview, snap markers, grip markers, tracking guides, and other transient state are not persisted.

## 13. Definition of complete

Source existence or Action registration is not enough. Validate the real chain:

`entry → parameters → prompt → preview → exact input → commit → property → grip/snap → save/open`

and verify that Commit/Cancel leaves no Preview or native transient residue.
