# OCCAD

OCCAD is an Avalonia CAD application and extensible CAD framework built on OcctCSharpBridge.

[中文说明](README.zh-CN.md)

## Scope

This repository contains the OCCAD product tree: CAD core, Avalonia desktop application, documentation and product build entrypoints. OCCAD consumes the installed OcctCSharpBridge binary SDK directly; Bridge wrapper sources and Bridge demos/tests remain in the OcctCSharpBridge repository.

The current framework includes document/entity ownership, stable registries, layers, selection/preselection/subobjects, grips, work planes, object snap and tracking, precision input, preview, history, actions/tools, 2D/3D entities, modify/modeling operations, annotations, measurement, groups/blocks, persistence and the Avalonia shell.

## Structure

- `src/OCCAD.Core` — document, entity, action/tool, selection, precision, history and persistence framework.
- `src/OCCAD.Avalonia` — Avalonia shell, menu/toolbar, OCCT viewport integration, dynamic input, native Layer/Property panels and command line.
- `docs/README.md` — documentation index.
- `docs/en-US` / `docs/zh-CN` — design and development baseline.
- `OCCAD.sln` — OCCAD solution.

## Requirements

- Windows x64 for the current packaged runtime
- .NET SDK defined by `global.json`
- Avalonia 12.1.0 restored by NuGet
- Installed OcctCSharpBridge SDK containing `OcctNet.Avalonia.dll`
- Compatible OCCT runtime

## Build and run

Install/update the shared Bridge SDK first from OcctCSharpBridge, then:

```powershell
.\build.ps1
.\run.ps1 -OcctRoot "D:\tools\occt-vc144-64"
```

Publish:

```powershell
.\publish.ps1 -OcctRoot "D:\tools\occt-vc144-64"
```

Normal OCCAD builds do not rebuild or resync OcctCSharpBridge.

## Design rules

Entity geometry and document state are authoritative; viewer objects are derived presentation state. Tools are explicit interactive state machines, and preview/commit share the same resolved-point contract. The Avalonia layer adapts Core state only and does not own duplicate business models.

Avoid reflection-based invocation, duplicate public APIs, migration/compatibility layers, excessive smoke/check frameworks, GitHub Actions, and artificial suffixes such as `Advanced`, `Extended`, `V1` or `V2`.
