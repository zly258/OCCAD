# 02 UI Specification

## Direction

OCCAD uses a native Avalonia 12 compact CAD desktop shell:

```text
Menu: File Edit Draw Modify Model Annotation View Panels Settings
ToolBar: Work Plane | Current Layer | Snap | Ortho | Polar | Finish | Cancel
Model Dock | Viewport | Layer / Property Tabs
Command Line
StatusBar: Prompt | Selection | History | Snap | Precision | Work Plane | Coordinate
+ non-modal ToolPanel
```

The viewport remains the dominant area. The UI uses Fluent Compact density plus a restrained industrial palette; no Ribbon, icon-heavy command surface, or third-party shell/theme framework is part of the architecture.

## Menu

Menus invoke registered Action IDs only. Real command families such as Circle, Arc, Ellipse, Boolean and Array may use one additional submenu; unregistered actions are not presented as usable commands. Object Snap settings expose only modes with completed runtime support.

## ToolBar and ToolPanel

The toolbar contains only global/current-stage state. Entity/tool-specific parameters live in the single native Avalonia `CadToolPanel`. Its close button hides the panel without canceling the Tool. A new Tool opens its panel again. Parameter edits drive Preview immediately; Finish/Cancel use the Tool lifecycle.

## Docks

Model is left. Layer and Property share a compact tabbed right dock instead of two permanently stacked panels. Both docks are views over Workspace state and can be hidden without changing business state.

Layer editing provides current layer, color, visible, locked, line width and line style using native Avalonia controls. Property editing uses `TypeDescriptor` metadata, supports single/multi-selection, common properties, layer/enum/bool/color editors, three-decimal display and transaction/history rollback. WinForms PropertyGrid is not used.

## Viewport / HUD / Cursor

`OcctAvaloniaViewport` is the only OCCT host. Drawing uses the custom hollow cross cursor; middle navigation uses the navigation cursor; normal/selection stages use the normal pointer. Snap aperture and dynamic HUD account for Avalonia render scaling so 125%/150% DPI remains aligned with native viewport input.

S/F/T change only the work plane. Right click is the CAD secondary action. F3/F8/F10 toggle Snap/Ortho/Polar. Tab cycles snap candidates. ViewCube is hidden; the lower-left trihedron remains.

## Localization

All durable UI text is resolved through the embedded English/Chinese resource maps. Tool prompts, command results, action labels, history names, layer/property labels and panel labels refresh on language change.
