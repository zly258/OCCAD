# 01 Product Goals

## Positioning

OCCAD is a lightweight but architecturally complete desktop CAD framework/application base built on OCCT through OcctCSharpBridge. It is not an OCCT API demo.

The goal is a stable CAD foundation where adding an Entity, Tool, file format, or domain module does not require redesigning MainWindow, Selection, Preview, History, Property, Snap, or Grip infrastructure.

## Responsibility model

The project keeps the useful boundaries demonstrated by `OCCTBIM-Source`:

- Document owns persistent entities.
- Entity owns valid geometry, appearance data, Snap/Grip semantics, copy/restore, and persistence state.
- Tool owns staged interaction, prompt, input kind, Preview, Finish/Cancel/StepBack.
- Action is the stable command entry used by menu, shortcuts, and command line.
- Viewport renders/maps input without owning CAD business rules.
- Property and Layer mutate Core through explicit transaction/history APIs.
- Snap chooses candidates; Entity supplies semantic snap geometry.
- GripManager displays/hit-tests grips; Entity defines grip geometry behavior.

The reference project is behavioral guidance only; OCCAD does not copy Qt widgets or singleton coupling.

## Functional baseline

The framework maintains one coherent contract across 2D drawing, 3D primitives/features, modify operations, object snap, work planes, tracking, precision input, selection, grips, layers, properties, Undo/Redo, persistence, annotation, and measurement.

Breadth is secondary to correctness. A command is complete only when Preview, Commit, Cancel, StepBack, selection state, property state, Undo/Redo, localization, and persistence agree.

## UI goal

The UI target is a compact industrial CAD shell closer to AutoCAD/Inventor density than consumer Fluent layouts: neutral gray tool surfaces, dark viewport, consistent typography/control heights, low/no decorative corner radius, shallow menus, left Model panel, resizable right Layer/Property panels, Command Line above StatusBar, and a non-modal ToolPanel.

## Property goal

Normal properties expose business-meaningful CAD values only. Viewer handles, IDs, internal selectable state, material/display-mode plumbing, and serialization byte counts do not belong in the normal Property panel.

Color, LineStyle, and LineWidth each have integrated ByLayer state. Editing an explicit value exits that ByLayer mode; re-enabling ByLayer preserves the stored override.

## Quality goal

Recoverable invalid geometry, Preview, Grip, Property/Layer, and file failures do not terminate the app or leave inconsistent transient state. A feature is complete only when the whole interaction contract is coherent, not merely when an OCCT shape can be created.
