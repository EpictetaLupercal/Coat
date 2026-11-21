# Coat 1.2

## 1.2 — Color-aware .map geometry

- Nested and adjacent fills merge only when their normalized RGB colors, level and extrusion settings match. Different floor colors remain distinct, just like walls and floors.
- Inner shapes keep their original contours and classification. An orange floor with white pillars exports each pillar independently, using the configured wall color and level heights.
- The outer surface is partitioned around inner shapes to prevent overlapping solids. Empty shapes remain holes; compatible same-color pieces still merge into convex brushes.
- Source colors are retained in brush comments. The existing Valve 220 format and `__TB_empty` texture remain unchanged; these comments do not apply colored materials in the map editor.
- Existing `.coat` files remain compatible. The title uses the configured release name, **Coat — v1.2**.

## 1.1.7 — Ignore Groups selection toggle

- Select → Groups now always exposes **Ignore Groups**, including when nothing is selected. The active button is highlighted.
- Enable it to select vertices, edges and faces normally without group expansion. The current selection is cleared when enabling the mode so that the next click can pick one element directly.
- This mode persists through deselection, Escape, selection-mode changes and geometry edits. Click the button again to restore group picking; any currently selected group members immediately expand to their complete groups.
- Double-click individual selection remains available. Its active state is reflected by the button, which can also be used to return to group selection.
- Group definitions and membership remain intact. The toggle does not modify project data or add an Undo step. Opening a project starts in normal group-selection mode.
- **Ignore Groups** is configurable under selection shortcuts; no shortcut is assigned automatically.

## 1.1.6 — Text confirmation and mixed marquee selection

- **Enter** confirms annotation text. **Shift+Enter** inserts a line break at the caret, including replacement of selected text. Existing multiline content is preserved.
- In Select, dragging a rectangle from empty canvas also selects text annotations and tokens, in Vertices, Edges and Faces modes. An annotation's full visible bounds must fit inside the rectangle, matching the existing full-containment rule for geometry.
- Selected texts and tokens show individual outlines and can be moved with geometry, duplicated, copied, pasted, cut, deleted or assigned to a level together. Undo restores the entire operation in one step. Ctrl-click and Ctrl-drag support additive selection; Escape clears it.
- Cross-level selection includes annotations on visible lower levels when enabled. Hidden annotations and upper-level notes (which are not rendered by the geometry-guide display) are excluded. Clipboard operations preserve relative level offsets.
- Single-annotation editing and the deselected state of newly placed tokens remain available. Geometry caches are retained when moving only annotations.

## 1.1.5 — Live color popup and faster token placement

- A newly placed token remains deselected. Picking another icon changes the next token, so different symbols can be placed consecutively. Existing tokens can still be selected deliberately for editing.
- Color buttons now show a color-wheel icon and current-color swatch. They open a compact anchored popup containing the wheel, editable HSV values, Hex, RGB feedback, saved colors, Add and an icon-only eyedropper. No Apply/Accept/Cancel action is required.
- Valid changes apply immediately to the active drawing color, selected annotation/token color, selected gradient end colors, or wall-classification color. Incomplete Hex/HSV input keeps the last valid color. Click outside or press Escape to close the popup while retaining the current color.
- A document color-editing session is grouped into one Undo step when the popup closes. Setting a drawing color alone does not create a document-history entry. Color-only rendering invalidation reuses the geometry cache.
- The eyedropper samples directly on Coat's canvas, including reference images. Click to sample; Escape or right-click cancels sampling. It returns to the same popup without opening another window or requiring confirmation.
- Saved palette slots keep their existing Add and right-click Delete behavior. All new controls follow the selected interface language. Existing projects and exported token shapes remain compatible.

## 1.1.4 — Dedicated Tokens tool and symbol palette

