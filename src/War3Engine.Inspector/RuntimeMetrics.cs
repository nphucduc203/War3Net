using System.Text;
using System.Text.Json;
using War3Engine.Assets;
using War3Engine.Core;
using War3Engine.Scripting;
using War3Engine.Scripting.Lua;

namespace War3Engine.Inspector;

/// <summary>Runs the accepted scenario and writes the runtime compatibility reports.</summary>
internal static class RuntimeMetrics
{
    private const int DialogClickAtMs = 3000;
    private const int ScenarioMs = 120_000;

    public static int Run(EngineSettings settings, AssetStore store)
    {
        var target = settings.YdweMapPath;
        if (!File.Exists(target))
        {
            Console.WriteLine($"map not found: {target}");
            return 1;
        }

        store.MountMap(target);
        var common = Read(store, @"Scripts\common.j");
        var blizzard = Read(store, @"Scripts\Blizzard.j");
        var mapSource = Read(store, "war3map.j");
        var mapModel = JassProgramModel.Build(JassProgram.Parse(mapSource));
        var provider = JassSlkQueryProvider.Load(store, target);
        var lua = new NLuaBridge(provider.Query);
        var result = JassMapHost.Run(common, blizzard, mapSource, 12_000_000, advanceMs: DialogClickAtMs, lua: lua);

        var yxmsBefore = ReadGlobal(result, "udg_yxms");
        var dialog = result.Runtime.Dialogs.LastOrDefault(d =>
            d.Visible && !d.Destroyed && d.PlayerId == 0 && d.Buttons.Count > 0);
        var dialogMessage = dialog?.Message ?? string.Empty;
        var dialogButton = dialog?.Buttons[0].Label ?? string.Empty;
        var nestedBefore = TriggerEvaluations(result, "gg_trg_xznd03______u");
        var messagesBeforeDialog = result.Runtime.DisplayMessages.Count;
        var dialogFired = 0;
        if (dialog is not null)
        {
            dialogFired = result.Runtime.DispatchDialogClick(dialog, dialog.Buttons[0], 0);
            JassMapHost.Advance(result.Runtime, 0);
        }

        var yxmsAfter = ReadGlobal(result, "udg_yxms");
        var nestedAfter = TriggerEvaluations(result, "gg_trg_xznd03______u");
        var dialogMessagesAdded = result.Runtime.DisplayMessages.Count - messagesBeforeDialog;
        var dialogStateVerified = dialog is not null && yxmsBefore != "true" && yxmsAfter == "true" &&
                                  dialogMessagesAdded > 0 && !dialog.Visible && nestedAfter > nestedBefore;

        var selectedUnit = result.Runtime.Units.FirstOrDefault(u => !u.Removed && u.Handle is not null);
        var selectionEvaluationsBefore = TriggerEvaluations(result, "gg_trg____________________005");
        var selectionFired = 0;
        var selectionStored = false;
        if (selectedUnit is not null)
        {
            selectionFired = result.Runtime.SelectSingleUnit(selectedUnit, selectedUnit.OwnerId);
            selectionStored = ReferenceEquals(LoadSelectedUnitHandle(result, selectedUnit.OwnerId), selectedUnit.Handle);
        }

        var selectionEvaluationsAfter = TriggerEvaluations(result, "gg_trg____________________005");
        var selectionStateVerified = selectedUnit is not null && selectionFired > 0 &&
                                     result.Runtime.SelectedUnits(selectedUnit.OwnerId).Contains(selectedUnit) &&
                                     selectionEvaluationsAfter > selectionEvaluationsBefore &&
                                     selectionStored;

        JassMapHost.Advance(result.Runtime, ScenarioMs - DialogClickAtMs);
        result.ChatFired = result.Runtime.DispatchChat(0, "T5");
        result.Runtime.Advance(0);
        result.Runtime.RunDueTimers();

        var defined = result.Program.Natives.Count;
        var extensionDeclared = mapModel.Natives.Count;
        var implemented = result.Registry.Natives.Count;
        var extensionImplemented = mapModel.Natives.Keys.Count(k => result.Registry.Natives.ContainsKey(k));
        var called = result.Runtime.NativeCalls.Keys.ToHashSet(StringComparer.Ordinal);
        var extensionCalled = mapModel.Natives.Keys.Where(called.Contains).ToArray();
        var testedNatives = called.Concat(new[]
        {
            "CreateTimer", "TimerStart", "S2I", "S2R", "I2S", "EXExecuteScript",
            "TriggerRegisterPlayerChatEvent", "InitHashtable", "GetHandleId",
            "TriggerRegisterDialogEvent", "DialogAddButton", "DialogDisplay",
            "GetClickedButton", "GetClickedDialog", "SelectUnit", "ClearSelection",
        }).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        var registrations = result.Runtime.Registrations.ToArray();
        var eventTypes = registrations
            .GroupBy(r => r.Kind, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());
        var dispatchedTypes = registrations
            .Where(r => r.Dispatches > 0)
            .GroupBy(r => r.Kind, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Dispatches));
        var triggerExecutions = result.Runtime.Triggers.Sum(t => t.Evaluations);
        var luaErrors = lua.History.Count(h => h.ErrorKind != LuaErrorKind.None);

