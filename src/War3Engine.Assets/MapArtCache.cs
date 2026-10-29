using War3Net.Build.Object;
using War3Net.Common.Extensions;

namespace War3Engine.Assets;

public sealed record UnitArtModel(string ModelPath, float Scale);

/// <summary>Resolves runtime unit ids (including custom object-data units) to model files.</summary>
public sealed class MapArtCache
{
    private readonly AssetStore _store;
    private readonly GameDataTables _tables;
    private readonly Dictionary<int, (string? Model, int BaseId)> _overrides;
    private readonly War3Net.Build.Environment.MapEnvironment? _environment;

    private MapArtCache(AssetStore store, GameDataTables tables, Dictionary<int, (string?, int)> overrides, War3Net.Build.Environment.MapEnvironment? environment)
    {
        _store = store;
        _tables = tables;
        _overrides = overrides;
        _environment = environment;
    }

    public static MapArtCache Load(AssetStore store, string mapPath)
    {
        var package = MapPackage.Load(mapPath);
        var tables = GameDataTables.Load(store);
        var overrides = new Dictionary<int, (string?, int)>();
        foreach (var modification in package.Map.UnitObjectData?.NewUnits ?? new List<SimpleObjectModification>())
        {
            var file = modification.Modifications.FirstOrDefault(m => m.Id.ToRawcode() == "umdl")?.Value as string;
            overrides[modification.NewId] = (string.IsNullOrWhiteSpace(file) ? null : file, modification.OldId);
        }

        foreach (var modification in package.Map.UnitObjectData?.BaseUnits ?? new List<SimpleObjectModification>())
        {
            var file = modification.Modifications.FirstOrDefault(m => m.Id.ToRawcode() == "umdl")?.Value as string;
            overrides[modification.OldId] = (string.IsNullOrWhiteSpace(file) ? null : file, modification.OldId);
        }

        return new MapArtCache(store, tables, overrides, package.Map.Environment);
    }

    public int OverrideCount => _overrides.Count;

    public bool TryGetOverride(int typeId, out string? model, out int baseId)
    {
        if (_overrides.TryGetValue(typeId, out var value))
        {
            model = value.Model;
            baseId = value.BaseId;
            return true;
        }

        model = null;
        baseId = 0;
        return false;
    }

    /// <summary>Terrain surface height at a WC3 world position (W3E height + cliff layer - 256).</summary>
    public float SampleHeight(float x, float y)
    {
        if (_environment is null)
        {
            return 0f;
        }

        var columns = (int)_environment.Width + 1;
        var rows = (int)_environment.Height + 1;
        var fx = (x - _environment.Left) / 128f;
        var fy = (y - _environment.Bottom) / 128f;
        var x0 = Math.Clamp((int)MathF.Floor(fx), 0, columns - 1);
        var y0 = Math.Clamp((int)MathF.Floor(fy), 0, rows - 1);
        var x1 = Math.Min(x0 + 1, columns - 1);
        var y1 = Math.Min(y0 + 1, rows - 1);
        var tx = Math.Clamp(fx - MathF.Floor(fx), 0f, 1f);
        var ty = Math.Clamp(fy - MathF.Floor(fy), 0f, 1f);

        float H(int gx, int gy)
        {
            var tile = _environment.TerrainTiles[gx + (gy * columns)];
            return (tile.Height * 128f) + (tile.CliffLevel * 128f) - 256f;
        }

        var top = H(x0, y0) + ((H(x1, y0) - H(x0, y0)) * tx);
        var bottom = H(x0, y1) + ((H(x1, y1) - H(x0, y1)) * tx);
        return top + ((bottom - top) * ty);
    }

    public UnitArtModel? ResolveUnit(int typeId, int variation = 0)
    {
        var art = _tables.FindUnit(typeId);
        string? file = null;
        if (_overrides.TryGetValue(typeId, out var over) && over.Model is { } custom)
        {
            file = custom;
        }
        else if (_overrides.TryGetValue(typeId, out over) && over.BaseId != typeId)
        {
            art = _tables.FindUnit(over.BaseId) ?? art;
        }

        file ??= art?.File;
        if (file is null)
        {
            return null;
        }

        var modelPath = ResolveModelPath(file, variation);
        if (modelPath is null)
        {
            return null;
        }

        return new UnitArtModel(modelPath, art?.ModelScale ?? 1f);
    }

    private string? ResolveModelPath(string file, int variation)
    {
        var candidates = new List<string>();
        var lower = file.ToLowerInvariant();
        if (lower.EndsWith(".mdl", StringComparison.Ordinal))
        {
            candidates.Add(file[..^4] + ".mdx");
        }
        else if (lower.EndsWith(".mdx", StringComparison.Ordinal))
        {
            candidates.Add(file);
        }
        else
        {
            if (variation > 0)
            {
                candidates.Add($"{file}{variation}.mdx");
            }

            candidates.Add(file + ".mdx");
        }

        return candidates.FirstOrDefault(_store.Exists);
    }
}
