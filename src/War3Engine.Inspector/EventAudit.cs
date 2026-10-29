using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using War3Engine.Assets;
using War3Engine.Core;
using War3Engine.Scripting;
using War3Engine.Scripting.Lua;

namespace War3Engine.Inspector;

/// <summary>
/// Runs the target map, produces a real dialog click through the engine input layer and audits
/// every trigger registration against the event producers the engine actually has.
/// </summary>
internal static class EventAudit
{
    private const int StartupAdvanceMs = 3000;
    private const int TimerProbeMs = 1000;

    private static readonly Dictionary<string, Producer> ProducersByFamily = new(StringComparer.Ordinal)
    {
        ["timer"] = new(true, true, "Timer scheduler dispatches due timers with GetExpiredTimer context."),
        ["timer-expire"] = new(true, true, "Timer scheduler dispatches expiry subscribers."),
        ["chat"] = new(true, true, "Engine input adapter (F5 / DispatchChatHotkey)."),
        ["dialog"] = new(true, true, "Dialog UI hit-test -> DispatchDialogClick (real mouse in the viewer)."),
        ["dialog-button"] = new(true, false, "Same dialog click producer, exact-button matching; covered by fixture tests."),
        ["player-unit-event:24"] = new(true, true, "Unit selection state transition (viewer click / JASS SelectUnit) with owner and player scoping."),
        ["player-unit-event:25"] = new(true, true, "Unit deselection state transition."),
        ["unit-event:57"] = new(true, false, "Unit-scoped selected event for the exact registered unit."),
        ["unit-event:58"] = new(true, false, "Unit-scoped deselected event for the exact registered unit."),
    };

    private static Producer ProducerFor(JassEventRegistration registration) =>
        ProducersByFamily.TryGetValue(FamilyKey(registration), out var producer)
            ? producer
            : ProducersByFamily.TryGetValue(registration.Kind, out var kindProducer)
                ? kindProducer
                : new Producer(false, false, "No producer.");

