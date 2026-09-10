# 07 Code Organization

## 1. Purpose

This document describes the current repository structure and the ownership rules that determine where new code belongs. It is normative for source placement and naming; architecture semantics remain authoritative in [04 Architecture](04-ARCHITECTURE.md).

## 2. Projects

### `src/OCCAD.Core`

Contains CAD domain and interaction logic. Major areas include:

- `Actions/` — stable command entry points and command metadata;
- `Document/` — persistent Entity membership and presentation synchronization;
- `Entities/` — persistent Entity types and geometry semantics;
- `Geometry/` — reusable geometry algorithms that do not own UI state;
- `Grips/` — Grip discovery, hot/drag state, and edit interaction;
- `History/` — Undo/Redo entries and history state;
- `Interaction/` — Tool lifecycle, Preview/transient ownership, precision interaction;
- `Layers/` — Layer state and current-layer ownership;
- `Properties/` — property descriptors, value semantics, conversion metadata;
- `Selection/` — Entity/Subobject/Preselection state;
- `Snapping/` — Snap discovery, ranking, markers, and cache/invalidation;
- persistence/exchange code — document serialization and external-format boundaries.

`CadWorkspace` composes one CAD session and coordinates these owners. Core must not reference Avalonia controls.

### `src/OCCAD.Avalonia`

Contains presentation and adapters only:

- `Application/` — Avalonia application startup/composition;
- `Shell/` — `MainWindow` partials, menus, compact toolbar, status and Tool parameter surfaces;
- `Viewport/` — OCCT viewport adaptation and pointer interaction;
- `Input/` — direct keyboard input plus CAD cursor behavior;
- `Panels/Properties/` — PropertyGrid presentation and editor construction;
- `Panels/Layers/` — Layer-panel presentation;
- `Panels/Model/` — Model-tree presentation;
- `Dialogs/` — dialogs and settings surfaces;
- `Localization/` — Chinese/English resources;
- `Theming/` — CAD-specific layout metrics and structural brushes;
- `Diagnostics/` — application-side diagnostics.

The current product has no Ribbon, permanent Command Line, floating Tool panel, or separate Dynamic HUD subsystem. Do not reintroduce directories or abstractions for retired UI concepts unless the product direction explicitly changes.

## 3. Dependency direction

Allowed:

`OCCAD.Avalonia → OCCAD.Core → OcctNet/OcctCSharpBridge → OCCT`

Forbidden:

- Core → Avalonia;
- Entity/Document → UI controls;
- UI controllers owning a parallel Document, Selection, Layer, Tool, or History model;
- reflection/service-locator dispatch that bypasses explicit owners.

## 4. Source-placement rule

Before adding a file, classify its state:

- persistent domain state → Document/Entity/Layer;
- staged user interaction → Tool/Interaction;
- formal selection → Selection;
- Snap/Grip behavior → the matching Core subsystem;
- undoable mutation → Transaction/History owner;
- display/input adaptation only → Avalonia;
- serialized compatibility contract → persistence/registry.

If a class cannot be assigned to one owner, the design is not sufficiently resolved.

## 5. Naming

Prefer one principal public/internal type per file. Small tightly coupled records/enums may remain with their owner.

Use stable domain names such as `CadEntity`, `CadWorkspace`, `CadToolManager`, `CadSelectionManager`, and `CadPropertyTransaction`.

Avoid artificial parallel names such as:

- `Advanced`;
- `Extended`;
- `Legacy`;
- `Compat`;
- `New`;
- `V1` / `V2`;
- `Helper2`;
- vague `Common` / `Util` buckets.

`Manager` is reserved for a real lifecycle/state owner. Pure algorithms should use names that describe the algorithm or transformation.

## 6. State ownership

