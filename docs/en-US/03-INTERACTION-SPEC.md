# 03 Interaction Specification

## 1. Unified chain

Mouse, keyboard, Precision, Snap, Tracking, and Grip input flow through Workspace and the active Tool. MainWindow triggers commands and presents state; it does not directly mutate Entity geometry.

```text
Menu / Toolbar / Shortcut / Direct Input
              ↓
CadActionManager / CadCommandManager
              ↓
CadToolManager / CadTool / Precision Input
              ↓
ResolvePoint / WorkPlane / Tool State / Preview
              ↓
Transaction / Document / History
```

The initial release has no permanent command-input box. The active Tool prompt is shown in the bottom status strip, and Tool parameters live in the fixed strip immediately above it.

## 2. Tool lifecycle

Unified lifecycle:

`Idle → Activate → Drawing/WaitForSelect → Preview → Commit Stage → Next/Complete → Neutral`

Rules:

- activating a new Tool cancels the current Tool first;
- activation clears Preselection, Subobject state, and Grip state;
- every Tool owns its transient/preview lifecycle;
- Backspace = StepBack;
- Enter/Space use the shared ToolManager acceptance/finish rule;
- right-click finishes when meaningful and otherwise cancels;
- Complete/Cancel must restore neutral interaction state;
- Point steps never reuse stale/off-viewport pointer coordinates.

### Escape

`Esc` is an application-level CAD cancel operation, not only a local Tool key handler:

```text
Cancel active Tool
      ↓
Clear Entity Selection
Clear Subobject Selection
Clear Preselection
      ↓
Clear Snap / Tracking
      ↓
Return focus to Viewport
```

The final semantics must be the same whether focus is in the viewport, a Tool parameter editor, or another normal main-window control.

## 3. Direct precision input

Removing the permanent CommandLine must never remove precise CAD input.

When a Tool is active, keyboard text may be collected by the lightweight transient direct-input path and displayed with the current operation prompt. The UI does not own geometry parsing; confirmed text is delegated to the shared command/precision pipeline.

Supported Core forms include finite length/angle/factor values, coordinate input, polar input, `parameter=value` Tool parameters, and command options supported by the active Tool.

Direct input is transient state only. It never becomes Document, History, or Entity state by itself.

## 4. Work plane

`WorkPlane` is the formal geometric plane for Tool point input, not a decorative UI guide.

XY / YZ / XZ requests must go through `CadToolManager.TryChangeDrawingPlane()` so buttons, shortcuts, and future integrations share one policy.

Rules:

- when there is no fixed temporary construction plane, apply the requested preset immediately;
- when the active Tool is in a fixed temporary construction-plane stage, Core steps back through `StepBackCurrent()` to the nearest stage that permits a plane change, then applies the preset;
- a locked user plane or fixed Grip plane still blocks the request;
- changing the plane clears Snap and Tracking;
- the active Tool receives `OnWorkPlaneChanged()` and rebuilds preview from the last valid pointer when possible;
- planar Tools that cache Normal/XAxis/YAxis must refresh those cached vectors;
- Tool completion/cancel ends the Tool plane;
- active work-plane state must not leak after Tool completion.

A fixed height/local-axis construction stage must never be silently rotated in place because that would mix already accepted points with later points from a different coordinate frame.

## 5. Tool parameters and live values

`CadTool.ParameterPanel` is the single source for the parameter UI.

Bidirectional flow:

```text
Editor commit → tool.TrySetParameter() → Tool state → Preview
Preview/Tool update → measurable actual value → Editor display
```

Requirements:

- UI code does not mutate Tool private fields;
- real preview dimensions take precedence over provisional mouse text for display;
- a focused TextBox/ComboBox/CheckBox is never replaced by automatic refresh;
- the parameter strip reserves a fixed bottom row and never resizes the viewport when Tool parameters appear/disappear;
- ToolChanged/ToolUpdated rebuild UI from the current Tool instead of preserving a second parameter-state model.

## 6. Point resolution

Unified point-resolution chain:

`Screen → View Ray / Effective WorkPlane → Object Snap → Tracking → Axis/Angle/Length Constraint → Final World Point`

Tools do not reimplement XY projection, polar math, or lock precedence.

## 7. Snap / selection

Entities provide semantic Snap candidates; SnapManager owns candidate collection, screen tolerance, depth, type priority, hysteresis, and temporary modes.

Selection, Preselection, and SubobjectSelection remain separate:

- left-to-right box = Window;
- right-to-left box = Crossing;
- hidden, locked, or non-selectable entities do not enter formal Selection;
- activating a normal drawing Tool clears Grip state;
- Tool completion restores Grips from current Selection;
- `Esc` explicitly clears Entity, Subobject, and Preselection state.

## 8. Grip transaction

Grip editing is preview-transactional:

`capture original → duplicate preview → suppress real presentation → MoveGrip(preview) → Preview.Update → accept → apply once → one History entry → clear preview → restore presentation`

PointerMove never mutates the persistent Entity.

## 9. Preview / commit

Preview never belongs to Document, Selection, or History.

Create-style commit boundary:

`Validate → Clear/Suppress Preview → Persistent mutation → Complete Tool → Install History → Redraw`

If the persistent mutation fails while the Tool can still continue, the last valid preview may be restored. Once the Tool has completed, the system must never manufacture a state where committed geometry and an obsolete preview coexist.

Pointer-driven intermediate geometry is provisional. `ArgumentException`, `InvalidOperationException`, `ArithmeticException`, and preview-stage `OcctException` failures may clear Preview/Snap/Tracking and keep the active Tool alive. Final commit failures are propagated and never reported as successful.

## 10. Entity / Tool / Action consistency

The product surface must keep the following chain consistent:

`Entity Registry ↔ Tool Registry ↔ Action Registry ↔ Menu/Toolbar ↔ Model Tree`

The UI and Model Tree must not maintain a second stale Entity whitelist. Internal persistence helpers such as `Path` are not ordinary user-visible model nodes.

The initial release intentionally does not restore Move / Copy / Rotate / Scale / Mirror / Array / Trim / Extend / Fillet / Chamfer edit commands, and the Model Tree does not expose Delete.

## 11. Property / Layer

Entity property edits use:

`Capture → Validate → Apply → Presentation/Dependency Refresh → History`

Layer edits follow the same Core-owned transaction rule. Direct Color / LineStyle / LineWidth edits disable the corresponding ByLayer flag, and ByLayer changes are history-recorded.

## 12. Prompt / error boundaries

- the current Tool prompt is shown in the bottom status area;
- transient direct keyboard text may be shown with that prompt rather than in a permanent command bar;
- idle prompt state is blank, with no `Ready` or `Command: Ready` text;
- recoverable Preview failures do not raise a fatal error dialog;
- property, layer, and file-operation failures remain explicit;
- fatal OOM, StackOverflow, and AccessViolation failures are never represented as recovered.
