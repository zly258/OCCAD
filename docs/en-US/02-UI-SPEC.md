# 02 UI Specification

## Direction

OCCAD uses a native WPF classic CAD desktop shell. Ribbon and third-party UI themes are not part of the target architecture. The UI should be compact, layout-stable, shallow in hierarchy, and maintainable.

```text
Menu: File Edit Draw Modify View Panels Settings
ToolBar: S F T | Current Layer | L/lock | A/lock | Finish | Cancel
Model Dock | Viewport | Layer Dock / Property Dock
StatusBar: Prompt | Snap | Ortho | Polar | Coordinate
+ non-modal ToolPanel
```

The viewport remains the dominant area and the top command region does not jump with Tool lifecycle changes.

## Menu

Top-level menus are fixed to File, Edit, Draw, Modify, View, Panels, Settings. Routine commands live directly one level below and separators organize 2D/3D or view/display groups. Do not reintroduce wrapper submenus such as 2D, 3D, Visibility, View, Display, or Language solely for categorization. Genuine command families may use one additional level, for example temporary object-snap variants.

File owns document lifecycle only. Menus, shortcuts, and any future buttons invoke the same Action IDs.

## ToolBar

The toolbar is always visible. With no active Tool, stage controls are disabled rather than collapsed. It contains S/F/T work-plane shortcuts, Current Layer, current-stage Length/Angle telemetry and locks, Finish/Cancel, and only truly global precision state. Radius, Diameter, Width, Height, Depth, Sides, and other tool-specific stable parameters do not belong here.

## ToolPanel

ToolPanel is the single UI for stable Tool parameters. All panels derive from `CadToolPanel`: native WPF, non-modal, does not steal viewport focus, draggable, close hides only, and Tool finish/cancel hides automatically.

Typical parameters: Circle Radius/Diameter; Rectangle Width/Height; RegularPolygon Sides/Radius; Box Width/Depth/Height; Cylinder Radius/Height; Cone Radius/Height; Frustum BottomRadius/TopRadius/Height; Sphere Radius; Torus MajorRadius/MinorRadius. Parameter edits update Preview immediately but do not commit. Numeric display defaults to three decimals while model precision remains full.

## Docks

Model defaults left; Layer upper-right; Property lower-right. Docks are views over Workspace/Document/Selection, not second business models. Header sizing/background/margins are consistent and the close button is a transparent borderless `×` with subtle hover only.

## Layer Dock

Shows Name, Color, Visible, Locked, LineWidth, LineStyle. Color opens ColorDialog on one click; width/style are one-click combos; visibility/locked are one-click toggles. Editing another layer does not implicitly make it current. Refresh uses stable collections/local updates rather than `ItemsSource=null` rebuilds.

## Property Dock

Single selection shows full properties; multi-selection shows common editable properties. Independent ByLayer/custom Color, LineWidth, and LineStyle are required. Color opens ColorDialog directly; enums are localized; doubles display `0.000` but retain actual precision. Failed apply restores prior state and keeps Viewer/History consistent.

## Viewport / StatusBar / HUD

Default viewport is black with a small lower-left trihedron and upper-right ViewCube without redundant external XYZ labels. Preview uses final resolved appearance including current-layer ByLayer values. Snap/Grip/Selection markers use screen-stable sizing.

StatusBar contains prompt/error/result, Snap, ORTHO, POLAR, coordinates, and background progress only. The pointer HUD shows local dynamic length/angle/coordinate/snap/tracking feedback and does not duplicate the full Tool prompt.

## Localization and DPI

All visible text comes from resources or a single display mapping. Raw enum `ToString()` is not user UI. Fonts inherit system settings. Layout uses DIP and remains usable at 125%/150% scaling without clipping or overflow.
