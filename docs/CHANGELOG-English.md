# Coat 1.1.0 — Epicteta Lupercal

## 1.1.0 — Contours and .map export

- Application titles, the header and About use the project assembly version automatically. The title begins with `Coat — v1.1.0` and retains the document name and unsaved indicator.
- Closed shapes nested on the same level cut holes in the enclosing fill. Even-odd compound paths are used for the canvas, PNG and SVG. Each inner face can still be filled independently; contours on different levels never cut one another.
- The minimum zoom is now 1%, including mouse wheel, keyboard commands, framing and saved projects.
- Convex contours without holes bypass horizontal slicing: rotated rectangles export as one six-plane brush; N-sided circles and polygonal ellipses export as one N+2-plane brush. Existing transformed polygon vertices are preserved rather than reconstructed from rendering triangles.
- Concave regions and holes retain the existing slab partition. Adjacent pieces are merged only when they share a boundary and their convex hull has the same area. This reduces brush counts without filling holes; the greedy merge does not promise a globally minimal partition.
- Redundant collinear vertices are removed. Local-coordinate area calculations improve numerical stability. Wall/floor classification, height rules, Valve 220 format and existing `.coat` files remain compatible.

See [export validation](EXPORT-1.1.0.md) for the test matrix and verification limits.

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
- In Select, the line properties include **Arrow Start**, **Arrow End**, **Double Arrow**, **Triangle / Open / Diamond**, and **Arrow Size** in plan units. Arrow heads are limited to 45% of their edge length to avoid overlap. Splitting a line retains heads at the original endpoints.
- Focus a numeric field and turn the mouse wheel to adjust it by one. Expressions are evaluated first; bounded controls retain their limits. Validate coordinate/transform fields with Enter or Apply.
- Solid, Stripes and Dots use visual swatches with translated tooltips and accessibility names.

PNG and SVG include tokens, gradients and arrows. These are drawing decorations: `.map` exports the existing filled geometry, using the gradient's start color for wall/floor classification. New commands receive no default shortcut.

## 1.0.6

- Added optional bindings for editor commands, shape types, styles, exports, and organization. Existing keys are retained; new commands remain unassigned. Conflicts name both actions, and individual bindings can be cleared.
- Added arithmetic expressions and contextual Enter confirmation to numeric fields. Multiline annotations keep normal line breaks.
- Added multi-level selection, group renaming, and the Levels → Groups → Shapes Outliner. Multi-level duplication and clipboard operations preserve level offsets.
- Added Open Recent with ten deduplicated projects and missing-file handling.
- Saved View settings now include grid, length labels, upper/lower visibility, and selection scope in addition to framing. Legacy files remain supported.
- Cached render geometry and a bounded navigation image avoid rebuilding/rasterizing the scene on every pan. Exports retain vector rendering. Spatial buckets and per-level interval searches reduce normalization work.


## 1.0.5

- Esc cancels the current gesture, clears the selection, and returns to Select, including when nothing is selected.
- Preferences now group shortcuts by task, with search and Ergonomic / Classic presets. Shortcut capture requires an explicit click.
- Ergonomic defaults: Z/X/F for selection modes, A to frame all, W to move, Shift+R to rotate, Shift+S to scale, Shift+H for lower levels, and Ctrl+, for preferences.
- Legacy defaults migrate without overwriting custom bindings or creating conflicts.
- Circle and stair fields retain manual input and now offer increment/decrement arrows and Up/Down keys, within their valid ranges.
- No separate sample files in the delivery. The workspace separates the current app, package, guides, archives, and test artifacts.
- Project format 6 and .map export are unchanged.
- Publication review: English build/test messages, GitHub-ready README links, and explicit Windows runtime requirements.
- English drawing-preview measurements follow the selected interface language; SVG exports use an English title. Regional settings and Unicode filenames are covered by regression tests.

# Coat 1.0.4 — Epicteta Lupercal

- Corrected hotfix numbering: the previous fixes are 1.0.1, 1.0.2, and 1.0.3.
- Cleaner sidebar: settings, actions, and selection measurements remain visible. Hover over or click each tool’s ? button for instructions.
- Removed redundant headings and permanent explanatory paragraphs from tool and level panels.
- Elevation Z is now labeled Height. Elevation calculations and map exports are unchanged.

# Coat 1.0.3 — Epicteta Lupercal

- Map export now creates walls from the wall color and floors from all other fills. Unfilled faces and shapes create no objects.
- Floors are 16 units thick, with their top at the level elevation. Nested empty faces remain holes, and walls and floors stay separate.
- Generic Export to .map and Map Export labels throughout the interface and documentation. The included example is now named Example-Map.
- Orthogonal contours use rectangles, while diagonal and curved footprints are preserved. Project format 6 is unchanged.

# Coat 1.0.2 — 23 October 2026 — Epicteta Lupercal

- File → Export to .map: filled white faces become wall brushes in Valve 220 format. Customize the wall color in the Color tool.
- Editable Elevation Z in the Levels panel. Walls extend from their level’s Z to the next existing level’s Z; the highest level defaults to 192 units above its base.
- Orthogonal contours use rectangular brushes. Diagonal sections, curved contours, and openings retain their shapes. Export includes every level regardless of 2D visibility.
- Elevations and wall color support Undo/Redo and persist in .coat format 6. Formats 1–5 remain readable.
- Includes an editable .coat example and its .map export. Export creates wall geometry; configure materials and other game elements in a compatible map editor.

# Coat 1.0.1 — 22 October 2026

**Epicteta Lupercal**

- Faster geometry creation: operation-local vertex and edge indexes, bounding-box rejection before intersection tests, and level-specific vertex searches.
- View → Show Lower Levels toggles lower-level vertices, edges, fills, and annotations independently of upper-level outlines.
- Frame All, PNG, and SVG respect both visibility options. The active level remains visible. Switching interface languages retains the visibility setting for the session.
- The .coat file format is unchanged; existing plans need no conversion.

# Coat 1.0 — 22 October 2026

**Epicteta Lupercal**

- English is the default interface language. Choose English or Français in Edit → Preferences and save to apply the change without restarting.
- English localization covers menus, tool panels, contextual help, shortcut names, color controls, level controls, canvas labels, validation messages, application confirmations, and built-in help.
- Standard editor terminology throughout: Vertices / Edges / Faces, Move / Rotate / Scale, Fill, Long dashes, Eyedropper, and Straight / L-shaped / U-shaped / Spiral stairs.
- Existing custom shortcuts are retained. The language preference is saved atomically with the shortcut settings.
- Changing language preserves the open plan, selection, view, palette, and undo history. User-authored annotations and group names are not translated.
- An English user guide and an English-labeled stair example are included. Project format 5 is unchanged.

## Earlier features retained

- Personal palette with 12 initially empty slots, + to save a color, right-click to delete, and an eyedropper that samples the plan and reference image.
- Stair outlines remain solid; tread lines use native long dashes.
- Numeric and mouse-based movement, rotation, and scaling; rectangular, circular, and stair shapes.
- Groups, clipboard operations, levels, reference images, annotations, line styles, and fill patterns.
- PNG export includes only the visible plan, grid, and scale. SVG exports a vector plan.
