# 08 UI Detail Specification

## Single shell target

OCCAD uses a native WPF classic CAD shell: `Menu + ToolBar + Viewport + Docks + StatusBar + non-modal ToolPanel`. Ribbon is not a second supported shell.

## Menu

Top-level menus are File, Edit, Draw, Modify, View, Panels, Settings. Routine commands live directly one level below the top menu; deeper hierarchy is reserved for genuine command families such as temporary snap variants. Menu items invoke shared Actions and never duplicate Tool logic.

## ToolBar

The toolbar is always visible and layout-stable. Recommended order: `S F T | Current Layer | L [lock] | A [lock] | Finish | Cancel`. With no active Tool, contextual controls are disabled instead of collapsing the bar. Radius/Width/Height and other tool-specific parameters do not belong here.

## ToolPanel

`CadToolPanel` is the only floating-panel base. It is non-modal, does not steal viewport focus, is draggable, hides instead of cancelling on close, disappears on Tool completion/cancel, updates language immediately, and does not recreate the Window for ToolUpdated events.

Parameter examples: Circle Radius/Diameter; Rectangle Width/Height; RegularPolygon Sides/Radius; Box Width/Depth/Height; Cylinder Radius/Height; Cone Radius/Height; Frustum BottomRadius/TopRadius/Height; Sphere Radius; Torus MajorRadius/MinorRadius.

## Docks

Model defaults left; Layer upper-right; Property lower-right. Viewport remains dominant. Dock close buttons are transparent borderless `×` controls with subtle hover only. Toggling a dock changes UI visibility, not business-object lifetime.

## Layer UI

Columns: Name, Color, Visible, Locked, LineWidth, LineStyle. Color opens ColorDialog directly. Width/style use one-click combo controls and do not require DataGrid edit-mode double-click. Editing a non-current layer does not make it current. Layer UI uses stable collections and local refresh, not `ItemsSource=null` rebuilds.

## Property UI

Single selection shows the complete object; multi-selection shows common editable properties. ByLayer flags are independent from override values. Color opens ColorDialog. Enums use localized display mappings. Doubles display three decimals while retaining full precision.

## Viewport and StatusBar

Default viewport is black with a small lower-left trihedron and upper-right ViewCube without duplicate external axis labels. StatusBar contains prompt/error/result, Snap, ORTHO, POLAR, coordinates, and optional background progress only.

## DPI

Use system fonts and native control metrics. Avoid per-control FontFamily/FontSize and screenshot-derived fixed pixel layout assumptions.
