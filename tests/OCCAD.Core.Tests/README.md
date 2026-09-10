# Core interaction regression tests

Requires the .NET 10 SDK and the shared OcctCSharpBridge SDK used by the main solution.

Run managed tests (native viewer cases are explicitly skipped):

```sh
dotnet test --project tests/OCCAD.Core.Tests -p:Platform=x64
```

Run all tests, including real OCCT presentation and history checks, on Linux with an X11 display and the SDK's native dependencies installed:

```sh
OCCAD_NATIVE_TESTS=1 dotnet test --project tests/OCCAD.Core.Tests -p:Platform=x64
```

Native tests create and destroy their own X11 windows and OCCT engines. They do not attach to the running CAD application. The native fixture currently supports Linux only.

Coverage follows the current compact OCCTBIM-aligned surface: entity selection operations, topology masks and scope isolation, preselection filtering, placement axes, active tool cancel/cleanup, common 2D drawing preview/commit, grip editing, native topology regeneration, undo/redo, layers, properties, snapping, center-line/center-mark dependencies, and the common Move/Copy/Rotate/Scale/Mirror/Array edit family.

Trim, Extend, Offset, Fillet and Chamfer are intentionally outside the current product surface and therefore have no active tool or native-regression contract in this branch.

`InteractionTests.AssertNeutral` checks the manager's neutral-state violations. Native tests additionally check scene object counts so logical cleanup cannot hide orphan presentation objects.

## Interaction contracts

- `CadEntityFilterKind` / `CadEntityFilter` classify whole entities. `CadSelectionScope` and `CadSubshapeMask` control topology selection. A tool declares its scope; activation overrides and deactivation restores the user's scope and mask.
- All tool geometry previews go through `CadPreviewManager`. Passing a live document entity as a preview is rejected. Replacement-source restoration and retry ownership live with this manager for the tools that need replacement preview semantics.
- Grip drag feedback belongs to `CadGripManager`, separate from model previews. Tools must not allocate native presentation objects directly.
- A grip's explicit plane takes precedence; otherwise its entity placement supplies world-space local axes. Fixed grip planes reject drawing-plane shortcuts.
- Neutral cleanup attempts every channel even when one cleanup operation fails. Failed native deletions retain ownership for a later retry.

Additional regressions cover mixed transactions, rollback and redo preservation, no-op edits, atomic property failures and arrays, domain events, owner-based transient cleanup, command/property descriptors, coordinate input, common accept routing, snap priority and pixel hysteresis.

Windows native UI, DPI variations and prolonged manual sessions remain separate acceptance work.
