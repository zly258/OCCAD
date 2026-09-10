# 07 Code Organization

## Projects
`src/OCCAD.Core` contains CAD domain and interaction code: Actions, Document, Entities, Geometry, Grips, History, Interaction, Layers, Properties, Selection, Snapping, WorkPlane/Drafting/Precision, Persistence.

`src/OCCAD.Avalonia` contains presentation/adapters: Program/Application/Theme, MainWindow partials, viewport interaction, ToolPanel, Layer/Property controllers, Command Line, localization, dialogs, and UI-only helpers.

## Dependency rule
Avalonia may reference Core. Core must not reference Avalonia. MainWindow/controllers do not duplicate geometry algorithms, transaction semantics, or Layer/Property business logic.

## File naming
Prefer one principal Entity or Tool per file. Shared helpers exist only for real reusable responsibility. Do not create Advanced/Extended/Extension/Compat/Legacy/V1/V2 parallel APIs to avoid fixing the primary contract.

## State ownership
Document owns persistent entities. Workspace composes session services. LayerManager owns layer collection/current layer. ToolManager owns active Tool lifecycle. Selection/Subobject/Preselection own their own state. PreviewManager owns transient previews. GripManager owns displayed grips. SnapManager owns snap state. CadTheme owns common Avalonia metrics. Localization resources own durable UI wording.

## Mutation ownership
Entity property edits go through `CadPropertyTransaction`. Layer color/style/width/visible/locked edits go through `CadWorkspace`. Tool commits go through Core Document/History APIs. UI may validate input format, but authoritative validation/rollback stay in Core.

## Repository hygiene
Keep root build surface small: `build.ps1`, `run.ps1`, `publish.ps1`. Daily build does not rebuild/sync Bridge or automatically run large smoke/check frameworks. Do not add GitHub Actions unless policy changes. Do not commit build output, local logs, temporary screenshots, migration scripts, generated icon pipelines, or one-off validation artifacts. Documentation describes stable contracts, not commit-by-commit progress.

## Current directory layout

Avalonia: `Application/`, `Shell/`, `Viewport/`, `Input/{Cursor,DynamicInput,CommandLine}/`, `Panels/{Properties,Layers,Tools,Model}/`, `Dialogs/`, `Localization/`, `Theming/`, `Diagnostics/`. Namespaces and the Core/Avalonia assembly boundary are unchanged.

See [interaction contracts](../INTERACTION-CONTRACTS.md) for transaction, input, transient, event and descriptor ownership.
