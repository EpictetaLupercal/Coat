// Coat — Epicteta Lupercal
using System.IO;
using System.Text;
using MoonSharp.Interpreter;

namespace Coat;

internal record LuaPlugin(string Name, string Path)
{
    private const int MaxSourceBytes = 256 * 1024;

    internal static IReadOnlyList<LuaPlugin> Discover(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<LuaPlugin>();
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        var scripts = Directory.EnumerateFiles(folder, "*", options)
            .Where(path => string.Equals(System.IO.Path.GetExtension(path), ".lua", StringComparison.OrdinalIgnoreCase))
            .Select(path => new LuaPlugin(System.IO.Path.GetFileNameWithoutExtension(path), path));
        var packages = Directory.EnumerateDirectories(folder, "*", options)
            .Select(path => new LuaPlugin(System.IO.Path.GetFileName(path), System.IO.Path.Combine(path, "main.lua")))
            .Where(plugin => File.Exists(plugin.Path) && (File.GetAttributes(plugin.Path) & FileAttributes.ReparsePoint) == 0);
        return scripts.Concat(packages).OrderBy(plugin => plugin.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(plugin => plugin.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal string ReadSource()
    {
        using var file = File.OpenRead(Path);
        if (file.Length > MaxSourceBytes) throw new InvalidDataException(L.T("Le script Lua dépasse 256 Kio."));
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
    }
}

internal record LuaLevel(int Number, double Z, int Points, int Edges);
internal record LuaPlanInfo(int ActiveLevel, int Grid, IReadOnlyList<LuaLevel> Levels)
{
    internal static LuaPlanInfo Capture(Plan plan)
    {
        var levelByPoint = plan.Vertices.ToDictionary(point => point.Id, point => point.Level);
        var points = levelByPoint.Values.ToLookup(level => level);
        var edges = plan.Edges.ToLookup(edge => levelByPoint[edge.A]);
        return new(plan.ActiveLevel, plan.Grid, plan.Levels.Order()
            .Select(level => new LuaLevel(level, plan.LevelZ(level), points[level].Count(), edges[level].Count())).ToArray());
    }
}

internal record LuaRectangle(double X, double Y, double Width, double Height)
{
    internal V2[] Corners() => [new(X,Y), new(X+Width,Y), new(X+Width,Y+Height), new(X,Y+Height)];
}

internal record LuaRun(string Output, IReadOnlyList<LuaRectangle> Rectangles);

internal static class CoatLua
{
    private const int OutputLimit = 16000;

    internal static LuaRun Run(string source, string name, LuaPlanInfo plan)
    {
        if (source.StartsWith("MoonSharp_dump_b64::", StringComparison.Ordinal))
            throw new ScriptRuntimeException("Use Lua source code rather than compiled bytecode.");

        var rectangles = new List<LuaRectangle>();
        var output = new StringBuilder();
        var script = new Script(CoreModules.Preset_HardSandbox);
        script.Options.DebugPrint = line =>
        {
            if (output.Length < OutputLimit)
                output.AppendLine(line[..Math.Min(line.Length, OutputLimit - output.Length)]);
        };
        var api = new Table(script);
        script.Globals["coat"] = api;
        api["api_version"] = 1;
        api["active_level"] = plan.ActiveLevel;
        api["grid"] = plan.Grid;
        var levels = new Table(script);
        api["levels"] = levels;
        foreach (var level in plan.Levels)
        {
            var entry = new Table(script);
            entry["number"] = level.Number;
            entry["z"] = level.Z;
            entry["points"] = level.Points;
            entry["edges"] = level.Edges;
            levels.Append(DynValue.NewTable(entry));
        }

        api["add_rectangle"] = DynValue.NewCallback((_, args) =>
        {
            if (args.Count != 4 || Enumerable.Range(0, 4).Any(i => args[i].Type != DataType.Number))
                throw new ScriptRuntimeException("Expected coat.add_rectangle(x, y, width, height).");
            var rectangle = new LuaRectangle(args[0].Number, args[1].Number, args[2].Number, args[3].Number);
            if (rectangle.Width < 1e-5 || rectangle.Height < 1e-5 || !rectangle.Corners().All(SelectionTransform.Valid))
                throw new ScriptRuntimeException("Rectangle dimensions must be positive and within the plan bounds.");
            if (rectangles.Count >= 64) throw new ScriptRuntimeException("A plugin can add up to 64 rectangles per run.");
            rectangles.Add(rectangle);
            return DynValue.Nil;
        });

        var job = script.CreateCoroutine(script.LoadString(source, codeFriendlyName: name)).Coroutine;
        // Stop accidental endless loops between instruction slices.
        job.AutoYieldCounter = 1000;
        for (int slice = 0; slice < 500; slice++)
        {
            if (job.Resume().Type != DataType.YieldRequest)
                return new(output.ToString().TrimEnd(), rectangles);
        }
        throw new ScriptRuntimeException("Lua instruction limit exceeded.");
    }

    internal static Plan? Apply(string original, LuaRun result)
    {
        if (result.Rectangles.Count == 0) return null;
        var copy = Plan.Parse(original);
        foreach (var rectangle in result.Rectangles)
        {
            var corners = rectangle.Corners().Select(point => copy.Point(point)).ToArray();
            for (int i = 0; i < corners.Length; i++) copy.AddEdge(corners[i], corners[(i + 1) % corners.Length]);
        }
        copy.Normalize();
        if (copy.Vertices.Count > 20000 || copy.Edges.Count > 30000)
            throw new InvalidDataException(L.T("Le plugin dépasse la capacité du plan."));
        return copy;
    }
}
