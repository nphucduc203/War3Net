using War3Engine.Scripting;

namespace War3Engine.Client;

/// <summary>Engine input-dispatch layer: maps a UI action to a runtime event (no OS input).</summary>
public static class ViewerInput
{
    public static int DispatchChatHotkey(JassGameRuntime runtime, string? chatMessage, int playerId = 0)
    {
        var fired = runtime.DispatchChat(playerId, chatMessage ?? "T5");
        runtime.Advance(0);
        runtime.RunDueTimers();
        return fired;
    }

    public static int DispatchDialogClick(JassGameRuntime runtime, DialogState dialog, DialogButtonState button, int playerId = 0)
    {
        var fired = runtime.DispatchDialogClick(dialog, button, playerId);
        runtime.Advance(0);
        runtime.RunDueTimers();
        return fired;
    }

    public static int SelectUnit(JassGameRuntime runtime, UnitState unit, int playerId = 0)
    {
        var fired = runtime.SelectSingleUnit(unit, playerId);
        runtime.Advance(0);
        runtime.RunDueTimers();
        return fired;
    }

    public static int ClearSelection(JassGameRuntime runtime, int playerId = 0)
    {
        var fired = runtime.ClearSelection(playerId);
        runtime.Advance(0);
        runtime.RunDueTimers();
        return fired;
    }
}
