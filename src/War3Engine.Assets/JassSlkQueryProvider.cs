using War3Net.Build.Object;
using War3Net.Common.Extensions;
using War3Net.IO.Slk;

namespace War3Engine.Assets;

/// <summary>
/// jass.slk provider for the fields used by the target map: item.class/prio/Name/Ubertip and
/// unit.weapsOn. Precedence: map object data (w3t/w3u) over base SLKs, with TRIGSTR_ values
/// resolved from war3map.wts. Fields outside the verified set return nil.
/// </summary>
public sealed class JassSlkQueryProvider
{
    private readonly SylkTable _items;
    private readonly SylkTable _weapons;
    private readonly Dictionary<string, int> _itemCols;
    private readonly Dictionary<string, int> _weaponCols;
    private readonly Dictionary<int, int> _itemRows;
    private readonly Dictionary<int, int> _weaponRows;
    private readonly Dictionary<string, (string? Name, string? Ubertip)> _itemStrings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Dictionary<string, object>> _itemOverrides = new();
    private readonly Dictionary<int, int> _unitBaseIds = new();
    private readonly Dictionary<int, int> _itemBaseIds = new();
    private readonly Dictionary<int, string> _wts = new();
    private readonly List<string> _missingFields = new();
    private readonly List<string> _unresolvedTriggerStrings = new();

    private JassSlkQueryProvider(
        SylkTable items,
        SylkTable weapons,
        Dictionary<string, (string?, string?)> strings,
        Dictionary<int, Dictionary<string, object>> itemOverrides,
        Dictionary<int, int> unitBaseIds,
        Dictionary<int, int> itemBaseIds,
        Dictionary<int, string> wts)
    {
        _items = items;
        _weapons = weapons;
        _itemCols = Columns(items);
        _weaponCols = Columns(weapons);
        _itemRows = Rows(items, _itemCols["itemID"]);
        _weaponRows = Rows(weapons, _weaponCols["unitWeapID"]);
        foreach (var (id, value) in strings)
        {
            _itemStrings[id] = value;
        }

        _itemOverrides = itemOverrides;
        _unitBaseIds = unitBaseIds;
        _itemBaseIds = itemBaseIds;
        _wts = wts;
    }

    public int CustomFieldQueries { get; private set; }

    public int CustomFieldHits { get; private set; }

    public IReadOnlyList<string> MissingFields => _missingFields;

    public IReadOnlyList<string> UnresolvedTriggerStrings => _unresolvedTriggerStrings;

    public int WtsEntries => _wts.Count;

    public string? DebugWts(int index) => _wts.TryGetValue(index, out var value) ? value : null;

    public string DebugItemFields(int objectId) =>
        _itemOverrides.TryGetValue(objectId, out var fields)
            ? string.Join(",", fields.Select(p => $"{p.Key}={(p.Value is string s ? s.Length + ":'" + s[..Math.Min(20, s.Length)] + "'" : p.Value)}"))
            : "<none>";

    public static JassSlkQueryProvider Load(AssetStore store, string? mapPath = null)
    {
        var items = LoadTable(store, @"Units\ItemData.slk");
        var weapons = LoadTable(store, @"Units\UnitWeapons.slk");
        var strings = ParseItemStrings(store);
        var itemOverrides = new Dictionary<int, Dictionary<string, object>>();
        var itemBaseIds = new Dictionary<int, int>();
        var unitBaseIds = new Dictionary<int, int>();
        var wts = new Dictionary<int, string>();

        if (!string.IsNullOrEmpty(mapPath) && File.Exists(mapPath))
        {
            store.MountMap(mapPath);
            var package = MapPackage.Load(mapPath);
            var unitData = package.Map.UnitObjectData;
            if (unitData is not null)
            {
                foreach (var modification in unitData.NewUnits)
                {
                    unitBaseIds[modification.NewId] = modification.OldId;
                }

                foreach (var modification in unitData.BaseUnits)
                {
                    unitBaseIds[modification.OldId] = modification.OldId;
                }
            }

            var itemData = package.Map.ItemObjectData;
            if (itemData is not null)
            {
                foreach (var modification in itemData.NewItems.Concat(itemData.BaseItems))
                {
                    var id = modification.NewId != 0 ? modification.NewId : modification.OldId;
                    itemBaseIds[id] = modification.OldId;
                    var fields = itemOverrides.TryGetValue(id, out var existing)
                        ? existing
                        : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (var field in modification.Modifications)
                    {
                        fields[field.Id.ToRawcode()] = field.Value;
                    }

                    itemOverrides[id] = fields;
                }
            }

            wts = ParseWts(store);
        }

        return new JassSlkQueryProvider(items, weapons, strings, itemOverrides, unitBaseIds, itemBaseIds, wts);
    }

    public object? Query(string kind, int objectId, string field)
    {
        if (kind == "item")
        {
            return QueryItem(objectId, field);
        }

        if (kind == "unit" && field == "weapsOn")
        {
            var baseId = _unitBaseIds.TryGetValue(objectId, out var baseUnit) ? baseUnit : objectId;
            var value = GetNumber(_weapons, _weaponCols, "weapsOn", _weaponRows, baseId);
            if (value is null && baseId != objectId)
            {
                value = GetNumber(_weapons, _weaponCols, "weapsOn", _weaponRows, objectId);
            }

            return value ?? 0;
        }

        return null;
    }

    private object? QueryItem(int objectId, string field)
    {
        // Object-data naming: utub = item "Ubertip" (extended tooltip), utip = "Tip".
        var rawField = field switch
        {
            "class" => "icla",
            "prio" => "ipri",
            "Name" => "unam",
            "Ubertip" => "utub",
            _ => null,
        };
        if (rawField is null)
        {
            return null;
        }

