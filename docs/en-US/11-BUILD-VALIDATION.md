# 11 Build and Manual Validation

## 1. Current validation strategy

OCCAD currently does not maintain a separate Test project and does not use GitHub Actions as the product acceptance path. The main solution contains only:

- `OCCAD.Core`
- `OCCAD.Avalonia`

The authoritative validation order is:

```text
Static inspection
→ local build.ps1
→ run the real application
→ manual/native interaction regression
→ documentation/product-surface review
```

Never claim a successful Windows build without actual `build.ps1` output.

## 2. Windows build baseline

Windows x64 is the primary product environment. The authoritative entry point is:

```powershell
.\build.ps1
```

A build must confirm at least:

- the OCCAD source SHA printed by the script matches the main HEAD being validated;
- Bridge SDK path is valid;
- Bridge source/runtime diagnostics are as expected;
- `OCCAD.Core` compiles;
- `OCCAD.Avalonia` compiles;
- no nullable/compiler warning survives `TreatWarningsAsErrors`;
- application output contains the required Bridge runtime.

Default SDK:

```text
C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64
```

Override with `OCCTCSHARPBRIDGE_SDK` when required.

## 3. Runtime

When the Bridge SDK is a flat runtime and does not contain a complete OCCT runtime, run with:

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

or configure `OCCT_ROOT` / `CASROOT`.

`OCCT resources: False` does not automatically mean the build contract is invalid. Compilation/Bridge contract and final runtime-resource configuration are separate concerns.

## 4. Linux build

Linux uses:

```bash
./build.sh
```

Linux is useful for cross-platform compilation/runtime checks but does not replace Windows initial-release acceptance.

## 5. Initial release feature surface

2D: Point, Line, Polyline, Polygon, Regular Polygon, Rectangle, Circle, Arc, Ellipse, Spline.

3D and curves: Box, Cylinder, Cone, Frustum, Sphere, Ellipsoid, Torus, Helix.

Modeling: Extrude, Revolve, Sweep, Loft.

Interaction infrastructure: Entity/Subobject Selection, Preselection, Window/Crossing, Snap, Grip, XY/YZ/XZ WorkPlane, ORTHO, POLAR, Preview, Tracking, Precision Input, Layer, Property, History/Transaction, persistence/exchange entry points.

See [14-FEATURE-MATRIX.md](14-FEATURE-MATRIX.md).

## 6. Required Create-loop validation

For every supported creation Tool validate:

```text
Start Tool
→ Pointer / parameter path
→ Preview
→ Commit
→ Select result
→ Inspect/Edit Properties
→ Grip where applicable
→ Undo/Redo where reachable
→ Cancel/exit
```

Check that final Preview does not remain as a ghost, no Tool-owned transient remains, Snap/Tracking markers clear, Grip markers match the formal Entity, WorkPlane/Drafting state is not corrupted, and model/native presentation do not diverge.

## 7. Drawing-method validation

Validate each exposed entry, not only the default Method.

Circle: Center+Radius, Center+Diameter, Two Points, Three Points, Point+Center.

Arc: Three Points, Center→Start→End, Start→Center→End, Start→End→Center, Start→End→Point, Start→End→Tangent.

Ellipse: Center+Axes, Axis Endpoints+Minor Axis.

Regular Polygon: Inscribed, Circumscribed.

Confirm that each Action supplies the intended initial parameter to the shared Tool.

## 8. Tool parameter strip and live synchronization

Validate the fixed bottom parameter strip as a layout and state contract:

1. start a parameterized Tool and confirm its editors appear immediately above the status strip;
2. switch to a Tool with no parameters or cancel the Tool and confirm the reserved row height does not change;
3. verify the viewport does not jump vertically;
4. edit a parameter and verify Preview changes immediately;
5. move the pointer and verify common actual dimensions flow back to the displayed parameter values;
6. verify a focused TextBox/ComboBox/CheckBox is not rebuilt or overwritten;
7. after ToolChanged, ToolUpdated, and language switching, verify the parameter surface still has exactly one Visual Parent.

Live values that must remain covered include Circle Radius/Diameter, Rectangle Width/Height, Ellipse Major/Minor Radius, Box Length/Width/Height, and Cylinder Radius/Height.

## 9. Work-plane switching

Work-plane regression must include changes **while a Tool is active**, not only idle button checks.

For 2D Tools such as Line, Polyline, Rectangle, Circle, Arc, Ellipse, and Regular Polygon:

