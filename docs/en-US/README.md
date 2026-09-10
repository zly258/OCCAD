# OCCAD Documentation Index

This tree contains the English version of OCCAD's long-lived product, user, architecture, development, extension, build, quality, and maintenance documentation.

## Recommended reading

### Using OCCAD

1. [13 User Guide](13-USER-GUIDE.md)
2. [14 Initial Release Feature Matrix](14-FEATURE-MATRIX.md)
3. [02 UI Specification](02-UI-SPEC.md)
4. [03 Interaction Specification](03-INTERACTION-SPEC.md)

### New contributors

1. [01 Product Goals](01-PRODUCT-GOALS.md)
2. [04 Architecture](04-ARCHITECTURE.md)
3. [05 Entity and Tool Contract](05-ENTITY-TOOL-CONTRACT.md)
4. [07 Code Organization](07-CODE-ORGANIZATION.md)
5. [08 Development Guide](08-DEVELOPMENT-GUIDE.md)
6. [09 Extension Guide](09-EXTENSION-GUIDE.md)
7. [10 Transaction, Event, and Native Resource Contracts](10-TRANSACTION-RESOURCE-CONTRACT.md)

### Build, validation, and maintenance

- [06 Quality and Data](06-QUALITY-AND-DATA.md)
- [11 Build and Validation](11-BUILD-VALIDATION.md)
- [12 Maintenance and Troubleshooting](12-MAINTENANCE-TROUBLESHOOTING.md)
- [ADR index](../adr/README.md)

## Topics

1. `01-PRODUCT-GOALS.md` — product scope, goals, and non-goals.
2. `02-UI-SPEC.md` — minimal compact Fluent CAD shell, panels, toolbar, and status strip.
3. `03-INTERACTION-SPEC.md` — pointer, selection, snap, grip, work-plane, and preview behavior.
4. `04-ARCHITECTURE.md` — layering, dependencies, authoritative state, components, and event boundaries.
5. `05-ENTITY-TOOL-CONTRACT.md` — implementation contracts for Entity and Tool types.
6. `06-QUALITY-AND-DATA.md` — data semantics, precision, localization, performance, and quality.
7. `07-CODE-ORGANIZATION.md` — directories, naming, repository hygiene, and code structure.
8. `08-DEVELOPMENT-GUIDE.md` — environment, workflow, coding rules, and pre-commit checks.
9. `09-EXTENSION-GUIDE.md` — standard extension paths for Entity, Tool, Action, properties, Snap/Grip, and UI adapters.
10. `10-TRANSACTION-RESOURCE-CONTRACT.md` — transaction/history/event/preview/native-ownership invariants.
11. `11-BUILD-VALIDATION.md` — local builds, native validation, and the release regression matrix.
12. `12-MAINTENANCE-TROUBLESHOOTING.md` — diagnosis, logging, native resources, and maintenance strategy.
13. `13-USER-GUIDE.md` — installation, running, UI, drafting, properties, snap, grip, save/open, and common workflows.
14. `14-FEATURE-MATRIX.md` — actual initial-release registrations, drawing methods, supported surface, and intentionally hidden functionality.

## Documentation governance

- `docs/en-US` and `docs/zh-CN` keep the same numbering and topic set.
- Documents describe durable contracts and the real current product surface, not commit-by-commit history.
- Architecture, lifecycle, public API, transaction, persistence, and extension-point changes update the matching document in the same change set.
- Cross-cutting, long-lived decisions with meaningful trade-offs require an ADR.
- Source code is authoritative when implementation and documentation disagree, but the mismatch is itself a defect.
