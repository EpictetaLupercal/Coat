# Coat performance probe — Epicteta Lupercal

Build with `dotnet publish tools/Performance/Performance.csproj -c Release -o artifacts/perf-bin` from the Coat directory. Run `artifacts/perf-bin/Performance.exe <input.coat> <output-directory>`.

The probe only reads the input file. It clones the plan before each operation, warms up each case once, then records seven measurements in milliseconds. Loading and cloning are outside the timed region. Cases cover normalization, adding a segment, rectangle, and 64-vertex circle, and moving the active level. A separate case includes the normal edit commit, undo snapshot, inspector refresh, and WPF rendering through the dispatcher. Outputs include the resulting plans, raw timings, and a screenshot; keep these local if the input plan is private.

To compare implementations, preserve the baseline executable and its dependencies before editing the geometry code. Run both builds against the same source and compare the output `.coat` files byte for byte, in addition to comparing timing medians.

## 1.0.1 investigation

The reported plan has 331 vertices, 413 edges, and six levels (-1 through 4). Previously, `Normalize` repeatedly scanned the entire vertex list inside the pairwise intersection loop and again inside the edge-splitting predicate. This made normalization roughly O(E²V + EV²), before additional duplicate-edge scans. Indexing endpoints locally removes the extra V factor. Bounds reject disjoint segments early, split-point searches use the edge's level, and an endpoint dictionary replaces repeated duplicate-edge scans. Local indexes avoid stale caches during direct vertex moves, undo, or document replacement.

Measured medians on the development PC, seven repetitions after warm-up:

| Operation | 1.0 (ms) | 1.0.1 (ms) |
| --- | ---: | ---: |
| Normalize | 459.48 | 6.36 |
| Add segment | 474.02 | 6.23 |
| Add rectangle | 494.28 | 6.40 |
| Add 64-vertex circle | 719.97 | 10.14 |
| Move active-level geometry | 463.93 | 6.14 |
| Add segment including UI refresh | 472.93 | 41.20 |

All five resulting geometry files are byte-identical to the baseline, including IDs, fills, patterns, line styles, and groups. Raw results are in `artifacts/legacy-tests/perf-before` and `artifacts/legacy-tests/perf-after`. Timings depend on hardware and document geometry; pairwise candidate enumeration remains quadratic, so this is not a guarantee of constant-time editing on arbitrarily large plans.

## Navigation probe (1.0.6)

Run `Performance.exe <input.coat> <output-directory> --navigation` to measure panning with and without length labels. The probe discards three warm-up frames, records twelve frames for each case, and writes raw measurements to `navigation.json`. The elapsed time includes WPF dispatcher rendering, rather than just assigning camera coordinates.

Add `--stress` for sixty measured frames with a longer back-and-forth path that crosses the navigation cache margin. This also measures zoom changes, segment creation on cloned plans, and render-geometry refresh. Output includes median, 95th percentile, and maximum times. Do not run other UI tests concurrently with performance measurements.

The compositor-managed drawing cache is bounded to sixteen million pixels and renewed when it runs beyond its 256-DIP screen margin. Geometry edits, selection changes, zoom, window size, DPI, language, and display flags invalidate the relevant cache. PNG exports bypass the drawing-brush cache; SVG and .map continue to use document geometry. First-frame preparation and cache renewal cost more than steady panning.

Vertex lookup and per-level render lists replace repeated linear scans. Normalization uses spatial buckets for coincident points, sorted per-level edge intervals for candidate intersections, and binary searches for split-point candidates. Dense intersecting geometry can still produce quadratic work; recursion alone would not reduce that complexity. Group expansion uses an indexed queue to visit reachable groups without repeated global scans.

See [the 1.0.6 review](../../docs/PERFORMANCE-1.0.6.md) for measurements, limitations, and validation.
