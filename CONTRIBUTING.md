# Contributing to OCCAD

OCCAD is maintained as a CAD product core, not as an OCCT API demo. Contributions should preserve ownership, transaction/history semantics, Tool lifecycle, native-resource cleanup, and the separation between Core and Avalonia.

## Start here

Read:

- `docs/README.md`
- `docs/en-US/04-ARCHITECTURE.md` or `docs/zh-CN/04-ARCHITECTURE.md`
- `docs/en-US/08-DEVELOPMENT-GUIDE.md` or `docs/zh-CN/08-DEVELOPMENT-GUIDE.md`
- `docs/en-US/10-TRANSACTION-RESOURCE-CONTRACT.md` or `docs/zh-CN/10-TRANSACTION-RESOURCE-CONTRACT.md`
- `docs/en-US/11-TESTING-VALIDATION.md` or `docs/zh-CN/11-TESTING-VALIDATION.md`

## Change policy

Prefer small changes with one clear responsibility. Do not combine unrelated architecture, UI styling, entity expansion, and bug fixes into one change.

For Core work, identify the authoritative state owner and failure/rollback semantics before implementation.

For UI work, keep Avalonia as an adapter over Core. Do not create a second command system, transaction path, property model, layer model, or formal selection model.

## Build

Primary Windows validation:

```powershell
.\build.ps1
```

Normal builds must not clone or rebuild OcctCSharpBridge.

## Tests

Add regression coverage for changed invariants. Prefer non-native tests when native presentation is not required. Use native tests for ViewerObject mapping, object counts, Snap/Grip/Preview markers, native selection, rectangle queries, and presentation consistency.

If native tests cannot run in the current environment, say so explicitly. Do not report unexecuted tests as passed.

## Core review checklist

- [ ] Single owner is clear.
- [ ] Authoritative state point is clear.
- [ ] No-op does not create History/Modified state.
- [ ] Rollback is defined for failure paths.
- [ ] Undo/Redo are symmetric.
- [ ] Tool cancel/deactivate returns neutral.
- [ ] Preview/transient state does not enter Document/History.
- [ ] Native handles cannot be forgotten after failed cleanup.
- [ ] Event type is classified as veto, strict propagation, or notification.
- [ ] Public notification observers are isolated when state is already authoritative.
- [ ] New public extension points have documentation and tests.

## UI review checklist

- [ ] No duplicated Core business logic.
- [ ] No direct viewer state used as business truth.
- [ ] DPI/scaling remains usable at common Windows scaling factors.
- [ ] Prompt information is not redundantly duplicated across Command Line, Floating Tool Panel, and Status Bar.
- [ ] Localization resources are updated where required.

## Architecture decisions

Use `docs/adr/0000-template.md` when a change alters dependency direction, ownership, Tool lifecycle, transaction/history semantics, persistence compatibility, plugin/global-service policy, or threading/native scheduling.

## Repository hygiene

Avoid:

- generated build artifacts;
- local SDK paths committed into source;
- temporary investigation notes in long-lived docs;
- reflection-based dispatch;
- compatibility layers that preserve incorrect APIs;
- artificial type suffixes such as `Advanced`, `Extended`, `V1`, or `V2`;
- GitHub Actions unless project policy explicitly changes through an accepted architecture decision.

## Documentation

When behavior or architecture changes, update the matching document in `docs/en-US` and `docs/zh-CN` in the same change. Long-lived architectural trade-offs also require an ADR.
