# Core interaction lifecycle

This note defines the minimum transient-state contract used by the simplified OCCAD core.

1. A tool owns its preview, tracking, snap state and temporary work-plane/drafting locks.
2. Preview presentations are never document entities and must not be selectable.
3. Commit first converts the preview geometry into a persistent document entity, then exits through the same common deactivation path as Finish/Cancel.
4. Tool replacement, normal completion, cancellation and activation failure must restore a neutral workspace state.
5. Neutral state means: no preview, tracking, active snap, preselection, pointer observation, temporary precision factor, drafting lock or tool plane; OCCT automatic highlight is enabled again.
6. One preview manager may delete only presentations it owns. It must never scan and remove another transient owner's tagged objects.

These rules are intentionally small and form the baseline for later Selection/Snap/Grip behavior alignment with OCCTBIM-Source.
