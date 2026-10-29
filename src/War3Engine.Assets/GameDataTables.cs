using War3Net.IO.Slk;

namespace War3Engine.Assets;

public sealed record UnitArt(string File, float ModelScale, int TeamColor);

public sealed record DoodadArt(string File, float DefaultScale, float MinScale, float MaxScale, bool FixedRotation, string Class)
{
    public int ReplaceableId { get; init; }

    public string? ReplaceableTexture { get; init; }
};

/// <summary>
/// Game data tables (SLK) that map placement ids to model files:
/// UnitUI.slk (unit art), Doodads.slk (doodad art), TerrainArt\Terrain.slk (tile textures).
/// </summary>
public sealed class GameDataTables
{
    private readonly SylkTable _units;
    private readonly SylkTable _doodads;
    private readonly SylkTable _destructables;
    private readonly SylkTable _terrain;
    private readonly SylkTable _water;
    private readonly Dictionary<int, int> _unitRows;
    private readonly Dictionary<int, int> _doodadRows;
    private readonly Dictionary<int, int> _destructableRows;
    private readonly Dictionary<int, int> _terrainRows;
    private readonly Dictionary<string, int> _waterRows;
    private readonly Dictionary<string, int> _waterCols;
    private readonly Dictionary<string, int> _unitCols;
    private readonly Dictionary<string, int> _doodadCols;
    private readonly Dictionary<string, int> _destructableCols;
    private readonly Dictionary<string, int> _terrainCols;

    private GameDataTables(SylkTable units, SylkTable doodads, SylkTable destructables, SylkTable terrain, SylkTable water)
    {
        _water = water;
        _waterCols = ColumnMap(water);
        _waterRows = StringRowMap(water, _waterCols["waterID"]);
        _units = units;
        _doodads = doodads;
        _destructables = destructables;
        _terrain = terrain;
        _unitCols = ColumnMap(units);
        _doodadCols = ColumnMap(doodads);
        _destructableCols = ColumnMap(destructables);
        _terrainCols = ColumnMap(terrain);
        _unitRows = RowMap(units, _unitCols["unitUIID"]);
        _doodadRows = RowMap(doodads, _doodadCols["doodID"]);
        _destructableRows = RowMap(destructables, _destructableCols["DestructableID"]);
        _terrainRows = RowMap(terrain, _terrainCols["tileID"]);
    }

    public static GameDataTables Load(AssetStore store)
    {
        return new GameDataTables(
            LoadTable(store, @"Units\UnitUI.slk"),
            LoadTable(store, @"Doodads\Doodads.slk"),
            LoadTable(store, @"Units\DestructableData.slk"),
            LoadTable(store, @"TerrainArt\Terrain.slk"),
            LoadTable(store, @"TerrainArt\Water.slk"));
    }

    public UnitArt? FindUnit(int typeId)
    {
        if (!_unitRows.TryGetValue(typeId, out var row))
        {
            return null;
        }

        var file = GetString(_units, _unitCols, "file", row);
        if (string.IsNullOrWhiteSpace(file))
        {
            return null;
        }

        var scale = GetFloat(_units, _unitCols, "modelScale", row, 1f);
        var teamColor = (int)GetFloat(_units, _unitCols, "teamColor", row, 0f);
        return new UnitArt(file, scale, teamColor);
    }

