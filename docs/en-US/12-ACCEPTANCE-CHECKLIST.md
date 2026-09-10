# 12 Acceptance Checklist

A feature is complete only when its interaction and state contract passes this checklist, not merely when classes exist.

## Shell

Native Menu/ToolBar/StatusBar only; toolbar always visible; routine commands no deeper than one submenu; dock close controls are transparent/borderless; 125%/150% DPI remains usable; language switching refreshes the visible UI immediately.

## Tool and ToolPanel

Activate initializes cleanly; PointerMove changes transient state only; prompts are stage-specific; LeftClick/Enter/Finish semantics agree; right-click finishes when possible otherwise cancels; Backspace steps back; Cancel clears transient state; ToolPanel is the single non-modal base, does not steal focus, updates Preview immediately, hides rather than cancels, shows three decimals without losing precision, and is fully localized.

## Preview

Never enters Document/History/Selection; uses final Entity+Layer appearance including current layer before commit; replacement is atomic; degenerate pointer positions keep the previous valid frame; completion/cancel leaves no viewer objects behind.

## Selection / Snap / Tracking

Point, Replace/Add/Remove/Toggle, Window/Crossing, hidden/locked/selectability, preselection separation, and model-tree synchronization behave consistently. Snap markers reappear after hide, keep screen size, and temporary modes do not mutate persistent settings. Direction constraint precedence is explicit and Length is applied after direction resolution.

## Grip

Semantic markers are compact and clear; hit tolerance is pixel-based; drag edits only a preview copy; invalid geometry rolls back and keeps the tool active; Accept records exactly one real entity change/history entry; Cancel restores the original; recoverable failures do not terminate the app.

## Layer / Property

Color opens ColorDialog directly. Visible/Locked/LineWidth/LineStyle are one-click editable. Editing a non-current layer does not change current layer. Layer changes update all ByLayer entities without whole ItemsSource rebuilds. Property supports independent ByLayer/custom color, width and style; doubles display three decimals with full precision storage; enums are localized; multi-select exposes common properties; failed edits roll back; one user edit creates one history item.

## Entity

Every Entity is checked for BuildShape, Duplicate, RestoreGeometry, transforms, Snap, Grip/MoveGrip, Tool, Preview, Property, ByLayer, History, Serialization, localization, and degenerate input. Cone and Frustum remain separate Entity/Tool types.

## Document / History / Files

New/Open/Save/SaveAs modified state is correct; all basic changes Undo/Redo correctly; failed operations create no empty history; Reset clears transient state; large file loading reports bottom progress and remains cancellable/responsive; bulk display changes are batched.

## Release gate

`build.ps1` succeeds; one manual 2D/3D/edit/grip/layer/property/save-open pass succeeds in Chinese and English; no unused third-party UI package, obsolete style, hidden compatibility control, or temporary patch remains; documentation matches actual behavior.
