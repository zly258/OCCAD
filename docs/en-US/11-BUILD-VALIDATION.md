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

or configure:

```text
OCCT_ROOT
CASROOT
```

`OCCT resources: False` does not automatically mean the build contract is invalid. Compilation/Bridge contract and final runtime-resource configuration are separate concerns.

## 4. Linux build

Linux uses:

```bash
./build.sh
```

Linux is useful for cross-platform compilation/runtime checks but does not replace Windows initial-release acceptance.

## 5. Initial release feature surface

### 2D

- Point
- Line
- Polyline
- Polygon
- Regular Polygon: Inscribed / Circumscribed
- Rectangle
- Circle: 5 methods
- Arc: 6 methods
- Ellipse: 2 methods
- Spline

### 3D and curves

- Box
- Cylinder
- Cone
- Frustum
- Sphere
- Ellipsoid
- Torus
- Helix

### Modeling

- Extrude
- Revolve
- Sweep
- Loft

### Interaction infrastructure

- Entity/Subobject Selection
- Preselection
- Window/Crossing
- Snap
- Grip / Grip Edit
- WorkPlane XY/YZ/XZ
- ORTHO / POLAR
- Preview / Tracking / transient cleanup
- Layer / Property inspection/editing
- History / Transaction infrastructure
- persistence / exchange entry points

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

Check that:

- final Preview does not remain as a ghost;
- no Tool-owned transient remains after completion;
- Snap markers clear;
- Grip markers match the formal Entity;
- work-plane/drafting state is not corrupted;
- model and native presentation do not diverge.

## 7. Drawing-method validation

Validate each exposed entry, not only the default Method.

Circle:

1. Center + Radius
2. Center + Diameter
3. Two Points
4. Three Points
5. Point + Center

Arc:

1. Three Points
2. Center → Start → End
3. Start → Center → End
4. Start → End → Center
5. Start → End → Point
6. Start → End → Tangent

Ellipse:

1. Center + Axes
2. Axis Endpoints + Minor Axis

Regular Polygon:

1. Inscribed
2. Circumscribed

Confirm that each Action really supplies the intended initial parameter to the shared Tool.

## 8. Property / Normal / Layer validation

Validate at least:

- numeric editors are left-aligned;
- labels/values do not use unexpected right alignment;
- editable `Normal` exists for Circle/Arc/Ellipse/Rectangle/RegularPolygon;
- zero/invalid normal vectors are rejected;
- normal edits synchronize geometry and Viewer presentation;
- property edits create correct history behavior;
- top Layer ComboBox and Layer panel agree on current layer;
- ByLayer appearance resolves correctly.

## 9. Selection / Snap / Grip validation

Selection:

- left→right Window;
- right→left Crossing;
- Replace / Add / Remove / Toggle;
- Entity / Subobject scope.

Snap should at least exercise:

- Endpoint
- Midpoint
- Center
- Intersection
- Perpendicular
- Tangent

and verify candidate/current marker cleanup after Tool completion/Cancel.

Grip validation includes marker display, hot state, drag preview, valid commit, invalid-target no-pollution behavior, and cleanup.

## 10. View validation

All fixed-view controls must execute real Actions:

- 6 orthographic views;
- 4 corner-isometric views;
- Fit;
- Wireframe;
- Shaded.

Also confirm:

- ViewCube remains disabled;
- ViewCube remains disabled after engine recreation;
- lower-left triedron remains available.

## 11. UI validation

Verify that:

- FluentTheme uses Compact Density;
- there is one grouped three-row Toolbar;
- no large Ribbon, Reference Shell, or Floating Tool Panel appears;
- no bottom Command Input is visible;
- no `Command: Ready`, `命令：就绪`, or `Ready - OCCT` text is shown;
- current Layer and Chinese/English controls are present at the top;
- the bottom strip contains only XY/YZ/XZ, SNAP, ORTHO, POLAR;
- Model / Properties / Layers layout is stable;
- 125% / 150% DPI causes no obvious clipping, drift, or blur;
- errors remain visible through Dialog/Error Window surfaces.

## 12. Save/Open validation

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

## 13. Exchange validation

A file-picker extension alone does not prove complete bidirectional format support. Validate every externally claimed Import/Export direction separately and record its Bridge/OCCT runtime requirements.

## 14. Build-failure handling order

When `build.ps1` fails:

1. fix the first real compiler error;
2. do not hide real nullable/contracts issues using `!`, empty implementations, or compatibility stubs;
3. identify whether the error belongs to the initial product surface or obsolete residue;
4. required initial-release dependencies must be fixed;
5. remove historical residue only when it is truly unused;
6. after compilation succeeds, run manual regression.

## 15. Definition of done

An initial-release change is complete only when:

- both current projects compile on local Windows;
- the affected real user flow executes;
- no fake/placeholder command is added;
- no Preview/Snap/Grip/SelectionWindow artifact is introduced;
- model/history/native presentation remain coherent;
- UI and documentation agree;
- both supported languages work;
- new code has explicit ownership and lifecycle.
