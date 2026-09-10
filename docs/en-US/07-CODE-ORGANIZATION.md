# 07 Code Organization

## Responsibilities

src/OCCAD.Core contains Actions, Document, Entities, Geometry, Grips, History, Interaction, Layers, Properties, Selection, Snapping, and WorkPlane.

src/OCCAD.Avalonia contains localization, Program, application/theme setup, MainWindow partials, viewport interaction, ToolPanel, Layer/Property panels, command line, and small UI helpers.

Avalonia owns UI/adaptation only. Core does not reference Avalonia. Entity and Tool never manipulate controls. Explicit registries remain the extension mechanism.

## Naming and files

Prefer one principal Entity or Tool per file. Shared geometry belongs in focused helpers. Do not create parallel APIs or artificial suffixes such as Advanced, Extended, Extension, Compat, V1, or V2 merely to avoid changing the correct contract.

Small helper files must justify an independent responsibility. A one-method helper with no reuse should be folded into its owner.

## State ownership

Document owns persistent entities. Workspace composes session services. ToolManager owns active Tool lifecycle. Selection/Subobject/Grip/Snap/Preview each have one Core owner. MainWindow and controllers observe and invoke these services rather than duplicating state.

## Repository hygiene

Root scripts remain build.ps1, run.ps1, and publish.ps1. Normal builds do not rebuild or sync Bridge and do not run large migration/check/smoke frameworks. No GitHub Actions are required.

Do not keep commit notes, local validation logs, obsolete UI migration documents, stale gap analyses, or generated artifacts in the repository. Documentation describes stable contracts, not transient progress.
