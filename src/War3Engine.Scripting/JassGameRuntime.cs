using War3Engine.Scripting.Lua;

namespace War3Engine.Scripting;

public sealed class HashtableState
{
    public Dictionary<long, int> Integers { get; } = new();
    public Dictionary<long, float> Reals { get; } = new();
    public Dictionary<long, bool> Booleans { get; } = new();
    public Dictionary<long, string> Strings { get; } = new();
    public Dictionary<long, JassHandle> Handles { get; } = new();

    public static long Key(int parent, int child) => ((long)parent << 32) | (uint)child;

    public void RemovePrefix(int parent)
    {
        RemovePrefix(Integers, parent);
        RemovePrefix(Reals, parent);
        RemovePrefix(Booleans, parent);
        RemovePrefix(Strings, parent);
        RemovePrefix(Handles, parent);
    }

    private static void RemovePrefix<T>(Dictionary<long, T> dictionary, int parent)
    {
        var keys = dictionary.Keys.Where(k => (int)(k >> 32) == parent).ToArray();
        foreach (var key in keys)
        {
            dictionary.Remove(key);
        }
    }
}

public sealed class TriggerState
{
    public List<JassValue> Actions { get; } = new();
    public List<JassValue> Conditions { get; } = new();
    public List<JassEventRegistration> Registrations { get; } = new();
    public bool Enabled { get; set; } = true;
    public bool Destroyed { get; set; }
    public int Evaluations { get; set; }
    public JassHandle? Handle { get; set; }
}

public sealed class ConditionState
{
    public ConditionState(JassValue code) => Code = code;

    public JassValue Code { get; }
}

public sealed class TimerState
{
    public JassValue Callback { get; set; }
    public double PeriodMs { get; set; }
    public double RemainingMs { get; set; }
    public bool Periodic { get; set; }
    public bool Running { get; set; }
    public bool Destroyed { get; set; }
    public int HandleId { get; set; }
    public double FiredAtMs { get; set; }
    public JassHandle? Handle { get; set; }
}

public sealed class UnitState
{
    public int TypeId { get; init; }
    public int OwnerId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Facing { get; set; }
    public float Hp { get; set; } = 100f;
    public float MaxHp { get; set; } = 100f;
    public float Mana { get; set; }
    public bool Removed { get; set; }
    public JassHandle? Handle { get; set; }
    public string Model { get; set; } = string.Empty;
    public Dictionary<int, int> Abilities { get; } = new();
    public HashSet<int> Types { get; } = new();
}

public sealed class GroupState
{
    public List<JassHandle> Units { get; } = new();
}

public sealed class ForceState
{
    public HashSet<int> Players { get; } = new();
}

public sealed class RectState
{
    public float MinX { get; set; }
    public float MinY { get; set; }
    public float MaxX { get; set; }
    public float MaxY { get; set; }

    public float CenterX => (MinX + MaxX) * 0.5f;
    public float CenterY => (MinY + MaxY) * 0.5f;

