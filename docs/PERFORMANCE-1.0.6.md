# Coat 1.0.6 performance review

Author: Epicteta Lupercal. Verified on 2026-11-15.

## Scenario and method

The synthetic plan contains 12 levels, 2,160 filled rectangles, 8,640 vertices, and 8,640 edges. The viewport is 1440 × 900 including the sidebar; all lower levels are visible. Measurements use the same development machine and Release builds.

The baseline captures twelve pan steps after three warm-up frames. The final stress probe captures sixty steps per display setting along a longer back-and-forth path, including cache-boundary crossings. Times include WPF dispatcher rendering. They describe this test configuration, not guaranteed frame rates on every GPU or document. Raw measurements and the fixture remain in `artifacts/update106/`.

## Results

| Operation | Previous median | 1.0.6 median | 1.0.6 p95 | 1.0.6 maximum |
| --- | ---: | ---: | ---: | ---: |
| Pan | 3480.57 ms | 18.84 ms | 24.53 ms | 35.64 ms |
| Pan with length labels | 4249.41 ms | 47.78 ms | 56.61 ms | 63.10 ms |
| Zoom with length labels | Not measured | 449.51 ms | 527.12 ms | 527.12 ms |
| Add segment, geometry only | Not measured | 96.31 ms | 265.66 ms | 265.66 ms |
| Rebuild render geometry | 73.81 ms | 22.18 ms | 22.18 ms | 22.18 ms |

## Changes

- Rendering previously performed repeated linear vertex searches, per-level scans, and per-frame path construction. The canvas now resolves vertices and faces by ID, groups render data by level, and caches face geometry.
- Lines with the same style and vertex markers are batched into paths, reducing drawing-command overhead while preserving geometry and styles.
- Camera translation reuses the scene drawing. WPF receives a bounded DrawingBrush cache hint for the viewport plus a 256-DIP margin. Editing, selection, zoom, DPI, language, visibility, and window size invalidate the appropriate cache. PNG exports render the vector scene; SVG and .map use document data.
- Coincident-point detection uses spatial buckets. Intersection candidates are sorted and separated by level, and split-point searches use sorted X intervals. Dense intersecting geometry can still require quadratic work.
- Group expansion uses an indexed queue. Changing an iterative loop into recursion alone would not reduce its complexity.

The synchronous RenderTargetBitmap navigation experiment was rejected because cache preparation could stall the interface. Only the compositor-managed drawing cache is included in the delivered build. Earlier experimental measurements are not release performance results.

Zooming with thousands of length labels remains more expensive than panning because screen-sized labels and strokes must be rebuilt. The stress test measured approximately half a second for this extreme case. Turning off length labels avoids that extra text cost. No claim of constant-time editing or universal 60 FPS is made.

WPF cache behavior follows Microsoft's [DrawingBrush caching guidance](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.cachinghint).

## Validation

The regression suite includes existing geometry, normalization, styles, clipboard, .map export, language, and undo tests, plus arithmetic, unassigned/conflicting shortcuts, Enter confirmation, multi-level groups, Outliner membership, project View settings, recent files, cache renewal and invalidation, and multiline text behavior. Screenshots were checked for the selection sidebar, preferences, and Outliner. Test files and private preferences are excluded from release packages.
