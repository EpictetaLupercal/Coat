# Lua plugins

1. Choose **Plugins → Open Plugin Folder…** to open the `plugins` folder beside `Coat.exe`.
2. Add a UTF-8 `Example.lua` file or a folder named `Example` containing `main.lua`.
3. Open **Plugins** again. Choose an extension, then **View Source…** to read it or **Run Plugin** to start it.

The bundled **Plan summary** plugin prints the grid size and geometry counts for each level. Plugin output appears in a window where you can select and copy text. Coat scans filenames when the menu opens and runs a script only when you choose **Run Plugin**.

## API version 1

Coat uses the [MoonSharp interpreter](https://www.moonsharp.org/), which implements Lua 5.2. It starts a fresh environment for each run with the Lua string, table and math libraries. File access, system commands, loading other scripts and .NET access are outside the plugin API.

| Name | Meaning |
| --- | --- |
| `coat.api_version` | Version of the Coat scripting API |
| `coat.active_level` | Active level number when the script starts |
| `coat.grid` | Grid step in plan units |
| `coat.levels` | Array of tables with `number`, `z`, `points` and `edges` |
| `coat.add_rectangle(x, y, width, height)` | Queue an outline on the active level using absolute plan coordinates |
| `print(...)` | Show text in the output window |

Save this as `Add platform outline.lua` to try an editing plugin:

```lua
local step = coat.grid
coat.add_rectangle(0, 0, 8 * step, 6 * step)
print("Platform outline added on level " .. coat.active_level)
```

Queued edits apply together on successful completion and can be undone in one step. If the script fails or the plan changes during execution, Coat discards the queued rectangles. The entry file can be up to 256 KiB, and one run can queue up to 64 rectangles. Long-running Lua loops are subject to an instruction limit. Use scripts you trust.