    private static readonly Dictionary<string, string[]> ResponseContracts = new(StringComparer.Ordinal)
    {
        ["EVENT_PLAYER_UNIT_ATTACKED"] = new[] { "GetTriggerUnit", "GetAttacker", "GetTriggerPlayer" },
        ["EVENT_PLAYER_UNIT_DEATH"] = new[] { "GetTriggerUnit", "GetDyingUnit", "GetKillingUnit", "GetTriggerPlayer" },
        ["EVENT_PLAYER_UNIT_SELECTED"] = new[] { "GetTriggerUnit", "GetTriggerPlayer" },
        ["EVENT_PLAYER_UNIT_DESELECTED"] = new[] { "GetTriggerUnit", "GetTriggerPlayer" },
        ["EVENT_PLAYER_UNIT_SPELL_EFFECT"] = new[] { "GetTriggerUnit", "GetSpellAbilityUnit", "GetSpellAbilityId", "GetSpellTargetUnit", "GetSpellTargetX", "GetSpellTargetY" },
        ["EVENT_PLAYER_UNIT_SPELL_FINISH"] = new[] { "GetTriggerUnit", "GetSpellAbilityUnit", "GetSpellAbilityId" },
        ["EVENT_PLAYER_UNIT_SPELL_CAST"] = new[] { "GetTriggerUnit", "GetSpellAbilityUnit", "GetSpellAbilityId" },
        ["EVENT_PLAYER_UNIT_SPELL_CHANNEL"] = new[] { "GetTriggerUnit", "GetSpellAbilityUnit", "GetSpellAbilityId" },
        ["EVENT_PLAYER_UNIT_SPELL_ENDCAST"] = new[] { "GetTriggerUnit", "GetSpellAbilityUnit", "GetSpellAbilityId" },
        ["EVENT_PLAYER_UNIT_ISSUED_ORDER"] = new[] { "GetOrderedUnit", "GetIssuedOrderId", "GetOrderTargetUnit", "GetOrderPointX", "GetOrderPointY" },
        ["EVENT_PLAYER_UNIT_ISSUED_POINT_ORDER"] = new[] { "GetOrderedUnit", "GetIssuedOrderId", "GetOrderPointX", "GetOrderPointY" },
        ["EVENT_PLAYER_UNIT_ISSUED_TARGET_ORDER"] = new[] { "GetOrderedUnit", "GetIssuedOrderId", "GetOrderTargetUnit" },
        ["EVENT_PLAYER_UNIT_DROP_ITEM"] = new[] { "GetTriggerUnit", "GetManipulatedItem" },
        ["EVENT_PLAYER_UNIT_PICKUP_ITEM"] = new[] { "GetTriggerUnit", "GetManipulatedItem" },
        ["EVENT_PLAYER_UNIT_USE_ITEM"] = new[] { "GetTriggerUnit", "GetManipulatedItem", "GetItemTypeId" },
        ["EVENT_PLAYER_UNIT_TRAIN_FINISH"] = new[] { "GetTriggerUnit", "GetTrainedUnit" },
        ["EVENT_PLAYER_UNIT_CONSTRUCT_FINISH"] = new[] { "GetTriggerUnit" },
        ["EVENT_PLAYER_UNIT_UPGRADE_START"] = new[] { "GetTriggerUnit" },
        ["EVENT_PLAYER_UNIT_UPGRADE_FINISH"] = new[] { "GetTriggerUnit" },
        ["EVENT_PLAYER_UNIT_RESEARCH_FINISH"] = new[] { "GetTriggerUnit", "GetResearched" },
        ["EVENT_PLAYER_UNIT_SELL"] = new[] { "GetTriggerUnit", "GetBuyingUnit", "GetSoldUnit", "GetSellingUnit" },
        ["EVENT_PLAYER_UNIT_SELL_ITEM"] = new[] { "GetTriggerUnit", "GetBuyingUnit", "GetSoldItem" },
        ["EVENT_PLAYER_UNIT_PAWN_ITEM"] = new[] { "GetTriggerUnit", "GetManipulatedItem" },
        ["EVENT_PLAYER_HERO_LEVEL"] = new[] { "GetTriggerUnit", "GetLevelingUnit", "GetHeroLevel" },
        ["EVENT_UNIT_DAMAGED"] = new[] { "GetTriggerUnit", "GetEventDamageSource", "GetEventDamage" },
        ["EVENT_UNIT_DEATH"] = new[] { "GetDyingUnit", "GetKillingUnit", "GetTriggerUnit" },
        ["EVENT_UNIT_ATTACKED"] = new[] { "GetTriggerUnit", "GetAttacker" },
        ["EVENT_UNIT_SELECTED"] = new[] { "GetTriggerUnit", "GetTriggerPlayer" },
        ["EVENT_UNIT_DESELECTED"] = new[] { "GetTriggerUnit", "GetTriggerPlayer" },
        ["EVENT_PLAYER_CHAT"] = new[] { "GetEventPlayerChatString", "GetEventPlayerChatStringMatched", "GetTriggerPlayer" },
        ["EVENT_PLAYER_LEAVE"] = new[] { "GetTriggerPlayer" },
        ["EVENT_DIALOG_CLICK"] = new[] { "GetClickedButton", "GetClickedDialog", "GetTriggerPlayer", "GetTriggeringTrigger" },
        ["EVENT_DIALOG_BUTTON_CLICK"] = new[] { "GetClickedButton", "GetClickedDialog", "GetTriggerPlayer", "GetTriggeringTrigger" },
        ["EVENT_GAME_ENTER_REGION"] = new[] { "GetTriggeringRegion", "GetEnteringUnit" },
        ["EVENT_GAME_LEAVE_REGION"] = new[] { "GetTriggeringRegion", "GetLeavingUnit" },
    };

