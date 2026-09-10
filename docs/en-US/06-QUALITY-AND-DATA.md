# 06 Quality and Data

## Stability

Recoverable invalid input, degenerate geometry, preview failure, grip failure, property failure, and file parse failure must not terminate the application or leave Document, Selection, Preview, Grip, WorkPlane, Drafting, or History inconsistent.

Use local transaction boundaries. Validate finite values, dimensions, vectors, indices, topology references, work-plane intersections, and viewer-object lifetime before mutation. Do not swallow fatal runtime failures such as OutOfMemoryException, StackOverflowException, or AccessViolationException.

## Preview and Grip

Preview builds transient state only. A failed frame keeps the last valid preview. Cancel and completion remove transient presentation. Grip edit works on a duplicate preview; Accept applies one atomic change to the real entity and records one history item; Cancel restores the original state.

## Performance

PointerMove must avoid whole-panel refreshes, repeated marker construction, duplicate redraws, synchronous I/O, and unconditional delete/recreate. Prefer local presentation updates, cached snap/grip data, batches, and incremental tree/panel refreshes.

Large loads should separate read/parse/model/presentation stages and expose bottom-status progress. Viewer calls must respect OCCT thread ownership.

## Localization

Chinese and English are first-class. Visible Menu, Tool, ToolPanel, Property, enum values, prompts, errors, status, snap names, grip results, and file messages use stable resource keys. Internal IDs, EntityType, Action IDs, and serialization field names stay language-independent.

## Numeric and unit semantics

Engineering values keep full model precision. UI formatting may show concise decimals but must not round stored state, history snapshots, or serialized values. Parsing rejects empty, NaN, Infinity, and out-of-range values locally.

Units belong to display/parse conversion; entities store canonical values.

## Appearance and data

Color, line width, and line style keep independent ByLayer flags and override values. Turning ByLayer on does not destroy the stored override.

Serialization writes stable identifiers, precise numeric state, and language-independent enum/type values. Compatibility handling, when unavoidable for persisted files, stays at serializer boundaries and does not leak into public entity or UI contracts.

## Release quality

A stable release requires a clean Release build and manual verification of representative 2D drawing, 3D modeling, editing, snapping, grips, property/layer edits, undo/redo, save/open, language switching, DPI scaling, and invalid-input recovery. This is a release practice, not a permanently versioned progress checklist.
