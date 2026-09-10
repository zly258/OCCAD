# OCCAD CAD Core Contract

These documents define stable domain and implementation rules while the UI is intentionally absent.

- [01 Product Goals](01-PRODUCT-GOALS.md)
- [03 Interaction Specification](03-INTERACTION-SPEC.md)
- [04 Architecture](04-ARCHITECTURE.md)
- [05 Entity and Tool Contract](05-ENTITY-TOOL-CONTRACT.md)
- [06 Quality and Data](06-QUALITY-AND-DATA.md)
- [07 Code Organization](07-CODE-ORGANIZATION.md)

Core rule: Entity geometry and Document state are authoritative. Viewer state is derived. Tool is a staged state machine. Preview, Grip, Snap, Property, History, Selection, Layer and Settings each have one Core owner. Avalonia may eventually present and invoke those services, but it must not create a second business model.

The old UI specification was removed together with the old UI implementation. A new UI contract is blocked until core alignment is complete.
