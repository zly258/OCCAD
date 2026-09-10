# Contributing to OCCAD

OCCAD is maintained as a CAD product core, not as an OCCT API demo. Contributions must preserve ownership, transaction/history semantics, Tool lifecycle, native-resource cleanup, and the Core/Avalonia boundary.

## Start here

Read these documents before changing code:

- `docs/README.md`
- `docs/en-US/04-ARCHITECTURE.md` or `docs/zh-CN/04-ARCHITECTURE.md`
- `docs/en-US/07-CODE-ORGANIZATION.md` or `docs/zh-CN/07-CODE-ORGANIZATION.md`
- `docs/en-US/08-DEVELOPMENT-GUIDE.md` or `docs/zh-CN/08-DEVELOPMENT-GUIDE.md`
- `docs/en-US/10-TRANSACTION-RESOURCE-CONTRACT.md` or `docs/zh-CN/10-TRANSACTION-RESOURCE-CONTRACT.md`
- `docs/en-US/11-BUILD-VALIDATION.md` or `docs/zh-CN/11-BUILD-VALIDATION.md`

## Change policy

Prefer small changes with one clear responsibility. Do not combine unrelated architecture changes, UI styling, entity expansion, and bug fixes.

For Core work, identify the authoritative state owner and failure/rollback semantics before implementation.

For UI work, keep Avalonia as an adapter over Core. Do not create a second command system, transaction path, property model, layer model, selection model, or viewer-owned business state.

During release stabilization, do not broaden the product surface. Accept only build fixes, reproducible blockers, interaction/native cleanup fixes, localization corrections, documentation corrections, and build/run/publish fixes.

## Build

Primary Windows validation:

```powershell
.\build.ps1
```

Linux validation:

```bash
./build.sh
```

Normal builds consume the installed OcctCSharpBridge SDK. They do not clone, rebuild, or synchronize Bridge source automatically.

## Validation

The repository currently does not maintain a separate Test project. Validate changed invariants through the build scripts and the manual/native regression matrix in `11-BUILD-VALIDATION.md` and `16-RELEASE-GUIDE.md`.

If a build or native regression cannot run in the current environment, state that explicitly. Never report an unexecuted validation as passed.

## Core review checklist

- [ ] One authoritative owner is clear.
- [ ] Commit point and rollback path are clear.
- [ ] No-op does not create History or Modified state.
- [ ] Undo/Redo are symmetric and atomic.
- [ ] Tool cancel/deactivate returns the interaction system to neutral.
- [ ] Preview/Snap/Tracking/Grip transient state never enters Document/History.
- [ ] Native handles are retained until cleanup is confirmed.
- [ ] Events are classified as veto, strict propagation, or post-state notification.
- [ ] Recoverable notification failures do not invalidate authoritative state.
- [ ] Fatal failures are not swallowed as recoverable errors.
- [ ] New public extension points have documentation and validation.

## UI review checklist

- [ ] No duplicated Core business logic.
- [ ] Viewer/presentation state is never used as business truth.
- [ ] The Classic Shell remains compact and usable at 125%/150% DPI.
- [ ] Operation Prompt and fixed Tool Parameter Strip have distinct responsibilities and do not duplicate full command UI.
- [ ] PropertyGrid editors update Core through the formal transaction path.
- [ ] Entity Color/LineStyle/LineWidth support explicit override and ByLayer restoration.
- [ ] Localization resources are complete for Chinese and English.

## Architecture decisions

Use `docs/adr/0000-template.md` when a change alters dependency direction, state ownership, Tool lifecycle, transaction/history semantics, persistence compatibility, plugin/global-service policy, threading, or native scheduling.

## Repository hygiene

Do not commit:

- generated build/publish artifacts;
- local logs or crash dumps;
- temporary screenshots;
- local SDK/OCCT absolute paths;
- one-off migration or investigation scripts;
- generated icon pipelines that are not part of the product build;
- temporary validation output.

Avoid reflection-based dispatch, compatibility layers that preserve incorrect APIs, and artificial naming such as `Advanced`, `Extended`, `Legacy`, `Compat`, `V1`, or `V2` unless the concept itself is real and documented.

GitHub Actions are not part of the current repository policy.

## Documentation

When behavior or architecture changes, update the matching document in both `docs/en-US` and `docs/zh-CN` in the same change.

Documentation rules:

- describe current stable behavior, not commit history;
- use stable Action/Tool/Entity IDs exactly as implemented;
- distinguish **Initial Release**, **Infrastructure**, **Internal**, and **Out of Scope**;
- use `ByLayer` for inherited entity appearance and `Override` for entity-specific appearance;
- use `Preview`, `Snap`, `Tracking`, `Grip`, `Selection`, `WorkPlane`, `Tool`, `Action`, `Transaction`, and `History` consistently as product terms;
- do not claim a feature merely because a helper, Entity, or transaction API exists;
- do not duplicate long normative contracts when a canonical document already owns them; link to that document instead.

Long-lived architectural trade-offs require an ADR.
