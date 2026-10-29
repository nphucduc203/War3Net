using System.Diagnostics;

namespace War3Engine.Compat;

/// <summary>Confirmed components of the local installation. Nothing is inferred from file names alone.</summary>
public sealed record DetectedComponent(string Name, string Kind, string Path, string? Version, string Evidence);

public static class YdweInstallation
{
    public static string? GameVersion(string gamePath)
    {
        var exe = Path.Combine(gamePath, "War3.exe");
        return File.Exists(exe) ? FileVersion(exe) : null;
    }

    public static IReadOnlyList<DetectedComponent> Detect(string gamePath)
    {
        var components = new List<DetectedComponent>();
        AddFile(components, gamePath, "War3.exe", "game-client", "Warcraft III executable version");
        AddFile(components, gamePath, "game.dll", "game-runtime", "game logic DLL version");
        AddFile(components, gamePath, "dz_w3_plugin.dll", "runtime-plugin", "DzAPI plugin exports Dz*/RequestExtra* natives");
        AddFile(components, gamePath, "dznep.dll", "runtime-plugin", "NEP platform plugin");
        AddFile(components, gamePath, "version.dll", "runtime-proxy", "DLL proxy loader");

        foreach (var variant in SafeDirectories(gamePath, "dz_w3_plugin*"))
        {
            AddFile(components, variant, "dz_w3_plugin.dll", "runtime-plugin-variant", "DzAPI plugin variant");
        }

        foreach (var editorRoot in SafeDirectories(gamePath, "*WorldEdit*"))
        {
            foreach (var config in SafeFiles(editorRoot, "YDWEConfig.exe", 4))
            {
                components.Add(new DetectedComponent(
                    "YDWEConfig",
                    "editor-runtime",
                    config,
                    FileVersion(config),
                    "YDWE editor/launcher (not required by the engine)"));
            }

            foreach (var kkwe in SafeDirectories(editorRoot, "KKWE*"))
            {
                components.Add(new DetectedComponent(
                    Path.GetFileName(kkwe),
                    "editor-plugin-set",
                    kkwe,
                    null,
                    "KKWE plugin set directory"));
            }
        }

        return components;
    }

    private static void AddFile(List<DetectedComponent> components, string directory, string file, string kind, string evidence)
    {
        var path = Path.Combine(directory, file);
        if (File.Exists(path))
        {
            components.Add(new DetectedComponent(file, kind, path, FileVersion(path), evidence));
        }
    }

    private static string? FileVersion(string path)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(path).ProductVersion ?? FileVersionInfo.GetVersionInfo(path).FileVersion;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> SafeDirectories(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateDirectories(root, pattern, SearchOption.TopDirectoryOnly).ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeFiles(string root, string file, int depth)
    {
        if (depth <= 0)
        {
            yield break;
        }

        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var entry in entries)
        {
            if (File.Exists(entry) && string.Equals(Path.GetFileName(entry), file, StringComparison.OrdinalIgnoreCase))
            {
                yield return entry;
            }
            else if (Directory.Exists(entry))
            {
                foreach (var nested in SafeFiles(entry, file, depth - 1))
                {
                    yield return nested;
                }
            }
        }
    }
}
