# Interaction and UI contracts

OCCAD keeps the Core + Avalonia split. These contracts consolidate existing services rather than introduce another application framework.

## Mutations and history

Single operations use operation-specific history entries inside the `CadTransaction.Execute` display/change-set boundary. Geometry, grip and multi-entity property changes share `CadTransaction.ApplyEntities`, which captures only the affected entities, skips no-op edits, and restores all targets on failure.

Use `using (var transaction = workspace.BeginTransaction(name))` for a batch containing multiple operations or direct document mutations. Call `Commit()` explicitly. A batch captures entity/layer state and identities, suppresses intermediate history entries, and records one Undo entry. Disposal without Commit restores the previous state and saved flag without destroying the redo stack. Nested batch transactions and Undo/Redo during a batch are rejected. Full-document snapshots are limited to explicit batches; ordinary pointer motion and single operations do not use them.

Undo/Redo update the saved flag after the document change set closes. Import commits its batch before selecting the imported entity.

## Input

`CadToolManager.SubmitCurrent` is the accept boundary for clicks, Enter/Space, command input, dynamic input and tool-panel acceptance. Resolved coordinates enter `CadTool.CommitCurrentStage(CadResolvedPoint)`; pointer input enters the corresponding current-stage method. Existing tool pointer handlers remain adapters where needed. Rejected input is not replayed or treated as Finish. Array's final stage is Confirmation, not Point.

`CadPrecisionInputManager` owns coordinate-text resolution and one-shot Offset state:

- `100,200` or `#100,200`: absolute coordinates in the persistent user plane.
- `@100,0`: relative to the tool reference, using its effective axes.
- `100<30`: polar coordinates from the user-plane origin.
- `@100<30`: polar coordinates from the tool reference.
- `O` during a point stage: pick/type an offset base without advancing the tool, then enter a relative displacement. `#` remains absolute. Backspace cancels the pending offset; completing/canceling the tool clears it.

Snap candidates are ordered by priority group, screen-pixel distance, depth and stable enumeration order. Endpoint/intersection precede midpoint/center, then perpendicular/tangent, other explicit features and nearest. Temporary modes filter candidates before ranking. Hysteresis operates only within the best priority group; explicit candidate cycling remains available. Tracking remains a drafting fallback when no object snap is available.

Selection/preselection share OCCT's pixel aperture. Snap and grips use independently validated pixel radii. Model-space geometric tolerances are not UI pick radii.

## Transients and events

`CadTransientScene` registers the existing managers' cleanup callbacks and state checks. A tool session owns preview (including replacement sources), Snap, Tracking, drag-grip feedback, preselection and the viewport selection rectangle. Formal subobject markers have workspace lifetime. Clearing an owner attempts every registered channel; managers retain native handles after failed deletion. `ClearAll` is reserved for workspace disposal. The scene does not duplicate native presentation management.

`CadWorkspaceEvents` forwards existing typed document, selection, tool, layer and modified-state events through one workspace-local event surface. MainWindow consumes the domain events; low-level managers can retain their direct subscriptions. There is no static event bus, queue or cross-thread dispatch in Core.

## Descriptors and organization

`CadCommandDescriptor` combines the action, localization key, English caption, aliases, shortcut, repeatability and optional tool ID. CAD toolbar/context-menu captions, command completion/aliases and action execution share this metadata. File dialogs remain asynchronous UI operations; they do not acquire Core dependencies on Avalonia.

`CadPropertyDescriptor` owns editor choice, localization/category keys, units, ByLayer relationships and dynamic choices. The layer editor reads current layer names from the descriptor. Avalonia owns controls and value presentation.

Avalonia files are organized under Application, Shell, Viewport, Input (Cursor/DynamicInput/CommandLine), Panels (Properties/Layers/Tools/Model), Dialogs, Localization, Theming and Diagnostics. Namespaces and assemblies are unchanged. `CadPointResolver` owns projection/Snap/drafting composition; Workspace retains the observation/result state.

## Verification

See `tests/OCCAD.Core.Tests/README.md` for managed and Linux native-viewer test commands. These regressions do not replace Windows, DPI or long-running manual acceptance.
