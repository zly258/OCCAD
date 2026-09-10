# 01 Product Goals

## Positioning

OCCAD is not an OCCT API demo and does not expose kernel calls directly as UI. It is a lightweight but usable and architecturally complete desktop CAD framework/application base covering stable 2D/3D drawing, editing, selection, snapping, grips, properties, layers, document lifecycle, undo/redo, and viewport interaction.

The short-term goal is not mature-CAD entity breadth. The core framework must be complete enough that adding Entities, Tools, file formats, or domain modules does not redesign MainWindow or the interaction foundation.

## Ideas retained from OCCTBIM-Source

Retain the responsibility boundaries around Document/Model, Entity, Tool, Action, Viewport, Dock, PropertyEditor, Grip, and Snap—not Qt widgets, singleton choices, or exact class names.

Entity is a business object with identity, layer, appearance, geometry, properties, Grip/Snap semantics, copy/restore, and persistence behavior. Tool is a staged Activate → input → Preview → Commit/Cancel/StepBack state machine. Action is the shared command entry for Menu, ToolBar, and shortcuts. Document uniquely owns persistent entity state. Viewport maps input and renders without owning CAD business rules. Grip/Snap semantics come from Entity. Property/Dock observe current Document/Selection rather than maintaining a second model.

## First usable release

Document: New/Open/Save/SaveAs, modified state, Undo/Redo, rollback-safe failures.

2D: Point, Line, Polyline, Rectangle, Polygon, RegularPolygon, Circle, Arc, Ellipse, Spline through one Tool/Preview/Snap/Precision/Grip/Property/History/Serialization contract.

3D: Box, Cylinder, Cone, Frustum, Sphere, Torus. Cone and Frustum remain separate Entity/Tool types and are never merged through TopRadius=0 semantics.

Editing: complete Delete, Move, Copy, Grip Edit first; extend Rotate, Scale, Mirror through the same framework.

Selection: Point Pick, Replace/Add/Remove/Toggle, Window/Crossing, Preselection, and consistent Locked/Hidden/Selectable rules.

Precision: WorkPlane, Snap, ORTHO, POLAR, Axis/Angle/Length locks share one ResolvePoint pipeline.

Properties/Layers: both ByLayer and custom Color/LineWidth/LineStyle; direct ColorDialog; three-decimal UI display with full precision storage.

## UI target

The single shell is native Avalonia compact `Menu + persistent ToolBar + Viewport + Docks + StatusBar + non-modal ToolPanel`. Ribbon and third-party theme shells are not supported targets. Common commands stay shallow; ToolBar owns common stage input; stable Radius/Width/Height-style values belong to ToolPanel.

## Non-goals

The current completion gate does not require every DWG/DXF entity, a full parametric solver, BIM discipline systems, broad advanced surface modeling, a plugin marketplace, or large smoke/check script frameworks.

## Quality goals

Recoverable invalid input, degenerate geometry, Grip/Preview/Property/file errors do not terminate the app and roll back safely. Appearance, point resolution, selection, history, preview, and property mutation each have one authoritative path. New basic entities add focused files and registration rather than large MainWindow switches. PointerMove avoids unnecessary delete/recreate; bulk display is batched; large loads are staged with progress. Chinese and English fully cover visible UI while internal IDs remain stable.

A feature is complete only when entry points, Tool lifecycle, preview/final consistency, normal/error exits, Undo/Redo, UI state, localization, persistence, and extensibility all agree. `12-ACCEPTANCE-CHECKLIST.md` is the release gate.
