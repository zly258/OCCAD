# 06 Quality and Data

## Stability
Recoverable invalid input, degenerate geometry, Preview/Grip/Property/Layer/file failures must not terminate the app or leave inconsistent Document, Selection, Preview, Grip, WorkPlane, Drafting, or History state. Validate before mutation when possible and roll back real-state mutations on failure. Fatal exceptions are not treated as safely recoverable.

## Preview and Grip
Preview is transient; failed updates keep the last valid preview. Grip editing modifies a duplicate preview only. The real source presentation is suppressed during edit so source and preview are not drawn together. Accept applies once and records one History entry.

## Redraw and performance
Do not add explicit redraws after Bridge operations that already request redraw or after display-batch disposal already performs pending redraw. PointerMove avoids synchronous I/O, panel rebuilds, repeated marker creation, duplicate redraw, unnecessary delete/recreate, and persistent geometry mutation. Prefer display batches, cached Snap/Grip data, and incremental UI refresh.

## Large data
Large file/model operations separate read, parse, model creation, and presentation stages. Progress belongs in the lower application status area. OCCT viewer calls obey their owning UI/viewer thread contract.

## Localization
English and Chinese resource maps remain key-synchronized. Visible UI text uses stable localization keys. Internal IDs, EntityType, Action IDs, Tool IDs, enum serialization values, and persistence field names remain language-neutral.

## Numeric and appearance data
Model/history/persistence keep full numeric precision. Reject NaN, Infinity, invalid ranges, degenerate directions, and invalid topology indices at Core boundaries.

Entity stores Color/LineStyle/LineWidth overrides plus independent ByLayer flags. Effective values come from Document + Layer. ByLayer never destroys the stored override. Multi-selection mixed values display as `—`.

## History
UI-facing mutations create one coherent History record or none. Property and Layer UI use Core mutation paths. Undo/Redo cancels the active Tool and clears stale Selection/Subobject/Preselection/Grip references before restoring history state.

## Release validation
Manually validate representative 2D/3D drawing, modify operations, Snap, Tracking, Grip, Property, Layer, Undo/Redo, Save/Open, language switching, DPI scaling, Preview cleanup, selection rectangle cleanup, and invalid-input recovery. Do not replace this with large temporary smoke/check script frameworks.