    public bool Contains(float x, float y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
}

public sealed class RegionState
{
    public List<RectState> Rects { get; } = new();
}

public sealed class LocationState
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public sealed class DialogButtonState
{
    public required DialogState Dialog { get; init; }
    public string Label { get; init; } = string.Empty;
    public int Hotkey { get; init; }
    public JassHandle? Handle { get; set; }
}

public sealed class DialogState
{
    public string Message { get; set; } = string.Empty;
    public List<DialogButtonState> Buttons { get; } = new();
    public bool Visible { get; set; }
    public int PlayerId { get; set; } = -1;
    public bool Destroyed { get; set; }
    public JassHandle? Handle { get; set; }
}

public sealed class FrameState
{
    public required int Id { get; init; }
    public required string Kind { get; init; }
    public string Name { get; set; } = string.Empty;
    public int Parent { get; init; }
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public string Text { get; set; } = string.Empty;
    public string Texture { get; set; } = string.Empty;
    public string Font { get; set; } = string.Empty;
    public float Width { get; set; }
    public float Height { get; set; }
    public int Point { get; set; }
    public int RelativeFrame { get; set; }
    public int RelativePoint { get; set; }
    public float PointX { get; set; }
    public float PointY { get; set; }
    public int TextAlignment { get; set; }
    public Dictionary<int, JassValue> Scripts { get; } = new();
}

public sealed class ItemState
{
    public int TypeId { get; init; }
    public float X { get; set; }
    public float Y { get; set; }
    public JassHandle? Handle { get; set; }
}

public sealed class DestructableState
{
    public int TypeId { get; init; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Life { get; set; } = 100f;
    public JassHandle? Handle { get; set; }
}

public sealed class MultiboardItemState
{
    public required MultiboardState Board { get; init; }
    public int Row { get; init; }
    public int Column { get; init; }
    public string Value { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public float Width { get; set; } = 0.03f;
    public bool ShowValue { get; set; } = true;
    public bool ShowIcon { get; set; }
}

public sealed class MultiboardState
{
    public string Title { get; set; } = string.Empty;
    public int Rows { get; set; }
    public int Columns { get; set; }
    public bool Visible { get; set; }
    public bool Minimized { get; set; }
    public bool ShowValues { get; set; } = true;
    public bool ShowIcons { get; set; }
    public string ItemsValue { get; set; } = string.Empty;
    public float ItemsWidth { get; set; } = 0.03f;
    public string ItemsIcon { get; set; } = string.Empty;
    public Dictionary<(int Row, int Column), MultiboardItemState> Items { get; } = new();
}

public sealed class SoundState
{
    public string Label { get; set; } = string.Empty;
    public bool Playing { get; set; }
    public bool Looping { get; set; }
    public int Volume { get; set; } = 100;
    public int Pitch { get; set; } = 1;
}

public sealed class EffectState
{
    public string Model { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Size { get; set; } = 1f;
    public float Speed { get; set; } = 1f;
    public float ScaleX { get; set; } = 1f;
    public float ScaleY { get; set; } = 1f;
    public float ScaleZ { get; set; } = 1f;
    public float RotateX { get; set; }
    public float RotateY { get; set; }
    public float RotateZ { get; set; }
}

public sealed class AbilityState
{
    public int AbilityId { get; init; }
    public int Level { get; set; } = 1;
    public Dictionary<int, float> Reals { get; } = new();
    public Dictionary<int, int> Integers { get; } = new();
    public Dictionary<int, string> Strings { get; } = new();
}

/// <summary>
/// Stateful services behind JASS natives: players, units, triggers, timers, groups,
/// regions, dialogs and YDWE frames. Rendering is not involved.
/// </summary>
public sealed class JassGameRuntime
{
    private static readonly Dictionary<(string Type, int Id), JassHandle> EnumHandleCache = new();

    public static JassHandle EnumHandle(string type, int id)
    {
        if (!EnumHandleCache.TryGetValue((type, id), out var handle))
        {
            handle = new JassHandle(type, id);
            EnumHandleCache[(type, id)] = handle;
        }

        return handle;
    }

    public JassVirtualMachine? Vm { get; set; }

    public IJassLua? Lua { get; set; }

    public List<UnitState> Units { get; } = new();

    public List<TriggerState> Triggers { get; } = new();

    public List<TimerState> Timers { get; } = new();

    public Dictionary<int, JassHandle> PlayerHandles { get; } = new();

    public Dictionary<int, FrameState> Frames { get; } = new();

