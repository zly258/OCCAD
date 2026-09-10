# 04 Architecture

## Layers

OCCAD uses a layered, event-driven, registry-based architecture inspired by OCCTBIM-Source responsibility boundaries without copying Qt singleton patterns:

```text
Presentation (WPF)
  MainWindow / Menu / ToolBar / Docks / ToolPanel / StatusBar / PropertyGrid
        ↓ observes + invokes
Application / Interaction
  Workspace / ActionManager / ToolManager / Selection / Snap / Tracking / Grip / Preview
        ↓
Domain
  Document / Entity / Layer / History / Registries / Serialization
        ↓
Geometry & Presentation Bridge
  OcctNet / OcctCSharpBridge / OCCT
```

Dependency direction is `WPF → Core → OcctNet`. Core never references WPF; Entity never references MainWindow; Tool never manipulates WPF controls directly.

`CadWorkspace` is the session composition root holding Document, Layers, Selection, Subobjects, Preselection, WorkPlane, Snap, Tracking, Precision, Preview, Grips, History, Tools, Actions, and Engine attachment. It composes services rather than becoming a god object.

Document uniquely owns persistent Entity state and handles add/remove/find, presentation mapping, `ResolveAppearance`, entity-change presentation updates, change sets, engine attach/detach, and document events. It does not own mouse or Tool-stage logic.

Entity owns stable identity/type, Name/Layer, visibility/selectability, appearance plus independent ByLayer flags, geometry and BuildShape, duplicate/restore, transforms, Snap, Grip, change events, and persistence semantics. Cone and Frustum are distinct types; unrelated modes do not collapse multiple entities into a generic class.

Entity/Tool/Action use explicit registries with stable IDs and factories/metadata/serialization contracts. UI reflection discovery and large MainWindow type switches are prohibited.

Action is an instantaneous command entry for files, Undo/Redo/Delete, view/display operations, Tool activation, and dock toggles. Menu, ToolBar, and shortcuts share Action IDs. Continuous pointer state belongs to Tool.

Tool is the staged interaction state machine: Id, State, Activate, Stage/Prompt, Pointer/Keyboard, CanFinish, StepBack, PrecisionReferencePoint/Inputs, ParameterPanel, Preview, Commit, Cancel/Complete. Stable Tool parameters are described by Core descriptors and rendered by the single WPF `CadToolPanel` base without Core knowing a Window.

Preselection, formal Entity Selection, and SubobjectSelection are separate. Viewport provides hit input; SelectionManager owns modifier, Window/Crossing, and selectability rules.

Point resolution is shared: screen/ray → WorkPlane → Snap → direction tracking → length → final point. Persistent user DrawingPlane, transient Tool Plane, and Grip Plane are distinct states.

Entity provides Snap and Grip semantics. SnapManager resolves candidate priority/pixel tolerance and transient markers. GripManager only renders/hit-tests/hot-starts GripEditTool. GripEditTool modifies a duplicate preview, rolls back failures, and applies one real entity/history change on Accept.

PreviewManager owns transient entities/presentations outside Document, supports single/multiple preview, atomic replacement, previous-frame retention on failure, and the same appearance resolver as final presentation including current-layer ByLayer state.

History stores business state only; pointer moves never create entries and each Tool/Grip/Property commit creates one logical entry.

Layer owns Name, Color, LineWidth, LineStyle, Visible, Locked. Document is the single final appearance resolver. PropertyGrid and Docks are UI views over business state, adapted only for localization, formatting, and editors.

Entity identity never depends on ViewerObjectId. Entity geometry changes rebuild presentation; appearance changes reapply appearance. Preview, Grip, Snap, Preselection, and selection rectangle own separate transient lifecycles.

All modifying paths follow Capture → Validate/Build → Apply → History, restoring on failure. Dispatcher containment is a final safety net, not a substitute for local Tool/Grip/Property/Document transactions.
