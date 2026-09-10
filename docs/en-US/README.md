# OCCAD CAD Development Specification

This set defines long-lived product and implementation contracts. It intentionally excludes temporary development plans, current-gap lists, commit notes, and release checklists that become stale as the code changes.

## Documents

- [01 Product Goals](01-PRODUCT-GOALS.md)
- [02 UI Specification](02-UI-SPEC.md)
- [03 Interaction Specification](03-INTERACTION-SPEC.md)
- [04 Architecture](04-ARCHITECTURE.md)
- [05 Entity and Tool Contract](05-ENTITY-TOOL-CONTRACT.md)
- [06 Quality and Data](06-QUALITY-AND-DATA.md)
- [07 Code Organization](07-CODE-ORGANIZATION.md)

## Baseline rules

Correct interaction is more important than command count. Entity geometry and Document state are authoritative; viewer objects are derived presentation state. Tool is a staged state machine. Preview never becomes persistent state before commit. Grip, Snap, Property, History, and Selection each have one authoritative Core path. Avalonia adapts Core state and does not own a parallel business model.

OCCAD consumes the installed OcctCSharpBridge SDK and does not carry Bridge source, compatibility wrappers, reflection dispatch, migration frameworks, or duplicate V1/V2/Advanced/Extended APIs.
