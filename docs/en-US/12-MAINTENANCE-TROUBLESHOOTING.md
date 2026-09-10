# 12 Maintenance and Troubleshooting

## 1. Maintenance goal

Maintain OCCAD by locating the broken invariant before patching UI symptoms. Most difficult defects belong to Tool lifecycle, transient ownership, Document/viewer synchronization, Selection/Snap/Grip state, transaction/history rollback, Avalonia adapter lifecycle, or engine recreation.

## 2. Recommended diagnosis order

For interaction defects check in this order:

1. Is model state correct?
2. Is History state correct?
3. Is Active Tool/stage correct?
4. Are Preview/Snap/Tracking/GripDrag states clean?
5. Do ViewerObjects match Entities?
6. Do Selection/Grips match formal state?
7. Is the remaining defect only UI presentation?

Do not start with redraw, color, or UI-handler patches that can hide a Core-state defect.

## 3. Ghost Preview

Symptoms include a final preview surviving commit/cancel or old preview appearing after Undo.

Check `CadPreviewManager.HasTransient`, `CadTransientScene.CurrentToolOwner`, Tool cancellation/deactivation completion, retained preview native handles after failed delete, and replacement-source restoration.

Do not use a full-scene redraw as a substitute for fixing ownership.

## 4. Snap marker/candidate leak

Check `Snap.Current`, `Snap.Candidates`, candidate index, `TemporaryModes`, marker handle ownership, cache invalidation after document changes, and neutral cleanup during Tool cancel/Undo.

A failed marker delete must not be followed by forgetting the handle.

## 5. Grip leak or wrong position

Check whether `Grips.Entities` matches formal Selection, geometry/property changes refresh grips, marker/grip counts match, retired markers are hidden/non-selectable, and GripDrag marker is cleared.

If model geometry is correct but grips are wrong, fix refresh/marker state rather than mutating the Entity again.

## 6. Model/viewer divergence after Undo/Redo

Verify Entity geometry/placement, History `CurrentStateId`, ViewerObject existence in the engine, document reverse mapping, and native object count.

If model state is correct but presentation is wrong, inspect strict Entity/Layer → Document → native propagation. If model state is wrong, inspect History snapshots and rollback.

Do not mask incorrect history snapshots by unconditionally calling full `Regenerate()` after Undo.

## 7. “Operation succeeded but API reported failure”

This is commonly a post-state observer exception. Inspect direct multicast `Changed?.Invoke(...)` calls and classify the event.

If state is already authoritative, isolate observers. If the event is mandatory internal synchronization, keep it strict and preserve rollback.

## 8. “Model changed but view did not”

This usually means strict internal propagation was incorrectly treated as notification and suppressed.

Inspect Entity `StateChanged` → Document, Layer `StateChanged` → LayerManager → Document, presentation rebuild/apply appearance, and caller rollback behavior.

## 9. Native object count keeps growing

Typical causes:

- preview handle lost after delete failure;
- Grip/Snap/Subobject marker partial creation;
- temporary edge/subshape not deleted;
- stale old-engine objects after engine recreation;
- workspace teardown aborted by an earlier failure.

Record persistent Entity count, engine object count, and transient-channel state before/after repeated user actions. Linear growth strongly suggests ownership leakage.

## 10. First-frame or engine-recreation problems

Check engine initialization timing, `Workspace.AttachEngine()` ordering, document presentation rebuild, manager reattachment, and Avalonia controller synchronization after engine recreation.

Avoid scattering random redraw/invalidation calls through business logic.

## 11. WorkPlane / S-F-T defects

Inspect UserPlane/ToolPlane/GripPlane, fixed/locked state, whether the current Tool stage allows plane change, Snap/Tracking cleanup after switching, and pointer re-resolution.

WorkPlane state is authoritative; UI labels are not.

## 12. Selection mismatch

Compare Core selected/primary state, native viewer selection, `Grips.Entities`, subobject selection, and validity refresh after layer lock/visibility changes.

Formal Selection is authoritative; native highlight is presentation.

## 13. Property-edit defects

Verify property changes use Core transactions, no-op filtering works, geometry properties rebuild presentation, selected grips refresh, and one property edit creates one Undo.

PropertyGrid must not implement its own rollback/history.

## 14. Dispose/exit failures

Workspace/UI teardown uses best-effort handling for recoverable cleanup failures. Check that internal observers are unsubscribed first, cleanup steps are independent, fatal exceptions are not suppressed, and transient-channel registration disposal cannot abort later event unsubscription.

## 15. Large-file maintenance strategy

Do not split a 20KB+ controller merely because it is large. Extract only when there is a clear owner, stable input/output contract, reduced native/resource ambiguity, and regression coverage.

Good extraction candidates include marker owners, schedulers, command/session coordinators, and pure geometry services. Avoid generic “Helper” extraction just to reduce line count.

## 16. Documentation drift

When source and docs disagree:

1. determine the correct current contract;
2. fix implementation or documentation;
3. add an ADR for a long-lived architectural trade-off;
4. do not preserve contradictory descriptions.

## 17. When to stop refactoring

When the core workflow

```text
Create
→ Preview
→ Exact/Pointer input
→ Commit
→ Select
→ Property
→ Grip
→ Transform
→ Undo/Redo
→ Cancel
```

is stable under build/regression, stop broad “optimization”. Continue only for reproducible defects, measured performance issues, real product requirements, or structural debt with clear measurable value.

A stable CAD core is more valuable than perpetual refactoring.
