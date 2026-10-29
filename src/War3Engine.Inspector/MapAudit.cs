using System.Text;
using System.Text.Json;
using War3Engine.Assets;
using War3Engine.Compat;
using War3Engine.Core;
using War3Engine.Scripting;
using War3Net.Common.Extensions;

namespace War3Engine.Inspector;

/// <summary>
/// Phase A audit: classifies the target map's extension natives and generates
/// docs/target-map-jass-dependencies.md + docs/target-map-native-status.json.
/// </summary>
internal static class MapAudit
{
    public static int Run(EngineSettings settings, AssetStore store, string? mapPath, string outputDirectory)
    {
        var target = mapPath
                     ?? (string.IsNullOrWhiteSpace(settings.YdweMapPath) ? settings.MapPath : settings.YdweMapPath);
        if (!File.Exists(target))
        {
            Console.WriteLine($"map not found: {target}");
            return 1;
        }

        store.MountMap(target);
        var scriptBytes = store.ReadAllBytes("war3map.j", out _);
        var source = Encoding.UTF8.GetString(scriptBytes);
        var program = JassProgram.Parse(source);
        var model = JassProgramModel.Build(program);
        var inventory = JassScriptInventory.Parse(source);
        var startup = model.AnalyzeStartup();

        var commonSource = ReadScript(store, "Scripts\\common.j");
        var blizzardSource = ReadScript(store, "Scripts\\Blizzard.j");
        var common = JassProgramModel.Build(JassProgram.Parse(commonSource));
        var blizzard = JassProgramModel.Build(JassProgram.Parse(blizzardSource));
        var merged = JassProgramMerge.Merge(common, blizzard, model);
        var runtime = new JassGameRuntime();
        var registry = JassMapHost.CreateRegistry(merged, runtime);

        var locations = NativeLocations(source);
        var callCounts = inventory.Natives.ToDictionary(n => n.Name, n => n.CallCount, StringComparer.Ordinal);
        var imports = Program.ReadMapImports(store);
        var components = YdweInstallation.Detect(settings.WarcraftPath);
        var gameVersion = YdweInstallation.GameVersion(settings.WarcraftPath);

        var natives = new List<NativeAudit>();
        foreach (var (name, native) in model.Natives.OrderBy(n => n.Key, StringComparer.Ordinal))
        {
            var provider = NativeProviders.Classify(name);
            var category = Classify(name, provider, common, blizzard, model);
            locations.TryGetValue(name, out var location);
            natives.Add(new NativeAudit(
                name,
                FormatSignature(native),
                native.ReturnType,
                provider,
                category,
                callCounts.TryGetValue(name, out var calls) ? calls : 0,
                callCounts.TryGetValue(name, out var used) && used > 0,
                startup.ReachableNatives.Contains(name),
                location.Section,
                location.Line,
                registry.Natives.ContainsKey(name) ? "implemented" : "missing"));
        }

        var standardCalls = startup.ReachableNatives.Where(n => common.Natives.ContainsKey(n)).OrderBy(n => n).ToArray();
        var reachableCallNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var functionName in startup.ReachableFunctions)
        {
            foreach (var call in model.Functions[functionName].Calls)
            {
                reachableCallNames.Add(call);
            }
        }

