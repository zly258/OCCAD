# 06 Development Plan

## Strategy

Progress in this order: stability → UI/interaction foundation → complete per-Entity contracts → files/performance → advanced modeling. Do not measure progress by button count. Each stage ends with cleanup and acceptance regression.

## P0 Stability and compile health

Keep the branch buildable; fix Avalonia control/API ambiguity and invalid UI resources, and invalid resources. Complete local transaction exception boundaries for Tool/Grip/Property/Preview; Grip drag must not crash; Preview replacement is atomic and retains the last valid frame; Snap markers reappear after hide; navigation does not corrupt an active Tool.

Centralize finite/positive/tolerance/vector/index/work-plane/viewer-lifetime validation.

## P1 Native classic UI

Remove all Ribbon dependencies, naming, styles, and runtime patches. The shell is Menu + persistent ToolBar + Docks + StatusBar + CadToolPanel. Remove obsolete ToolParameterHost, FactorInput, old layer-column installers, duplicate color UI, and temporary visual-tree patches.

Keep menus shallow. ToolBar owns S/F/T, CurrentLayer, stage L/A, locks, Finish/Cancel. Stable Radius/Width/Height-style parameters belong to non-modal ToolPanel. Normalize docks, ColorDialog, DPI, and localization.

## P2 Point resolution / Selection / Snap / Grip

Complete one ResolvePoint pipeline; separate persistent user WorkPlane from transient Tool plane; unify ORTHO/POLAR/Axis/Angle/Length precedence. Finish Replace/Add/Remove/Toggle, Window/Crossing, Preselection/Subobject, and consistent Hidden/Locked/Selectable rules. Review Snap and Grip semantics for every entity; markers are screen-stable and flicker-free.

## P3 ToolPanel and basic Entity closure

2D: Point, Line, Polyline, Rectangle, Polygon, RegularPolygon, Circle, Arc, Ellipse, Spline.

3D: Box, Cylinder, Cone, Frustum, Sphere, Torus. Cone and Frustum permanently remain separate Entity/Tool types.

For each type complete Tool, Preview, ParameterPanel where useful, Snap, Grip, Precision, Property, ByLayer, History, Serialization, Localization, degenerate-input handling, and rollback.

## P4 Basic editing

Finish Move, Copy, Grip Edit first; then Rotate, Scale, Mirror. Editing tools use consistent pre/post-selection policy, pointer moves modify preview copies only, and Accept commits once. Delete/Hide/Isolate/ShowAll remain Action/state operations.

## P5 Layer / Property / Model

Use stable collections/local refresh rather than whole grid rebinding. Complete one-click Color/Visible/Locked/LineWidth/LineStyle and keep current layer separate from edited layer. Property supports multi-select common values, ByLayer/custom values, localized enums, three-decimal display/full precision storage, and rollback. ModelTree refreshes incrementally and synchronizes with viewport selection.

## P6 Document / File / DWG-DXF

Stabilize serializer versioning, registry-driven deserialization, safe unknown-data rejection, and modified/save-point history semantics. Large files run Read → Parse → Model → Viewer → Complete stages with cancellable bottom progress.

DWG/DXF uses Parser/Adapter → CadEntity; parsers do not drive Avalonia/viewer directly. Prioritize complete geometry/layers/colors/line styles/line widths/blocks/attributes display before broad entity count.

## P7 Performance

Deduplicate pointer hot paths; prefer presentation update over delete/recreate; use display batches; cache snap/grip marker styles; avoid whole Layer/Property/ModelTree refreshes; background large-file parsing where safe; add spatial indexing only when justified.

## P8 Advanced modeling

Only after the core is stable, add Extrude, Revolve, Boolean, Fillet/Chamfer through the same Tool/Preview/History/Property/exception contracts rather than a second 3D interaction framework.

## Stage completion

Run `build.ps1` and manually exercise Tool activation, Preview, Snap/Tracking, precision input, ToolPanel, Finish/Cancel/Backspace/right-click, Undo/Redo, Property, Layer, invalid Grip positions, navigation during active Tool, Chinese/English, and Save/Open. Remove transitional code and squash the stage into a coherent commit. See `12-ACCEPTANCE-CHECKLIST.md` for the release gate.
