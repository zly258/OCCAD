# 09 Robustness and Performance

## Principle

Robust CAD behavior comes from local transactions and recoverable state, not from swallowing every exception at the top level. After invalid user input, preview failure, grip failure, property error, or parse failure, Document, Selection, Preview, Grip, WorkPlane, and History must remain consistent.

## Exception boundaries

Use three layers: local Tool/Grip/Property/Document transaction rollback; input/viewport/UI dispatch containment for ordinary recoverable errors; application fallback for logging and user notification. Do not pretend to recover safely from OutOfMemory, StackOverflow, AccessViolation, or equivalent fatal states.

## Input validation

All numeric inputs validate `double.IsFinite`. Dimensions and scale factors follow semantic positive/non-zero rules; direction vectors must be valid; indices must be in range; failed work-plane intersection never produces fabricated coordinates. Geometry tolerances are centralized instead of scattered magic values.

## Preview transaction

Build the complete next entity/presentation and apply final appearance before replacing the old presentation. Failure deletes only incomplete new objects and keeps the previous valid frame. Degenerate pointer positions do not call Clear; Clear is reserved for Cancel, Complete, explicit step-back to an empty stage, and document reset.

## Grip transaction

Capture original geometry, edit an independent preview copy, restore the last valid preview on MoveGrip failure, and apply to the real entity only on Accept. One accepted drag creates one history entry.

## Viewer lifetime

Before update/delete/style calls, confirm the engine is initialized and the object still exists. Remove deleted objects from managed maps immediately. Preview, Snap, Grip, Preselection, Selection rectangle, and other overlays own separate lifecycles.

## PointerMove hot path

Avoid whole-layer/property refreshes, repeated marker pixmap construction, unchanged ToolPanel schema rebuilds, unconditional overlay recreation, multiple redraw/flush cycles, and synchronous file I/O. Prefer presentation updates and display batches.

## File loading

Stage large loads as Read → Parse → Model data → Viewer presentation → Fit/Complete with bottom status/task progress. Parsing can run in background where safe; viewer calls respect OCCT thread ownership. Cancellation releases intermediate objects and never leaves a half-replaced document.

## Logging and acceptance

Log operation, exception type, concise message, relevant context, and duration. Throttle repeated pointer errors. Interactive acceptance requires stable previews, stable snap/grip markers, no ToolPanel window recreation, no Layer-grid jumping, batched bulk display changes, and responsive cancellation during large file loads.
