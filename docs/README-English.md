# Coat user guide

## Run Coat

Extract the entire ZIP, then open **Coat.exe**. Keep the executable, DLL, and JSON runtime files together. When building from source, run **app/Coat.exe**.

Coat requires **Windows 10 or 11 and [.NET Desktop Runtime 8 (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)**. On the download page, choose **.NET Desktop Runtime → Windows → x64**. The runtime is not bundled. No Coat installer or account is required. Once the runtime is installed, Coat works offline.

## Lua plugins

Use **Plugins → Open Plugin Folder…** to add Lua scripts. Reopen the menu and select an extension to view its source or run it. [Lua plugin guide](LUA-PLUGINS.md)

## Appearance

In Edit → Preferences, select Classic or Bordeaux. The theme takes effect immediately. Hover over a button to see its name and keyboard shortcut, if assigned. Hold Shift to read its description. Keyboard focus also reveals command hints. Drag the divider at the left edge of the right panel to resize it. With a selection, press **S**, **R** or **T** for Scale, Rotate or Move, then drag on the plan. These contextual keys take priority over tool shortcuts while a selection is active.

## Language and preferences

**English is the default**, including when upgrading from an earlier version without a language preference. Open **Edit → Preferences → Interface language**, choose **English** or **Français**, then click **Save**. The interface changes immediately; no restart is required. The choice is retained between sessions. **Cancel** discards pending language and shortcut edits. Theme choices are saved as soon as you select them.

Open **Edit → Preferences** to assign shortcuts. New installations leave commands **Unassigned**, except for Enter confirmation. Upgrading preserves every existing binding and leaves newly introduced actions unassigned. Click a binding to capture a new combination, or **×** to clear it. Conflict messages identify both commands. Shortcuts are grouped and searchable.

**Ergonomic** and **Classic** are optional presets applied only when you explicitly select them and save. **Restore defaults** clears the draft bindings (except Enter) and selects English. Cancelling discards the preset. Shift+drag to extrude, Ctrl+click to add/remove selection, and Space+drag to pan remain mouse gestures.

Preferences are saved in `%LOCALAPPDATA%/Coat/preferences.json`. Existing custom shortcuts are preserved when upgrading. Changing the interface language does not change project content: annotations, group names, filenames, geometry, fills, selection, view, and undo history are retained.

Keyboard examples below refer to the optional **Ergonomic** preset. They are not assigned automatically on new installations. Every action is also available through the menus, toolbar, or sidebar. Windows file dialogs follow the operating system's language; Coat's menus, tool panels, messages, help, and confirmation buttons follow the language selected in Coat.

## Arithmetic and confirmation

All numeric entry fields accept `+`, `-`, `*`, `/`, parentheses, signed values, and decimal commas or points. Multiplication and division take precedence. Expressions are evaluated on Enter, when leaving the field, or when applying its action. Invalid expressions, division by zero, non-finite results, and values outside a control's range leave the stored value unchanged. Counts and level numbers require integer results.

Enter applies the relevant coordinates, translation, rotation, scale, level height, reference-image settings, or annotation settings. On the canvas, it completes a shape preview or transform gesture, or finishes an open drawing chain. In an annotation, Enter confirms the edit and Shift+Enter inserts a line break. Standard dialogs retain their default confirmation button.

## Multi-level selection and groups

Enable **Select Across Levels** in Select to include visible lower and upper levels. Ctrl+click, box selection, and Select All then operate across those levels. A group may contain geometry from multiple levels. Moving, rotating, scaling, duplicating, copying, and pasting retain the group's level structure. Assigning a selection to a level intentionally moves its members to that one level.

**View → Outliner**, also available in the level panel, displays Levels → Groups → Shapes. Expand a level to browse its contents. Select a group to manipulate all members, including members on other levels. A group spanning several levels appears under each level containing its geometry. Rename a selected group in the sidebar; ungrouping leaves its geometry intact. Shapes are existing closed outlines; this update does not convert them into new parametric objects.

## Recent projects and saved View settings

**File → Open Recent** lists the ten most recently opened or saved projects, without duplicates. Opening an entry protects unsaved work through the normal save prompt. Missing files produce a message and leave the current plan unchanged. **Clear Recent Files** clears the list, not the files. The list is stored in `%LOCALAPPDATA%/Coat/recent-files.json`.

Saving a project records camera position, zoom, grid spacing and visibility, length-label visibility, upper/lower-level visibility, and multi-level selection scope. Reopening restores those settings. Older files retain their stored camera/grid settings and receive the previous visibility defaults. Navigation alone does not create an undo entry; use Save to persist the view.

## Draw your first plan

1. Choose **Draw** (`D`) and set the grid spacing. Click a grid intersection to place the first vertex, then click another intersection to draw an edge. Each click extends the outline.
2. Click the first vertex again to close the outline. Press `Esc` or right-click to finish an open outline; completed edges remain in the plan.
3. Choose **Color** (`C`), select **Fill faces**, and choose a color. Click inside a closed outline to fill the face. **Color edges** applies a color and line style to individual edges.
4. Choose **Select** (`V`) to edit vertices, edges, or faces. Drag selected geometry to move it. Hold `Ctrl` while clicking to add to the selection. Drag in empty space to box-select fully enclosed elements.
5. Press `Ctrl+S` to save a `.coat` project. Projects store geometry, levels, groups, fills, styles, annotations, view settings, and an embedded reference image.

**File → Open Example Plan** opens the built-in sample. Separate example files are no longer included in the application folder.

## Navigate and set the grid

Use the mouse wheel to zoom. Pan with the arrow keys, middle/right mouse drag, or Space+left-drag. Hold Shift with an arrow key for faster panning. Press `A` or click **Frame All** to fit the plan in the view.

The grid menu and number keys `1` through `0` select spacings of **1, 2, 4, 8, 16, 32, 64, 128, 256, and 512 units**. `G` toggles the grid; `M` toggles edge-length labels. Zooming changes the view, not the dimensions of the geometry. At very small zoom levels, fewer grid lines are drawn while the snap spacing stays unchanged.

## Select and transform geometry

The selection modes are **Vertices** (`Z`), **Edges** (`X`), and **Faces** (`F`). A face is an enclosed 2D region, not a 3D volume. A fully selected shape can be dragged by its interior as well as by a vertex or edge.

- **Move** (W): drag to move on the grid, or enter Offset X/Y and click **Move selection**. Entered offsets are retained during the session. In Edge mode, Shift+drag an edge to extrude it and add thickness, or use **Extrude by offset**.
- **Rotate** (Shift+R): drag around the center of the selection or enter an angle and click **Apply rotation**. Positive angles rotate counterclockwise. Mouse rotation uses 1° increments, or 15° while holding Shift. If a drag begins at the pivot, move horizontally to rotate.
- **Scale** (Shift+S): drag for uniform scaling about the selection center, or enter a factor and click **Apply scale**. A factor of 1 keeps the original size, 2 doubles it, and 0.5 halves it. Turn off **Uniform scale** to enter independent X/Y factors. Negative factors mirror the selection. Numeric factors must have an absolute value between 0.001 and 1000; zero is not allowed. Mouse scaling ranges from 0.01 to 1000; Shift snaps to increments of 0.1.

Rotation and scaling preserve the shape without snapping each vertex independently. Intersections are normalized as in ordinary drawing. These transformations apply to geometry; annotations have separate position and text-size controls. `Esc` cancels a drag in progress. `Ctrl+Z` undoes an entire transformation.

Press `Ctrl+G` to group a selection and `Ctrl+Shift+G` to ungroup it. Clicking any member selects its group. Use `Ctrl+C`, `Ctrl+X`, and `Ctrl+V` to copy, cut, and paste, including between Coat windows. Pasting places the selection near the view center, aligning its lowest selected level with the active level and retaining relative level offsets; repeated pastes are offset by one grid step. Styles, fills, annotations, and fully included groups are preserved. Text fields retain their normal clipboard shortcuts.

## Shapes and stairs

**Rectangle:** place two opposite corners, or click and drag. The shape has four vertices.

**Circle:** place the center and radius, or click and drag. Choose 3 to 128 vertices for the outline. The center and radius snap to the grid; the vertices remain evenly spaced.

**Stairs:** choose **Straight**, **L-shaped**, **U-shaped**, or **Spiral**. Two clicks or a click-drag creates the staircase with a live preview.

- Straight, L-shaped, and U-shaped stairs start at the center of the entrance. The dominant drag axis sets the orientation and direction of ascent. Drag distance sets the combined run of the flights, excluding the landing. Set the width in units; L-shaped and U-shaped stairs also have a left/right turn setting. U-shaped stairs include a gap between the flights.
- Spiral stairs start at the center. Drag to set the outer radius and initial radial direction. The angle controls a sweep from 15° to 360°. Turn direction is adjustable. The central opening is 22% of the outer radius.
- Set a total of 2 to 128 steps. L-shaped and U-shaped stairs distribute them across two flights; landings are additional.

For circle vertex counts, stair step counts, stair width, and spiral angle, type a value or use the small **▴ / ▾** buttons. **Up / Down** in the field adjusts the value by one without panning. Arrows respect the valid range. These settings also affect a shape preview already in progress.

Stair outlines are solid, and tread lines use the native **Long dashes** style. Each staircase is grouped automatically. Creation is undone in a single step. Sidebar settings apply to new stairs; placed stairs are editable geometry, rather than parametric objects. Ungroup them to edit individual parts.

## Colors, patterns, and annotations

The 12 saved-color slots start empty. Open **Color wheel…**, choose a color, and click **+** to save it in the first empty slot. An existing color is not duplicated. Right-click a saved color and choose **Delete** to free that slot without shifting other colors or altering the plan. Saved colors are shared by the sidebar and color picker and persist in `%LOCALAPPDATA%/Coat/palette.json`.

Use the wheel and Hue, Saturation, and Brightness sliders, or enter a six-digit hex color. **Use this color** confirms the choice. **Cancel** leaves the active color unchanged; colors explicitly saved with + remain in the palette.

**Eyedropper…** temporarily hides the color picker so you can sample a pixel from the visible plan, including its reference image. Click to choose a color, or press `Esc`/right-click to cancel. Sampling includes the visible opacity, grid, and patterns. Use + to save the sampled color, or **Use this color** to apply it to the active tool.

Line styles are **Solid**, **Long dashes**, and **Short dashes**. Fill patterns are **Solid**, **Stripes**, and **Dots**. Apply them in Select mode to existing geometry, or set them in Color before painting.

Choose **Text** (`T`) and click to place an annotation. Click existing text to edit it, or drag to move it. Set its text, size, color, and coordinates in the sidebar, then click **Apply text**. Annotations belong to a level and are included in exports.

## Levels and reference images

**View → Show Lower Levels** or **Shift+H** toggles lower-level vertices, edges, fills, and annotations. Lower levels are visible by default. Frame All and image exports respect this setting; the active level always remains visible.

Use the level panel or `Page Up`/`Page Down` to switch levels. Adjacent navigation creates a level if needed. The active level is shown in red in the read-only three-quarter preview. Lower levels fade progressively with distance but remain visible. Upper levels are hidden by default; **View → Show Upper Levels** or `H` toggles thin red outlines. Upper-level outlines become selectable when **Select Across Levels** is enabled in the Select sidebar. Hidden levels are excluded from direct and box selection; explicitly selecting a group selects all of its members.

Use **Move Selection to Level** to assign selected elements to another level. Moving vertices also moves their connected edges. **Delete this level** removes its geometry, fills, and annotations; `Ctrl+Z` restores them.

Press `Ctrl+I` or choose **File → Import Image…** to add a PNG, JPG, or BMP reference image. With no tool active, the sidebar shows its visibility, opacity, origin, and width controls. The grid is drawn above the image. Images are embedded in `.coat` files.

**Esc** cancels the current gesture, clears the selection, and returns to **Select**, even when nothing is selected. Completed geometry is retained. To reveal reference-image settings, click the active tool button again.

## Save, export, and recover changes

- `Ctrl+S`: save. `Ctrl+Shift+S`: save as. `Ctrl+N`: new plan. `Ctrl+O`: open.
- `Ctrl+Z`: undo. `Ctrl+Y` or `Ctrl+Shift+Z`: redo. `Ctrl+D`: duplicate. `Delete`: delete the selection.
- **File → Export PNG…** exports the current visible plan, grid, and scale at the displayed framing and zoom. Menus, toolbars, and the sidebar are excluded.
- **File → Export SVG…** exports a vector plan. Fills, line styles, patterns, annotations, and the embedded reference image are included. Upper-level guide visibility is respected by both formats.

`.coat` is a JSON project format. Coat saves projects in **format 6** and can open formats 1–5.

## Export to .map

1. Set **Height** in the Levels panel and click **Apply**. Existing levels default to their number × 192 units. New neighbouring levels are spaced 192 units from the nearest existing level. Heights may be negative or fractional.
2. Fill closed faces with white (`#FFFFFF`) to mark walls. Under **Color → Map Export**, use **Wall Color…** to choose another color with the color wheel, or **Use Current Color for Walls**. This changes the wall classification for this project; it does not repaint any faces. Fill patterns do not change wall geometry. Colored edges alone are not walls.
   Faces filled with any other color become floors. Unfilled faces or shapes create no objects.
3. Choose **File → Export to .map…**. The export includes filled faces on every level, regardless of visibility in the 2D view. X/Y coordinates and scale are preserved, with one Coat unit equal to one map unit.

The wall base is the current level's Z. Its top is the Z of the next existing level by level number, even if that level is empty. The highest level uses base +192. For example, Z 64 followed by Z 320 produces walls 256 units high. A top at or below the base prevents export and identifies the level to correct. Elevations and wall color are saved with the project and support Undo/Redo.

Floors are **16-unit-thick slabs**, from **Z − 16 to Z**, so the top surface matches the level elevation. Orthogonal walls and floors use rectangular brushes; diagonal and curved sections use convex prisms following the original polygon. For nested faces, the innermost face determines the result: wall color → wall, other fill → floor, no fill → empty space. Openings remain clear, and walls never merge with floors.

Output uses **Valve 220**, with a Quake game header and untextured `__TB_empty` brushes in worldspawn. Open as **Quake / Valve** in a compatible map editor and assign game materials there. This export creates walls and floors, without automatic ceilings, lights, or player starts. It does not compile a playable map. Reference images, annotations, and line styles remain in the `.coat` plan.

The exporter preserves the designed dimensions; allow clearance beyond the 40 × 80-unit player size when designing passages.

## Build and verify

Run `./build.ps1` with the .NET 8 SDK to publish to `app`. Run **./build.ps1 -Package** to create the delivery ZIP in **dist/**. Run **./test.ps1** to build and run the regression suite. Test artifacts are written to `artifacts/tests`; tests use isolated preference files and do not overwrite the user's settings.

English strings are embedded from `src/Locales/en.json`. French source strings remain translation keys; shortcut identifiers are stable and independent of their displayed language. Context-sensitive labels distinguish Undo from Cancel and vertices from fill-pattern dots.

For an isolated regression run, use `./test.ps1 -OutputDirectory app-1.3 -TestOutputDirectory artifacts/tests-1.3`.