    private static readonly Dictionary<string, (string Category, int Id)> EnumLookup = new(StringComparer.Ordinal)
    {
        ["chat"] = ("PlayerEvent", 16),
        ["dialog"] = ("DialogEvent", 91),
        ["dialog-button"] = ("DialogEvent", 90),
        ["enter-region"] = ("GameEvent", 5),
        ["leave-region"] = ("GameEvent", 6),
    };

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
        var provider = JassSlkQueryProvider.Load(store, target);
        var lua = new NLuaBridge(provider.Query);
        var result = JassMapHost.Run(common, blizzard, mapSource, 12_000_000, advanceMs: StartupAdvanceMs, lua: lua);
        if (result.Failure is not null)
        {
            Console.WriteLine($"RESULT: failed before dialog: {result.Failure.Message}");
            return 2;
        }

        var runtime = result.Runtime;
        var registrations = runtime.Registrations.ToArray();
        var triggerCount = result.TriggerCount;
        var dialog = runtime.Dialogs.LastOrDefault(d => d.Visible && !d.Destroyed && d.PlayerId == 0 && d.Buttons.Count > 0);
        if (dialog is null)
        {
            Console.WriteLine("RESULT: no visible startup dialog");
            return 3;
        }

        var timerMessageBefore = dialog.Message;
        runtime.Advance(TimerProbeMs);
        runtime.RunDueTimers();
        var timerMessageAfter = dialog.Message;
        var timerStateVerified = timerMessageBefore != timerMessageAfter && timerMessageAfter.Contains('[');

        var yxmsBefore = ReadGlobal(result, "udg_yxms");
        var messagesBefore = runtime.DisplayMessages.Count;
        var evaluationsBefore = runtime.Triggers.Sum(t => t.Evaluations);
        var nestedBefore = TriggerEvaluations(result, "gg_trg_xznd03______u");
        var button = dialog.Buttons[0];
        var fired = runtime.DispatchDialogClick(dialog, button, 0);
        runtime.Advance(0);
        runtime.RunDueTimers();
        var yxmsAfter = ReadGlobal(result, "udg_yxms");
        var messagesAfter = runtime.DisplayMessages.Count;
        var evaluationsAfter = runtime.Triggers.Sum(t => t.Evaluations);
        var nestedAfter = TriggerEvaluations(result, "gg_trg_xznd03______u");
        var dialogStateVerified = yxmsBefore != "true" && yxmsAfter == "true" &&
                                  messagesAfter > messagesBefore &&
                                  nestedAfter > nestedBefore &&
                                  !dialog.Visible;
        var contextRestored = runtime.EventDepth == 0;

        var selectionUnit = runtime.Units.FirstOrDefault(u => !u.Removed && u.Handle is not null);
        var selectionEvaluationsBefore = TriggerEvaluations(result, "gg_trg____________________005");
        var selectionFired = 0;
        var selectionStored = false;
        if (selectionUnit is not null)
        {
            selectionFired = runtime.SelectSingleUnit(selectionUnit, selectionUnit.OwnerId);
            selectionStored = ReferenceEquals(LoadSelectedUnitHandle(result, selectionUnit.OwnerId), selectionUnit.Handle);
        }

        var selectionEvaluationsAfter = TriggerEvaluations(result, "gg_trg____________________005");
        var selectionStateVerified = selectionUnit is not null && selectionFired > 0 &&
                                     runtime.SelectedUnits(selectionUnit.OwnerId).Contains(selectionUnit) &&
                                     selectionEvaluationsAfter > selectionEvaluationsBefore &&
                                     selectionStored;

        var waveBefore = ReadGlobal(result, "udg_guaibs");
        var chatMessagesBefore = runtime.DisplayMessages.Count;
        var chatFired = runtime.DispatchChat(0, "T5");
        runtime.Advance(0);
        runtime.RunDueTimers();
        var waveAfter = ReadGlobal(result, "udg_guaibs");
        var chatStateVerified = waveBefore != "5" && waveAfter == "5" && chatFired > 0 &&
                                runtime.DisplayMessages.Count > chatMessagesBefore;

