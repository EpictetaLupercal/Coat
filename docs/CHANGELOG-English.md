# Coat 1.7.2 — Epicteta Lupercal

## 1.7.2 — Lower overhead when reopening plans

Reopening the current plan preserves its editor state. The OpenGL context survives interface changes, and undo shares unchanged reference image data across snapshots.


## 1.7.1 — Smoother wheel zoom

The OpenGL viewport reuses the existing drawing during rapid wheel zoom and restores full detail at the final zoom level.


## 1.7 — OpenGL viewports

The plan and the orbiting cube now render using OpenGL. Camera panning reuses the recorded scene and cached GPU pixels. The minimap keeps its cached topology and updates its camera indicator as you move.

## 1.6.5 — Faster camera panning

- Mouse movements during a pan are coalesced into one viewport update per frame.
- Decorative menu animations pause while panning and resume from the same position afterward.
- The minimap displays a low-resolution silhouette of all outlines in one color. While the camera moves, its frame and markers update over the cached silhouette.

## 1.6.4 — Displayed application FPS

- The status bar uses PresentMon events for Coat's process to measure frames displayed on screen. It shows an unavailable reading when Windows graphics tracing is denied.

## 1.6.3 — Viewport FPS

- The status bar shows how often the 2D viewport redraws.

## 1.6.2 — Straight minimap and ping pulses

- The minimap follows the plan axes. A small edge arrow points toward the camera when its center is outside the minimap.
- A new ping sends out a short, expanding halo in the plan and the minimap.

## 1.6.1 — Minimap drawer

- The minimap handle slides down to the ping buttons when the map is folded and returns to the top when reopened.

## 1.6 — Minimap, pings, and project progression

- Use the foldable 45° minimap in the lower left to inspect visible levels, center the camera or drag its viewport frame without changing zoom.
- Place Landmark, Review and Playtest pings from either view. Right-click to pin a ping as an editable project marker. Ping shortcuts are unassigned and the ping sound can be changed in the sound preferences.
- The viewport HUD shows project level, exact XP on hover, and contextual commands. Save, create, export and complete the project quests to earn XP. Each project keeps its own progress.

## 1.5.5 — Full selection outlines

- The halo lights the entire selected edge or face outline immediately. Face outlines glow more strongly and include the borders of holes.

## 1.5.4 — Edge and face selection feedback

- Selecting an edge or face shows a short contracting halo at the click. The edge highlight spreads in both directions, and face highlights follow the outer and inner contours.

## 1.5.3 — Extrusion feedback

- Committing an extrusion by Shift-drag or by offset plays the construction highlight when it creates a closed area.

## 1.5.2 — Interaction feedback

- Brief halos and highlights mark point, edge and face selection, marquee selection and completed moves.
- Closing a drawn area or placing a shape triggers a construction highlight. Stairs animate in the direction of ascent.
- **Preferences → Interaction Feedback** controls selection and construction independently. The effects follow the Windows animation preference and stay out of exports.

## 1.5.1 — Selection modes

- Vertex, edge and face selection now appear at the start of the Select tool's contextual bar, beside its transform controls.

## 1.5 — Contextual tool options

- A contextual bar below the main toolbar keeps selection transforms, shape presets and common numeric controls near the canvas.
- The inspector retains selection properties, group controls, extrusion settings and detailed stair settings.

## 1.4.3 — Toolbar and inspector

- The grouped toolbar gives direct access to vertex, edge and face selection and keeps Dimension between Draw and Erase.
- Right-click in Select mode for grouping, moving to a level, isolating or mirroring a selection, and existing geometry commands.
- The inspector has three tabs: **Plan**, **Select** and **Modifiers**. The **Plan** tab contains level settings, a collapsible preview and the reference image.

## 1.4.2 — Focus Mode

- Press **/** with a selection to isolate and frame it in the 2D view. Press **/** again to restore the previous camera. The shortcut can be changed in Preferences.

## 1.4.1 — Mirror and Boolean

- Reflect a selection around a horizontal or vertical axis with **Mirror**. Enable **Keep original** to create a reflected copy.
- The **Modifiers** tab previews Boolean union, difference and intersection before applying them. Pick two closed areas on the active level or draw a temporary rectangle as the second operand. Applying the result is undoable.

## 1.4 — Editor sounds

- Selection and drawing play short, distinct clicks. In **Options → Editor Sounds**, adjust the volume, enable either sound independently, preview a sound, or choose a local WAV file. Coat uses its built-in sound if a custom file is missing or cannot be played.

## 1.3.9 — 3D View

- Open **Options → 3D View** for a separate window with a centered cube. Drag with the left mouse button to orbit and scroll to zoom. Press **F** in that window to frame the cube. The shortcut can be changed in Preferences.

## 1.3.8 — Inspector background

- The inspector has a cooler background and subtler faceted shapes with shaded faces.

## 1.3.7 — Levels preview

- The three-quarter Levels preview has a little more vertical space in both window layouts.

## 1.3.6 — Animated menu decorations

- The menu bar and toolbar share one subtle animated circuit. The inspector scrolls a cached field of geometric facets that matches the selected theme. Turn off both backgrounds in **Preferences → Appearance**.
- When animations are disabled in Windows, the decorations stay still.

## 1.3.5 — Header logo

- The official logo blends into the header with a transparent background and a single wordmark.

## 1.3.4 — Official logo

- The official Coat logo appears in the editor header, the Windows application icon and the README.

## 1.3.3 — Button shortcuts

- Command hints show the active keyboard shortcut in parentheses after the button name. Hold Shift to reveal the description.

## 1.3.2 — Lua plugins

- The Plugins menu discovers `.lua` files and addon folders with a `main.lua` entry point. It can open the plugin directory, show source code, and run a selected plugin.
- The Lua API exposes grid and level information and lets plugins add rectangular outlines as a single undoable edit. Includes the Plan summary plugin.

## 1.3.1 — Inspector and selection shortcuts

- Drag the left edge of the right panel to resize it. The Levels preview is taller and sidebar commands use compact icons.
- Drop-down menus use black text on white while the menu bar follows the selected theme.
- With a selection, S, R and T activate Scale, Rotate and Move. Text and tokens can be resized or rotated on the canvas.

## 1.3 — Compact interface and Bordeaux theme

The toolbar and transform controls use accessible icons. Hold Shift over a command for details. The Groups and Map Export sections remember their expanded state during editing. Select Bordeaux in Preferences for burgundy panels, brass rules and ivory text.


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
