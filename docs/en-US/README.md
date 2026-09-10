# OCCAD CAD Development Specification

This documentation set is the single product, UI, interaction, and technical baseline for the `cad` branch. The second-pass review compares both the real OCCTBIM-Source module boundaries and the current OCCAD code while defining OCCAD's own .NET/WPF contracts.

## Single UI target

Native WPF classic CAD shell: `Menu + persistent ToolBar + Viewport + Docks + StatusBar + non-modal ToolPanel`. Ribbon is not a second supported shell.

## Index

- [01 Product Goals](01-PRODUCT-GOALS.md)
- [02 UI Specification](02-UI-SPEC.md)
- [03 Interaction Specification](03-INTERACTION-SPEC.md)
- [04 Architecture](04-ARCHITECTURE.md)
- [05 Entity and Tool Contract](05-ENTITY-TOOL-CONTRACT.md)
- [06 Development Plan](06-DEVELOPMENT-PLAN.md)
- [07 Current Gap Analysis](07-CURRENT-GAP-ANALYSIS.md)
- [08 UI Detail Specification](08-UI-DETAILS.md)
- [09 Robustness and Performance](09-ROBUSTNESS-PERFORMANCE.md)
- [10 Localization, Numeric Display, and Data Semantics](10-LOCALIZATION-AND-DATA.md)
- [11 Code Organization and Cleanup](11-CODE-ORGANIZATION.md)
- [12 Acceptance Checklist](12-ACCEPTANCE-CHECKLIST.md)

The Chinese tree is `../zh-CN/`; both language trees must keep matching structure and meaning.

The reference value of OCCTBIM-Source is its separation of Document, Entity, Tool, Action, Dock, Property, Grip, and Snap responsibilities—not its Qt widgets, singleton choices, or exact class names.

Development prioritizes correct interaction over entity count; staged Tools over button logic; transactional Preview/Grip/Property edits; one point-resolution pipeline; independent ByLayer and custom appearance; complete localization; full model precision behind formatted UI values; and recovery from ordinary invalid input without terminating the application.

Use 07/06 to choose work, 05 for Entity/Tool implementation, 02/08 for UI, 03 for interaction, 09 for error/performance work, 10 for localization/data behavior, and 12 as the completion gate. Finish each stage with the cleanup rules in 11.
