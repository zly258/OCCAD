# Validation status

The repository currently has no GitHub Actions workflow. This stabilization branch has been statically inspected through the repository connector, but a full Windows build/runtime validation is still required before merge.

Required local checks:

- `git pull`
- `./build.ps1`
- start the Avalonia application
- draw Line, Polyline, Rectangle, Circle, Arc, Box, Cylinder, Cone and Sphere
- for each tool verify Commit, Finish, Escape/Cancel and immediate tool switching leave no preview/tracking/snap/preselection residue
- verify selection highlighting and grips return after every tool exits
- verify Move/Copy and Undo/Redo

Do not merge solely on static inspection.