    public Dictionary<string, JassValue> Globals { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> NativeCalls { get; } = new(StringComparer.Ordinal);

    public double TimeMs { get; set; }

    public JassValue FilterUnit { get; set; }

    public JassValue EnumUnit { get; set; }

    public JassValue EnumPlayer { get; set; }

    public int InitTriggersFired { get; set; }

    private readonly List<JassEventContext> _contexts = new();
    private long _registrationSequence;

    public JassEventContext? Context => _contexts.Count == 0 ? null : _contexts[^1];

    public int EventDepth => _contexts.Count;

    public List<JassEventRegistration> Registrations { get; } = new();

    public List<DialogState> Dialogs { get; } = new();

    private readonly Dictionary<int, List<UnitState>> _selection = new();

    public IReadOnlyDictionary<int, List<UnitState>> Selection => _selection;

    public List<string> DialogClicks { get; } = new();

    public Dictionary<string, int> EventsRaised { get; } = new(StringComparer.Ordinal);

    public int EventCallbacksSucceeded { get; private set; }

    public int EventCallbacksFailed { get; private set; }

    public List<string> EventFailures { get; } = new();

    public int ChatEventsFired { get; set; }

    public TriggerState? UiTrigger { get; set; }

    public int TimerCallbacksFired { get; set; }

    public List<string> LoadedTocs { get; } = new();

    public List<string> SyncLog { get; } = new();

    public List<string> DisplayMessages { get; } = new();

    public bool MultiboardSuppressed { get; set; }

    public float[] CameraBounds { get; } = new float[8];

    public string SoundEnvironment { get; set; } = string.Empty;

    public string[] DayNightModels { get; } = new string[2];

    public int[] WaterColor { get; } = new int[4];

    public string MapMusic { get; set; } = string.Empty;

    public bool FogEnabled { get; set; }

    public bool FogMaskEnabled { get; set; }

    public Dictionary<(int Source, int Target, int Alliance), bool> Alliances { get; } = new();

    public Dictionary<int, float> FloatGameState { get; } = new();

    public Dictionary<int, int> IntegerGameState { get; } = new();

    public Dictionary<(int Player, int State), int> PlayerStates { get; } = new();

    public Dictionary<int, int> PlayerColors { get; } = new();

    public Dictionary<int, int> PlayerRaces { get; } = new();

    public Dictionary<int, int> PlayerTeams { get; } = new();

    public Dictionary<int, int> PlayerStartLocations { get; } = new();

    public Dictionary<(int Player, int Tech), int> TechResearched { get; } = new();

    public Dictionary<(int Player, int Tech), int> TechMaxAllowed { get; } = new();

    public int AllItemTypeSlots { get; set; } = 6;

    public int AllUnitTypeSlots { get; set; } = 18;

    public List<ItemState> Items { get; } = new();

    public List<DestructableState> Destructables { get; } = new();

    public Dictionary<int, float> CameraFields { get; } = new();

    public float CameraX { get; set; }

    public float CameraY { get; set; }

    public float CameraZ { get; set; } = 1000f;

    public JassHandle Player(int id)
    {
        if (!PlayerHandles.TryGetValue(id, out var handle))
        {
            handle = new JassHandle("player", id);
            PlayerHandles[id] = handle;
        }

        return handle;
    }

    public void CountNative(string name)
    {
        NativeCalls[name] = NativeCalls.TryGetValue(name, out var count) ? count + 1 : 1;
    }

    public int Version { get; set; } = 8;

    public bool EvaluateConditions(TriggerState trigger)
    {
        foreach (var condition in trigger.Conditions)
        {
            if (condition.Kind != JassValueKind.Handle || condition.Handle?.State is not ConditionState state)
            {
                throw new JassRuntimeException("trigger condition is not a condition handle");
            }

            var result = Vm!.InvokeCode(state.Code);
            if (result.Kind != JassValueKind.Boolean || !result.Boolean)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Runs the trigger in the current context; returns true when its actions ran.</summary>
    public bool ExecuteTrigger(TriggerState trigger, bool checkConditions = false)
    {
        if (trigger.Destroyed || !trigger.Enabled)
        {
            return false;
        }

        var ownsContext = Context is null;
        if (ownsContext)
        {
            _contexts.Add(new JassEventContext());
        }

        var context = Context!;
        var previousTrigger = context.Trigger;
        context.Trigger = trigger;
        try
        {
            trigger.Evaluations++;
            if (checkConditions && !EvaluateConditions(trigger))
            {
                return false;
            }

            foreach (var action in trigger.Actions)
            {
                Vm!.InvokeCode(action);
            }

            return true;
        }
        finally
        {
            context.Trigger = previousTrigger;
            if (ownsContext)
            {
                PopContext(context);
            }
        }
    }

    public JassEventRegistration Register(
        TriggerState trigger,
        string kind,
        string function,
        int eventId = -1,
        int playerId = -1,
        JassHandle? source = null,
        JassValue filter = default,
        string key = "",
        bool exact = false)
    {
        var registration = new JassEventRegistration
        {
            Kind = kind,
            Function = function,
            EventId = eventId,
            PlayerId = playerId,
            Source = source,
            Filter = filter,
            Key = key,
            Exact = exact,
            Trigger = trigger,
            Site = Vm?.CallerFunction ?? "<unknown>",
            Chain = Vm?.CallerChain(3) ?? string.Empty,
            Sequence = ++_registrationSequence,
        };
        trigger.Registrations.Add(registration);
        Registrations.Add(registration);
        return registration;
    }

    public void DestroyTrigger(TriggerState trigger)
    {
        trigger.Destroyed = true;
        trigger.Enabled = false;
        foreach (var registration in trigger.Registrations)
        {
            Registrations.Remove(registration);
        }

        trigger.Registrations.Clear();
    }

    /// <summary>Publishes typed events through matching registrations; returns actions executed.</summary>
    public int Dispatch(Func<JassEventRegistration, JassEvent?> select, TimerState? expiredTimer = null)
    {
        var executed = 0;
        foreach (var registration in Registrations.ToArray())
        {
            if (registration.Trigger.Destroyed || !registration.Trigger.Enabled)
            {
                continue;
            }

            var payload = select(registration);
            if (payload is null)
            {
                continue;
            }

            var context = new JassEventContext { Event = payload, ExpiredTimer = expiredTimer };
            _contexts.Add(context);
            registration.Dispatches++;
            try
            {
                if (registration.HasFilter && !EvaluateFilter(registration))
                {
                    continue;
                }

                ExecuteTrigger(registration.Trigger, checkConditions: true);
                registration.Callbacks++;
                EventCallbacksSucceeded++;
                executed++;
            }
            catch
            {
                registration.Failures++;
                EventCallbacksFailed++;
                EventFailures.Add($"{payload.Kind}:{payload.EventId} in {registration.Function} at {registration.Site}");
                throw;
            }
            finally
            {
                PopContext(context);
            }
        }

        return executed;
    }

    private bool EvaluateFilter(JassEventRegistration registration)
    {
        if (registration.Filter.Handle?.State is not ConditionState condition)
        {
            throw new JassRuntimeException("event filter is not a condition handle");
        }

        var result = Vm!.InvokeCode(condition.Code);
        return result.Kind == JassValueKind.Boolean && result.Boolean;
    }

    private void PopContext(JassEventContext context)
    {
        if (_contexts.Count == 0 || !ReferenceEquals(_contexts[^1], context))
        {
            throw new JassRuntimeException("event context stack corrupted");
        }

        _contexts.RemoveAt(_contexts.Count - 1);
    }

    private void RaiseEvent(string kind, int eventId)
    {
        var key = eventId < 0 ? kind : $"{kind}:{eventId}";
        EventsRaised[key] = EventsRaised.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    /// <summary>Runs timers due at the current time; returns the number of callbacks executed.</summary>
    public int RunDueTimers(int maxCallbacks = 10_000)
    {
        var executed = 0;
        for (var guard = 0; guard < maxCallbacks; guard++)
        {
            TimerState? due = null;
            foreach (var timer in Timers)
            {
                if (timer.Running && !timer.Destroyed && timer.RemainingMs <= 0 && (due is null || timer.FiredAtMs < due.FiredAtMs))
                {
                    due = timer;
                }
            }

            if (due is null)
            {
                return executed;
            }

            due.FiredAtMs = TimeMs;
            if (due.Periodic)
            {
                due.RemainingMs += due.PeriodMs;
            }
            else
            {
                due.Running = false;
            }

            executed++;
            TimerCallbacksFired++;
            RaiseEvent("timer", -1);
            var timerEvent = new JassEvent { Kind = "timer" };
            var timerContext = new JassEventContext { Event = timerEvent, ExpiredTimer = due };
            _contexts.Add(timerContext);
            try
            {
                if (due.Handle is not null)
                {
                    Dispatch(
                        registration => ReferenceEquals(registration.Source, due.Handle) &&
                                        registration.Kind is "timer" or "timer-expire"
                            ? timerEvent
                            : null,
                        due);
                }

                if (due.Callback.Kind == JassValueKind.Code)
                {
                    Vm!.InvokeCode(due.Callback);
                }
            }
            finally
            {
                PopContext(timerContext);
            }
        }

        throw new JassRuntimeException($"timer callbacks exceeded limit ({maxCallbacks})");
    }

    /// <summary>Dispatches a player chat message to registered chat triggers; returns fired count.</summary>
    public int DispatchChat(int playerId, string message)
    {
        RaiseEvent("chat", 16);
        var player = JassValue.FromHandle(Player(playerId));
        var matchedTriggers = new HashSet<TriggerState>();
        var fired = Dispatch(registration =>
        {
            if (registration.Kind != "chat" || registration.PlayerId != playerId)
            {
                return null;
            }

            var pattern = registration.Key;
            // WC3 treats an empty match string as "any chat"; the map's command processor
            // registers exactly this and parses commands from GetEventPlayerChatString.
            var match = pattern.Length == 0 ||
                        (registration.Exact
                            ? string.Equals(message, pattern, StringComparison.Ordinal)
                            : message.Contains(pattern, StringComparison.Ordinal));
            if (!match || !matchedTriggers.Add(registration.Trigger))
            {
                return null;
            }

            return new JassEvent
            {
                Kind = "chat",
                EventId = 16,
                PlayerId = playerId,
                Player = player,
                ChatString = message,
                ChatMatched = pattern,
            };
        });
        ChatEventsFired += fired;
        return fired;
    }

    /// <summary>Produces a dialog click event; the caller must have resolved the hit-test.</summary>
    public int DispatchDialogClick(DialogState dialog, DialogButtonState button, int playerId)
    {
        if (dialog.Destroyed)
        {
            return 0;
        }

        RaiseEvent("dialog", -1);
        dialog.Visible = false;
        var payload = new JassEvent
        {
            Kind = "dialog",
            PlayerId = playerId,
            Player = JassValue.FromHandle(Player(playerId)),
            ClickedButton = button.Handle is null ? JassValue.Null : JassValue.FromHandle(button.Handle),
            ClickedDialog = dialog.Handle is null ? JassValue.Null : JassValue.FromHandle(dialog.Handle),
        };
        DialogClicks.Add($"dialog={dialog.Message} button={button.Label} player={playerId}");
        return Dispatch(registration => registration.Kind switch
        {
            "dialog" when ReferenceEquals(registration.Source, dialog.Handle) => payload,
            "dialog-button" when ReferenceEquals(registration.Source, button.Handle) => payload,
            _ => null,
        });
    }

    public IReadOnlyList<UnitState> SelectedUnits(int playerId) =>
        _selection.TryGetValue(playerId, out var units) ? units : Array.Empty<UnitState>();

    /// <summary>Adds (or removes) a unit for one player; the unit owner must match the selecting player.</summary>
    public int SelectUnit(UnitState unit, int playerId, bool add)
    {
        if (unit.Removed || unit.Handle is null || unit.OwnerId != playerId)
        {
            return 0;
        }

        if (!add)
        {
            return DeselectUnit(unit, playerId);
        }

        if (!_selection.TryGetValue(playerId, out var units))
        {
            units = new List<UnitState>();
            _selection[playerId] = units;
        }

        if (units.Contains(unit))
        {
            return 0;
        }

        units.Add(unit);
        return RaiseSelection(unit, playerId, selected: true);
    }

    public int DeselectUnit(UnitState unit, int playerId)
    {
        if (!_selection.TryGetValue(playerId, out var units) || !units.Remove(unit))
        {
            return 0;
        }

        if (units.Count == 0)
        {
            _selection.Remove(playerId);
        }

        return RaiseSelection(unit, playerId, selected: false);
    }

    public int ClearSelection(int playerId)
    {
        if (!_selection.TryGetValue(playerId, out var units))
        {
            return 0;
        }

        var selected = units.ToArray();
        _selection.Remove(playerId);
        var fired = 0;
        foreach (var unit in selected)
        {
            fired += RaiseSelection(unit, playerId, selected: false);
        }

        return fired;
    }

    /// <summary>UI single-click semantics: replace the player's selection with one unit.</summary>
    public int SelectSingleUnit(UnitState unit, int playerId)
    {
        if (unit.Removed || unit.Handle is null || unit.OwnerId != playerId)
        {
            return 0;
        }

        var current = SelectedUnits(playerId);
        if (current.Count == 1 && ReferenceEquals(current[0], unit))
        {
            return 0;
        }

        return ClearSelection(playerId) + SelectUnit(unit, playerId, add: true);
    }

    public void RemoveUnit(UnitState unit)
    {
        foreach (var player in _selection.Keys.ToArray())
        {
            var units = _selection[player];
            units.Remove(unit);
            if (units.Count == 0)
            {
                _selection.Remove(player);
            }
        }

        unit.Removed = true;
    }

    private int RaiseSelection(UnitState unit, int playerId, bool selected)
    {
        if (unit.Handle is null)
        {
            return 0;
        }

        var unitValue = JassValue.FromHandle(unit.Handle);
        var playerEventId = selected ? 24 : 25;
        var unitEventId = selected ? 57 : 58;
        var playerPayload = new JassEvent
        {
            Kind = "player-unit-event",
            EventId = playerEventId,
            PlayerId = playerId,
            Player = JassValue.FromHandle(Player(playerId)),
            TriggerUnit = unitValue,
        };
        RaiseEvent("player-unit-event", playerEventId);
        var fired = Dispatch(registration =>
            registration.Kind == "player-unit-event" &&
            registration.EventId == playerEventId &&
            registration.PlayerId == playerId
                ? playerPayload
                : null);

        var unitPayload = playerPayload with { Kind = "unit-event", EventId = unitEventId };
        RaiseEvent("unit-event", unitEventId);
        fired += Dispatch(registration =>
            registration.Kind == "unit-event" &&
            registration.EventId == unitEventId &&
            ReferenceEquals(registration.Source, unit.Handle)
                ? unitPayload
                : null);
        return fired;
    }

    public void Advance(double milliseconds)
    {
        TimeMs += milliseconds;
        foreach (var timer in Timers)
        {
            if (timer.Running && !timer.Destroyed)
            {
                timer.RemainingMs -= milliseconds;
            }
        }
    }
}
