# Coat 1.1.0

## 1.1.0 — Contours and .map export

- Application titles, the header and About use the project assembly version automatically. The title begins with `Coat — v1.1.0` and retains the document name and unsaved indicator.
- Closed shapes nested on the same level cut holes in the enclosing fill. Even-odd compound paths are used for the canvas, PNG and SVG. Each inner face can still be filled independently; contours on different levels never cut one another.
- The minimum zoom is now 1%, including mouse wheel, keyboard commands, framing and saved projects.
- Convex contours without holes bypass horizontal slicing: rotated rectangles export as one six-plane brush; N-sided circles and polygonal ellipses export as one N+2-plane brush. Existing transformed polygon vertices are preserved rather than reconstructed from rendering triangles.
- Concave regions and holes retain the existing slab partition. Adjacent pieces are merged only when they share a boundary and their convex hull has the same area. This reduces brush counts without filling holes; the greedy merge does not promise a globally minimal partition.
- Redundant collinear vertices are removed. Local-coordinate area calculations improve numerical stability. Wall/floor classification, height rules, Valve 220 format and existing `.coat` files remain compatible.

See [export validation](docs/EXPORT-1.1.0.md) for the test matrix and verification limits.

2D level-design plan and blueprint editor — **Epicteta Lupercal**.

## Selection and drawing in 1.0.8

- Move offsets, vertex coordinates and independent scale factors use compact X/Y rows. Group and Ungroup share a row. Select retains line and fill styling; arrow settings now live in Draw.
- Scale displays eight handles: side handles affect one axis, corners affect both. Hold Shift while dragging a corner to preserve proportions. The opposite side/corner stays fixed. Numeric factors still scale around the selection center and support mirroring. Handles have a constant screen size, including when zoomed out.
- Right-click without dragging opens a white context menu. Right-drag, middle-drag and Space-drag still pan the view.
- **Dissolve Vertices** reconnects vertices with exactly two incident edges. Junctions, endpoints, triangle collapses and replacements crossing unrelated geometry are retained. **Dissolve Edges** removes shared interior borders to merge adjacent faces; exterior boundaries and loose edges are retained. Delete keeps its existing behavior.
- Dissolve preserves fills and groups where applicable and is undoable in one step. A merged filled face inherits the source with the greatest overlapping area; ties use a stable face key. Its pattern and gradient settings follow that source.
- **Draw** includes a visual line-style dropdown plus Arrow Start, Arrow End, Double Arrow, arrowhead style and size. Settings affect newly drawn segments and any retained edge selection. Select existing edges, then switch to Draw to edit their arrows. The canvas previews the chosen stroke and arrowheads.
- Dissolve commands can be assigned in Preferences; no new default keys are assigned.


## Visual tools in 1.0.7

- **Text → Token** places circular markers. Select a token to change its color, size or coordinates. Tokens support moving, copying, duplication, undo and per-level visibility.
- **Snap to Vertices, Midpoints and Grid** snaps annotations and tokens to a vertex or edge midpoint within ten screen pixels, otherwise to the grid. Disable it for free placement. Exact coordinates remain editable.
- **Color → Fill faces → Gradient** fills a closed face using the active color, an end color and an angle. Click **Apply Gradient** to validate the angle and apply it to selected faces, or click a face to paint it. **Fill** restores ordinary solid/patterned painting. The linear gradient is relative to each face's bounding box; splitting a face retains its settings and fits the gradient to each resulting face.
- In Draw, the line properties include **Arrow Start**, **Arrow End**, **Double Arrow**, **Triangle / Open / Diamond**, and **Arrow Size** in plan units. Arrow heads are limited to 45% of their edge length to avoid overlap. Splitting a line retains heads at the original endpoints.
- Focus a numeric field and turn the mouse wheel to adjust it by one. Expressions are evaluated first; bounded controls retain their limits. Validate coordinate/transform fields with Enter or Apply.
- Solid, Stripes and Dots use visual swatches with translated tooltips and accessibility names.

PNG and SVG include tokens, gradients and arrows. These are drawing decorations: `.map` exports the existing filled geometry, using the gradient's start color for wall/floor classification. New commands receive no default shortcut.


## Download

**[Download Coat 1.1.0 for Windows (x64)](https://github.com/EpictetaLupercal/Coat/releases/download/v1.1.0/Coat-1.1.0-Windows-x64.zip)**

The link above is the published **1.1.0** build. This source tree contains **1.1.0**; build it locally or upload the new package and update this link when publishing.

Extract the entire ZIP, then launch **Coat.exe**. Keep the accompanying files alongside the executable. Use the download link above for the ready-to-run Windows application.

Coat requires **Windows 10 or 11 (x64)** and **[.NET Desktop Runtime 8 (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)**. On the download page, choose **.NET Desktop Runtime → Windows → x64**. The runtime is not bundled.

The interface defaults to **English**. French is available under **Edit → Preferences → Interface language**. Windows file dialogs follow your Windows language settings.

## What's new in 1.0.6

- Optional shortcuts for editor commands, exports, shapes, styles, and organization. New bindings remain **Unassigned**; existing saved bindings are preserved. Conflicts name both actions. Enter confirms actions as explicitly requested.
- Arithmetic in numeric fields: `10+5`, `10-5`, `10*2`, `10/2`, parentheses, and decimal commas or points. Validate with Enter or the corresponding Apply button. Multiline annotations retain normal line breaks.
- **Select Across Levels**, multi-level groups, group renaming, and an **Outliner** organized as Levels → Groups → Shapes. Copying and duplicating preserve level membership.
- **File → Open Recent** lists the last ten projects.
- Projects save and restore camera framing, grid, length labels, upper/lower visibility, and selection scope. Existing `.coat` files remain compatible.
- Indexed geometry, cached contours, bounded navigation rendering, and more efficient intersection checks improve navigation and edits in complex plans.

## Folder structure

| Folder | Contents |
| --- | --- |
| `app/` or `app-<version>/` | Application builds and user guides |
| `dist/` | Current release ZIP |
| `docs/` | English and French guides and release notes |
| `src/` | C# source code and translation catalog |
| `tools/` | Development and performance measurement tools |
| `artifacts/tests/` | Current test results and screenshots |
| `artifacts/legacy-tests/` | Preserved test results, benchmarks, and fixtures |
| `archive/builds/` | Older application builds |
| `archive/packages/` | Original ZIP packages from previous releases |
| `.build/` | Local build cache |

## Documentation and building

- [English guide](docs/README-English.md) · [French guide](docs/Guide-Francais.md)
- [Performance review](docs/PERFORMANCE-1.0.6.md)
- [English release notes](docs/CHANGELOG-English.md) · [French release notes](docs/CHANGELOG.md)
- Install the **[.NET 8 SDK for Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)** to build from source. Run the commands below from the folder containing this README.
- `./build.ps1`: build into `app/`. Launch `app/Coat.exe` after the build completes.
- `./test.ps1`: build and run the regression suite using isolated preferences.
- `./build.ps1 -Package`: build and create the release ZIP in `dist/`.
- Add `-OutputDirectory app-1.1.0` to either script to keep a running older build in `app/` untouched.

`.coat` projects use JSON format 6, supported since Coat 1.0.2. This update retains the existing project format.

See the [publishing checklist](docs/PUBLISHING.md) before uploading the project to GitHub. Generated applications, packages, test artifacts, and local caches are excluded from the source repository by `.gitignore`.