        var initStandardNatives = reachableCallNames
            .Where(n => common.Natives.ContainsKey(n) && !model.Natives.ContainsKey(n))
            .OrderBy(n => n)
            .ToArray();
        var initBlizzardFunctions = reachableCallNames
            .Where(n => blizzard.Functions.ContainsKey(n))
            .OrderBy(n => n)
            .ToArray();
        var initUnknownCalls = reachableCallNames
            .Where(n => !model.Functions.ContainsKey(n) && !model.Natives.ContainsKey(n)
                        && !common.Natives.ContainsKey(n) && !blizzard.Functions.ContainsKey(n))
            .OrderBy(n => n)
            .ToArray();
        var unknown = natives.Where(n => n.Category == "unknown").ToArray();
        var called = natives.Where(n => n.Called).ToArray();

        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "target-map-native-status.json");
        var mdPath = Path.Combine(outputDirectory, "target-map-jass-dependencies.md");

        WriteJson(jsonPath, new
        {
            map = target,
            mapBytes = new FileInfo(target).Length,
            gameVersion,
            script = new
            {
                language = "jass",
                lines = inventory.Lines,
                bytes = inventory.ScriptBytes,
                functions = model.Functions.Count,
                nativesDeclared = model.Natives.Count,
                globals = model.Globals.Count,
                libraries = inventory.Libraries.Select(l => l.Name).ToArray(),
            },
            startup = new
            {
                root = "main",
                reachableFunctions = startup.ReachableFunctions.Count,
                reachableDeclaredNatives = startup.ReachableNatives.Count,
                reachableStandardNatives = standardCalls.Length,
                initStandardNatives = initStandardNatives,
                initBlizzardFunctions = initBlizzardFunctions,
                initUnknownCalls = initUnknownCalls,
            },
            summary = new
            {
                declared = natives.Count,
                called = called.Length,
                initReachable = natives.Count(n => n.InitReachable),
                byCategory = called.GroupBy(n => n.Category).ToDictionary(g => g.Key, g => g.Count()),
                byProvider = called.GroupBy(n => n.Provider).ToDictionary(g => g.Key, g => g.Count()),
            },
            components = components.Select(c => new { c.Name, c.Kind, c.Version, c.Path }).ToArray(),
            imports = new
            {
                total = imports.Count,
                resolved = imports.Count(i => i.Resolved),
                missing = imports.Where(i => !i.Resolved).Select(i => i.Path).ToArray(),
            },
            natives,
        });

        var md = new StringBuilder();
        md.AppendLine("# Target map JASS dependencies (Phase A audit)");
        md.AppendLine();
        md.AppendLine($"- Map: `{target}` ({new FileInfo(target).Length:N0} bytes)");
        md.AppendLine($"- Game version: {gameVersion ?? "unknown"}");
        md.AppendLine($"- Components: {string.Join(", ", components.Where(c => c.Kind != "editor-runtime").Select(c => $"{c.Name}={c.Version}"))}");
        md.AppendLine($"- Script: JASS, {inventory.Lines:N0} lines, {model.Functions.Count} functions, {model.Natives.Count} declared extension natives, {model.Globals.Count} globals");
        md.AppendLine($"- Libraries ({inventory.Libraries.Count}): {string.Join(", ", inventory.Libraries.Select(l => l.Name))}");
        md.AppendLine($"- Imports: {imports.Count} total, {imports.Count(i => !i.Resolved)} missing ({string.Join(", ", imports.Where(i => !i.Resolved).Select(i => i.Path))})");
        md.AppendLine();
        md.AppendLine("## Classification");
        md.AppendLine();
        md.AppendLine($"- Declared extension natives: {natives.Count}; called: {called.Length}; reachable from `main`: {natives.Count(n => n.InitReachable)}.");
        md.AppendLine($"- Provider split (called): {string.Join(", ", called.GroupBy(n => n.Provider).Select(g => $"{g.Key}={g.Count()}"))}.");
        md.AppendLine("- Category rules: standard = present in `Scripts\\common.j` natives; helper = present in `Blizzard.j` or map functions; YDWE-* = Dz*/EX*/RequestExtra* naming used by the verified plugins; unknown = everything else.");
        md.AppendLine($"- Standard native names redeclared by map: {(standardCalls.Length == 0 ? "none" : string.Join(", ", standardCalls))}.");
        md.AppendLine($"- Unknown-meaning natives: {(unknown.Length == 0 ? "none" : string.Join(", ", unknown.Select(u => u.Name)))}.");
        md.AppendLine();
        md.AppendLine("## Called natives");
        md.AppendLine();
        md.AppendLine("| Native | Signature | Provider | Category | Calls | Init-reachable | Status | Source |");
        md.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var native in called.OrderByDescending(n => n.InitReachable).ThenByDescending(n => n.CallCount).ThenBy(n => n.Name))
        {
            md.AppendLine($"| `{native.Name}` | `{native.Signature}` | {native.Provider} | {native.Category} | {native.CallCount} | {(native.InitReachable ? "yes" : "no")} | {native.Status} | {native.Section}:{native.Line} |");
        }

        md.AppendLine();
        md.AppendLine("## Startup reachability");
        md.AppendLine();
        md.AppendLine($"- Reachable functions: {startup.ReachableFunctions.Count}");
        md.AppendLine($"- Reachable declared natives ({startup.ReachableNatives.Count}): {string.Join(", ", startup.ReachableNatives.OrderBy(n => n))}");
        md.AppendLine($"- Reachable standard natives seen in the call graph: {standardCalls.Length}");
        md.AppendLine();
        md.AppendLine("Static reachability follows direct calls/function references from `main` plus global initializers.");
        md.AppendLine("Callback names passed as strings (`ExecuteFunc`, trigger conditions) are not followed, so later gameplay natives may be missing here.");
        md.AppendLine();
        md.AppendLine("## Status");
        md.AppendLine();
        md.AppendLine($"Implemented native bindings: {registry.Natives.Count} (standard + YDWE).");
        md.AppendLine($"Extension natives implemented: {natives.Count(n => n.Status == "implemented")}/{natives.Count}; missing: {natives.Count(n => n.Status == "missing")}.");
        md.AppendLine("The machine-readable status file is the source for Phase B/C progress.");
        md.AppendLine("Standard natives reachable from init that are implemented: " + string.Join(", ", initStandardNatives.Where(n => registry.Natives.ContainsKey(n))));
        File.WriteAllText(mdPath, md.ToString());

        Console.WriteLine($"wrote {mdPath}");
        Console.WriteLine($"wrote {jsonPath}");
        Console.WriteLine($"declared={natives.Count} called={called.Length} initReachable={natives.Count(n => n.InitReachable)} standard={standardCalls.Length} unknown={unknown.Length}");
        Console.WriteLine($"init standard natives={initStandardNatives.Length} blizzard helpers={initBlizzardFunctions.Length} unknown calls={initUnknownCalls.Length}");
        Console.WriteLine("init standard sample: " + string.Join(", ", initStandardNatives.Take(60)));
        if (initUnknownCalls.Length > 0)
        {
            Console.WriteLine("unknown init calls: " + string.Join(", ", initUnknownCalls.Take(40)));
        }
        return 0;
    }

    private static string ReadScript(AssetStore store, string path)
    {
        try
        {
            return Encoding.UTF8.GetString(store.ReadAllBytes(path, out _));
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
    }

    private static string Classify(
        string name,
        string provider,
        JassProgramModel common,
        JassProgramModel blizzard,
        JassProgramModel map)
    {
        if (common.Natives.ContainsKey(name))
        {
            return "standard";
        }

        if (blizzard.Functions.ContainsKey(name) || map.Functions.ContainsKey(name))
        {
            return "helper";
        }

        return provider switch
        {
            NativeProviders.DzApi => "ydwe-dzapi",
            NativeProviders.Japi => "ydwe-japi",
            NativeProviders.Extra => "ydwe-extra",
            _ => "unknown",
        };
    }

    private static string FormatSignature(JassNativeModel native)
    {
        var parameters = native.Parameters.Count == 0
            ? "nothing"
            : string.Join(", ", native.Parameters.Select(p => $"{p.Type} {p.Name}"));
        return $"takes {parameters} returns {native.ReturnType}";
    }

    private static Dictionary<string, (int Line, string Section)> NativeLocations(string source)
    {
        var result = new Dictionary<string, (int, string)>(StringComparer.Ordinal);
        var section = "global";
        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("//globals from ", StringComparison.Ordinal))
            {
                section = line["//globals from ".Length..].TrimEnd(':');
            }
            else if (line.StartsWith("native ", StringComparison.Ordinal) ||
                     line.StartsWith("constant native ", StringComparison.Ordinal))
            {
                var rest = line.StartsWith("constant native ", StringComparison.Ordinal)
                    ? line["constant native ".Length..]
                    : line["native ".Length..];
                var name = rest.Split(' ')[0];
                result[name] = (i + 1, section);
            }
        }

        return result;
    }

    private static void WriteJson(string path, object value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));
    }

    private sealed record NativeAudit(
        string Name,
        string Signature,
        string ReturnType,
        string Provider,
        string Category,
        int CallCount,
        bool Called,
        bool InitReachable,
        string Section,
        int Line,
        string Status);
}
