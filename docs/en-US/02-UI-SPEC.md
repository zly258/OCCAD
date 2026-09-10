# 02 UI Specification

## 1. Single UI baseline

The OCCAD initial release has one shell only: **classic CAD Menu + single-row Toolbar + fixed bottom Tool parameter strip + status strip**.

Do not restore or run in parallel:

- Ribbon;
- legacy CleanShell/Ribbon compatibility layers;
- grouped three-row command surfaces;
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
├─ Workspace
│  ├─ Model Tree
│  ├─ CAD Viewport
│  └─ Layers / Properties
├─ Fixed Tool Parameter Strip   ← fixed height; content follows active Tool
└─ Status Strip
   ├─ Current Operation Prompt
   └─ XY / YZ / XZ / SNAP / ORTHO / POLAR
```

The Tool parameter strip is docked immediately above the status strip. Its row height does not change when commands start, change stage, or finish, so the viewport does not jump vertically.

## 3. Theme rules

The application uses Avalonia 12 native `FluentTheme`. OCCAD does not maintain a second global control skin.

`CadTheme` may contain only:

- layout metrics for panel/property/status surfaces;
- CAD-scene colors required by Viewport, Overlay, Snap, Grip, etc.;
- borders/spacing required by CAD table-style surfaces.

Standard Button / TextBox / ComboBox / CheckBox / Menu controls use native Fluent states. Historical global classes such as `cad-input`, `cad-compact`, and `cad-primary` must not be reintroduced.

The custom ColorTable remains because it is a CAD business control rather than decorative application theming.

## 4. Menu

Top-level categories are File, Draw, Model, View, Window, and Language.

Command families with multiple construction methods use submenus and reuse one Tool implementation rather than duplicating geometry logic.

## 5. Toolbar

The Toolbar is one row of frequent commands; it is not a surface for flattening every command.

Requirements:

- single row;
- horizontal scrolling when width is insufficient;
- no functional icons in the current phase;
- short text labels;
- native Button controls;
- no forced custom background/border scheme;
- current Layer ComboBox may appear near the end;
- language switching may use Menu or ComboBox but must not maintain separate language state.

## 6. Fixed Tool parameter strip

`CadTool.ParameterPanel` is the single parameter source. The surface is docked at the bottom rather than repeatedly inserted into the top shell.

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

Parameter synchronization is bidirectional:

1. **UI → Tool**: confirmed edits update the active Tool and refresh preview;
2. **Tool/Preview → UI**: measurable preview values update the editor, except an editor that currently owns focus is never overwritten.

Live dimensions that must remain covered include:

- Circle: Radius / Diameter;
- Rectangle: Width / Height;
- Ellipse: Major Radius / Minor Radius;
- Box: Length / Width / Height;
- Cylinder: Radius / Height.

The strip must never resize the viewport as Tool parameter availability changes.

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
- color cells and ColorTable may retain CAD-specific visuals.

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

Planar `Normal` remains editable for Circle / Arc / Ellipse / Rectangle / Regular Polygon through the real geometry/transaction path.

## 10. Viewport

- dark by default;
- lower-left triedron retained;
- ViewCube disabled by default;
- Snap / Grip / Preview / Tracking / Selection Window exist only during relevant lifecycles;
- no permanent version, OCCT, Ready, or help watermark;
- Preview/transients must be cleared after Commit/Cancel.

## 11. Status Strip

The bottom status strip contains the current operation prompt, XY / YZ / XZ, SNAP, ORTHO, and POLAR. The prompt region may be blank while idle.

Do not permanently show Ready, Command: Ready, version text, XYZ coordinates, duplicated WorkPlane/Drafting captions, or another command input.

## 12. Dialogs

Dialogs use native Fluent Button/TextBox/ComboBox controls. OCCAD does not maintain a separate decorative MessageBox skin system.

Rules:

- action captions are horizontally and vertically centered;
- Settings label columns use a shared content-sized `Auto` width so the longest current-language label determines the label column;
- value columns consume remaining width;
- fixed label widths must not create large empty gaps or clip long Chinese/English text;
- CAD business controls such as ColorTable may keep their required custom visuals.

## 13. Localization

- supported languages: `zh-CN` and `en-US`;
- Action/Tool IDs never change with language;
- UI text uses localization keys;
- language changes refresh Menu, Toolbar, Tool parameter strip, panels, and operation prompt;
- the parameter strip must retain a single Visual Parent during shell rebuilds;
- the preference is persisted.

## 14. UI acceptance

At minimum validate:

1. Entity / Tool / Action registration is consistent;
2. Menu/Toolbar entry works;
3. Tool parameter strip remains fixed at the bottom without viewport jumps;
4. parameter edits and real preview values stay synchronized without interrupting focused editing;
5. prompt, preview, and exact input are correct;
6. `Esc` cancels the Tool and clears formal selection state;
7. XY/YZ/XZ behaves consistently in 2D and staged 3D Tools;
8. Property editing, Snap, Grip, and Selection work correctly;
9. long Chinese/English Settings labels size correctly and all dialog action captions are centered;
10. Save/Open restores the result;
11. Commit/Cancel leaves no native transient;
12. 125% / 150% DPI layouts remain usable;
13. actual Windows/Linux build and manual interaction validation succeed before completion is claimed.

Source existence or Action registration alone does not prove product completion.
