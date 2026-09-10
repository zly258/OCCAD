# OCCAD Documentation

This directory contains OCCAD's long-lived product and implementation contracts. It intentionally avoids sprint notes, commit logs, temporary gap lists, and local validation transcripts.

- English: [en-US/README.md](en-US/README.md)
- 中文: [zh-CN/README.md](zh-CN/README.md)

Both language trees describe the same seven topics:

1. Product goals
2. UI specification
3. Interaction specification
4. Architecture
5. Entity and Tool contract
6. Quality, performance, localization, and data semantics
7. Code organization and repository hygiene

The current baseline reflects the actual Avalonia implementation: compact industrial styling through `CadTheme`, Command Line as the sole full Tool-prompt surface, resizable Layer/Property panels, integrated ByLayer appearance editors, Core-owned layer/property transactions, atomic Preview commit order, and preview-only Grip pointer edits.

`OCCTBIM-Source/release-1.0` remains a behavioral and architectural reference for Document / Entity / Tool / Action / Grip / Snap / Property responsibility boundaries. OCCAD does not copy its Qt widgets, singleton architecture, or exact class hierarchy.
