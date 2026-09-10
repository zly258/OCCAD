# 16 Release and Delivery Guide

This document defines the standard path from OCCAD source state to a releasable delivery package. Release readiness must be based on **real local builds and real interactive regression**, not static inspection or documentation completeness alone.

## 1. Release objective

A releasable build should satisfy at least:

- `main` is the single source of truth for the release;
- `OCCAD.Core` and `OCCAD.Avalonia` build without errors or warnings;
- Bridge SDK/runtime layout passes script validation;
- core CAD interaction loops pass;
- no obvious Preview/Snap/Grip/Tracking ghost state;
- Undo/Redo remains atomic;
- basic Save/Open regression passes;
- Chinese/English and 125%/150% DPI remain usable;
- README, User Guide, Feature Matrix, and Command Reference match source;
- Windows/Linux packages contain the correct launcher/runtime/resources.

## 2. Release freeze

During release stabilization do not add:

- new Entity families;
- unvalidated modify Tools;
- broad directory/naming refactors;
- a different transaction/history model;
- a replacement shell architecture;
- unrelated large Import/Export work.

Allowed release-stage changes are limited to compiler fixes, reproducible blockers, interaction/transient stability fixes, localization/documentation corrections, and build/run/publish script fixes.

## 3. Version information

The repository does not currently enforce a fixed version-number policy. Semantic Versioning is recommended:

```text
MAJOR.MINOR.PATCH
```

A first stable `1.0.0` is appropriate only after all Release Gates are satisfied. If public validation is still required, use a `0.x` or prerelease tag.

Once a version number appears in packages, Releases, or file-format compatibility notes, keep it consistent for that release.

## 4. Start from a clean source state

Windows example:

```powershell
git status
git pull --ff-only
git rev-parse HEAD
```

Requirements:

- clean working tree;
- HEAD is the intended release commit;
- build logs record that commit SHA.

## 5. Bridge SDK validation

Windows default:

```text
C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64
```

Linux default:

```text
~/.local/share/OcctCSharpBridge/SDK/3.0/linux-x64
```

Override with:

```text
OCCTCSHARPBRIDGE_SDK
```

The SDK root provides managed assemblies and Bridge metadata. A portable SDK additionally provides:

```text
package-manifest.json
runtime/
occt/resources/
```

OCCAD accepts both:

```text
SDK/portable/...
```

and an SDK root that is itself the portable root.

## 6. Windows build gate

Run:

```powershell
.\build.ps1
```

Verify:

- `[build] OCCAD source` equals the intended HEAD;
- Bridge SDK/source is correct;
- runtime layout is correct;
- `OCCAD.Core` succeeds;
- `OCCAD.Avalonia` succeeds;
- no warning survives `TreatWarningsAsErrors`;
- output contains the required Bridge runtime files.

**Do not claim a successful Windows build without real successful `build.ps1` output.**

## 7. Windows startup gate

Run:

```powershell
.\run.ps1
```

Flat Bridge with external OCCT:

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

Pass conditions:

- process starts normally;
- main window becomes visible;
- viewport renders on first display without requiring pointer movement;
- OCCT engine initializes;
- no startup fatal error;
- logs do not show continuous native-resource failure.

## 8. Linux build/startup gate

```bash
./build.sh
./run.sh
```

Verify:

- `OCCAD.dll`;
- `runtime/libOcctNative.so`;
- `occt/resources/`;
- `bridge-portable-manifest.json`;
- application startup.

Linux is useful cross-platform validation but does not replace the primary Windows acceptance path.

## 9. Create regression matrix

At minimum cover:

### 2D

- Point;
- Line;
- Polyline;
- Polygon;
- Regular Polygon;
- Rectangle;
- Circle;
- Arc;
- Ellipse;
- Spline.

### 3D/Curve

- Box;
- Cylinder;
- Cone;
- Frustum;
- Sphere;
- Ellipsoid;
- Torus;
- Helix.

### Feature

- Extrude;
- Revolve;
- Sweep;
- Loft.

For each:

```text
Start
→ Pointer/parameter input
→ Preview
→ Exact Input when applicable
→ Commit
→ Select result
→ Property inspect/edit
→ Undo/Redo
→ Cancel second run
```

## 10. Drawing-method gate

### Circle

Validate all:

- Center + Radius;
- Center + Diameter;
- Two Points;
- Three Points;
- Point + Center.

### Arc

Validate all:

- Three Points;
- Center → Start → End;
- Start → Center → End;
- Start → End → Center;
- Start → End → Point;
- Start → End → Tangent.

### Ellipse

- Center + Axes;
- Axis Endpoints + Minor Axis.

### Regular Polygon

- Inscribed;
- Circumscribed;
- direct Sides input.

## 11. Modify/history gate

### Move

```text
Select multiple entities
→ confirm
→ base point
→ target point
→ Preview
→ Commit
→ Ctrl+Z
→ Ctrl+Y
```

Verify:

- Preview is replacement presentation and does not create duplicate formal objects;
- one move creates one Undo unit;
- Undo restores all moved Entities;
- Redo reapplies the complete move.

### Delete

Validate single-entity delete, multi-entity delete, Undo, and Redo.

### Not a release gate

Copy / Rotate / Scale / Mirror are not in the official interactive product surface and must not be listed as initial-release user features.

## 12. Transient/neutral gate

For Line, Circle, Arc, Rectangle, Box, Move, GripEdit, and other representative paths, test both Commit and Cancel.

After each operation verify:

- Active Tool is null;
- no Preview remains;
- no Snap marker remains;
- no Tracking remains;
- no GripDrag remains;
- Preselection is coherent;
- temporary WorkPlane state is released;
- the next Tool can start normally.

This is release-blocker-level validation.

## 13. Selection / Snap / Grip gate

Selection:

- Replace;
- Add;
- Remove;
- Toggle;
- Window;
- Crossing;
- Entity/Subobject.

Snap at minimum:

- Endpoint;
- Midpoint;
- Center;
- Intersection;
- Perpendicular;
- Tangent;
- Tab cycling.

Grip:

- normal filled rectangular marker;
- circular Hot marker;
- circular Drag marker;
- valid commit;
- invalid target;
- Esc cancel;
- cleanup.

## 14. WorkPlane / Drafting gate

For Line, Rectangle, Circle, Arc, Ellipse, and RegularPolygon:

```text
Start Tool
→ enter 0–1 stage points
→ T/S/F or XY/YZ/XZ
→ move pointer
→ inspect Preview
→ Commit
```

Also test:

- F3 SNAP;
- F8 ORTHO;
- F10 POLAR;
- Tracking recalculation;
- safe StepBack from a fixed temporary ToolPlane.

## 15. Property / Layer gate

Verify at least:

- left-aligned numeric editors;
- Layer ComboBox;
- CAD ColorTable;
- ByLayer Color/LineStyle/LineWidth;
- Normal editing for Circle/Arc/Ellipse/Rectangle/RegularPolygon;
- multi-selection common properties;
- property Undo/Redo;
- synchronization between top Current Layer ComboBox and Layers panel;
- Layer 0 cannot be removed or renamed.

## 16. Save/Open gate

Run:

```text
Create mixed 2D/3D/Feature
→ create/change Layers
→ change Properties
→ Save .ocad
→ New/close
→ Open .ocad
```

Compare:

- Entity count/types;
- geometry;
- placement;
- layer;
- appearance;
- feature references/parameters;
- presentation.

Confirm Preview/Snap/Grip/Tracking/Selection transient state was not persisted.

## 17. UI / localization / DPI gate

Test both Chinese and English:

- Menu;
- two-row Toolbar;
- Tool parameter strip;
- Operation Prompt;
- Properties;
- Layers;
- Settings;
- Message/Error dialogs.

DPI:

- 100%;
- 125%;
- 150%.

Verify no clipping/overlap, stable viewport height, readable ComboBoxes, centered dialog button text, and no reappearance of Ribbon/permanent Command Line/Ready noise.

## 18. Windows publish package

Run:

```powershell
.\publish.ps1
```