- **Tokens** is a dedicated toolbar button next to **Text**. The old Text/Token switch has been removed from the sidebar. Text and token placement are independent.
- The compact palette contains 22 icon-only choices: Circle, Square, Triangle, Cross, Check, Skull, Star, Heart, Armor, Explosion, Ammo, Rocket, and digits 0–9. Names appear in translated tooltips and accessibility labels, not as large letter placeholders.
- Choose a symbol, size and color, then click the canvas to place it. Select an existing token to change its shape, color, size or coordinates. Palette selection also sets the shape for the next token.
- All symbols use vector geometry, including outlined digit glyphs for reliable SVG export. Shapes remain visible in PNG/SVG, follow level visibility, support snapping, dragging, undo/redo, duplication and clipboard operations.
- Token shape is saved in the existing project format. Older tokens remain circles. The existing optional Token shortcut opens the dedicated tool; no shortcut is assigned by default.

## 1.1.3 — Dedicated Dimension tool

- **Dimension** is now a toolbar tool between Draw and Erase. It is removed from Draw's line-style list. Place two points to finish each dimension; Draw continues to create ordinary line chains.
- Select a dimension in Select mode to edit **Font Size** and **Arrow Size** in the right panel. Confirm with Enter or **Apply Dimension**. Font-size expressions are supported, from 4 to 256. Undo, redo, duplication and clipboard operations retain the setting.
- The Dimension tool also exposes these settings for new dimensions. Existing dimensions keep their own values. Text size is saved in `.coat` and used by canvas/PNG and SVG rendering. Legacy dimensions default to size 12.
- The existing optional Dimension shortcut activates the dedicated tool; no default key is assigned.

## 1.1.2 — Dimension lines and independent arrowheads

- In Draw, **Start** and **End** are independent visual dropdowns: None, Triangle, Diamond, Round, and the retained Open style. Set different shapes at either end. Changes also apply to previously selected edges.
- **Arrow Size** accepts expressions. Press Enter or leave the field to apply; the old checkbox, Double Arrow, style-button row and Apply Arrows button have been removed from the panel. Existing configurable commands remain available; new Dimension and Round commands are unassigned by default.
- Choose **Dimension** from the line-style dropdown, then place two points to draw a measured line with two triangular arrowheads, perpendicular endpoint bars, and a centered distance in plan units. Distances update automatically when endpoints move or scale and are included in PNG and SVG, independently of View → Show Lengths. Dimension uses fixed triangular heads; switch back to Solid/Long/Short to edit independent endpoint shapes.
- Dimensions use ordinary editable edges. Drawing across existing geometry follows the normal intersection/splitting rules; each resulting segment displays its own distance. This is a drawing style, not an automatic .map measurement entity.
- Saved projects retain independent caps and dimension styling. Legacy common arrow styles still load. Copying, duplication, level transfers and normalization retain the added settings.

## 1.1.1 — Individual selection within groups

- In Select, double-click a vertex, edge or face to select it individually within its group. Group definitions and membership remain intact.
- Subsequent clicks and Ctrl-clicks select individual elements until the selection is cleared. Dragging and numeric transforms keep the precise selection after geometry normalization.
- Press Escape or click empty canvas to clear the selection and restore normal group picking. Changing selection mode, Select All and opening another project also return to normal group selection.

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

**[Download Coat 1.0.5 for Windows (x64)](https://github.com/EpictetaLupercal/Coat/raw/refs/heads/main/Coat-1.0.5-Windows-x64.zip)**

The link above is the published **1.0.5** build. This source tree contains **1.2**; build it locally or upload the new package and update this link when publishing.

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
- [.map export verification for 1.2](docs/EXPORT-1.2.md)
- [English release notes](docs/CHANGELOG-English.md) · [French release notes](docs/CHANGELOG.md)
- Install the **[.NET 8 SDK for Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)** to build from source. Run the commands below from the folder containing this README.
- `./build.ps1`: build into `app/`. Launch `app/Coat.exe` after the build completes.
- `./test.ps1`: build and run the regression suite using isolated preferences.
- `./build.ps1 -Package`: build and create the release ZIP in `dist/`.
- Add `-OutputDirectory app-1.2` to either script to keep a running older build in `app/` untouched.

`.coat` projects use JSON format 6, supported since Coat 1.0.2. This update retains the existing project format.

See the [publishing checklist](docs/PUBLISHING.md) before uploading the project to GitHub. Generated applications, packages, test artifacts, and local caches are excluded from the source repository by `.gitignore`.

For an isolated regression run, use `./test.ps1 -OutputDirectory app-1.2 -TestOutputDirectory artifacts/tests-1.2`.
