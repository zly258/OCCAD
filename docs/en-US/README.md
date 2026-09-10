# OCCAD Documentation Index

This tree contains the English version of OCCAD's long-lived product, user, feature, command, architecture, development, extension, build, release, quality, and maintenance documentation.

## Recommended reading

### Using OCCAD

1. [13 User Guide](13-USER-GUIDE.md)
2. [14 Initial Release Feature Matrix](14-FEATURE-MATRIX.md)
3. [15 Command and Feature Reference](15-COMMAND-REFERENCE.md)
4. [02 UI Specification](02-UI-SPEC.md)
5. [03 Interaction Specification](03-INTERACTION-SPEC.md)

### New contributors

1. [01 Product Goals](01-PRODUCT-GOALS.md)
2. [04 Architecture](04-ARCHITECTURE.md)
3. [05 Entity and Tool Contract](05-ENTITY-TOOL-CONTRACT.md)
4. [07 Code Organization](07-CODE-ORGANIZATION.md)
5. [08 Development Guide](08-DEVELOPMENT-GUIDE.md)
6. [09 Extension Guide](09-EXTENSION-GUIDE.md)
7. [10 Transaction, Event, and Native Resource Contracts](10-TRANSACTION-RESOURCE-CONTRACT.md)

### Build, validation, and release

1. [11 Build and Validation](11-BUILD-VALIDATION.md)
2. [14 Initial Release Feature Matrix](14-FEATURE-MATRIX.md)
3. [15 Command and Feature Reference](15-COMMAND-REFERENCE.md)
4. [16 Release and Delivery Guide](16-RELEASE-GUIDE.md)
5. [12 Maintenance and Troubleshooting](12-MAINTENANCE-TROUBLESHOOTING.md)
6. [06 Quality and Data](06-QUALITY-AND-DATA.md)
7. [ADR index](../adr/README.md)

## Topics

1. `01-PRODUCT-GOALS.md` — product scope, goals, and non-goals.
2. `02-UI-SPEC.md` — minimal compact Fluent CAD shell, panels, toolbar, and status strip.
3. `03-INTERACTION-SPEC.md` — pointer, selection, snap, grip, work-plane, and preview behavior.
4. `04-ARCHITECTURE.md` — layering, dependencies, authoritative state, module responsibilities, data flows, lifecycle, and event boundaries.
5. `05-ENTITY-TOOL-CONTRACT.md` — implementation contracts for Entity and Tool types.
6. `06-QUALITY-AND-DATA.md` — data semantics, precision, localization, performance, and quality.
7. `07-CODE-ORGANIZATION.md` — directories, naming, repository hygiene, and code structure.
8. `08-DEVELOPMENT-GUIDE.md` — environment, directory responsibilities, Entity/Tool/Action/Property/Snap/Grip workflows, coding rules, and release-preparation checks.
9. `09-EXTENSION-GUIDE.md` — standard extension paths for Entity, Tool, Action, properties, Snap/Grip, and UI adapters.
10. `10-TRANSACTION-RESOURCE-CONTRACT.md` — transaction/history/event/preview/native-ownership invariants.
11. `11-BUILD-VALIDATION.md` — local builds, native validation, and the release regression matrix.
12. `12-MAINTENANCE-TROUBLESHOOTING.md` — diagnosis, logging, native resources, and maintenance strategy.
13. `13-USER-GUIDE.md` — detailed operation manual: launch, exact input, 2D/3D, Move/Delete, Snap/Grip, properties, layers, persistence, and shortcuts.
14. `14-FEATURE-MATRIX.md` — actual initial-release registrations, drawing methods, support surface, and intentionally hidden functionality.
15. `15-COMMAND-REFERENCE.md` — stable Action IDs, aliases, Tools, default methods, exact-input syntax, and the formal command surface.
16. `16-RELEASE-GUIDE.md` — code freeze, Windows/Linux builds, interactive regression, package inspection, Release Notes, tags, and release gates.

## Documentation governance

- `docs/en-US` and `docs/zh-CN` keep the same numbering and topic set.
- Documents describe durable contracts and the real current product surface, not commit-by-commit history.
- Architecture, lifecycle, public API, transaction, persistence, command, and product-surface changes update the matching document in the same change set.
- Cross-cutting, long-lived decisions with meaningful trade-offs require an ADR.
- Source code is authoritative when implementation and documentation disagree, but the mismatch is itself a defect.
- The existence of an Entity, geometry helper, or transaction API does not make an incomplete interactive flow a formal product feature.

## Current release documentation status

The documentation now forms a complete user → feature → command → architecture → development → build → release path. Formal release still requires a real Windows build, application startup, and manual/native regression according to [16 Release and Delivery Guide](16-RELEASE-GUIDE.md).
