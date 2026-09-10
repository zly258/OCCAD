# Core regression checklist

Before merge, verify the simplified core on Windows:

- Line/Polyline/Rectangle/Circle/Arc: preview appears only while active and disappears after commit/cancel.
- Box/Cylinder/Cone/Sphere: same preview lifecycle behavior.
- Escape, right-click secondary action, Enter/Space submission, double-click finish and tool-to-tool switching restore neutral state.
- Selection highlighting and grips return after drawing/editing tools exit.
- Snap and tracking marks are cleared when a command exits and reactivate normally for the next command.
- Move/Copy do not leak preview objects.
- Undo/Redo restores persistent entities only, never transient preview presentations.
- View navigation remains available after every command.
