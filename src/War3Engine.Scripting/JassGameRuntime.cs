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
    public List<string> Events { get; } = new();
    public bool Enabled { get; set; } = true;
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
    public TriggerState? Trigger { get; set; }
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

public sealed class DialogState
{
    public string Message { get; set; } = string.Empty;
    public List<JassHandle> Buttons { get; } = new();
    public bool Visible { get; set; }
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

    public TriggerState? CurrentTrigger { get; set; }

    public TriggerState? UiTrigger { get; set; }

    public int TimerCallbacksFired { get; set; }

    public List<string> LoadedTocs { get; } = new();

    public List<string> SyncLog { get; } = new();

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

    public void ExecuteTrigger(TriggerState trigger, bool checkConditions = false)
    {
        if (!trigger.Enabled)
        {
            return;
        }

        trigger.Evaluations++;
        if (checkConditions && !EvaluateConditions(trigger))
        {
            return;
        }

        var previous = CurrentTrigger;
        CurrentTrigger = trigger;
        try
        {
            foreach (var action in trigger.Actions)
            {
                Vm!.InvokeCode(action);
            }
        }
        finally
        {
            CurrentTrigger = previous;
        }
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
            if (due.Trigger is not null)
            {
                ExecuteTrigger(due.Trigger, checkConditions: true);
            }
            else if (due.Callback.Kind == JassValueKind.Code)
            {
                Vm!.InvokeCode(due.Callback);
            }
        }

        throw new JassRuntimeException($"timer callbacks exceeded limit ({maxCallbacks})");
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
