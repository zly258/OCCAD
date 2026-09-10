# 03 Interaction Specification

## Unified input chain

Mouse, keyboard, Command Line, Precision, Snap, Tracking, and Grip input all flow through Workspace and the active Tool. MainWindow forwards and presents state; it does not directly mutate Entity geometry.

```text
Ribbon / Shortcut / Command Line / Viewport
                   ↓
          CadActionManager / ToolManager
                   ↓
      ResolvePoint / Selection / Preview
                   ↓
          Document / History commit
```

## Tool lifecycle

`Idle → Activate → Drawing/WaitForSelect → Preview → Commit Stage → Next/Complete`.

- `Esc`: cancel the active Tool; when idle, clear formal selection.
- `Backspace`: StepBack.
- `Enter`: ToolManager accepts the current valid stage or finishes when allowed.
- Right click: unified secondary action while a Tool is active; viewport context menu while idle.
- Point steps never reuse stale/off-viewport pointer coordinates.

## Viewport navigation

Navigation remains owned by the native OcctCSharpBridge viewport: middle-button navigation/pan, wheel zoom, `Shift + Middle` rotation, and middle-button double-click `FitAll`. Navigation input does not enter CAD Selection or Tool pointer processing.

Drawing state uses the CAD cross cursor; navigation switches to the move cursor. Snap markers, Grips, and selection overlays must not be obscured by ordinary UI controls.

## WorkPlane and drafting aids

During drawing:

- `T` → XY;
- `F` → XZ;
- `S` → YZ;
- `F3` → Object Snap;
- `F8` → Orthogonal Tracking;
- `F10` → Polar Tracking;
- `Tab / Shift+Tab` → cycle valid Snap candidates.

WorkPlane changes go through `CadToolManager.TryChangeDrawingPlane`; Avalonia does not mutate the Tool Plane directly.

## Point resolution

`Screen → view ray / active WorkPlane → Object Snap → Tracking → explicit axis/angle/length constraint → final world point`.

Tools do not duplicate XY projection, polar math, or lock precedence. Command Line coordinate, length, angle, and Tool parameter input is routed through `CadCommandManager`.

## Selection / Preselection / Subobject

Selection, Preselection, and SubobjectSelection remain independent.

- Point selection: Replace by default; `Ctrl` Add; `Shift` Remove; `Ctrl+Shift` Toggle.
- Left-to-right rectangle: Window.
- Right-to-left rectangle: Crossing.
- Hidden / locked / non-selectable entities do not enter formal Selection.
- WaitForSelect Tools reuse the same selection gesture instead of creating a second temporary selection model.
- Subobject selection uses stable references and is re-resolved after geometry regeneration.

## Grip transaction

A normally selected single entity may expose Grips.

`capture original → duplicate preview → suppress real presentation → MoveGrip(preview) → accept → apply once to real Entity → one History entry → clear preview → restore real presentation`.

PointerMove never mutates the persistent Entity.

## Preview / commit

Preview never belongs to Document, Selection, or History.

Normal commit: `Validate → Document/History mutation → Clear Preview → Complete Tool`.

Replacement operations such as Trim / Extend / Fillet / Chamfer retain the last valid preview and source suppression until the real mutation succeeds. Failure keeps the Tool usable and must not leave transient presentation behind.

## Command Line

The bottom Command Line is the only complete prompt and exact-input surface:

- Action alias/ID starts a command;
- while a Tool is active it accepts coordinates, lengths, angles, parameters, and Tool options;
- empty input repeats the last command or accepts the current Tool stage;
- `Up / Down` browses command history;
- focus returns to the Viewport after execute/cancel.

No second floating command editor or duplicate Tool prompt panel is retained.

## Property / Layer

Entity property edits use `Capture → Validate → Apply → presentation/dependency refresh → History`. Layer editing follows the same Core-owned transaction rule.

Direct Color / LineStyle / LineWidth editing must correctly update the corresponding ByLayer state. ByLayer changes are history-recorded edits. Avalonia never writes Entity or Layer fields to bypass Core transactions.

## Viewport context menu

The idle viewport context menu is reduced to the Source primary workflow: ShowAll, Hide, Isolate, SelectAll, SelectInvert, Move, Copy, Delete, and Properties. Unsupported actions are not rendered; unavailable real actions are disabled through `CanExecute`.

## Prompt / error boundaries

The complete Tool prompt is shown only in the Command Line region. Status presents only short selection, layer, drafting, WorkPlane, and coordinate state.

Recoverable failures remain local to Tool/Grip/Property/Layer transactions; fatal runtime failures are not misrepresented as recovered.
