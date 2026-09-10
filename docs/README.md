# OCCAD Documentation

This directory contains OCCAD's long-lived product, user, architecture, command, development, build, release, quality, and maintenance documentation.

- 中文: [zh-CN/README.md](zh-CN/README.md)
- English: [en-US/README.md](en-US/README.md)
- Architecture decisions: [adr/README.md](adr/README.md)

## Documentation set

The two language trees are kept structurally synchronized.

1. Product goals and scope
2. UI specification
3. Interaction specification
4. Architecture design
5. Entity and Tool contract
6. Quality and data semantics
7. Code organization
8. Development guide
9. Extension guide
10. Transaction, event, and native-resource contracts
11. Build and validation
12. Maintenance and troubleshooting
13. Detailed user guide
14. Initial-release feature matrix
15. Command and feature reference
16. Release and delivery guide

## Recommended reading

For end users:

`README → 13 User Guide → 14 Feature Matrix → 15 Command Reference`

For contributors:

`01 Product Goals → 04 Architecture → 05 Entity/Tool Contract → 08 Development Guide → 09 Extension Guide → 10 Transaction/Resource Contract`

For UI/interaction work:

`02 UI Specification → 03 Interaction Specification → 04 Architecture → 13 User Guide`

For release preparation:

`11 Build Validation → 14 Feature Matrix → 15 Command Reference → 16 Release Guide`

## Documentation rules

- Documentation describes stable product behavior and engineering contracts, not temporary sprint notes or commit history.
- `docs/en-US` and `docs/zh-CN` must keep the same numbered topic set.
- Architecture, lifecycle, public extension-point, transaction, persistence, command, or product-surface changes must update the matching document in the same change set.
- Source code is authoritative if implementation and documentation disagree; the mismatch is a defect to be corrected.
- Public extension points must document ownership, lifecycle, failure semantics, Undo/Redo semantics, native/thread assumptions, and validation expectations.
- Cross-cutting architectural decisions with durable trade-offs require an ADR.
- A feature is not release-ready merely because an Entity/API exists; the full user flow must be validated.

## Current architectural baseline

`CadWorkspace` is the composition root for one CAD session. Document/Entity state is authoritative; OCCT viewer objects are derived presentation. Core owns business state, transaction/history, Tool lifecycle, Selection, Snap, Grip, WorkPlane, precision input, and transient ownership. Avalonia is an adapter layer for input, viewport presentation, panels, dialogs, settings, and localization.

The initial product shell is a compact Fluent desktop shell with two compact toolbar rows, a dark viewport, Model/Layer/Property panels, explicit Chinese/English switching, a fixed Tool parameter strip, and a drafting status strip. It deliberately does not display a large Ribbon, permanent Command Line, permanent Ready text, or redundant coordinate/status noise.

## Release documentation status

The documentation set now covers the initial release surface in enough detail for user operation, maintenance, extension, and packaging. Formal release readiness still requires the real build and manual/native regression gates in [16 Release Guide](en-US/16-RELEASE-GUIDE.md) / [16 发布指南](zh-CN/16-RELEASE-GUIDE.md).
