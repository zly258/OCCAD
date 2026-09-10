# OCCAD CAD Development Contract

These documents define stable product and implementation rules. They do not track temporary development status.

- [01 Product Goals](01-PRODUCT-GOALS.md)
- [02 UI Specification](02-UI-SPEC.md)
- [03 Interaction Specification](03-INTERACTION-SPEC.md)
- [04 Architecture](04-ARCHITECTURE.md)
- [05 Entity and Tool Contract](05-ENTITY-TOOL-CONTRACT.md)
- [06 Quality and Data](06-QUALITY-AND-DATA.md)
- [07 Code Organization](07-CODE-ORGANIZATION.md)

Core rule: Entity geometry and Document state are authoritative. Viewer state is derived. Tool is a staged state machine. Preview, Grip, Snap, Property, History, Selection, and Layer each have one Core owner. Avalonia presents and invokes those services without creating a second business model.

OCCAD consumes the installed OcctCSharpBridge SDK directly and does not carry Bridge source, compatibility wrappers, reflection dispatch, migration frameworks, or duplicate V1/V2/Advanced/Extended APIs.
