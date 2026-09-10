# OCCAD Documentation

This directory contains long-lived product and Core implementation contracts. Temporary migration plans, gap matrices, commit logs, local validation transcripts, and UI experiments do not belong here.

- English: [en-US/README.md](en-US/README.md)
- 中文: [zh-CN/README.md](zh-CN/README.md)

The active architecture baseline is UI-independent. `src/OCCAD.Avalonia` is intentionally reduced to an empty application shell while Core ownership is being stabilized.

`OCCTBIM-Source/release-1.0` is the behavioral and responsibility-boundary reference for Document / Entity / Layer / Property / Tool / Grip / Snap / Settings. OCCAD keeps its C#/.NET/OCCT bridge architecture and does not copy Qt widgets, singleton patterns, database implementation, or class hierarchy literally.

Core rules:

- Entity geometry and document/model state are authoritative.
- Layer references use stable IDs; names are editable metadata.
- Property metadata is a Core contract, not a PropertyGrid model.
- Tool state and parameter schema are UI-neutral.
- Selection, Snap, Grip, Preview, WorkPlane, History, and Settings each have one Core owner.
- Native presentation state is derived and must be recoverable/clearable.
- Avalonia may present and invoke Core, but must not create a second CAD state model.
