# 02 UI Specification

## Visual direction

OCCAD uses a compact industrial CAD shell:

```text
Ribbon
┌──────────────┬──────────────────────────────┬─────────────────────┐
│ Model        │          Viewport            │ Layer               │
│              │   Floating Tool Panel        │ splitter            │
│              │                              │ Property            │
└──────────────┴──────────────────────────────┴─────────────────────┘
Command Line
Selection | Work Plane | SNAP ORTHO POLAR | XYZ
```

The viewport remains dominant. The application no longer constructs the legacy Menu, legacy Toolbar, or post-startup UI refinement shells.

## Theme contract

`CadTheme` is the single visual-metric source. Major controls do not invent local typography, control height, radius, or panel width.

Baseline: 11 px main UI font, 10/10.5 px secondary text, 22 px compact controls, ~23–24 px headers, restrained light-gray surfaces, dark viewport, blue accent only for active/current state, low/no corner radius, thin separators, and a Segoe UI / Microsoft YaHei UI / CJK fallback font stack.

The Ribbon uses native Avalonia controls only. Groups use a compact three-row layout. When horizontal space is insufficient the Ribbon scrolls instead of forcing the main window wider at 125% or 150% DPI.

## Ribbon / Actions / Tool Panel

Ribbon controls invoke registered Action IDs and never duplicate business logic. Real command families use one drop-down layer, such as Circle, Arc, Regular Polygon, Ellipse, and 3D Primitives.

`CadActionManager`, `CadToolManager`, and `CadCommandManager.ForWorkspace()` are the single state sources for Actions, Tools, and the command session. Ribbon, Command Line, and Floating Tool Panel are different input surfaces for the same Core state machine.

Stable Tool parameters such as Radius, Width, Height, Angle, and Factor belong in the Floating Tool Panel. The panel does not duplicate the full prompt. Parameter edits, precision input, Back, Accept, Finish, and Cancel directly operate on the active Tool and do not maintain a second parameter model.

## Model / Layer / Property

Model is on the left. Layer and Property are independent resizable panels stacked on the right.

Layer columns are:

```text
Current | Name | V | C | Style | Width | L
```

The current layer uses an explicit `●/○` state. Only the Current column changes the current layer; clicking the name only inspects it. The default layer cannot be renamed or removed. Layer UI calls `CadWorkspace`; transactions, rollback, and history remain in Core.

Property uses Core descriptors/editor semantics and supports categories, common-property multi-selection, mixed values, Layer, ByLayer, Color, Enum, Numeric, and Point/Vector X/Y/Z editing. Changes are committed through `CadPropertyTransaction`.

Appearance rows remain compact:

```text
Color      [ByLayer] [value]
LineStyle  [ByLayer] [value]
LineWidth  [ByLayer] [value]
Transparency
Visible
```

ByLayer booleans remain Core state but are not separate rows.

## Prompt and status ownership

- Command Line: the only full Tool prompt, typed command input, history/completion, results, and errors.
- Floating Tool Panel: current step, exact coordinates, Length/Angle/Factor, Tool parameters, Back/Accept/Finish/Cancel.
- Dynamic HUD: pointer-adjacent Length/Angle, SNAP, and ORTHO/POLAR tracking feedback.
- StatusBar: Selection, Work Plane, SNAP/ORTHO/POLAR, and XYZ coordinates.

Do not copy next-step Tool prompts, History, or Precision text back into the StatusBar.

## Viewport / cursor / DPI

`OcctAvaloniaViewport` is the only OCCT host. Drawing uses the hollow cross cursor. Snap aperture and Dynamic HUD remain screen-size stable. ViewCube is hidden and the lower-left triedron remains.

Do not issue duplicate explicit redraws after Bridge operations that already request redraw. Render scaling, overlays, and native viewport input coordinates must remain aligned at common Windows scale values such as 125% and 150%.

## Application preferences

Long-lived viewport/interaction preferences are application state, not Document state. `Manage → Preferences...` edits and persists scene background, grip/snap marker sizes, grip/snap/selection pixel tolerances, mouse-wheel zoom sensitivity, and viewer display deviation/angle.

Changing a preference applies immediately to the current viewport. Viewer display precision is presentation tessellation quality only and must never be presented as changing CAD model mathematical precision.
