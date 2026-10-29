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

    /// <summary>W3X files place a 512-byte HM3W header before the MPQ archive.</summary>
    public static MapPackage Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 4 && bytes[0] == 'H' && bytes[1] == 'M' && bytes[2] == '3' && bytes[3] == 'W')
        {
            bytes = bytes[512..];
        }

        using var stream = new MemoryStream(bytes, writable: false);
        var map = Map.Open(stream, MapFiles.All);
        return new MapPackage(map, path);
    }
}