- Document owns persistent Entity membership.
- Entity owns its formal geometry, placement, layer assignment, appearance override, and metadata.
- LayerManager owns layer collection/current layer.
- ToolManager owns the active Tool lifecycle.
- Selection/SubobjectSelection/Preselection each own their formal state.
- PreviewManager owns Tool preview presentation.
- SnapManager owns Snap candidates/marker/current snap state.
- GripManager owns Grip marker/hot/drag state.
- `CadTransientScene` coordinates transient channel ownership.
- History owns Undo/Redo state.
- CadTheme owns shared Avalonia metrics, not business state.

Native viewer objects are derived presentation and are never an independent business source of truth.

## 7. Mutation ownership

Entity property changes use `CadPropertyTransaction` or another explicit Core transaction path.

For entity appearance:

- `ColorByLayer=true`, `LineStyleByLayer=true`, `LineWidthByLayer=true` mean inherited Layer appearance;
- directly assigning Color/LineStyle/LineWidth through the PropertyGrid creates an Entity **Override** by disabling the corresponding ByLayer flag in the same transaction;
- re-enabling ByLayer restores Layer inheritance;
- viewer refresh is derived from the resulting Core state.

Layer color/style/width/visibility/locking changes go through Core Layer/Workspace APIs. Tool commits use Core Document/Transaction/History APIs. Avalonia may parse input and show errors but does not implement rollback semantics.

## 8. MainWindow partials and controllers

`MainWindow` is split by presentation responsibility. A partial file should adapt or coordinate one UI concern, not become a second owner for Core behavior.

When a partial grows:

1. first remove duplicate refresh/subscription paths;
2. move reusable UI-only behavior to a narrowly named controller;
3. move business behavior to the actual Core owner;
4. do not create a parallel framework merely to reduce file length.

Large files are not automatically defects; duplicated ownership is.

## 9. Dead-code cleanup policy

Delete code when all of the following are true:

1. it is not referenced by the current registered product surface;
2. it is not part of persistence/registry compatibility;
3. it is not a public extension contract documented for consumers;
4. it is not required by current native/presentation lifecycle behavior;
5. deletion can be validated by a real build.

Do **not** delete an Entity/geometry type solely because it is absent from the current UI. Persistence, feature profiles, selection filters, or future-compatible document data may still depend on it.

During release stabilization, prefer removing dead metadata, duplicate adapters, stale docs, and unreachable UI helpers before deleting Core geometry families.

## 10. Repository hygiene

Keep the root operational surface small and explicit:

- `build.ps1` / `build.sh`;
- `run.ps1` / `run.sh`;
- `publish.ps1` / `publish.sh`;
- README / CHANGELOG / license files;
- `docs/`;
- `src/`.

Do not commit generated build output, local logs, temporary screenshots, editor caches, local SDK paths, one-off migration scripts, or temporary validation artifacts.

Normal OCCAD builds consume the installed Bridge SDK. They do not clone or rebuild Bridge automatically.

## 11. Canonical documentation ownership

Avoid repeating long contracts across documents:

- product boundary → [01 Product Goals](01-PRODUCT-GOALS.md) and [14 Feature Matrix](14-FEATURE-MATRIX.md);
- UI structure → [02 UI Specification](02-UI-SPEC.md);
- interaction → [03 Interaction Specification](03-INTERACTION-SPEC.md);
- architecture/ownership → [04 Architecture](04-ARCHITECTURE.md);
- Entity/Tool contract → [05 Entity and Tool Contract](05-ENTITY-TOOL-CONTRACT.md);
- development workflow → [08 Development Guide](08-DEVELOPMENT-GUIDE.md);
- extension procedure → [09 Extension Guide](09-EXTENSION-GUIDE.md);
- transaction/native invariants → [10 Transaction and Resource Contract](10-TRANSACTION-RESOURCE-CONTRACT.md);
- build validation → [11 Build and Validation](11-BUILD-VALIDATION.md);
- user operation → [13 User Guide](13-USER-GUIDE.md);
- command IDs/aliases → [15 Command Reference](15-COMMAND-REFERENCE.md);
- release gate → [16 Release Guide](16-RELEASE-GUIDE.md).

When another document needs one of these rules, summarize only the local consequence and link to the canonical owner.
