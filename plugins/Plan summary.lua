-- Summarize the open Coat plan. Runs only when selected in the Plugins menu.
print("Active level: " .. coat.active_level)
print("Grid: " .. coat.grid .. " units")
for _, level in ipairs(coat.levels) do
    print(string.format("Level %d | Z %g | %d points | %d edges",
        level.number, level.z, level.points, level.edges))
end