        var catalog = EventCatalog.Parse(common);
        var triggerNames = BuildTriggerNames(result);
        var verifiedKinds = new HashSet<string>(StringComparer.Ordinal);
        if (timerStateVerified)
        {
            verifiedKinds.Add("timer");
            verifiedKinds.Add("timer-expire");
        }

        if (dialogStateVerified)
        {
            verifiedKinds.Add("dialog");
        }

        if (chatStateVerified)
        {
            verifiedKinds.Add("chat");
        }

        if (selectionStateVerified)
        {
            verifiedKinds.Add("player-unit-event:24");
        }

        var groups = registrations
            .GroupBy(r => FamilyKey(r))
            .OrderByDescending(g => g.Count())
            .Select(g => BuildFamily(g.Key, g.ToArray(), catalog, result, verifiedKinds))
            .ToArray();

        var producers = new[]
        {
            new
            {
                kind = "timer",
                available = true,
                tested = timerStateVerified,
                description = ProducersByFamily["timer"].Description,
                evidence = $"dialog message '{timerMessageBefore}' -> '{timerMessageAfter}' after {TimerProbeMs} ms",
            },
            new
            {
                kind = "dialog",
                available = true,
                tested = dialogStateVerified,
                description = ProducersByFamily["dialog"].Description,
                evidence = $"clicked '{button.Label}' in '{dialog.Message}': fired={fired}, udg_yxms {yxmsBefore}->{yxmsAfter}, messages +{messagesAfter - messagesBefore}, nested xznd03 evals +{nestedAfter - nestedBefore}",
            },
            new
            {
                kind = "dialog-button",
                available = true,
                tested = false,
                description = ProducersByFamily["dialog-button"].Description,
                evidence = "no map registration; fixture tests cover exact-button dispatch",
            },
            new
            {
                kind = "chat",
                available = true,
                tested = chatStateVerified,
                description = ProducersByFamily["chat"].Description,
                evidence = $"T5: fired={chatFired}, udg_guaibs {waveBefore}->{waveAfter}, messages +{runtime.DisplayMessages.Count - chatMessagesBefore}",
            },
            new
            {
                kind = "player-unit-event:24",
                available = true,
                tested = selectionStateVerified,
                description = ProducersByFamily["player-unit-event:24"].Description,
                evidence = selectionUnit is null
                    ? "no runtime unit available"
                    : $"selected {Rawcode(selectionUnit.TypeId)} as owner P{selectionUnit.OwnerId}: fired={selectionFired}, trigger evals +{selectionEvaluationsAfter - selectionEvaluationsBefore}, stored={selectionStored}",
            },
            new
            {
                kind = "player-unit-event",
                available = false,
                tested = false,
                description = "Selection and deselection are produced; orders/attack/damage have no producer yet.",
                evidence = "other player-unit event IDs registered only",
            },
            new
            {
                kind = "unit-event",
                available = false,
                tested = false,
                description = "No damage/state producer yet.",
                evidence = "registered only",
            },
            new
            {
                kind = "player-event",
                available = false,
                tested = false,
                description = "No player leave/defeat/victory producer yet.",
                evidence = "registered only",
            },
            new
            {
                kind = "enter-region",
                available = false,
                tested = false,
                description = "Units never move through regions yet.",
                evidence = "registered only",
            },
            new
            {
                kind = "leave-region",
                available = false,
                tested = false,
                description = "Units never move through regions yet.",
                evidence = "registered only",
            },
            new
            {
                kind = "game-state",
                available = false,
                tested = false,
                description = "No game state limit producer yet.",
                evidence = "registered only",
            },
            new
            {
                kind = "key-event",
                available = false,
                tested = false,
                description = "YDWE key registrations; viewer keyboard is not dispatched to JASS yet.",
                evidence = "registered only",
            },
            new
            {
                kind = "sync",
                available = false,
                tested = false,
                description = "No multiplayer/sync producer.",
                evidence = "registered only",
            },
        };

