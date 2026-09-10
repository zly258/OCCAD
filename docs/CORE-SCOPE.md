# OCCAD core scope

The default OCCAD command surface is intentionally reduced while interaction behavior is stabilized against the OCCTBIM-Source reference architecture.

## Enabled interactive core

- 2D entities/tools: Point, Line, Polyline, Rectangle, Circle, Arc.
- 3D primitives/tools: Box, Cylinder, Cone, Sphere.
- Editing: Move, Copy, Delete.
- Selection: Select, Select All, Invert Selection.
- View/display: Fit, standard orthographic/isometric views, Wireframe, Shaded, Hide, Isolate, Show All.
- History/document: New, Clear, Undo, Redo.
- Measurement: Distance.
- Infrastructure retained: Layer, Property, WorkPlane, Snap, Tracking, Precision, Selection/Preselection, Grip and Tool parameter panels.

## Persistence compatibility

The interactive command surface and document serialization registry are separate concerns. Advanced entity codecs remain registered so existing OCCAD documents can still be opened and saved without losing known entity types. Those advanced entities are compatibility-only in this stabilization phase: their creation tools/actions are not registered by default.

## Disabled from the default interactive surface

Text/dimensions, ellipse/spline/polygon creation, rotate/scale/mirror, trim/extend/offset/break/join, arrays/patterns, fillet/chamfer, Region/Extrude/Revolve/Boolean/Sweep/Loft/Shell/Shape Offset and other advanced solid feature commands.

## Interaction contract

Every transient presentation belongs to exactly one owner. Tool exit must return the workspace to a neutral state: no preview, tracking, snap marker or preselection residue; transient drafting/work-plane state is reset; automatic highlight is restored; persistent selection/grips remain valid.

Physical deletion of compatibility entity implementations is deferred until document migration policy is explicit and the simplified core has passed local Windows build/runtime regression tests.
