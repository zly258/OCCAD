# 08 Development Guide

## 1. Development priorities

Daily OCCAD development prioritizes:

1. Correct interaction behavior.
2. Model/presentation coherence.
3. Atomic Undo/Redo.
4. Fully neutral Tool teardown.
5. No native/transient ghost state.
6. Extensions that preserve existing ownership and dependency boundaries.

Do not refactor stable paths merely for stylistic uniformity. Prefer reproducible defects and explicit architectural debt.

## 2. Environment

Repository files are authoritative:

- .NET SDK: `global.json`
- Solution: `OCCAD.sln`
- Core: `src/OCCAD.Core`
- Avalonia: `src/OCCAD.Avalonia`
- Bridge SDK default: `C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`

Override the Bridge SDK path with `OCCTCSHARPBRIDGE_SDK`.

Primary Windows validation entry point:

```powershell
git pull
.\build.ps1
```

Run with:

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

A portable Bridge runtime removes the need for a separate `OcctRoot`.

## 3. Standard workflow

### 3.1 Locate the responsibility layer first

Classify the feature as Domain, Interaction, Presentation, or Persistence, then find the single owner. Do not start by adding logic to a UI event handler.

### 3.2 Core first

Business behavior belongs in Core before adding an Avalonia adapter:

- Entity semantics → `Entities`
- Document membership/presentation → `Document`
- Tool lifecycle → `Interaction`
- Selection → `Selection`
- Snap → `Snapping`
- Grip → `Grips`
- History → `History`
- Layers → `Layers`
- Property transactions → `CadPropertyTransaction` / `CadTransaction`
- Actions → `Actions` / `CadCoreRegistration`

### 3.3 Define failure semantics before implementation

For every model/native mutation define:

- the commit point;
- rollback conditions;
- history-installation point;
- whether Tool completion is part of commit;
- transient release timing;
- whether observer failure may affect the business result.

### 3.4 Add regression tests

Test invariants, not internal implementation details. Typical assertions include one Undo per user action, no history for invalid input, fully neutral cancellation, isolated notification failures, and rollback on strict native presentation failure.

### 3.5 Add UI last

UI code collects parameters, calls Core, observes notification, and renders visual state. It must not duplicate transactions or geometry rules.

## 4. Coding rules

### Naming

- Types use the `CadXxx` convention.
- `Manager` means a real lifecycle owner, not a generic helper.
- `TryXxx` is for expected failure branches.
- `CaptureXxx` returns snapshots.
- `RestoreXxx` restores snapshots.
- `PublishXxx` means notification and should not hide model mutation.
- `StateChanged` is reserved for strict internal propagation.

Avoid artificial suffixes such as `Advanced`, `Extended`, `New`, `V1`, `V2`, and vague utility buckets.

### Files and visibility

- Prefer one primary type per file.
- Extract ownership-focused components from oversized controllers instead of adding more regions.
- Use the minimum visibility required.
- Public APIs require stable semantics, validation, documentation, and tests.

## 5. Exception rules

Use argument exceptions for invalid input and `InvalidOperationException` for broken runtime invariants.

Recoverable native failures may be suppressed only at explicit presentation/transient-cleanup boundaries and must remain diagnosable.

Fatal failures such as `OutOfMemoryException`, `StackOverflowException`, and `AccessViolationException` are never treated as ordinary recoverable errors. Wrapped/aggregate errors must be classified recursively.

## 6. Event rules

Every new event must be classified during review as:

- pre-state veto;
- strict internal propagation; or
- post-state notification.

Public notifications normally isolate observers individually. Direct multicast invocation is appropriate only when the event contract is intentionally strict/veto.

## 7. Transaction rules

- Prefer lightweight history entries for frequent small edits.
- Use explicit `CadTransaction` for compound batch edits.
- Do not install nested Undo inside an outer transaction.
- No-op operations must not pollute History or modified state.
- Undo/Redo implementations must handle partial failure and rollback.

## 8. Native API rules

Every native object must immediately have an owner. Code must answer where the handle is stored, who deletes it, what engine recreation does, whether deletion failure loses ownership, and whether a failed cleanup can leave an object visible/selectable.

Temporary native objects must not rely on GC/finalizers for correctness.

## 9. Performance rules

High-frequency pointer paths should avoid full Document snapshots, unnecessary large allocations, repeated presentation rebuilds, redundant redraw, and repeated expensive snap geometry evaluation. Caches must never weaken invalidation correctness.

## 10. Pre-commit checklist

Core changes:

- ownership is explicit;
- no-op does not create history;
- rollback is defined;
- Undo/Redo are symmetric;
- event semantics are correct;
- Tool cancel/deactivate returns neutral;
- transient state never enters Document/History;
- native handles are not forgotten;
- new extension points include tests and docs.

UI changes additionally verify no duplicated Core business logic, DPI-safe layout, non-duplicated prompt surfaces, and complete localization.

## 11. Avoid

- GitHub Actions by default.
- Reflection-based command dispatch.
- Compatibility layers that preserve incorrect APIs.
- Parallel frameworks added to solve one bug.
- Broad entity expansion before core interaction loops are stable.