        var familyKeys = registrations.GroupBy(FamilyKey).ToArray();
        var summary = new
        {
            registeredEvents = registrations.Length,
            distinctFamilies = familyKeys.Length,
            triggerCount,
            producerBackedFamilies = familyKeys.Count(g => g.Any(r => ProducerFor(r).Available)),
            callbackVerifiedFamilies = familyKeys.Count(g => g.Sum(r => r.Callbacks) > 0),
            stateVerifiedFamilies = familyKeys.Count(g => verifiedKinds.Contains(FamilyKey(g.First())) && g.Sum(r => r.Callbacks) > 0),
            callbacks = registrations.Sum(r => r.Callbacks),
            failures = registrations.Sum(r => r.Failures),
            eventsRaised = runtime.EventsRaised,
            contextRestored,
        };

        var document = new
        {
            generatedUtc = DateTime.UtcNow.ToString("O"),
            map = target,
            runtime = new
            {
                warcraft = "1.27.0.52240",
                script = "JASS only",
                scenario = new
                {
                    advanceMs = StartupAdvanceMs + TimerProbeMs,
                    engineInput = "dialog button 0 (practice mode), chat T5",
                    osInput = false,
                    osInputNote = "Mouse clicks through the viewer window use the same producer but were not automated (Windows foreground lock).",
                },
            },
            summary,
            producers,
            verification = new
            {
                timer = new { before = timerMessageBefore, after = timerMessageAfter, verified = timerStateVerified },
                dialog = new
                {
                    dialog = dialog.Message,
                    button = button.Label,
                    fired,
                    yxmsBefore,
                    yxmsAfter,
                    messagesAdded = messagesAfter - messagesBefore,
                    evaluationsAdded = evaluationsAfter - evaluationsBefore,
                    nestedTrigger = "gg_trg_xznd03______u",
                    nestedEvaluationsAdded = nestedAfter - nestedBefore,
                    dialogHidden = !dialog.Visible,
                    verified = dialogStateVerified,
                },
                chat = new { command = "T5", fired = chatFired, waveBefore, waveAfter, verified = chatStateVerified },
                selection = new
                {
                    unit = selectionUnit is null ? null : Rawcode(selectionUnit.TypeId),
                    owner = selectionUnit?.OwnerId,
                    fired = selectionFired,
                    evaluationsAdded = selectionEvaluationsAfter - selectionEvaluationsBefore,
                    storedInMapHashtable = selectionStored,
                    verified = selectionStateVerified,
                },
                eventDepthAfterDispatch = runtime.EventDepth,
            },
            families = groups,
            registrations = registrations.Select(r => BuildRegistration(r, catalog, result, triggerNames, verifiedKinds)).ToArray(),
        };

