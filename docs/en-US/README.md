# OCCAD CAD Development Contract

This directory contains stable product, UI, and Core implementation contracts only. Temporary plans, gap matrices, commit logs, local validation transcripts, and UI experiments are excluded.

- [01 Product Goals](01-PRODUCT-GOALS.md)
- [02 UI Specification](02-UI-SPEC.md)
- [03 Interaction Specification](03-INTERACTION-SPEC.md)
- [04 Architecture](04-ARCHITECTURE.md)
- [05 Entity and Tool Contract](05-ENTITY-TOOL-CONTRACT.md)
- [06 Quality and Data](06-QUALITY-AND-DATA.md)
- [07 Code Organization](07-CODE-ORGANIZATION.md)

`OCCAD.Avalonia` now owns only the compact CAD shell: Ribbon, Model, Viewport, Property/Layer Inspector, Command Line, and Status. The UI must not duplicate Core state or restore decorative header bands, fake document tabs, duplicate toolbars, or placeholder commands without a real workflow.

`OCCTBIM-Source/release-1.0` is the behavioral and responsibility-boundary reference for Document / Entity / Layer / Property / Tool / Grip / Snap / Settings and primary CAD interaction. OCCAD retains its C#/.NET/OcctCSharpBridge architecture and does not copy Qt widgets, singleton patterns, database implementation, or concrete class hierarchy literally.

Core rules: entity geometry and document/model state are authoritative; layer references use stable IDs while names remain editable metadata; property and tool parameter metadata are UI-neutral Core schemas; Selection, Snap, Grip, Preview, WorkPlane, History, and Settings each have one Core owner; viewer/presentation state is derived; Avalonia may present and invoke Core but may not create a second CAD state model.
