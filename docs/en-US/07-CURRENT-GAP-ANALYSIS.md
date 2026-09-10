# 07 Current Gap Analysis

## Purpose

This file compares the current `cad` branch with the adopted OCCTBIM-Source design principles and the OCCAD target. Development should close these gaps before adding isolated features.

## Existing foundation

Core already contains Workspace, Document, Layers, Entity/Tool/Action registries, Selection, Subobject, Preselection, WorkPlane, Snap, Tracking, Precision, Preview, Grip, and History. Entity appearance already models Color/LineWidth/LineStyle with independent ByLayer flags. Cone and Frustum are separate entities.

Current 2D entities: Point, Line, Polyline, Rectangle, Polygon, RegularPolygon, Circle, Arc, Ellipse, Spline. Current 3D entities: Box, Cylinder, Cone, Frustum, Sphere, Torus. Class existence is not completion; each type must pass the acceptance matrix.

## UI gaps

The only target shell is native WPF `Menu + ToolBar + Viewport + Docks + StatusBar + non-modal ToolPanel`. Ribbon code, Ribbon naming, runtime visual-tree patches, duplicate styles, hidden compatibility hosts, and obsolete controls should be removed.

Common commands should be at most one submenu deep. ToolBar owns current layer, S/F/T, stage Length/Angle, locks, Finish/Cancel. Radius/Width/Height/Sides and similar stable tool parameters belong to ToolPanel.

## Preview gaps

Final rule: `PreviewAppearance = ResolveAppearance(preview entity, current drawing layer context)`. New entities must preview using the current layer before AddEntity. Degenerate pointer positions keep the last valid preview. Preview replacement is atomic. Move/Copy/Grip previews follow the same rule. PointerMove should update presentations instead of recreating objects when practical.

## Grip gaps

Grip kinds and work-plane metadata exist, but every entity still needs semantic review. GripManager renders and hit-tests only; Entity defines meaning. Dragging edits a duplicate preview, invalid geometry rolls back, and Accept applies once to the real entity and records one history entry.

## Selection/Snap/WorkPlane gaps

Point and box selection must share rules; Replace/Add/Remove/Toggle must agree; preselection stays separate; hidden/locked/non-selectable behavior must be identical everywhere. Snap priority and pixel tolerance must be explicit. Temporary snap must not mutate persistent modes. Persistent user work plane must be separate from transient Tool work plane.

## Property/Layer gaps

Layer editing must not rebuild the whole ItemsSource or require double-click edit mode. Editing another layer must not implicitly make it current. Entity properties must expose independent ByLayer/custom Color, LineWidth, and LineStyle, localized enums, three-decimal display with full precision storage, multi-selection common properties, rollback, and one history entry per user edit.

## Entity completion matrix

Each entity must provide: stable type/ID, BuildShape, Duplicate/RestoreGeometry, supported transforms, Snap, Grip/MoveGrip, Tool creation flow, live Preview, WorkPlane/precision integration, ByLayer appearance, Property editing, History, Serialization, localization, degenerate-input protection, and rollback-safe Preview/Grip behavior.

## Stability and performance gaps

Validate finite values, positive dimensions, vectors, indices, work-plane intersections, viewer-object lifetime, and OCCT construction failures locally. PointerMove must avoid whole-UI refreshes, repeated marker pixmaps, duplicate redraws, and synchronous I/O. Large file load is staged and reports progress.

## Priority

P0 compile/stability/state correctness. P1 remove old UI paths and complete classic shell/ToolPanel. P2 finish every Entity interaction contract. P3 file I/O and large-model performance. P4 only then expand advanced modeling.
