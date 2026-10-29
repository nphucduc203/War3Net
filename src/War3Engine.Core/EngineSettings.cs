using System.Text.Json;

namespace War3Engine.Core;

public sealed class EngineSettings
{
    public string WarcraftPath { get; set; } = string.Empty;

    public string MapPath { get; set; } = string.Empty;

    public static EngineSettings Load()
    {
        var settings = new EngineSettings();
        var configPath = FindConfigFile();
        if (configPath is not null)
        {
            settings = JsonSerializer.Deserialize<EngineSettings>(
                File.ReadAllText(configPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? settings;
        }

        settings.WarcraftPath = Environment.GetEnvironmentVariable("WAR3ENGINE_WARCRAFT_PATH") ?? settings.WarcraftPath;
        settings.MapPath = Environment.GetEnvironmentVariable("WAR3ENGINE_MAP_PATH") ?? settings.MapPath;
        return settings;
    }

    private static string? FindConfigFile()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "app", "settings.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
