# Changelog

All notable OCCAD product changes are recorded here. This file describes user-visible release changes rather than individual commits.

## Unreleased — Initial Release Preparation

### Added

- Classic Avalonia CAD shell with Menu, two compact toolbar rows, Model Tree, Layers, Properties, fixed Tool parameter strip, and drafting status strip.
- 2D drafting: Point, Line, Polyline, Free Polygon, Regular Polygon, Rectangle, Circle, Arc, Ellipse, Spline.
- Circle methods: Center+Radius, Center+Diameter, Two Points, Three Points, Point+Center.
- Arc methods: Three Points, Center→Start→End, Start→Center→End, Start→End→Center, Start→End→Point, Start→End→Tangent.
- Ellipse methods: Center+Axes and Axis Endpoints+Minor Axis.
- 3D/curve tools: Box, Cylinder, Cone, Frustum, Sphere, Ellipsoid, Torus, Helix.
- Modeling features: Extrude, Revolve, Sweep, Loft.
- Initial modify surface: Move and Delete.
- Entity/Subobject Selection, Preselection, Window/Crossing selection.
- Object Snap infrastructure with candidate cycling and configurable markers/tolerance.
- Grip / Hot Grip / Grip Edit workflow with configurable marker size and hit tolerance.
- XY/YZ/XZ WorkPlane switching, ORTHO, POLAR, precision point/length/angle/factor input.
- Core transaction/history model with atomic Undo/Redo entry points.
- Layer management, ByLayer appearance, descriptor-driven Properties, editable planar Normal.
- Chinese/English localization and application settings.
- OCCAD document Save/Open persistence.
- Windows/Linux build, run, and publish scripts using the current OcctCSharpBridge SDK 3.0 runtime contract.

### Stabilized

- Tool neutral-state cleanup retries Preview, Snap, Tracking, and GripDrag cleanup before the next Tool session.
- Transient ownership is retained when native cleanup still has state, preventing ownerless ghost presentation.
- Esc cleanup performs best-effort cleanup across all interaction channels before reporting recoverable failures.
- Undo/Redo UI paths use `CadWorkspace.Undo()` / `CadWorkspace.Redo()` rather than bypassing workspace cleanup.
- Action, direct-input, and precision-input exception boundaries no longer convert fatal runtime failures into ordinary user-input errors.
- Command catalog is aligned with actual registered Actions; obsolete/unreachable aliases were removed.
- Entity Color, LineStyle, and LineWidth can be overridden independently from Layer appearance. Direct value editing disables the corresponding ByLayer flag in the same property transaction; re-enabling ByLayer restores Layer inheritance.
- PropertyGrid editor resolution no longer recursively resolves nested ByLayer editors. Numeric properties use the dedicated numeric editor path while remaining normal left-aligned TextBox controls.
- Property and engineering UI text metrics were increased for practical readability at normal Windows DPI scaling.
- Windows/Linux build and publish scripts support nested or SDK-root portable Bridge layouts.
- Windows publish packages include a launcher that runs directly from the publish directory.

### Cleanup

- Removed an unused PropertyGrid editing helper and eliminated the Numeric-as-Text compatibility detour in editor resolution.
- Corrected contributor documentation that referenced the removed `11-TESTING-VALIDATION.md` name.
- Replaced stale code-organization references to retired `CommandLine`, `DynamicInput`, `ToolPanel`, `Panels/Tools`, and `INTERACTION-CONTRACTS.md` structures with the real current repository layout.
- Standardized code/document ownership rules so retired UI concepts are not reintroduced as parallel frameworks.
- Kept unexposed Core Entity/Geometry families when persistence, feature, selection, or compatibility dependencies could not be disproved without a real build; release cleanup is intentionally conservative at the Core boundary.

### Documentation

- Expanded the detailed User Guide.
- Expanded Architecture Design with ownership, data-flow, Tool, transaction, transient, engine, and UI boundaries.
- Expanded Developer Handbook with practical Entity/Tool/Action/Property/Snap/Grip/Persistence workflows.
- Added Command and Feature Reference.
- Added Release and Delivery Guide with build, regression, package, and release gates.
- Reworked Code Organization documentation against the actual current source tree.
- Added canonical-document ownership and terminology rules for Entity, Tool, Action, Preview, ByLayer, Override, release classifications, IDs, and product boundaries.
- Updated English/Chinese documentation indexes and root README release entry points.

### Current product boundaries

The initial release does **not** claim the following as completed interactive product features:

- Copy / Rotate / Scale / Mirror;
- Array;
- Offset;
- Trim / Extend;
- Fillet / Chamfer;
- Text / Dimension / Annotation;
- external CAD import/export directions that have not been individually validated.

These may have partial Core infrastructure, geometry helpers, or persistence types, but are not part of the official initial user-facing surface until their complete interaction and validation loops are finished.

### Release gate

The repository is release-prepared, but a version should be tagged only after the real Windows build, application startup, manual/native interaction regression, Save/Open regression, localization/DPI checks, and clean-directory package launch defined in `docs/en-US/16-RELEASE-GUIDE.md` / `docs/zh-CN/16-RELEASE-GUIDE.md` have passed.
