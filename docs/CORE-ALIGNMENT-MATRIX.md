# OCCAD core alignment matrix

Reference: `zly258/OCCTBIM-Source`, branch `release-1.0`.

The Avalonia implementation has been intentionally reduced to an empty shell. This matrix defines what must be true before any CAD UI is rebuilt.

| Area | Reference responsibility | OCCAD target | State |
| --- | --- | --- | --- |
| Document | aggregate/ownership, entity membership, presentation sync | `CadDocument` | keep, reduce cross-domain policy |
| Entity | geometry, presentation, layer reference, properties, grip/snap definitions | `CadEntity` | migrate layer name reference to stable layer id |
| Property | name/display/value/default/group/read-only/order/editor/editor params | `CadEntityProperty` + `CadPropertyService` | domain projection added; remove future UI reflection logic |
| Layer | stable id separate from name; default id `0` | `CadLayer` | stable id added |
| Layer manager | id/name lookup, current layer, add/remove/update | `CadLayerManager` | id map + name lookup added; document/entity persistence migration pending |
| Tool | one active lifecycle and selection/drawing state | `CadTool` / `CadToolManager` | keep richer staged lifecycle |
| Preview | transient only, deterministic cleanup | `CadPreviewManager` + `CadTransientScene` | must prove zero residual presentation after every exit path |
| Grip | entity-derived grip points + grip tool | `CadGripPoint` + `CadGripManager` + `GripEditTool` | structurally aligned; atomic drag validation pending |
| Snap | entity-derived points + central candidate manager | `CadSnapPoint` + `CadSnapManager` | structurally aligned; tolerance/ranking validation pending |
| Work plane | shared drawing plane independent of camera | `CadWorkPlane` | validate all tools consume same service |
| Settings | UI-independent key/value persistence | `CadSettingsStore` + drafting settings | core store added; workspace binding pending |
| History | one logical operation = one record | `CadTransaction` + `CadHistory` | retain; add contract tests for every editor/tool path |
| UI | presentation only | blank Avalonia shell | intentionally deferred |

## Hard rules

1. No CAD state is owned by Avalonia controls, dialogs, panels or view models.
2. No second preview, snap, grip, selection, layer or property mechanism may be introduced.
3. Layer ids are immutable. Names are labels and may change.
4. Entity property surfaces consume `CadPropertyService`; they do not inspect CLR metadata independently.
5. Tools may create transient state only through the core transient/preview services.
6. Commit, cancel, tool switch, undo and exception paths must all leave transient state neutral.
7. New UI work is blocked until entity-layer persistence, property service, grip/snap contracts and interaction cleanup tests pass.

## Next semantic migration

The highest-priority remaining incompatibility is entity layer persistence. Existing entity/document/workspace serialization still treats a layer display name as the reference. The next core change must introduce `LayerId` as the canonical entity reference, persist layer `Id` and `CurrentLayerId`, and load the old name-based schema through a one-time backward reader. Layer rename must then become metadata-only and must never rewrite entity references.
