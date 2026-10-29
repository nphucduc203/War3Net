using War3Engine.Assets;
using War3Engine.Client;
using War3Engine.Core;
using War3Engine.Scripting;
using War3Engine.Scripting.Lua;
using Xunit;

namespace War3Engine.Tests;

public class MapGameplayScenarioTests
{
    [Fact]
    public void TargetMapRunsTimerLuaBridgeChatAndDialogClick()
    {
        var settings = EngineSettings.Load();
        if (!File.Exists(settings.YdweMapPath))
        {
            Console.WriteLine($"SKIPPED (YDWE map not found): {settings.YdweMapPath}");
            return;
        }

        using var store = new AssetStore(settings.WarcraftPath);
        store.MountMap(settings.YdweMapPath);
        var common = System.Text.Encoding.UTF8.GetString(store.ReadAllBytes(@"Scripts\common.j", out _));
        var blizzard = System.Text.Encoding.UTF8.GetString(store.ReadAllBytes(@"Scripts\Blizzard.j", out _));
        var map = System.Text.Encoding.UTF8.GetString(store.ReadAllBytes("war3map.j", out _));

        var slk = JassSlkQueryProvider.Load(store, settings.YdweMapPath);
        var lua = new NLuaBridge(slk.Query);
        var result = JassMapHost.Run(common, blizzard, map, 5_000_000, advanceMs: 3000, lua: lua, chatMessage: "T5");

        if (result.Failure is UnsupportedNativeException unsupported)
        {
            Assert.Fail($"unsupported native on gameplay path: {unsupported.NativeName} ({unsupported.Signature})\n{unsupported.Describe()}");
        }

        Assert.Null(result.Failure);
        Assert.True(result.MainCompleted);
        Assert.True(result.TimerCallbacks > 0, "no timer callbacks executed");
        Assert.True(lua.History.Count > 0, "EXExecuteScript was not called");
        Assert.DoesNotContain(lua.History, h => h.ErrorKind != LuaErrorKind.None);
        Assert.True(result.ChatFired >= 1, "chat trigger was not dispatched");
        Assert.True(result.Vm.TryGetGlobalValue("udg_guaibs", out var wave));
        Assert.Equal(5, wave.Integer);
        Assert.NotEmpty(result.Runtime.DisplayMessages);

        var art = MapArtCache.Load(store, settings.YdweMapPath);
        var resolved = result.Runtime.Units.Where(u => !u.Removed).Count(u => art.ResolveUnit(u.TypeId) is not null);
        Assert.True(resolved > 0, "no runtime unit model resolved");

        var dialog = result.Runtime.Dialogs.LastOrDefault(d =>
            d.Visible && !d.Destroyed && d.PlayerId == 0 && d.Buttons.Count > 0);
        Assert.NotNull(dialog);
        var messagesBefore = result.Runtime.DisplayMessages.Count;
        var firedDialog = result.Runtime.DispatchDialogClick(dialog!, dialog!.Buttons[0], 0);
        result.Runtime.Advance(0);
        result.Runtime.RunDueTimers();
        Assert.True(firedDialog >= 1, "dialog click did not dispatch");
        Assert.True(result.Vm.TryGetGlobalValue("udg_yxms", out var yxms) && yxms.Boolean, "udg_yxms was not set by the dialog callback");
        Assert.True(result.Runtime.DisplayMessages.Count > messagesBefore, "dialog callback did not display a message");
        Assert.False(dialog.Visible, "clicked dialog was not hidden");
        Assert.Equal(0, result.Runtime.EventDepth);
        Assert.Equal(0, result.Runtime.EventCallbacksFailed);
        Assert.Contains(result.Runtime.Registrations, r => r.Kind == "dialog" && r.Callbacks > 0);

        var unit = result.Runtime.Units.First(u => !u.Removed && u.Handle is not null);
        var selectionTrigger = GetTrigger(result, "gg_trg____________________005");
        var selectionEvaluations = selectionTrigger.Evaluations;
        var wrongPlayer = unit.OwnerId == 0 ? 1 : 0;
        Assert.Equal(0, ViewerInput.SelectUnit(result.Runtime, unit, wrongPlayer));
        Assert.Equal(selectionEvaluations, selectionTrigger.Evaluations);

        var selectionFired = ViewerInput.SelectUnit(result.Runtime, unit, unit.OwnerId);
        Assert.True(selectionFired >= 1, "selection event did not dispatch");
        Assert.Contains(unit, result.Runtime.SelectedUnits(unit.OwnerId));
        Assert.Equal(selectionEvaluations + 1, selectionTrigger.Evaluations);
        Assert.Same(unit.Handle, LoadSelectedUnitHandle(result, unit.OwnerId));
        Assert.Equal(0, result.Runtime.EventDepth);

        ViewerInput.ClearSelection(result.Runtime, unit.OwnerId);
        Assert.Empty(result.Runtime.SelectedUnits(unit.OwnerId));

        Console.WriteLine($"scenario: callbacks={result.TimerCallbacks} lua={lua.History.Count} chat={result.ChatFired} wave={wave.Integer} units={result.UnitsAlive} renderedArt={resolved} messages={result.Runtime.DisplayMessages.Count} dialogFired={firedDialog} yxms={yxms.Boolean} selectionFired={selectionFired} selectedOwner={unit.OwnerId}");
    }

    private static TriggerState GetTrigger(JassMapRunResult result, string name)
    {
        Assert.True(result.Vm.TryGetGlobalValue(name, out var value));
        Assert.IsType<TriggerState>(value.Handle?.State);
        return (TriggerState)value.Handle!.State!;
    }

    private static JassHandle? LoadSelectedUnitHandle(JassMapRunResult result, int playerId)
    {
        Assert.True(result.Vm.TryGetGlobalValue("YDHT", out var tableValue));
        var table = Assert.IsType<HashtableState>(tableValue.Handle?.State);
        return table.Handles.TryGetValue(HashtableState.Key(result.Runtime.Player(playerId).Id, 0x2C1C8DD5), out var handle)
            ? handle
            : null;
    }
}