        Directory.CreateDirectory("docs");
        File.WriteAllText("docs/event-dispatch-coverage.json", JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));

        Console.WriteLine($"registrations={summary.registeredEvents} families={summary.distinctFamilies} producerBacked={summary.producerBackedFamilies} callbackVerified={summary.callbackVerifiedFamilies} stateVerified={summary.stateVerifiedFamilies}");
        Console.WriteLine($"dialog: '{button.Label}' fired={fired} yxms {yxmsBefore}->{yxmsAfter} messages+{messagesAfter - messagesBefore} nested+{nestedAfter - nestedBefore} hidden={!dialog.Visible} contextDepth={runtime.EventDepth}");
        Console.WriteLine($"timer: '{timerMessageBefore}' -> '{timerMessageAfter}' verified={timerStateVerified}; chat: fired={chatFired} wave {waveBefore}->{waveAfter}");
        Console.WriteLine($"selection: unit={(selectionUnit is null ? "-" : Rawcode(selectionUnit.TypeId))} owner={selectionUnit?.OwnerId} fired={selectionFired} evals+{selectionEvaluationsAfter - selectionEvaluationsBefore} stored={selectionStored}");
        Console.WriteLine("wrote docs/event-dispatch-coverage.json");
        return dialogStateVerified && timerStateVerified && chatStateVerified && selectionStateVerified && contextRestored ? 0 : 4;
    }

    private static object BuildFamily(
        string key,
        JassEventRegistration[] registrations,
        EventCatalog catalog,
        JassMapRunResult result,
        HashSet<string> verifiedFamilies)
    {
        var first = registrations[0];
        var enumName = EnumName(first, catalog, out var category, out var id);
        var producer = ProducerFor(first);
        var callbacks = registrations.Sum(r => r.Callbacks);
        var stateVerified = verifiedFamilies.Contains(key) && callbacks > 0;
        var level = stateVerified ? 5 : callbacks > 0 ? 4 : producer.Available ? 2 : 1;
        var responses = ResponseFor(enumName);
        return new
        {
            key,
            kind = first.Kind,
            eventId = id,
            enumName,
            enumCategory = category,
            declarations = EnumLookup.ContainsKey(first.Kind) || id >= 0 ? $"common.j constant {enumName}" : "no public JASS enum",
            registrations = registrations.Length,
            triggers = registrations.Select(r => r.Trigger).Distinct().Count(),
            registrationFunctions = registrations.Select(r => r.Function).Distinct().ToArray(),
            sites = registrations.Select(r => r.Site).Distinct().Take(12).ToArray(),
            players = registrations.Select(r => r.PlayerId).Where(p => p >= 0).Distinct().OrderBy(p => p).ToArray(),
            filters = registrations.Count(r => r.HasFilter),
            requiredResponseValues = responses,
            responseValuesMissing = responses.Where(n => !result.Registry.Natives.ContainsKey(n)).ToArray(),
            producerAvailable = producer.Available,
            producerVerifiedInRun = producer.Available && callbacks > 0,
            dispatched = registrations.Any(r => r.Dispatches > 0),
            dispatches = registrations.Sum(r => r.Dispatches),
            callbacks,
            failures = registrations.Sum(r => r.Failures),
            stateVerified,
            level,
            levelMeaning = level switch
            {
                5 => "end-to-end gameplay behavior verified",
                4 => "full event-to-JASS callback verified",
                3 => "event producer tested in isolation",
                2 => "registered and backed by an event producer",
                _ => "registered only",
            },
            notes = producer.Description,
        };
    }

    private static object BuildRegistration(
        JassEventRegistration registration,
        EventCatalog catalog,
        JassMapRunResult result,
        Dictionary<TriggerState, string> triggerNames,
        HashSet<string> verifiedFamilies)
    {
        var enumName = EnumName(registration, catalog, out var category, out var id);
        var producer = ProducerFor(registration);
        var stateVerified = verifiedFamilies.Contains(FamilyKey(registration)) && registration.Callbacks > 0;
        var level = stateVerified ? 5 : registration.Callbacks > 0 ? 4 : producer.Available ? 2 : 1;
        return new
        {
            kind = registration.Kind,
            eventId = id,
            enumName,
            enumCategory = category,
            function = registration.Function,
            trigger = TriggerName(registration, triggerNames),
            triggerHandleId = registration.Trigger.Handle?.Id,
            player = registration.PlayerId,
            source = SourceName(registration),
            filter = registration.HasFilter,
            site = registration.Site,
            chain = registration.Chain,
            registrationOrder = registration.Sequence,
            requiredResponseValues = ResponseFor(enumName),
            responseValuesMissing = ResponseFor(enumName).Where(n => !result.Registry.Natives.ContainsKey(n)).ToArray(),
            producerAvailable = producer.Available,
            dispatched = registration.Dispatches > 0,
            dispatches = registration.Dispatches,
            callbacks = registration.Callbacks,
            failures = registration.Failures,
            stateVerified,
            level,
        };
    }

    private static string FamilyKey(JassEventRegistration registration) =>
        registration.Kind is "player-unit-event" or "unit-event" or "player-event" or "game-event" or
            "game-state" or "player-state" or "unit-state" or "alliance" or "command" or "upgrade-command"
            ? $"{registration.Kind}:{registration.EventId}"
            : registration.Kind;

    private static string EnumName(JassEventRegistration registration, EventCatalog catalog, out string category, out int id)
    {
        if (EnumLookup.TryGetValue(registration.Kind, out var mapped))
        {
            category = mapped.Category;
            id = mapped.Id;
            return catalog.Name(category, id) ?? registration.Kind;
        }

        var (enumCategory, eventId) = registration.Kind switch
        {
            "player-unit-event" => ("PlayerUnitEvent", registration.EventId),
            "unit-event" or "unit-state" => ("UnitEvent", registration.EventId),
            "player-event" or "player-state" or "alliance" => ("PlayerEvent", registration.EventId),
            "game-event" or "game-state" => ("GameEvent", registration.EventId),
            _ => (string.Empty, registration.EventId),
        };

        if (!string.IsNullOrEmpty(enumCategory))
        {
            category = enumCategory;
            id = eventId;
            return catalog.Name(enumCategory, eventId) ?? $"{registration.Kind}:{eventId}";
        }

        category = string.Empty;
        id = registration.EventId;
        return registration.Kind;
    }

    private static string[] ResponseFor(string enumName) =>
        ResponseContracts.TryGetValue(enumName, out var responses) ? responses : Array.Empty<string>();

    private static Dictionary<TriggerState, string> BuildTriggerNames(JassMapRunResult result)
    {
        var names = new Dictionary<TriggerState, string>();
        foreach (var (name, value) in result.Vm.SnapshotGlobals())
        {
            if (value.Handle?.State is TriggerState trigger &&
                (!names.TryGetValue(trigger, out var existing) || name.Length < existing.Length))
            {
                names[trigger] = name;
            }
        }

        return names;
    }

    private static string TriggerName(JassEventRegistration registration, Dictionary<TriggerState, string> names) =>
        names.TryGetValue(registration.Trigger, out var name)
            ? name
            : $"local trigger @ {registration.Site}";

    private static string SourceName(JassEventRegistration registration) => registration.Source?.State switch
    {
        DialogState dialog => $"dialog '{dialog.Message}'",
        DialogButtonState button => $"button '{button.Label}'",
        TimerState timer => $"timer#{timer.HandleId}",
        RegionState => "region",
        UnitState unit => $"unit {unit.TypeId}",
        _ => registration.Source is null ? string.Empty : registration.Source.Type,
    };

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
            return Encoding.UTF8.GetString(store.ReadAllBytes(path, out _));
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
    }

    private sealed record Producer(bool Available, bool Verified, string Description);
}

internal sealed class EventCatalog
{
    private static readonly Regex ConstantPattern = new(
        @"constant\s+\w+\s+(EVENT_\w+)\s*=\s*Convert(\w+)\((\d+)\)",
        RegexOptions.Compiled);

    private readonly Dictionary<string, Dictionary<int, string>> _names = new(StringComparer.OrdinalIgnoreCase);

    public static EventCatalog Parse(string commonSource)
    {
        var catalog = new EventCatalog();
        foreach (Match match in ConstantPattern.Matches(commonSource))
        {
            var name = match.Groups[1].Value;
            var category = match.Groups[2].Value;
            if (!int.TryParse(match.Groups[3].Value, out var id))
            {
                continue;
            }

            if (!catalog._names.TryGetValue(category, out var byId))
            {
                byId = new Dictionary<int, string>();
                catalog._names[category] = byId;
            }

            byId[id] = name;
        }

        return catalog;
    }

    public string? Name(string category, int id) =>
        _names.TryGetValue(category, out var byId) && byId.TryGetValue(id, out var name) ? name : null;
}