Default output:

```text
artifacts\publish\OCCAD
```

Verify at least:

- `OCCAD.exe` / `OCCAD.dll`;
- `OcctNet.dll`;
- `OcctNet.Avalonia.dll`;
- `bridge-contract.json`;
- `bridge-manifest.json`;
- portable `runtime/` when applicable;
- portable `occt/resources/` when applicable;
- `bridge-portable-manifest.json` when portable;
- `run.ps1`.

From the **publish directory itself** run:

```powershell
.\run.ps1
```

The launcher must not depend on the repository source tree.

## 19. Linux publish package

Run:

```bash
./publish.sh
```

Default outputs:

```text
artifacts/publish/OCCAD-linux-x64/
artifacts/publish/OCCAD-linux-x64.tar.gz
```

Verify the package contains the apphost/dll, `runtime/`, `occt/resources/`, Bridge manifest, and `run.sh`.

Extract the tarball into a fresh directory and launch it once using the packaged `run.sh`.

## 20. Release Notes content

Release Notes must describe only the real product surface.

Recommended structure:

```text
OCCAD <version>

Highlights
- Classic Avalonia CAD shell
- 2D drafting
- 3D primitives
- Extrude/Revolve/Sweep/Loft
- Move/Delete
- Selection/Snap/Grip
- WorkPlane/ORTHO/POLAR
- Property/Layer
- Undo/Redo
- .ocad persistence
- Chinese/English

Runtime
- .NET 10
- Avalonia 12
- OcctCSharpBridge SDK 3.0 / ABI5
- OCCT 7.9.0
- Windows x64 / Linux x64

Known boundaries
- Copy/Rotate/Scale/Mirror not exposed yet
- no Array/Trim/Fillet/Annotation initial surface
- external exchange directions only when separately validated
```

Never convert “Core API exists” into a user-facing feature claim.

## 21. Tag / Release recommendation

The release commit should have synchronized docs, recorded build/interaction gates, no unpublished local fix, and Release Notes matching the same HEAD.

Recommended tag command:

```text
git tag -a v<version> <release-commit>
git push origin v<version>
```

Create the GitHub Release from that tag and attach the validated Windows/Linux delivery artifacts.

## 22. Release validation record

Keep a compact acceptance record:

```text
Version:
Commit:
Bridge source:
Windows build:
Windows package launch:
Linux build:
Linux package launch:
Create regression:
Move/Delete:
Undo/Redo:
Snap/Grip:
Save/Open:
zh-CN/en-US:
125%/150% DPI:
Known issues:
```

This is more traceable than a generic “tests passed” statement.

## 23. Release blockers

Do not formally release when any of the following remains:

- primary Windows build fails;
- application cannot start;
- normal Create leaves final Preview ghost state;
- Move/Delete diverges model and presentation;
- Undo/Redo corrupts Document state;
- Save/Open loses basic Entities;
- Grip commit mutates unrelated Entities;
- Tool Cancel prevents the next command from starting;
- major Chinese/English UI text is missing;
- publish package can run only when the repository source tree is present.

## 24. Known issues that may be non-blocking

When the basic loop is intact, these may be recorded as Known Issues:

- an edge case in a low-frequency Snap type;
- a complex-geometry failure in a low-frequency Feature;
- small font/layout differences between Linux and Windows;
- advanced modify commands not yet in the product surface;
- external format directions not formally claimed.

They must be described accurately rather than presented as completed features.

## 25. Post-release maintenance

Patch releases should follow:

1. reproduce;
2. determine whether the issue violates a release contract;
3. implement the smallest fix;
4. update the corresponding regression;
5. publish a Patch version.

Do not mix major architecture changes into a Patch release.

## 26. Definition of Release Ready

Mark a build Release Ready only after:

```text
Clean main HEAD
+ real Windows build
+ real application startup
+ core interaction regression
+ neutral/transient cleanup
+ Undo/Redo
+ Save/Open
+ localization/DPI
+ package launch from clean directory
+ documentation matches product surface
```

Documentation completion is one release gate; it does not replace real build and interactive acceptance.