    public DoodadArt? FindDoodad(int typeId)
    {
        if (_doodadRows.TryGetValue(typeId, out var row))
        {
            var file = GetString(_doodads, _doodadCols, "file", row);
            if (!string.IsNullOrWhiteSpace(file))
            {
                return new DoodadArt(
                    file,
                    GetFloat(_doodads, _doodadCols, "defScale", row, 1f),
                    GetFloat(_doodads, _doodadCols, "minScale", row, 1f),
                    GetFloat(_doodads, _doodadCols, "maxScale", row, 1f),
                    GetFloat(_doodads, _doodadCols, "fixedRot", row, 0f) != 0f,
                    GetString(_doodads, _doodadCols, "doodClass", row));
            }
        }

        // Map doodads may reference destructibles (trees, barricades, pathing blockers).
        if (_destructableRows.TryGetValue(typeId, out var destRow))
        {
            var file = GetString(_destructables, _destructableCols, "file", destRow);
            if (!string.IsNullOrWhiteSpace(file))
            {
                var texFile = GetString(_destructables, _destructableCols, "texFile", destRow);
                return new DoodadArt(
                    file,
                    1f,
                    GetFloat(_destructables, _destructableCols, "minScale", destRow, 1f),
                    GetFloat(_destructables, _destructableCols, "maxScale", destRow, 1f),
                    GetFloat(_destructables, _destructableCols, "fixedRot", destRow, 0f) != 0f,
                    GetString(_destructables, _destructableCols, "category", destRow))
                {
                    ReplaceableId = (int)GetFloat(_destructables, _destructableCols, "texID", destRow, 0f),
                    ReplaceableTexture = string.IsNullOrWhiteSpace(texFile) || texFile == "-" ? null : texFile + ".blp",
                };
            }
        }

        return null;
    }

    /// <summary>Resolves a W3E terrain type rawcode (e.g. 'Ydrt') to its texture path.</summary>
    public string? FindTerrainTexture(int terrainType)
    {
        if (!_terrainRows.TryGetValue(terrainType, out var row))
        {
            return null;
        }

        var dir = GetString(_terrain, _terrainCols, "dir", row);
        var file = GetString(_terrain, _terrainCols, "file", row);
        return string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(file)
            ? null
            : $"{dir}\\{file}.blp";
    }

    private static SylkTable LoadTable(AssetStore store, string path)
    {
        var bytes = store.ReadAllBytes(path, out _);
        using var stream = new MemoryStream(bytes, writable: false);
        return new SylkParser().Parse(stream, leaveOpen: false);
    }

    private static Dictionary<string, int> ColumnMap(SylkTable table)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 0; column <= table.Columns; column++)
        {
            var name = Convert.ToString(table[column, 0]);
            if (!string.IsNullOrEmpty(name))
            {
                map[name] = column;
            }
        }

        return map;
    }

    /// <summary>Water style id is four characters, e.g. "YSha" for the Cityscape tileset.</summary>
    public (float HeightTiles, string TextureBase)? FindWater(string tileset)
    {
        var id = tileset + "Sha";
        if (!_waterRows.TryGetValue(id, out var row))
        {
            return null;
        }

        var height = GetFloat(_water, _waterCols, "height", row, 0f);
        var tex = GetString(_water, _waterCols, "texFile", row);
        return (height, string.IsNullOrWhiteSpace(tex) || tex == "-" ? @"ReplaceableTextures\Water\Water" : tex);
    }

    private static Dictionary<string, int> StringRowMap(SylkTable table, int idColumn)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var row = 1; row <= table.Rows; row++)
        {
            var id = Convert.ToString(table[idColumn, row]);
            if (!string.IsNullOrEmpty(id))
            {
                map[id] = row;
            }
        }

        return map;
    }

    private static Dictionary<int, int> RowMap(SylkTable table, int idColumn)
    {
        var map = new Dictionary<int, int>();
        for (var row = 1; row <= table.Rows; row++)
        {
            if (table[idColumn, row] is { } id && TryGetRawcode(id.ToString(), out var rawcode))
            {
                map[rawcode] = row;
            }
        }

        return map;
    }

    private static bool TryGetRawcode(string? text, out int rawcode)
    {
        rawcode = 0;
        if (string.IsNullOrEmpty(text) || text.Length != 4)
        {
            return false;
        }

        // Rawcodes are little-endian: the first character is the low byte.
        rawcode = text[0] | (text[1] << 8) | (text[2] << 16) | (text[3] << 24);
        return true;
    }

    private static string GetString(SylkTable table, Dictionary<string, int> columns, string name, int row) =>
        columns.TryGetValue(name, out var column) ? Convert.ToString(table[column, row]) ?? string.Empty : string.Empty;

    private static float GetFloat(SylkTable table, Dictionary<string, int> columns, string name, int row, float fallback)
    {
        if (!columns.TryGetValue(name, out var column))
        {
            return fallback;
        }

        var value = table[column, row];
        return value switch
        {
            float f => f,
            int i => i,
            string s when float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => fallback,
        };
    }
}
