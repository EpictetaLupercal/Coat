# Coat 1.2 — Color-aware .map export verification

## Cause and correction

The exporter classified fills as Empty, Wall or Floor, then discarded their RGB color. Horizontal scanline runs, vertical rectangle coalescing and the final convex merge used that classification. Consequently, distinct floor colors could collapse into one solid. Wall-versus-floor classification already distinguished white pillars from a colored floor; that case is retained as a regression fixture.

Each region and brush now retains canonical `#RRGGBB`. All three merge steps require matching color in addition to the existing surface, level and extrusion constraints. Hex letter case does not create a different material. The existing contour detection, innermost-face ownership, convex direct-export path and rectangular decomposition are reused.

Outer surfaces are partitioned around inner faces. This preserves independent inner solids and avoids positive-volume overlaps, including differently colored floors at the same height. Empty faces still cut holes. Compatible same-color pieces continue to merge; no rendering triangles are introduced as export geometry.

Valve 220 plane generation, wall-color classification, next-level wall height, floor thickness and project format 6 remain unchanged. Source colors are included in brush comments for traceability. The placeholder texture is still `__TB_empty`; comments do not assign colored materials in TrenchBroom.

## Validation

Command, from the Coat folder:

```powershell
./test.ps1 -OutputDirectory app-1.2 -TestOutputDirectory artifacts/tests-1.2
```

**864 checks passed**, including 64 new checks. The suite verifies exact areas by source color, original dimensions and positions, extrusion heights, positive-volume intersections, nondegenerate convex plane winding, project round trips and read-only export. Existing convex, concave, hole, UI and localization tests also pass.

The ten generated fixtures were independently read with the TrenchBroom connector's `get_entity` parser:

| Fixture | Brushes | Result |
| --- | ---: | --- |
| Nested same-color floors | 1 | One six-plane slab, including mixed-case hex input |
| Nested same-color walls | 1 | One six-plane wall |
| Nested different-color floors | 5 | Four outer rectangles and one independent center |
| Orange floor with three white pillars | 11 | Eight floor rectangles and three independent six-plane pillars |
| Concave inner floor | 7 | Five outer pieces and two inner rectangles |
| Adjacent colors with holes | 8 | Four rectangles per color; vertical coalescing respects color |
| Four nested colors with an empty center | 16 | Four rectangles per color and an open center |
| Rotated outer floor with transformed ellipse | 15 | Inner 12-sided ellipse remains one 14-plane brush |
| Custom wall color and negative fractional height | 5 | Four outer wall rectangles and one inner floor |
| Nested shapes on two levels | 10 | Independent per-level ownership and extrusion |

The fixture files, test results and connector report are generated under `artifacts/tests-1.2/` and are excluded from release archives. The application header was visually checked, and title assertions confirm **Coat — v1.2** from project version metadata.

This validation includes a parser check through the connector, not an interactive import into the TrenchBroom editor or a BSP compilation.
