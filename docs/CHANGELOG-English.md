# Coat 1.0.5 — Epicteta Lupercal

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
