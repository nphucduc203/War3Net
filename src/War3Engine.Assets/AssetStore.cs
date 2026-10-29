using War3Net.IO.Mpq;

namespace War3Engine.Assets;

/// <summary>
/// Resolves Warcraft III assets across the installed game archives and an optional map.
/// Archives are opened in game-priority order; later entries override earlier ones.
/// </summary>
public sealed class AssetStore : IDisposable
{
    // Low to high priority; map assets are resolved before any game archive.
    private static readonly string[] GameArchiveNames =
    {
        "War3.mpq",
        "War3x.mpq",
        "War3Patch.mpq",
        "patch.mpq",
        "War3xlocal.mpq",
    };

    public static readonly string[] KnownMapFiles =
    {
        "war3map.w3i",
        "war3map.w3e",
        "war3map.w3v",
        "war3map.w3s",
        "war3map.w3u",
        "war3map.w3b",
        "war3map.w3d",
        "war3map.w3a",
        "war3map.w3t",
        "war3map.w3q",
        "war3map.w3h",
        "war3map.doo",
        "war3mapUnits.doo",
        "war3map.wpm",
        "war3map.shd",
        "war3map.wts",
        "war3map.j",
        "war3map.lua",
        "war3map.wtg",
        "war3map.wct",
        "war3map.imp",
        "war3mapMisc.txt",
        "war3mapSkin.txt",
        "war3mapExtra.txt",
    };

    private readonly List<MpqArchive> _archives = new();
    private readonly List<string> _archiveNames = new();
    private MpqArchive? _mapArchive;
    private string? _mapDirectory;

    public AssetStore(string installDirectory)
    {
        InstallDirectory = installDirectory;
        if (!Directory.Exists(installDirectory))
        {
            throw new DirectoryNotFoundException($"Warcraft III directory not found: {installDirectory}");
        }

        foreach (var name in GameArchiveNames)
        {
            var path = Path.Combine(installDirectory, name);
            if (!File.Exists(path))
            {
                continue;
            }

            _archives.Add(MpqArchive.Open(path));
            _archiveNames.Add(name);
        }

        if (_archives.Count == 0)
        {
            throw new FileNotFoundException($"No MPQ archives found in {installDirectory}");
        }
    }

    public string InstallDirectory { get; }

    public string? MountedMap { get; private set; }

    public IReadOnlyList<string> ArchiveNames => _archiveNames;

    public void MountMap(string mapPath)
    {
        _mapArchive?.Dispose();
        _mapArchive = null;
        _mapDirectory = null;

        if (Directory.Exists(mapPath))
        {
            _mapDirectory = mapPath;
            MountedMap = mapPath;
            return;
        }

        var bytes = File.ReadAllBytes(mapPath);
        if (bytes.Length >= 4 && bytes[0] == 'H' && bytes[1] == 'M' && bytes[2] == '3' && bytes[3] == 'W')
        {
            // W3X files carry a 512-byte HM3W header before the MPQ archive.
            bytes = bytes[512..];
        }

        var stream = new MemoryStream(bytes, writable: false);
        _mapArchive = new MpqArchive(stream);
        MountedMap = mapPath;
    }

    public bool TryOpen(string assetPath, out Stream stream) => TryOpen(assetPath, out stream, out _);

    public bool Exists(string assetPath) => TryOpen(assetPath, out var stream) && Dispose(stream);

    public byte[] ReadAllBytes(string assetPath, out string source)
    {
        var candidates = new List<string> { Normalize(assetPath) };
        var extension = Path.GetExtension(assetPath).ToLowerInvariant();
        if (extension is ".tga" or ".dds" or ".png" or ".jpg")
        {
            candidates.Add(Path.ChangeExtension(Normalize(assetPath), ".blp"));
        }

        foreach (var candidate in candidates)
        {
            if (TryOpen(candidate, out var stream, out source))
            {
                using (stream)
                {
                    using var memory = new MemoryStream();
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            }
        }

        source = "unresolved";
        throw new FileNotFoundException($"Asset not found: {assetPath}");
    }

    public IEnumerable<string> ListFiles()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var archive in _archives.Prepend(_mapArchive).OfType<MpqArchive>())
        {
            if (!archive.TryOpenFile("(listfile)", out var stream) || !stream.CanRead)
            {
                stream?.Dispose();
                continue;
            }

            using (stream)
            using (var reader = new StreamReader(stream))
            {
                while (reader.ReadLine() is { } line)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length > 0)
                    {
                        names.Add(trimmed);
                    }
                }
            }
        }

        if (_mapDirectory is not null)
        {
            foreach (var file in Directory.EnumerateFiles(_mapDirectory, "*", SearchOption.AllDirectories))
            {
                names.Add(Path.GetRelativePath(_mapDirectory, file));
            }
        }
        else if (_mapArchive is not null)
        {
            foreach (var known in KnownMapFiles)
            {
                if (_mapArchive.FileExists(known))
                {
                    names.Add(known);
                }
            }
        }

        return names;
    }

    public void Dispose()
    {
        foreach (var archive in _archives)
        {
            archive.Dispose();
        }

        _archives.Clear();
        _mapArchive?.Dispose();
    }

    private bool TryOpen(string assetPath, out Stream stream, out string source)
    {
        var normalized = Normalize(assetPath);

        if (_mapDirectory is not null)
        {
            var filePath = Path.Combine(_mapDirectory, normalized.Replace('\\', Path.DirectorySeparatorChar));
            if (File.Exists(filePath))
            {
                stream = File.OpenRead(filePath);
                source = "map-dir";
                return true;
            }
        }

        if (_mapArchive is not null && _mapArchive.TryOpenFile(normalized, out var mapStream))
        {
            stream = mapStream;
            source = "map";
            return true;
        }

        var diskPath = Path.Combine(InstallDirectory, normalized.Replace('\\', Path.DirectorySeparatorChar));
        if (File.Exists(diskPath))
        {
            stream = File.OpenRead(diskPath);
            source = "game-dir";
            return true;
        }

        for (var i = _archives.Count - 1; i >= 0; i--)
        {
            if (_archives[i].TryOpenFile(normalized, out var archiveStream))
            {
                stream = archiveStream;
                source = _archiveNames[i];
                return true;
            }
        }

        stream = Stream.Null;
        source = "unresolved";
        return false;
    }

    private static string Normalize(string assetPath) =>
        assetPath.Replace('/', '\\').TrimStart('\\');

    private static bool Dispose(Stream stream)
    {
        stream.Dispose();
        return true;
    }
}