        var custom = !_itemRows.ContainsKey(objectId) || _itemOverrides.ContainsKey(objectId);
        if (custom)
        {
            CustomFieldQueries++;
        }

        if (_itemOverrides.TryGetValue(objectId, out var fields))
        {
            foreach (var candidate in field == "Ubertip" ? new[] { "utub", "utip" } : new[] { rawField })
            {
                if (fields.TryGetValue(candidate, out var overrideValue))
                {
                    var normalized = NormalizeItemValue(candidate, overrideValue);
                    if (normalized is not null)
                    {
                        CustomFieldHits++;
                        return normalized;
                    }
                }
            }
        }

        var baseId = _itemBaseIds.TryGetValue(objectId, out var inherited) ? inherited : objectId;
        object? baseValue = field switch
        {
            "class" => GetString(_items, _itemCols, "class", _itemRows, baseId),
            "prio" => GetNumber(_items, _itemCols, "prio", _itemRows, baseId),
            "Name" => _itemStrings.TryGetValue(baseId.ToRawcode(), out var strings) ? strings.Name : null,
            "Ubertip" => _itemStrings.TryGetValue(baseId.ToRawcode(), out var strings) ? strings.Ubertip : null,
            _ => null,
        };

        if (baseValue is null && custom)
        {
            _missingFields.Add($"item[{objectId.ToRawcode()}].{field}");
        }

        return baseValue ?? field switch
        {
            "class" => string.Empty,
            "prio" => 0,
            _ => string.Empty,
        };
    }

    private object? NormalizeItemValue(string rawField, object value)
    {
        switch (value)
        {
            case string text:
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (text.StartsWith("TRIGSTR_", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(text["TRIGSTR_".Length..], out var index) && _wts.TryGetValue(index, out var resolved))
                    {
                        return resolved;
                    }

                    if (!_unresolvedTriggerStrings.Contains(text))
                    {
                        _unresolvedTriggerStrings.Add(text);
                    }
                }

                return text;
            case int i:
                return i;
            case float f:
                return rawField is "ipri" ? (int)f : f;
            case bool b:
                return b;
            default:
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static SylkTable LoadTable(AssetStore store, string path)
    {
        var bytes = store.ReadAllBytes(path, out _);
        using var stream = new MemoryStream(bytes, writable: false);
        return new SylkParser().Parse(stream, leaveOpen: false);
    }

    private static Dictionary<string, int> Columns(SylkTable table)
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

    private static Dictionary<int, int> Rows(SylkTable table, int idColumn)
    {
        var map = new Dictionary<int, int>();
        for (var row = 1; row <= table.Rows; row++)
        {
            var id = Convert.ToString(table[idColumn, row]);
            if (!string.IsNullOrEmpty(id) && id.Length == 4)
            {
                map[Rawcode(id)] = row;
            }
        }

        return map;
    }

    private static string? GetString(
        SylkTable table,
        Dictionary<string, int> columns,
        string name,
        Dictionary<int, int> rows,
        int objectId)
    {
        if (!columns.TryGetValue(name, out var column) || !rows.TryGetValue(objectId, out var row))
        {
            return null;
        }

        return Convert.ToString(table[column, row]);
    }

    private static int? GetNumber(
        SylkTable table,
        Dictionary<string, int> columns,
        string name,
        Dictionary<int, int> rows,
        int objectId)
    {
        if (!columns.TryGetValue(name, out var column) || !rows.TryGetValue(objectId, out var row))
        {
            return null;
        }

        return table[column, row] switch
        {
            int i => i,
            float f => (int)f,
            string s when int.TryParse(s, out var parsed) => parsed,
            string s when float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) => (int)f,
            _ => null,
        };
    }

    private static Dictionary<string, (string?, string?)> ParseItemStrings(AssetStore store)
    {
        var result = new Dictionary<string, (string?, string?)>(StringComparer.OrdinalIgnoreCase);
        string text;
        try
        {
            text = System.Text.Encoding.UTF8.GetString(store.ReadAllBytes(@"Units\ItemStrings.txt", out _));
        }
        catch (FileNotFoundException)
        {
            return result;
        }

        string? section = null;
        string? name = null;
        string? ubertip = null;
        void Flush()
        {
            if (section is not null)
            {
                result[section] = (name, ubertip);
            }
        }

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                Flush();
                section = line[1..^1];
                name = null;
                ubertip = null;
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var key = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim().Trim('"');
            switch (key)
            {
                case "Name":
                    name = value;
                    break;
                case "Ubertip":
                    ubertip = value;
                    break;
            }
        }

        Flush();
        return result;
    }

    private static Dictionary<int, string> ParseWts(AssetStore store)
    {
        var result = new Dictionary<int, string>();
        string text;
        try
        {
            var wtsBytes = store.ReadAllBytes("war3map.wts", out _);
            text = System.Text.Encoding.UTF8.GetString(wtsBytes).TrimStart('﻿').TrimStart('ï', '»', '¿');
        }
        catch (FileNotFoundException)
        {
            return result;
        }

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("STRING ", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(line["STRING ".Length..].Trim(), out var id))
            {
                continue;
            }

            while (i < lines.Length && lines[i].Trim() != "{")
            {
                i++;
            }

            var value = new System.Text.StringBuilder();
            i++;
            while (i < lines.Length && lines[i].Trim() != "}")
            {
                if (value.Length > 0)
                {
                    value.Append('\n');
                }

                value.Append(lines[i].TrimEnd('\r'));
                i++;
            }

            result[id] = value.ToString();
        }

        return result;
    }

    private static int Rawcode(string id) =>
        id[0] | (id[1] << 8) | (id[2] << 16) | (id[3] << 24);
}