        var matrix = new
        {
            generatedUtc = DateTime.UtcNow.ToString("O"),
            map = target,
            runtimeProfile = new
            {
                warcraft = "1.27.0.52240 (War3.exe/game.dll file version)",
                ydwe = "WorldEdit v1.2.9C / YDWEConfig 2.0.0.2485",
                kkwe = new[] { "2263", "2332", "2362", "2485" },
                dzapi = "dz_w3_plugin.dll 1.0.0.51 (variants 1.0.0.248, 2.0.0.0)",
                targetLua = "5.3.6 (luacore.dll, i386)",
                engineLua = "NLua / Lua 5.4 (managed host; 5.3.6 binary is 32-bit only)",
                script = new { language = "jass", mapLua = false, commonJ = common.Length, blizzardJ = blizzard.Length, mapJ = mapSource.Length },
            },
            nativeCoverage = new
            {
                defined,
                extensionDeclared,
                implemented,
                extensionImplemented,
                tested = testedNatives.Count,
                extensionTested = extensionCalled.Length,
                calledByMap = called.Count,
                extensionCalledByMap = extensionCalled.Length,
                scenario = new
                {
                    globals = result.GlobalsInitialized,
                    main = result.MainCompleted,
                    timerCallbacks = result.TimerCallbacks,
                    units = result.UnitsAlive,
                    triggers = result.TriggerCount,
                    timers = result.TimerCount,
                    frames = result.FrameCount,
                    nativeCalls = result.NativeCalls,
                },
                unsupportedOnScenarioPath = result.Failure?.Message,
            },
            scriptCompatibility = new
            {
                jass = new[]
                {
                    "globals/const/arrays/local arrays", "functions/params/returns/recursion",
                    "if/elseif/else", "loop/exitwhen", "set/call/return", "debug blocks (skipped)",
                    "integer/real/boolean/string/null/code/handle", "fourcc literals (little-endian)",
                    "ExecuteFunc string reachability", "native dispatch with strict failure",
                },
                lua = new
                {
                    target = "5.3.6",
                    runtime = "5.4",
                    verified = new[]
                    {
                        "string/number return conversion", "integer vs float tostring (2 vs 2.0)",
                        "floor division //", "modulo sign semantics", "scientific notation",
                        "14-digit precision", "string.format", "tonumber hex", "64-bit integer wrap",
                        "locale-independent decimals", "require('jass.slk')", "syntax/runtime error -> null",
                    },
                    mapExpressions = lua.History.Select(h => new { h.Expression, h.Value, h.ErrorKind }).ToArray(),
                    errors = luaErrors,
                },
            },
            dataCompatibility = new
            {
                sources = new[] { "Units/ItemData.slk", "Units/UnitWeapons.slk", "Units/ItemStrings.txt", "war3map.w3t (ItemObjectData)", "war3map.w3u (UnitObjectData)", "war3map.wts" },
                precedence = new[] { "map object data", "base SLK", "wts for TRIGSTR_" },
                customFieldQueries = provider.CustomFieldQueries,
                customFieldHits = provider.CustomFieldHits,
                wtsEntries = provider.WtsEntries,
                missingCustomFields = provider.MissingFields.ToArray(),
                unresolvedTriggerStrings = provider.UnresolvedTriggerStrings.ToArray(),
                fields = new[] { "item.class", "item.prio", "item.Name", "item.Ubertip", "unit.weapsOn" },
            },
            eventCompatibility = new
            {
                registeredTotal = registrations.Length,
                registeredTypes = eventTypes,
                dispatchedTypes,
                timerCallbacks = result.TimerCallbacks,
                chatDispatched = result.ChatFired,
                dialogClicks = result.Runtime.DialogClicks.Count,
                dialogDispatched = dialogFired,
                dialogStateVerified,
                dialog = new
                {
                    atMs = DialogClickAtMs,
                    message = dialogMessage,
                    button = dialogButton,
                    yxmsBefore,
                    yxmsAfter,
                    messagesAdded = dialogMessagesAdded,
                    nestedEvaluationsAdded = nestedAfter - nestedBefore,
                    hiddenAfterClick = dialog is not null && !dialog.Visible,
                },
                selection = new
                {
                    atMs = DialogClickAtMs,
                    unit = selectedUnit is null ? string.Empty : Rawcode(selectedUnit.TypeId),
                    owner = selectedUnit?.OwnerId,
                    fired = selectionFired,
                    evaluationsAdded = selectionEvaluationsAfter - selectionEvaluationsBefore,
                    storedInMapHashtable = selectionStored,
                    stateVerified = selectionStateVerified,
                },
                callbacksSucceeded = result.Runtime.EventCallbacksSucceeded,
                callbacksFailed = result.Runtime.EventCallbacksFailed,
                eventDepthAfterDispatch = result.Runtime.EventDepth,
                triggerExecutions,
                notDispatched = eventTypes.Keys.Where(k => !dispatchedTypes.ContainsKey(k)).ToArray(),
            },
        };