```text
Start Tool
→ accept zero or one construction point
→ switch XY/YZ/XZ
→ move pointer
→ inspect Preview plane
→ Commit
→ inspect entity Normal/Axis
```

For staged 3D Tools such as Box and Cylinder also validate immediate switching in compatible stages; safe StepBack from fixed temporary ToolPlane stages; no mixing of accepted points across incompatible frames; blocking by user-plane lock/fixed Grip plane; and Snap/Tracking recomputation after the change.

## 10. Escape validation

Press `Esc` with focus in the viewport, a Tool parameter TextBox, and another normal main-window control.

Every case must end with no active Tool, empty Entity Selection, empty Subobject Selection, empty Preselection, cleared Snap/Tracking transient state, viewport focus restored, and no preview residue.

## 11. Property / Normal / Layer validation

Validate at least:

- numeric editors are left aligned;
- labels/values do not use unexpected right alignment;
- editable `Normal` exists for Circle/Arc/Ellipse/Rectangle/RegularPolygon;
- zero/invalid normal vectors are rejected;
- normal edits synchronize geometry and Viewer presentation;
- property edits create correct history behavior;
- top Layer ComboBox and Layer panel agree on current layer;
- Layer Line Style / Line Width ComboBoxes have enough width to display readable values;
- ByLayer appearance resolves correctly.

## 12. Selection / Snap / Grip validation

Selection covers Window, Crossing, Replace/Add/Remove/Toggle, and Entity/Subobject scope.

Snap should at least exercise Endpoint, Midpoint, Center, Intersection, Perpendicular, and Tangent, including candidate/current marker cleanup after Tool completion/Cancel.

Grip validation includes marker display, hot state, drag preview, valid commit, invalid-target no-pollution behavior, and cleanup.

## 13. View validation

All fixed-view controls must execute real Actions: six orthographic views, four corner-isometric views, Fill (internal Action `view.fit`), Wireframe, and Shaded.

Also confirm ViewCube remains disabled before and after engine recreation and the lower-left triedron remains available.

## 14. UI / Dialog / DPI validation

Verify that:

- Avalonia native FluentTheme is used;
- the top contains Menu plus no more than two high-frequency Toolbar rows;
- Toolbar row 1 contains Undo/Redo, current Layer, frequent views, Fill, and display modes;
- Toolbar row 2 contains only frequent 2D / 3D / Feature commands;
- New/Open/Save and language switching remain in Menu rather than duplicating toolbar space;
- no Ribbon, legacy CleanShell, grouped three-row command surface, or Floating Tool Panel appears;
- the Tool parameter strip is fixed directly above the status strip;
- no permanent Command Input is visible;
- no `Command: Ready`, `命令：就绪`, or `Ready - OCCT` text is shown;
- the status strip contains the current operation prompt plus XY/YZ/XZ, SNAP, ORTHO, POLAR;
- Model / Properties / Layers layout is stable;
- Settings labels auto-size from the longest current-language label while the value column consumes remaining width;
- Message/Layer/Color/Error/Settings dialog action captions are horizontally and vertically centered;
- 125% / 150% DPI causes no obvious clipping, drift, or blur;
- errors remain visible through Dialog/Error Window surfaces.

## 15. Save/Open validation

Perform at least one round trip:

```text
Create mixed 2D/3D entities
→ change Layer/Properties
→ Save .ocad
→ close/new
→ Open .ocad
→ compare geometry/properties/presentation
```

Preview/Snap/Grip/Selection and other transient state must not be serialized.

## 16. Exchange validation

A file-picker extension alone does not prove complete bidirectional format support. Validate every externally claimed Import/Export direction separately and record its Bridge/OCCT runtime requirements.

## 17. Build-failure handling order

When `build.ps1` fails:

1. verify the OCCAD source SHA in the output;
2. fix the first real compiler error;
3. do not hide nullable/contract issues with `!`, empty implementations, or compatibility stubs;
4. decide whether the failure belongs to the current product surface or historical residue;
5. fix required product dependencies and delete truly unused residue;
6. build again;
7. only after compilation succeeds, run manual regression.

## 18. Definition of done

An initial-release change is complete only when both current projects compile on local Windows, the affected real user flow executes, no fake command or transient residue is added, model/history/native presentation remain coherent, UI and documentation agree, both supported languages work, and new code has explicit ownership/lifecycle.

Before real build and manual validation, describe the work only as modified/pending validation, never as passed.
