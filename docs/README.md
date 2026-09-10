# OCCAD Documentation

This directory contains OCCAD's long-lived product, user, feature, command, architecture, development, build, release, quality, and maintenance documentation.

- 中文: [zh-CN/README.md](zh-CN/README.md)
- English: [en-US/README.md](en-US/README.md)
- Architecture decisions: [adr/README.md](adr/README.md)

## Documentation set

The two language trees are structurally synchronized:

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

`01 Product Goals → 04 Architecture → 07 Code Organization → 08 Development Guide → 09 Extension Guide → 10 Transaction/Resource Contract`

For UI/interaction work:

`02 UI Specification → 03 Interaction Specification → 04 Architecture → 13 User Guide`

For release preparation:

`11 Build Validation → 14 Feature Matrix → 15 Command Reference → 16 Release Guide`

## Canonical ownership

Do not copy long normative sections between documents. Each subject has one primary owner:

- product scope and non-goals → `01` + `14`;
- shell/layout → `02`;
- pointer/selection/snap/grip/work-plane behavior → `03`;
- dependency, ownership and lifecycle architecture → `04`;
- Entity/Tool implementation contract → `05`;
- quality/data semantics → `06`;
- source layout and repository hygiene → `07`;
- daily development workflow → `08`;
- extension procedure → `09`;
- transaction/event/native-resource invariants → `10`;
- build/runtime validation → `11`;
- troubleshooting → `12`;
- user operation → `13`;
- release feature classification → `14`;
- Action IDs, aliases and Tool mapping → `15`;
- release gate and packaging → `16`.

Other documents should state only the local consequence and link to the canonical owner.

## Terminology standard

Use these terms consistently in both language trees:

- **Entity** — persistent CAD domain object;
- **Tool** — staged interactive command state machine;
- **Action** — stable command entry point;
- **Document** — persistent Entity owner;
- **Selection / Subobject Selection / Preselection** — distinct formal interaction states;
- **Preview / Snap / Tracking / Grip** — transient interaction presentation;
- **WorkPlane** — drafting coordinate plane;
- **Transaction / History / Undo / Redo** — authoritative mutation and reversal model;
- **ByLayer** — entity appearance inherited from its Layer;
- **Override** — entity-specific Color/LineStyle/LineWidth;
- **Initial Release** — validated user-facing product surface;
- **Infrastructure** — implemented Core capability not yet a complete user workflow;
- **Internal** — implementation/persistence support not presented as user functionality;
- **Out of Scope** — intentionally excluded from the current release.

Stable identifiers such as `edit.move`, Tool IDs, registry IDs, enum names, and filenames are never translated.

## Writing rules

- Describe current stable behavior, not sprint notes or commit history.
- Prefer short normative statements over repeated rationale.
- Use code font for IDs, paths, commands, properties, enum values, and shortcuts.
- Use the exact current source name for classes and stable identifiers.
- Do not call an API/Entity/helper a product feature unless the complete user flow is exposed and validated.
- Do not document retired Ribbon, permanent Command Line, floating Tool Panel, or Dynamic HUD as current UI.
- When implementation changes, update the matching English and Chinese document in the same change set.
- Cross-cutting decisions with durable trade-offs require an ADR.
- If source and documentation disagree, source is authoritative and the mismatch is a defect.

## Current architectural baseline

`CadWorkspace` is the composition root for one CAD session. Document/Entity state is authoritative; OCCT viewer objects are derived presentation. Core owns business state, transaction/history, Tool lifecycle, Selection, Snap, Grip, WorkPlane, precision input, and transient ownership. Avalonia is an adapter layer for input, viewport presentation, panels, dialogs, settings, and localization.

The initial product shell is a compact Fluent desktop shell with two compact toolbar rows, a dark viewport, Model/Layer/Property panels, explicit Chinese/English switching, a fixed Tool parameter strip, and a drafting status strip. It deliberately does not display a large Ribbon, permanent Command Line, permanent Ready text, or redundant coordinate/status noise.

## Release status

The documentation set covers the current initial-release surface. Formal release readiness still requires the real build and manual/native regression gates in [16 Release Guide](en-US/16-RELEASE-GUIDE.md) / [16 发布指南](zh-CN/16-RELEASE-GUIDE.md).
