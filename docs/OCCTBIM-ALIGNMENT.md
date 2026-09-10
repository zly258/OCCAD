# OCCTBIM-Source alignment boundary

This branch aligns OCCAD with `OCCTBIM-Source/release-1.0` at the architecture, interaction and UI responsibility level. It does not copy the reference repository wholesale and it does not re-enable advanced commands that were intentionally removed from the default core surface.

## Architecture rules carried into OCCAD

### Entity

- Entity is persistent document state and owns geometry/data semantics.
- One concrete Entity stays independently extensible and serializable.
- Persistence compatibility is separate from whether a command is currently exposed in the UI.
- Entity code must not own viewport input state, menu state or transient preview lifecycle.

### Tool

- Tool is a staged interaction state machine: activate -> input -> preview -> commit/finish/cancel -> deactivate.
- A Tool owns only its local staged state and tool-specific replacement-preview ownership.
- `CadToolManager` owns registration, activation/deactivation, event dispatch and the final neutral interaction reset.
- Preview, Snap, Tracking, Precision and temporary WorkPlane state are workspace services, not permanent Entity state.
- Grip editing remains a specialized Tool rather than a second independent interaction system.

### Action

- Action is the command/application boundary used by menu and command line.
- UI surfaces execute Action IDs rather than constructing Entity/Tool instances directly.
- Tool creation remains registry/factory based.
- Removed advanced commands must not remain as hidden menu or command-line aliases.

### Viewport

- Viewport converts device input to CAD interaction events and renders the scene.
- Viewport must not contain Entity-specific editing rules.
- The active Tool receives pointer input before normal selection logic.
- When no Tool is active, input order is grip -> subobject -> selection/navigation.
- Window/Crossing selection, snap tolerance and preselection are interaction services and must remain deterministic after tool switching.

### Property / Layer / Model panels

The reference UI separates `PropertyDock`, `LayerDock` and `ModelDock`. OCCAD keeps a lighter Avalonia implementation but follows the same ownership boundary:

- Model panel owns document-tree presentation and selection synchronization.
- Layer panel owns layer list/filter/edit presentation.
- Property panel owns common-property merging, editor selection and batch property apply.
- MainWindow coordinates panels; business editing must remain in workspace/controllers rather than accumulating in MainWindow.
- Property editing is semantic (`CadValueSemantic`) rather than relying only on CLR type.
- Multi-selection shows common properties and mixed values instead of silently choosing one entity.

## UI rules carried into OCCAD

- Compact industrial layout, flat controls, no decorative icons.
- High information density without duplicated status/prompt text.
- Command interaction text belongs to the command line/HUD; the status bar reports state such as selection, snap, work plane and coordinates.
- Layer and property surfaces use table/grid visual structure rather than card/form layouts.
- Selected/current items use restrained highlight; normal rows remain neutral.
- Dock/panel state can be hidden independently without changing document state.
- Error dialogs are reserved for operations that actually fail; normal invalid drawing input should be rejected in-place and leave the Tool usable.

## Explicit non-goals for the current core pass

- Ribbon parity with the Qt reference UI.
- Full advanced modeling/annotation tool parity.
- Reintroducing removed Rotate/Scale/Mirror/Array/Boolean/Feature tools before the core interaction regression matrix is stable.
- Copying OCCTBIM implementation details that conflict with the current OcctCSharpBridge SDK or Avalonia lifecycle.

## Current validation focus

1. Point / Line / Polyline / Rectangle / Circle / Arc.
2. Box / Cylinder / Cone / Sphere.
3. Move / Copy / GripEdit.
4. Snap / Tracking / WorkPlane / Precision.
5. Commit / cancel / tool switch cleanup.
6. Model / Layer / Property selection synchronization.
7. Property batch editing and mixed-value behavior.
8. 125% / 150% DPI and compact panel usability.