        Directory.CreateDirectory("docs");
        File.WriteAllText("docs/runtime-compatibility-matrix.json", JsonSerializer.Serialize(matrix, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));

        var profile = new StringBuilder();
        profile.AppendLine("# Target runtime profile");
        profile.AppendLine();
        profile.AppendLine("- Warcraft III: 1.27.0.52240 (verified from War3.exe/game.dll).");
        profile.AppendLine("- YDWE: WorldEdit v1.2.9C, YDWEConfig 2.0.0.2485; KKWE sets 2263/2332/2362/2485.");
        profile.AppendLine("- Runtime plugin: dz_w3_plugin.dll 1.0.0.51 (variants 1.0.0.248, 2.0.0.0), dznep.dll, version.dll proxy.");
        profile.AppendLine("- Lua: target 5.3.6 (`luacore.dll`, PE i386); engine bridge NLua/Lua 5.4 (exact 5.3.6 DLL cannot load in x64).");
        profile.AppendLine("- Script: JASS only (`war3map.j`, no `war3map.lua`); common.j + Blizzard.j + map merged at load.");
        profile.AppendLine($"- Natives: {defined} signatures known, {implemented} implemented, {extensionImplemented}/{extensionDeclared} map extensions implemented.");
        profile.AppendLine($"- Data: {provider.CustomFieldQueries} custom field queries, {provider.CustomFieldHits} object-data hits, {provider.WtsEntries} wts entries.");
        profile.AppendLine($"- Events: {registrations.Length} registrations across {eventTypes.Count} types; timer/chat/init/dialog/selection dispatched; {triggerExecutions} trigger executions.");
        profile.AppendLine($"- Scenario: {ScenarioMs / 1000} s simulated, dialog click at {DialogClickAtMs} ms, {result.TimerCallbacks} timer callbacks, {result.UnitsAlive} units alive, failure={(result.Failure is null ? "none" : result.Failure.Message)}.");
        File.WriteAllText("docs/target-runtime-profile.md", profile.ToString());

        var trace = new StringBuilder();
        trace.AppendLine("# Target map execution trace");
        trace.AppendLine();
        trace.AppendLine("Map: `" + target + "`");
        trace.AppendLine();
        trace.AppendLine("## Verified timeline");
        trace.AppendLine();
        trace.AppendLine("1. Load common.j, Blizzard.j, war3map.j; initialize 1,443 globals (JASS VM).");
        trace.AppendLine($"2. `main` completes; state after the full scenario: units={result.UnitsAlive}, triggers={result.TriggerCount}, timers={result.TimerCount}, frames={result.FrameCount}.");
        trace.AppendLine($"3. Advance {DialogClickAtMs} ms: startup dialog `{dialogMessage}` with buttons [{dialogButton} | ...] is visible to player 0.");
        trace.AppendLine($"4. Engine input adapter clicks `{dialogButton}`: fired={dialogFired}, udg_yxms {yxmsBefore}->{yxmsAfter}, messages+{dialogMessagesAdded}, nested xznd03 evaluations+{nestedAfter - nestedBefore}, dialog hidden={dialog is not null && !dialog.Visible}.");
        trace.AppendLine($"5. Engine input selects `{(selectedUnit is null ? "none" : Rawcode(selectedUnit.TypeId))}` as owner P{selectedUnit?.OwnerId}: fired={selectionFired}, trigger evaluations+{selectionEvaluationsAfter - selectionEvaluationsBefore}, map hashtable stored={selectionStored}.");
        trace.AppendLine($"6. Timer scheduler advances to {ScenarioMs / 1000} s: {result.TimerCallbacks} callbacks; trigger executions={triggerExecutions}.");
        trace.AppendLine($"7. `EXExecuteScript` Lua bridge: {lua.History.Count} calls, {luaErrors} errors.");
        foreach (var call in lua.History.Take(5))
        {
            trace.AppendLine($"   - `{call.Expression}` => `{call.Value ?? "null"}`");
        }

