# 02 UI Specification

## 1. Single UI baseline

The OCCAD initial release has one shell only: **classic CAD Menu + single-row Toolbar + active Tool parameter strip**.

Do not restore or run in parallel:

- Ribbon;
- grouped three-row command surface;
- a second Toolbar;
- Floating Tool Panel;
- permanent Dynamic HUD;
- permanent bottom Command Input;
- placeholder commands added only for visual completeness.

The UI exposes only functionality that is registered and reaches a real Core path.

## 2. Main window

```text
MainWindow
├─ Menu
│  ├─ File
│  ├─ Draw
│  ├─ Model
│  ├─ View
│  ├─ Window
│  └─ Language
├─ Single-row Toolbar
├─ Active Tool Parameter Strip   ← visible only when the active Tool has parameters
├─ Workspace
│  ├─ Model Tree
│  ├─ CAD Viewport
│  └─ Layers / Properties
└─ Status Strip
   ├─ Current Operation Prompt
   └─ XY / YZ / XZ / SNAP / ORTHO / POLAR
```

## 3. Theme rules

The application uses Avalonia 12 native `FluentTheme`. `DensityStyle.Compact` is not enabled and OCCAD does not register global skins for Button/TextBox/ComboBox/TreeView.

`CadTheme` may contain only:

- layout metrics for panels/property/status surfaces;
- CAD-scene visuals for Viewport, Overlay, Snap, Grip, etc.;
- colors/separators required by CAD table-style surfaces.

Standard controls should use native Fluent states.

The custom ColorTable remains because it is a CAD business control rather than decorative application theming.

## 4. Menu

Top-level categories are fixed to File, Draw, Model, View, Window, and Language.

Command families with multiple construction methods use submenus and reuse one Tool implementation.

Example:

```text
Draw
├─ Point
├─ Line
├─ Polyline
├─ Free Polygon
├─ Regular Polygon
│  ├─ Inscribed
│  └─ Circumscribed
├─ Rectangle
├─ Circle
├─ Arc
├─ Ellipse
└─ Spline
```

## 5. Toolbar

The Toolbar is one row of frequent commands; it is not a surface for flattening every command.

Requirements:

- single row;
- horizontal scrolling is allowed when width is insufficient;
- no functional icons;
- short text labels;
- native Button controls;
- no custom forced background/border scheme;
- current Layer ComboBox may appear near the end;
- language switching may use Menu or ComboBox but must not maintain separate language state.

## 6. Active Tool parameter strip

This is a required engineering-input surface.

When `CadTool.ParameterPanel` contains parameters, a strip is shown below the Toolbar. When there are no parameters, the strip is hidden completely.

Descriptor mapping:

```text
Integer        → TextBox
Double         → TextBox
OptionalDouble → TextBox
Boolean        → CheckBox
Choice         → ComboBox
String         → TextBox
```

Editors call `tool.TrySetParameter()`; UI code must not mutate Tool internals directly.

### Regular Polygon

```text
Regular Polygon: Sides [6]  Mode [Inscribed]
```

- Sides range: 3..360;
- Mode: Inscribed / Circumscribed;
- before center placement, the side count can also be typed directly and confirmed with Enter;
- after center placement, numeric exact input belongs to the radius/current geometry stage again.

The same mechanism serves existing parameterized tools such as Box, Cylinder, Cone, Frustum, Sphere, Ellipsoid, Torus, Helix, Ellipse, and Extrude.

## 7. Model Tree

- narrow left panel;
- native TextBox filtering;
- native TreeView;
- no decorative entity icons;
- entity types come from `CadEntityRegistry`;
- internal persistence helpers such as `Path` are hidden from normal browsing;
- selection stays synchronized with Workspace Selection;
- initial context menu: Properties, Show/Hide, Lock/Unlock, Rename only.

## 8. Layers

- upper-right panel;
- filter/new/rename/remove use native controls;
- default Layer cannot be renamed or removed;
- current layer, visibility, color, line style, line width, and lock state bind to real Layer state;
- color cells and ColorTable may retain CAD-specific visuals;
- do not reintroduce global `cad-input`, `cad-compact`, or similar styling classes.

## 9. PropertyGrid

PropertyGrid is driven by Core descriptors.

Requirements:

- Property / Value two-column layout;
- labels and values left aligned;
- numeric editors left aligned;
- categories use native `Expander`;
- mixed values are explicit;
- Layer uses ComboBox;
- Boolean uses CheckBox;
- Enum/Choice uses ComboBox;
- Color uses OCCAD ColorTable/Color Dialog;
- Measurement remains read-only.

Point / Vector / Normal use vertical component editing:

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

Do not restore horizontally compressed X/Y/Z editors.

Planar `Normal` remains editable for Circle / Arc / Ellipse / Rectangle / Regular Polygon through the real geometry/transaction path.

## 10. Viewport

- dark by default;
- lower-left triedron retained;
- ViewCube disabled by default;
- Snap / Grip / Preview / Tracking / Selection Window exist only during relevant lifecycles;
- no permanent version, OCCT, Ready, or help watermark;
- Preview/transients must be cleared after Commit/Cancel.

## 11. Status Strip

The bottom strip contains only:

- current operation prompt;
- XY / YZ / XZ;
- SNAP;
- ORTHO;
- POLAR.

The prompt region may be blank while idle.

Do not permanently show Ready, Command: Ready, version text, permanent XYZ coordinates, duplicated WorkPlane/Drafting captions, or another command input.

## 12. Dialogs

Dialogs use native Fluent Button/TextBox/ComboBox controls. OCCAD should not maintain a separate decorative MessageBox skin system.

CAD business controls such as the ColorTable may keep their required custom visuals.

## 13. Localization

- supported languages: `zh-CN` and `en-US`;
- Action/Tool IDs never change with language;
- UI text uses localization keys;
- language changes refresh Menu, Toolbar, Tool parameter strip, panels, and operation prompt;
- the preference is persisted.

## 14. UI acceptance

A feature is complete only when all of the following are true:

1. Entity / Tool / Action registration is consistent;
2. Menu/Toolbar entry works;
3. required parameters have a visible input surface;
4. prompt is clear;
5. preview is correct;
6. exact input works;
7. Commit / Cancel returns interaction to neutral;
8. Property editing works;
9. Snap / Grip / Selection behave correctly;
10. Save/Open restores the result;
11. no native transient remains;
12. actual Windows/Linux build and manual interaction validation succeed.

Source existence or Action registration alone does not prove product completion.
