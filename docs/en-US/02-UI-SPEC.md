# 02 UI Specification

## 1. Single UI baseline

The OCCAD initial release has one shell only: **classic CAD Menu + up to two compact Toolbar rows + a fixed bottom Tool parameter strip**.

Do not restore or run in parallel:

- Ribbon;
- grouped three-row command surface;
- a second Toolbar;
- Floating Tool Panel;
- permanent Dynamic HUD;
- permanent bottom Command Input;
- placeholder commands added only for visual completeness.

The UI exposes only functionality that reaches a real Core path.

## 2. Main window

```text
MainWindow
├─ Menu: File | Draw | Model | View | Window | Language
├─ Toolbar Row 1
│  └─ Undo / Redo | Layer | Views / Fill | Display mode
├─ Toolbar Row 2
│  └─ frequent 2D | frequent 3D / Feature
├─ Workspace
│  ├─ Model Tree
│  ├─ CAD Viewport
│  └─ Layers / Properties
├─ Fixed Active Tool Parameter Strip
└─ Status Strip
   ├─ Current Operation Prompt
   └─ XY / YZ / XZ / SNAP / ORTHO / POLAR
```

The toolbar may use one or two rows. The current baseline uses two compact high-frequency rows. The Tool parameter strip stays immediately above the status strip with stable height so the viewport does not move when tools change.

## 3. Theme rules

The application uses Avalonia 12 native `FluentTheme`. `DensityStyle.Compact` is not enabled and OCCAD does not register global Button/TextBox/ComboBox/TreeView skins.

`CadTheme` may contain panel/property/status layout metrics, CAD-scene visuals for Viewport/Overlay/Snap/Grip, and colors or separators needed by CAD table-style surfaces.

The custom ColorTable remains because it is a CAD business control.

## 4. Menu

Top-level categories are File, Draw, Model, View, Window, and Language.

Command families with multiple construction methods use submenus and reuse shared Tool implementations. File operations and language switching remain in Menu rather than consuming high-frequency toolbar space.

## 5. Toolbar

The toolbar contains only frequent commands.

Current recommended layout:

```text
Row 1
Undo Redo | Current Layer | Top Front Right Iso Fill | Wireframe Shaded

Row 2
Line Polyline Rectangle Circle Arc Polygon | Box Cylinder Sphere Extrude Revolve
```

Requirements:

- no more than two rows;
- horizontal scrolling is allowed per row when width is insufficient;
- no functional icons;
- short text labels;
- native Button / ComboBox controls;
- do not restore New/Open/Save/Language as toolbar buttons;
- Undo/Redo track `CadHistory.CanUndo/CanRedo`;
- Undo/Redo are disabled while a Tool is active;
- `view.fit` is presented to users as `Fill`.

## 6. Active Tool parameter strip

The strip is fixed above the bottom status strip. When the active Tool has no parameters, the row remains but its contents are empty, preventing viewport height changes.

Descriptor mapping:

```text
Integer        → TextBox
Double         → TextBox
OptionalDouble → TextBox
Boolean        → CheckBox
Choice         → ComboBox
String         → TextBox
```

Editors call `tool.TrySetParameter()`; UI code does not mutate Tool internals directly.

Live parameter rules:

- editor changes write back to the active Tool;
- observable Tool/Preview dimensions may refresh displayed values;
- focused editors are never overwritten by live refresh;
- Tool and language changes rebind the strip to current state.

Regular Polygon example:

```text
Regular Polygon: Sides [6]  Mode [Inscribed]
```

Sides range is 3..360. Before center placement, a side count may also be typed directly and confirmed with Enter.

## 7. Model Tree

Use a native TextBox filter and TreeView, no decorative entity icons, entity types from `CadEntityRegistry`, hidden internal persistence helpers such as `Path`, and synchronization with Workspace Selection.

## 8. Layers

- filter/new/rename/remove use native controls;
- default Layer cannot be renamed or removed;
- current layer, visibility, color, line style, line width, and lock state bind to real Layer state;
- Line Style and Line Width columns must be wide enough to display ComboBox content rather than a single clipped character;
- the right engineering panel is widened accordingly while Name continues to consume remaining width;
- color cells and ColorTable may retain CAD-specific visuals.

## 9. PropertyGrid

PropertyGrid is driven by Core descriptors. Labels and values are left aligned; categories use native `Expander`; Layer uses ComboBox; Boolean uses CheckBox; Enum/Choice use ComboBox; Color uses OCCAD ColorTable/Color Dialog; Measurement stays read-only.

Point / Vector / Normal use vertical component editing:

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

## 10. Viewport and views

Dark viewport by default, lower-left triedron retained, ViewCube disabled by default, transient CAD visuals only during their lifecycle, `view.fit` presented as Fill, and Preview/transients cleared after Commit/Cancel.

## 11. Status strip

The bottom strip contains only the current operation prompt plus XY / YZ / XZ / SNAP / ORTHO / POLAR. Do not permanently show Ready, version text, permanent coordinates, duplicated WorkPlane/Drafting captions, or another command input.

## 12. Dialogs

Dialogs use native Fluent controls. Button content is centered horizontally and vertically. Settings-style forms use an auto-sized shared label column plus a value column that consumes remaining width.

## 13. Localization

Support `zh-CN` and `en-US`; Action/Tool IDs never change with language; language changes refresh Menu, Toolbar, parameter strip, panels, and status prompt; preference is persisted.

## 14. UI acceptance

A feature is complete only when its entry is real, required parameters are visible, Prompt/Preview/Exact Input work, Commit/Cancel return to neutral, Property editing works, Snap/Grip/Selection behave correctly, Save/Open restores formal state, no native transient remains, and actual Windows/Linux builds plus manual interaction validation succeed.