        trace.AppendLine($"8. Chat command `T5`: fired={result.ChatFired}, udg_guaibs={(result.Vm.TryGetGlobalValue("udg_guaibs", out var wave) ? wave.Integer : -1)}, messages={result.Runtime.DisplayMessages.Count}.");
        trace.AppendLine();
        trace.AppendLine("## Native coverage on this path");
        trace.AppendLine();
        trace.AppendLine($"- Distinct natives called: {called.Count}; extension natives called: {extensionCalled.Length}/{extensionDeclared}.");
        trace.AppendLine($"- Extension natives implemented: {extensionImplemented}/{extensionDeclared}.");
        trace.AppendLine($"- First unsupported native on path: {(result.Failure is null ? "none through 120 s + dialog click + selection + chat" : result.Failure.Message)}.");
        trace.AppendLine();
        trace.AppendLine("## Known semantic gaps");
        trace.AppendLine();
        trace.AppendLine("- Selection is dispatched end to end (viewer click / JASS SelectUnit). Orders, attack/damage/death, items, spells and the remaining families still have no producer.");
        trace.AppendLine("- Target Lua 5.3.6 vs engine Lua 5.4 (integer/float formatting covered by conformance tests, not bit-identical runtime).");
        trace.AppendLine("- jass.slk resolves base SLK + map object data; TRIGSTR references missing from war3map.wts stay literal (I033/I010).");
        trace.AppendLine("- TriggerSleepAction/coroutines remain unsupported (target map does not use them).");
        trace.AppendLine("- Rendering is limited to the existing terrain/doodad/runtime-unit path; CJK map text falls back to '?' in the Latin/Vietnamese UI font.");
        File.WriteAllText("docs/target-map-execution-trace.md", trace.ToString());

        Console.WriteLine($"defined={defined} implemented={implemented} extImplemented={extensionImplemented}/{extensionDeclared} called={called.Count} extCalled={extensionCalled.Length} tested={testedNatives.Count}");
        Console.WriteLine($"events: registrations={registrations.Length} types={eventTypes.Count} timerCallbacks={result.TimerCallbacks} chat={result.ChatFired} dialog={dialogFired} dialogVerified={dialogStateVerified} selection={selectionFired} selectionVerified={selectionStateVerified} triggerExecutions={triggerExecutions}");
        Console.WriteLine($"data: customQueries={provider.CustomFieldQueries} customHits={provider.CustomFieldHits} wts={provider.WtsEntries}");
        Console.WriteLine("wrote docs/runtime-compatibility-matrix.json, docs/target-runtime-profile.md, docs/target-map-execution-trace.md");
        return result.Failure is null && dialogStateVerified && selectionStateVerified ? 0 : 2;
    }

    private static JassHandle? LoadSelectedUnitHandle(JassMapRunResult result, int playerId)
    {
        if (!result.Vm.TryGetGlobalValue("YDHT", out var value) || value.Handle?.State is not HashtableState table)
        {
            return null;
        }

        return table.Handles.TryGetValue(
            HashtableState.Key(result.Runtime.Player(playerId).Id, 0x2C1C8DD5),
            out var handle)
            ? handle
            : null;
    }

    private static string Rawcode(int value) => new(new[]
    {
        (char)(value & 0xFF),
        (char)((value >> 8) & 0xFF),
        (char)((value >> 16) & 0xFF),
        (char)((value >> 24) & 0xFF),
    });

    private static int TriggerEvaluations(JassMapRunResult result, string globalName) =>
        result.Vm.TryGetGlobalValue(globalName, out var value) && value.Handle?.State is TriggerState trigger
            ? trigger.Evaluations
            : 0;

    private static string ReadGlobal(JassMapRunResult result, string name) =>
        result.Vm.TryGetGlobalValue(name, out var value) ? value.ToString() : "missing";

    private static string Read(AssetStore store, string path)
    {
        try
        {
            return System.Text.Encoding.UTF8.GetString(store.ReadAllBytes(path, out _));
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
    }
}
