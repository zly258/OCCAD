# 03 Interaction Specification

## 1. Unified chain

Mouse, keyboard, Precision, Snap, Tracking, and Grip input flow through Workspace and the active Tool. MainWindow only triggers commands and presents state; it does not directly mutate Entity geometry.

```text
Toolbar / Shortcut / Direct Input
       ↓
CadActionManager / CadCommandManager
       ↓
Action / CadTool / PrecisionInput
       ↓
ResolvePoint / Tool State / Preview
       ↓
Document / History Commit
```

The initial release has no permanent command-input box. The active Tool prompt is shown in the bottom status strip. Exact numeric and coordinate input remains available through transient direct keyboard entry while a Tool is active.

## 2. Tool lifecycle

Unified lifecycle:

`Idle → Activate → Drawing/WaitForSelect → Preview → Commit Stage → Next/Complete → Neutral`

Rules:

- activating a new Tool cancels the current Tool first;
- activation clears Preselection, Subobject state, and Grip state;
- every Tool owns its transient/preview lifecycle;
- Esc = Cancel; when transient direct text exists, the input surface may clear that text before command cancellation;
- Backspace = StepBack when no transient direct text is being edited;
- Enter/Space use the shared ToolManager acceptance/finish rule; Enter commits a pending direct-input value first;
- right-click finishes when meaningful and otherwise cancels;
- Complete/Cancel must restore neutral interaction state;
- Point steps never reuse stale/off-viewport pointer coordinates.

These behavior semantics are aligned with the OCCTBIM-Source Tool/ToolManager interaction model. OCCAD keeps its stricter Transaction, transient ownership, observer isolation, and Neutral State contracts rather than copying the older viewer-driven architecture.

## 3. Direct precision input

Removing the permanent CommandLine must never remove precise CAD input.

When a Tool is active, keyboard text is collected by the lightweight direct-input surface and displayed inline with the bottom Tool prompt. The UI does not parse geometry itself; it forwards the final text to the shared `CadCommandManager`, which delegates to `CadToolManager` and `CadPrecisionInputManager`.

Supported Core forms include:

- finite length / angle / factor values when the active stage accepts them;
- absolute or relative point text such as coordinate pairs/triples;
- polar point input supported by `CadCoordinateInputParser`;
- `parameter=value` Tool parameters;
- existing command options such as Offset / Finish / StepBack where the active Tool supports them.

Direct input is transient state only. It is cleared when the Tool or Tool stage changes and never becomes Document, History, or Entity state by itself.

## 4. Work plane

`WorkPlane` is the formal geometric plane for Tool point input, not a decorative UI guide.

- a Tool may lock a work plane for a stage;
- users cannot override a Tool-fixed plane;
- switching XY/YZ/XZ clears Snap/Tracking and rebuilds Preview;
- Tool completion/cancel must call the equivalent `EndToolPlane()` cleanup;
- active work-plane state must not leak after Tool completion.

## 5. Point resolution

Unified point-resolution chain:

`Screen → View Ray / Active WorkPlane → Object Snap → Tracking → Axis/Angle/Length Constraint → Final World Point`

Tools do not reimplement XY projection, polar math, or lock precedence.

## 6. Snap / selection

Entities provide semantic Snap candidates; SnapManager owns candidate collection, screen tolerance, depth, type priority, hysteresis, and temporary modes.

Selection, Preselection, and SubobjectSelection remain separate:

- left-to-right box = Window;
- right-to-left box = Crossing;
- hidden, locked, or non-selectable entities do not enter formal Selection;
- activating a normal drawing Tool clears Grip state;
- Tool completion restores Grips from current Selection.

## 7. Grip transaction

Grip editing is preview-transactional:

`capture original → duplicate preview → suppress real presentation → MoveGrip(preview) → Preview.Update → accept → apply once → one History entry → clear preview → restore presentation`

PointerMove never mutates the persistent Entity.

## 8. Preview / commit

Preview never belongs to Document, Selection, or History.

Normal commit:

`Validate → Document/History mutation → Clear Preview → Complete Tool`

Pointer-driven intermediate geometry is provisional. `ArgumentException`, `InvalidOperationException`, `ArithmeticException`, and OCCT `OcctException` failures produced by an intermediate preview sample clear Preview/Snap/Tracking and keep the active Tool alive. Final commit failures are not hidden or reported as successful.

This prevents Revolve, Sweep, Loft, and similar tools from terminating merely because a temporary mouse position is geometrically unsolvable.

## 9. Entity / Tool / Action consistency

The product surface must keep the following chain consistent:

`Entity Registry ↔ Tool Registry ↔ Action Registry ↔ Toolbar ↔ Model Tree`

The UI and Model Tree must not maintain a second stale Entity whitelist. Internal persistence helpers such as `Path` are not ordinary user-visible model nodes.

A Tool Action is executable only when the OCCT engine is initialized and the target Tool is actually registered. A visible toolbar button must never be an optimistic placeholder.

The initial release intentionally does not restore Move / Copy / Rotate / Scale / Mirror / Array / Trim / Extend / Fillet / Chamfer edit commands, and the Model Tree does not expose Delete.

## 10. Property / Layer

Entity property edits use:

`Capture → Validate → Apply → Presentation/Dependency Refresh → History`

Layer edits follow the same Core-owned transaction rule. Direct Color / LineStyle / LineWidth edits disable the corresponding ByLayer flag, and ByLayer changes are history-recorded.

## 11. Prompt / error boundaries

- the current Tool prompt is shown in the bottom operation-status area;
- pending direct keyboard text is appended to that prompt rather than displayed in a permanent command bar;
- idle prompt state is blank, with no `Ready` or `Command: Ready` text;
- recoverable Preview failures do not raise a fatal error dialog;
- property, layer, and file-operation failures remain explicit;
- fatal OOM, StackOverflow, and AccessViolation failures are never represented as recovered.
