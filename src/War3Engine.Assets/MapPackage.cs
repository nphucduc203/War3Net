using War3Net.Build;

namespace War3Engine.Assets;

/// <summary>Typed view of a W3X map: info, environment, pathing, doodads, units, object data.</summary>
public sealed class MapPackage
{
    private MapPackage(Map map, string path)
    {
        Map = map;
        Path = path;
    }

    public Map Map { get; }

    public string Path { get; }

    public List<string> Diagnostics { get; } = new();

    /// <summary>W3X files place a 512-byte HM3W header before the MPQ archive.</summary>
    public static MapPackage Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 4 && bytes[0] == 'H' && bytes[1] == 'M' && bytes[2] == '3' && bytes[3] == 'W')
        {
            bytes = bytes[512..];
        }

        // YDWE maps can carry non-standard values (e.g. weather id "0000") that strict
        // enum parsing rejects. Retry without the offending sections instead of failing the map.
        var package = new MapPackage(null!, path);
        foreach (var files in new[] { MapFiles.All, MapFiles.All & ~MapFiles.Info, MapFiles.Environment | MapFiles.Doodads | MapFiles.Units | MapFiles.PathingMap | MapFiles.ImportedFiles | MapFiles.UnitObjectData | MapFiles.DoodadObjectData | MapFiles.DestructableObjectData })
        {
            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                package = new MapPackage(Map.Open(stream, files), path);
                if (files != MapFiles.All)
                {
                    package.Diagnostics.Add($"map parsed with reduced sections ({files})");
                }

                return package;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                package.Diagnostics.Add($"section set {files} failed: {ex.Message}");
            }
        }

        throw new InvalidDataException($"could not parse map {path}: {string.Join("; ", package.Diagnostics)}");
    }
}
