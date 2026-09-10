# OCCAD

OCCAD is a WPF CAD application and extensible CAD framework built on OcctCSharpBridge.

[中文说明](README.zh-CN.md)

## Scope

This repository contains the OCCAD product tree: CAD core, WPF application, documentation and product build entrypoints. OCCAD consumes OcctCSharpBridge through the binary SDK in `external/OcctCSharpBridge/win-x64`; Bridge wrapper sources and Bridge demo/test trees remain in the OcctCSharpBridge repository.

Current framework scope includes document/entity ownership, stable entity registration, layers, selection and preselection, persistent sub-object selection, grips, work planes, object snap and tracking, precision input, preview, history, actions/tools, basic 2D and 3D entities, basic modify operations, Group ownership, Block definitions/references, document persistence and the WPF shell.

## Structure

- `src/OCCAD.Core` — document, entity, action/tool, selection, precision, history and persistence framework.
- `src/OCCAD.Wpf` — WPF shell, menu/toolbars, viewport integration, dynamic input and property UI.
- `docs/README.md` — documentation index.
- `docs/en-US` — English design and development baseline.
- `docs/zh-CN` — Chinese design and development baseline.
- `OCCAD.sln` — OCCAD solution.

## Requirements

- Windows x64
- .NET SDK defined by `global.json`
- OCCT runtime compatible with the configured OcctCSharpBridge binary SDK

## Build and run

Refresh the Bridge SDK only when its contract changes or the local binary SDK is missing:

```powershell
.\build.ps1 -SyncBridge -BridgeBranch main -OcctRoot "D:\tools\occt-vc144-64"
```

Normal build:

```powershell
.\build.ps1
```

Run:

```powershell
.\run.ps1 -OcctRoot "D:\tools\occt-vc144-64"
```

Publish:

```powershell
.\publish.ps1 -OcctRoot "D:\tools\occt-vc144-64"
```

Normal builds do not rebuild or resync OcctCSharpBridge.

## Design rules

Entity geometry and document state are authoritative; viewer objects are derived presentation state. Tools are explicit interactive state machines, and preview and commit use the same resolved-point contract. New entity/tool implementations should normally use one concrete type per file, stable IDs, registries and explicit ownership.

Avoid reflection-based invocation, duplicate public APIs, migration/compatibility layers, smoke/check frameworks, GitHub Actions, and artificial suffixes such as `Advanced`, `Extended`, `V1` or `V2`.
