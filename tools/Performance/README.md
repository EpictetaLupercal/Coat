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
