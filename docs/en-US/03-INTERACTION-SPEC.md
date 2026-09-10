# 03 Interaction Specification

## Unified chain

Mouse, keyboard, command line, precision, Snap, Tracking, and Grip input flow through Workspace and the active Tool. MainWindow does not directly mutate Entity geometry.

```text
Menu / Shortcut / Command Line
              ↓
       CadActionManager
              ↓
          Action / Tool
              ↓
Tool state + ResolvePoint + Preview
              ↓
      Document / History commit
```

## Tool lifecycle

`Idle → Activate → Drawing/WaitForSelect → Preview → Commit Stage → Next/Complete`.

Esc = Cancel. Backspace = StepBack. Enter/Space use the shared ToolManager acceptance/finish rule. Right click finishes when meaningful and otherwise cancels, unless a specialized Tool explicitly defines a different secondary action.

Point steps never reuse stale/off-viewport pointer coordinates as implicit input.

## Point resolution

`Screen → view ray / active WorkPlane → Object Snap → Tracking → explicit axis/angle/length constraints → final world point`.

Tools do not duplicate XY projection, polar math, or lock precedence.

## Snap / selection

Entity supplies semantic snap points/curves. SnapManager collects/ranks candidates using screen tolerance, depth, type priority, hysteresis, and temporary modes.

Selection, Preselection, and SubobjectSelection remain separate. Left-to-right box is Window; right-to-left is Crossing. Hidden/locked/non-selectable entities do not enter formal Selection.

## Grip transaction

```text
capture original
→ duplicate preview
→ suppress real presentation
→ restore original into preview each frame
→ MoveGrip(preview)
→ Preview.Update
→ accept
→ apply once to real Entity
→ one History entry
→ clear preview
→ restore real presentation
```

PointerMove never mutates the persistent Entity.

## Preview / commit

Preview never belongs to Document, Selection, or History.

Normal commit: validate → mutate Document/History → clear Preview → complete Tool.

Replacement operations keep the last valid replacement preview and source suppression intact during the real mutation. Cleanup occurs only after success; failure keeps the Tool and last valid preview usable.

## Property / Layer

Entity property edits use `Capture → Validate → Apply → presentation/dependency refresh → History`. Layer edits use the same Core-owned transaction principle.

Direct Color/LineStyle/LineWidth edits switch the corresponding ByLayer flag off. ByLayer changes are history-recorded edits.

## Prompt / error boundaries

The full Tool prompt is displayed by Command Line only. ToolPanel and StatusBar show complementary state.

Recoverable failures remain local to Tool/Grip/Property/Layer transactions; fatal runtime failures are not misrepresented as recovered.
