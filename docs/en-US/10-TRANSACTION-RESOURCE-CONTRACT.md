# 10 Transaction, Event, and Native Resource Contracts

## 1. Why this contract exists

The difficult part of CAD is not only computing geometry. Model, History, Tool, Preview, Selection, Snap/Grip, and native Viewer state must remain coherent on both success and failure paths. This document defines those cross-cutting invariants.

## 2. Authoritative-state principle

Every operation must define the point at which state becomes authoritative.

Typical order:

```text
validate
  ↓
mutate model / build required presentation
  ↓
complete Tool-owned lifecycle obligations
  ↓
install History
  ↓
publish notification
```

Once History is successfully installed, ordinary notification failure must not roll back model state.

## 3. Lightweight operations

Use lightweight snapshots/history for frequent operations such as Move, Rotate, Scale, Mirror, Property edit, Grip edit, and small Create/Delete sets.

Rules:

1. Capture before state.
2. Apply mutation.
3. Capture after state.
4. Detect no-op.
5. Complete Tool contract when required.
6. Install history.
7. If history is not installed and failure occurs, roll back.

High-frequency operations should not snapshot the entire Workspace.

## 4. Explicit `CadTransaction`

Use explicit transactions for compound batch edits.

An outer transaction captures a workspace snapshot, suspends inner history recording, may aggregate document change sets/display batches, installs one Undo on `Commit()`, and restores the snapshot if disposed without commit.

Nested explicit transactions are not supported. Lightweight mutations may run inside an outer transaction, but they must not create nested Undo records.

## 5. Create commit

Tool-created entities follow this semantic order:

1. Preview holds the last valid shape.
2. Persistent Entity is added to Document.
3. Tool completion/transient release succeeds.
4. Create History is installed.

If step 2 succeeds but step 3 fails while History is not installed, the newly created Entity must be removed again.

## 6. Transform, Property, and Grip commit

Use lightweight entity snapshots:

- before: snapshot each target;
- mutate: real entities;
- complete: Tool completion when applicable;
- after: final state;
- history: one user action, one entry.

If a target fails mid-operation, restore already-mutated targets in reverse order.

## 7. No-op contract

No-op operations must not pollute History or modified state. Examples include zero displacement, equivalent full rotation, scale factor 1, unchanged property/layer values, or equal before/after state/geometry snapshots.

## 8. History contract

`CadHistory` owns Undo stack, Redo stack, and `CurrentStateId`.

- Entry Undo/Redo executes before moving history pointers.
- On entry failure, stacks/state ID remain unchanged.
- `History.Changed` is post-state notification.
- Observer failure cannot make a successful Undo/Redo appear unsuccessful.

## 9. Modified state

`CadWorkspace` compares the saved history state ID with the current state ID.

- `MarkSaved()` records the current state.
- Undo back to the saved state clears modified state.
- Redo away from it marks modified state.
- Notification failure cannot alter this truth.

## 10. Event contract

### Strict events

Used for pre-state validation/veto and mandatory internal model→document→native synchronization. Exceptions propagate.

### Notification events

Used for UI refresh, HUD/status updates, and integration observers.

```text
state already authoritative
for each observer:
    try invoke
    catch recoverable -> diagnostic only
```

One observer may not starve later observers.

## 11. Recoverable vs fatal

Presentation/cleanup layers may suppress explicitly recoverable failures while retaining diagnostics and ownership.

At minimum, `OutOfMemoryException`, `StackOverflowException`, and `AccessViolationException` are fatal. Wrapped and aggregate errors must be classified recursively.

## 12. Preview contract

Preview never belongs to Document, Selection, or History. Tool owns its semantic lifetime. Cleanup failure may not lose native-handle ownership. Replacement preview must be able to restore source visibility/presentation.

The goal is to avoid both an invalid blank frame between preview and commit and ghost preview objects after completion/cancel.

## 13. `CadTransientScene`

Typical channels include ToolPreview, Snap, Tracking, GripDrag, Preselection, SelectionWindow, and SelectionMarkers.

Tool-lifetime cleanup is strict. `ClearOwner(owner)` attempts every matching channel, releases owner/session bookkeeping even if individual cleanup fails, then reports cleanup failure.

Workspace-lifetime `ClearAll()` uses best-effort handling for recoverable cleanup failure so one transient channel cannot abort workspace teardown.

## 14. Native handle ownership

Every native object follows:

```text
create → owner stores handle immediately
      → configure
      → use
      → deletion confirmed
      → forget handle
```

Never lose a handle because configuration or deletion threw.

For recoverable deletion failure:

- keep handles in a temporary/retired collection;
- retry batch then individual deletion when appropriate;
- if still alive, make the object hidden and non-selectable;
- retain the handle for a later cleanup retry.

## 15. Engine recreation

Engine recreation must rebuild persistent Document presentation, best-effort clean stale old-engine presentation, reattach transient managers, avoid reusing old-engine handles, and preserve formal model/selection state according to product contracts.

## 16. Undo/Redo neutralization

Before moving History, Workspace first leaves active interaction:

- ActiveTool → null;
- Tool owner → 0;
- Preview/Snap/Tracking/GripDrag/Preselection cleared;
- temporary snap modes reset;
- pointer observation cleared;
- formal selection/grips handled according to the current product contract.

Only then may History move.

## 17. Dispose contract

Dispose is teardown, not a normal business transaction.

- Unsubscribe internal observers first to avoid re-entry.
- Attempt cleanup steps independently.
- Record recoverable failures and continue.
- Do not silently swallow fatal failures.
- Clear engine and pointer references at the end.

## 18. Review questions

For cross-cutting transaction/native changes reviewers must be able to answer:

- Where is the authoritative point?
- When is History installed?
- Who rolls back failure?
- Can this observer veto?
- Can a native handle be lost?
- Does Tool owner always return to zero?
- Does Undo/Redo represent exactly one user-semantic step?
- Can one cleanup failure abort the entire dispose chain?
