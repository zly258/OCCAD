# 02 UI Specification

## Visual direction

OCCAD uses a compact industrial CAD shell:

```text
Menu
Compact global toolbar
┌──────────────┬──────────────────────────────┬─────────────────────┐
│ Model panel  │          Viewport            │ Layer panel         │
│              │                              │ splitter            │
│              │                              │ Property panel      │
└──────────────┴──────────────────────────────┴─────────────────────┘
Command Line
StatusBar
+ non-modal ToolPanel over the viewport
```

The Viewport remains dominant.

## Theme contract

`CadTheme` is the single visual-metric source. Major controls do not invent local typography, control height, radius, or panel width.

Baseline: 11 px main UI font, 10/10.5 px secondary text, 22 px compact controls, ~23–24 px headers, restrained light-gray surfaces, dark viewport, blue accent only for active/current state, low/no corner radius, thin separators, and a Segoe UI / Microsoft YaHei UI / CJK fallback font stack.

## Menu / toolbar / ToolPanel

Menus invoke registered Action IDs. Use a submenu only for a real command family.

The persistent toolbar contains global/current drafting state. Stable Tool-specific Radius/Width/Height/etc. values belong in ToolPanel. ToolPanel shows parameters, precision, work-plane state, Accept/Finish/Cancel; it does not duplicate the full prompt.

## Model / Layer / Property

Model is left. Layer and Property are independent resizable panels stacked on the right.

Layer UI calls `CadWorkspace` for visibility, lock, color, style, and width; rollback/history stay in Core.

Property uses Core descriptors and supports common-property multi-selection.

Appearance rows are compact:

```text
Color      [ByLayer] [value]
LineStyle  [ByLayer] [value]
LineWidth  [ByLayer] [value]
Transparency
Visible
```

ByLayer booleans remain Core state but are not separate rows.

## Prompt ownership

- Command Line: the only full current Tool prompt + typed command input.
- ToolPanel: parameters/precision/work-plane/Accept/Finish/Cancel.
- StatusBar: application/tool status, selection, history, snap, precision, work plane, coordinates.

StatusBar may show Ready, active Tool name, errors, or blocked operations, but not the full next-step prompt.

## Viewport / cursor / overlays

`OcctAvaloniaViewport` is the only OCCT host. Drawing uses the hollow cross cursor. Snap aperture and dynamic HUD remain screen-size stable. ViewCube is hidden; the lower-left triedron remains.

Do not issue duplicate explicit redraws after Bridge operations that already request redraw.

## Localization / DPI

All durable UI strings use synchronized English/Chinese resources. Render scaling and viewport input coordinates must remain aligned at common Windows scaling values such as 125% and 150%.


## Application preferences

Long-lived user viewport/interaction preferences are application state, not Document state. `Settings → Preferences...` edits and persists scene background, grip/snap marker sizes, grip/snap/selection pixel tolerances, mouse-wheel zoom sensitivity, and viewer display deviation/angle.

Changing a preference applies immediately to the current viewport. Viewer display precision is presentation tessellation quality only and must never be presented as changing CAD model precision.
