using War3Engine.Client;
using War3Engine.Scripting;
using Xunit;

namespace War3Engine.Tests;

public class EventDispatchTests
{
    private const string Fixture = """
        native CreateTrigger takes nothing returns trigger
        native DestroyTrigger takes trigger whichTrigger returns nothing
        native DisableTrigger takes trigger whichTrigger returns nothing
        native EnableTrigger takes trigger whichTrigger returns nothing
        native TriggerAddAction takes trigger whichTrigger, code actionFunc returns triggeraction
        native TriggerAddCondition takes trigger whichTrigger, boolexpr condition returns triggercondition
        native Condition takes code func returns conditionfunc
        native Filter takes code func returns filterfunc
        native TriggerExecute takes trigger whichTrigger returns nothing
        native TriggerRegisterDialogEvent takes trigger whichTrigger, dialog whichDialog returns event
        native TriggerRegisterDialogButtonEvent takes trigger whichTrigger, button whichButton returns event
        native DialogCreate takes nothing returns dialog
        native DialogAddButton takes dialog whichDialog, string buttonText, integer hotkey returns button
        native DialogSetMessage takes dialog whichDialog, string messageText returns nothing
        native DialogDisplay takes player whichPlayer, dialog whichDialog, boolean flag returns nothing
        native GetClickedButton takes nothing returns button
        native GetClickedDialog takes nothing returns dialog
        native GetTriggerPlayer takes nothing returns player
        native GetTriggerUnit takes nothing returns unit
        native GetTriggeringTrigger takes nothing returns trigger
        native Player takes integer number returns player
        native GetPlayerId takes player whichPlayer returns integer
        native BoomNative takes nothing returns nothing
        native TestNestedEvent takes nothing returns nothing
        native TriggerRegisterPlayerChatEvent takes trigger whichTrigger, player whichPlayer, string chatMessage, boolean exactMatchOnly returns event
        native GetEventPlayerChatString takes nothing returns string
        native CreateUnit takes player whichPlayer, integer unitId, real x, real y, real facing returns unit
        native GetUnitTypeId takes unit whichUnit returns integer
        native SelectUnit takes unit whichUnit, boolean flag returns nothing
        native ClearSelection takes nothing returns nothing
        native RemoveUnit takes unit whichUnit returns nothing
        native ConvertPlayerUnitEvent takes integer i returns playerunitevent
        native ConvertUnitEvent takes integer i returns unitevent
        native TriggerRegisterPlayerUnitEvent takes trigger whichTrigger, player whichPlayer, playerunitevent whichEvent, boolexpr filter returns event
        native TriggerRegisterUnitEvent takes trigger whichTrigger, unit whichUnit, unitevent whichEvent returns event

        globals
            dialog D
            dialog D2
            button B
            button B2
            trigger T
            trigger T2
            trigger T3
            trigger T4
            trigger T5
            trigger T6
            trigger T7
            trigger T8
            trigger T9
            trigger T10
            trigger T11
            unit U
            unit U2
            integer SelectionRuns = 0
            integer UnitSelectionRuns = 0
            integer DeselectionRuns = 0
            integer FilterSelectionRuns = 0
            integer SelectedUnitId = 0
            integer SelectedPlayerId = -1
            string Order = ""
            boolean ActionSawCondition = false
            integer ConditionRuns = 0
            integer ActionRuns = 0
            integer SecondRuns = 0
            integer NestedRuns = 0
            integer PlayerSeen = -1
            boolean NestedButtonOk = false
            boolean NestedTriggerOk = false
            boolean AfterNestedButtonOk = false
            boolean OutsideButtonNull = false
            boolean OutsidePlayerNull = false
            boolean OutsideDialogNull = false
            boolean FilterRuns = false
            boolean NestedEventRan = false
            boolean NestedEventContextNull = false
            boolean OuterAfterNestedEventOk = false
            string NestedChatSeen = ""
        endglobals

        function Action takes nothing returns nothing
            set ActionRuns = ActionRuns + 1
            set ActionSawCondition = (ConditionRuns == 1)
            set Order = Order + "A"
            set PlayerSeen = GetPlayerId(GetTriggerPlayer())
        endfunction

        function SecondAction takes nothing returns nothing
            set SecondRuns = SecondRuns + 1
            set Order = Order + "B"
        endfunction

        function Boom takes nothing returns nothing
            call BoomNative()
        endfunction

        function NestedEventAction takes nothing returns nothing
            call TestNestedEvent()
            set OuterAfterNestedEventOk = (GetClickedButton() == B)
        endfunction

        function NestedChatAction takes nothing returns nothing
            set NestedEventRan = true
            set NestedEventContextNull = (GetClickedButton() == null)
            set NestedChatSeen = GetEventPlayerChatString()
        endfunction

        function SelectionAction takes nothing returns nothing
            set SelectionRuns = SelectionRuns + 1
            set SelectedUnitId = GetUnitTypeId(GetTriggerUnit())
            set SelectedPlayerId = GetPlayerId(GetTriggerPlayer())
        endfunction

        function UnitSelectionAction takes nothing returns nothing
            set UnitSelectionRuns = UnitSelectionRuns + 1
        endfunction

        function DeselectionAction takes nothing returns nothing
            set DeselectionRuns = DeselectionRuns + 1
        endfunction

        function FilterSelectionAction takes nothing returns nothing
            set FilterSelectionRuns = FilterSelectionRuns + 1
        endfunction

        function SelectUnitJass takes nothing returns nothing
            call SelectUnit(U, true)
        endfunction

        function ClearSelectionJass takes nothing returns nothing
            call ClearSelection()
        endfunction

        function NestedAction takes nothing returns nothing
            set NestedRuns = NestedRuns + 1
            set NestedButtonOk = (GetClickedButton() == B)
            set NestedTriggerOk = (GetTriggeringTrigger() == T2)
        endfunction

        function Cond takes nothing returns nothing
            set ConditionRuns = ConditionRuns + 1
            call TriggerExecute(T2)
            set AfterNestedButtonOk = (GetClickedButton() == B)
        endfunction

        function FilterFalse takes nothing returns boolean
            set FilterRuns = true
            return false
        endfunction

        function CheckOutside takes nothing returns nothing
            set OutsideButtonNull = (GetClickedButton() == null)
            set OutsidePlayerNull = (GetTriggerPlayer() == null)
            set OutsideDialogNull = (GetClickedDialog() == null)
        endfunction

        function DisableMain takes nothing returns nothing
            call DisableTrigger(T)
        endfunction

        function EnableMain takes nothing returns nothing
            call EnableTrigger(T)
        endfunction

        function DestroyMain takes nothing returns nothing
            call DestroyTrigger(T)
        endfunction

        function main takes nothing returns nothing
            set D = DialogCreate()
            call DialogSetMessage(D, "Fixture")
            set B = DialogAddButton(D, "Go", 0)
            set B2 = DialogAddButton(D, "Other", 0)
            call DialogDisplay(Player(0), D, true)
            set T = CreateTrigger()
            call TriggerRegisterDialogEvent(T, D)
            call TriggerAddCondition(T, Condition(function Cond))
            set T2 = CreateTrigger()
            call TriggerAddAction(T2, function NestedAction)
            set T3 = CreateTrigger()
            call TriggerAddAction(T3, function Action)
            call TriggerRegisterDialogEvent(T3, D)
            set T4 = CreateTrigger()
            call TriggerAddAction(T4, function SecondAction)
            set T5 = CreateTrigger()
            call TriggerAddAction(T5, function Boom)
            set T6 = CreateTrigger()
            call TriggerAddAction(T6, function NestedEventAction)
            set T7 = CreateTrigger()
            call TriggerAddAction(T7, function NestedChatAction)
            call TriggerRegisterPlayerChatEvent(T7, Player(0), "", true)
            set U = CreateUnit(Player(0), 'hfoo', 0.0, 0.0, 0.0)
            set U2 = CreateUnit(Player(0), 'hfoo', 128.0, 0.0, 0.0)
            set T8 = CreateTrigger()
            call TriggerRegisterPlayerUnitEvent(T8, Player(0), ConvertPlayerUnitEvent(24), null)
            call TriggerAddAction(T8, function SelectionAction)
            set T9 = CreateTrigger()
            call TriggerRegisterUnitEvent(T9, U, ConvertUnitEvent(57))
            call TriggerAddAction(T9, function UnitSelectionAction)
            set T10 = CreateTrigger()
            call TriggerRegisterPlayerUnitEvent(T10, Player(0), ConvertPlayerUnitEvent(25), null)
            call TriggerAddAction(T10, function DeselectionAction)
            set T11 = CreateTrigger()
            call TriggerAddAction(T11, function FilterSelectionAction)
            call CheckOutside()
        endfunction
        """;

    private static JassMapRunResult RunFixture()
    {
        var result = JassMapHost.Run(string.Empty, string.Empty, Fixture, maxSteps: 1_000_000);
        Assert.Null(result.Failure);
        Assert.True(result.MainCompleted);
        return result;
    }

    private static (DialogState Dialog, DialogButtonState Button) Visible(JassMapRunResult result)
    {
        var dialog = Assert.Single(result.Runtime.Dialogs);
        Assert.True(dialog.Visible);
        Assert.Equal(2, dialog.Buttons.Count);
        return (dialog, dialog.Buttons[0]);
    }

    [Fact]
    public void DialogClickDispatchesConditionAndNestedTriggerWithContextRestoration()
    {
        var result = RunFixture();
        var (dialog, button) = Visible(result);

        Assert.True(GetBool(result, "OutsideButtonNull"));
        Assert.True(GetBool(result, "OutsidePlayerNull"));
        Assert.True(GetBool(result, "OutsideDialogNull"));

        var fired = result.Runtime.DispatchDialogClick(dialog, button, 0);

        Assert.Equal(2, fired);
        Assert.Equal(1, GetInt(result, "ConditionRuns"));
        Assert.Equal(1, GetInt(result, "NestedRuns"));
        Assert.True(GetBool(result, "NestedButtonOk"));
        Assert.True(GetBool(result, "NestedTriggerOk"));
        Assert.True(GetBool(result, "AfterNestedButtonOk"));
        Assert.Equal(0, GetInt(result, "PlayerSeen"));
        Assert.Equal(0, result.Runtime.EventCallbacksFailed);
        Assert.Equal(2, result.Runtime.EventCallbacksSucceeded);
        Assert.False(dialog.Visible);
        Assert.Equal(0, result.Runtime.EventDepth);

        result.Vm.Invoke("CheckOutside");
        Assert.True(GetBool(result, "OutsideButtonNull"));
        Assert.True(GetBool(result, "OutsidePlayerNull"));
        Assert.True(GetBool(result, "OutsideDialogNull"));
    }

    [Fact]
    public void EngineInputAdapterDispatchesDialogClick()
    {
        var result = RunFixture();
        var (dialog, button) = Visible(result);

        var fired = ViewerInput.DispatchDialogClick(result.Runtime, dialog, button);

        Assert.Equal(2, fired);
        Assert.Equal(1, GetInt(result, "ConditionRuns"));
        Assert.False(dialog.Visible);
        Assert.Equal(0, result.Runtime.EventDepth);
    }

    [Fact]
    public void DisabledTriggerDoesNotDispatchAndEnableRestoresIt()
    {
        var result = RunFixture();
        var (dialog, button) = Visible(result);

        result.Vm.Invoke("DisableMain");
        result.Runtime.DispatchDialogClick(dialog, button, 0);
        Assert.Equal(0, GetInt(result, "ConditionRuns"));

        result.Vm.Invoke("EnableMain");
        result.Runtime.DispatchDialogClick(dialog, button, 0);
        Assert.Equal(1, GetInt(result, "ConditionRuns"));
    }

    [Fact]
    public void DestroyedTriggerRemovesRegistrationsAndCannotFire()
    {
        var result = RunFixture();
        var (dialog, button) = Visible(result);
        var trigger = GetTrigger(result, "T");
        var registered = result.Runtime.Registrations.Single(r => r.Kind == "dialog" && r.Trigger == trigger);

        result.Vm.Invoke("DestroyMain");
        Assert.True(trigger.Destroyed);
        Assert.DoesNotContain(registered, result.Runtime.Registrations);
        result.Runtime.DispatchDialogClick(dialog, button, 0);
        Assert.Equal(0, GetInt(result, "ConditionRuns"));
    }

    [Fact]
    public void EventFilterBlocksCallbackAndRunsInContext()
    {
        var result = RunFixture();
        var (dialog, button) = Visible(result);
        var trigger = GetTrigger(result, "T4");
        var filter = result.Vm.Invoke("Filter", JassValue.FromCode("FilterFalse"));
        var registration = result.Runtime.Register(trigger, "dialog", "test", source: dialog.Handle, filter: filter);

        result.Runtime.DispatchDialogClick(dialog, button, 0);

        Assert.True(GetBool(result, "FilterRuns"));
        Assert.Equal(0, GetInt(result, "SecondRuns"));
        Assert.Equal(1, registration.Dispatches);
        Assert.Equal(0, registration.Callbacks);
        Assert.Equal(1, GetInt(result, "ConditionRuns"));
    }

    [Fact]
    public void DialogButtonRegistrationMatchesOnlyItsButton()
    {
        var result = RunFixture();
        var (dialog, button) = Visible(result);
        var buttonTrigger = new TriggerState();
        var registration = result.Runtime.Register(buttonTrigger, "dialog-button", "test", source: dialog.Buttons[1].Handle);

        result.Runtime.DispatchDialogClick(dialog, dialog.Buttons[1], 0);
        Assert.Equal(1, registration.Dispatches);
        Assert.Equal(1, registration.Callbacks);

        dialog.Visible = true;
        result.Runtime.DispatchDialogClick(dialog, button, 0);
        Assert.Equal(1, registration.Dispatches);
        Assert.Equal(1, registration.Callbacks);
    }

    [Fact]
    public void TimerRegistrationDispatchesTriggerWithExpiredTimerContext()
    {
        var result = RunFixture();
        var (dialog, _) = Visible(result);
        var trigger = GetTrigger(result, "T4");
        var timer = new TimerState
        {
            RemainingMs = 100,
            PeriodMs = 100,
            Periodic = false,
            Running = true,
        };
        timer.Handle = new JassHandle("timer", timer);
        timer.HandleId = timer.Handle.Id;
        result.Runtime.Timers.Add(timer);
        result.Runtime.Register(trigger, "timer", "test", source: timer.Handle);

        result.Runtime.Advance(100);
        var executed = result.Runtime.RunDueTimers();

        Assert.Equal(1, executed);
        Assert.Equal(1, GetInt(result, "SecondRuns"));
        Assert.Equal(0, result.Runtime.EventDepth);
    }

    [Fact]
    public void NestedEventDispatchRestoresOuterContext()
    {
        var result = RunFixture();
        var (dialog, button) = Visible(result);
        var trigger = GetTrigger(result, "T6");
        var registration = result.Runtime.Register(trigger, "dialog", "test", source: dialog.Handle);
        result.Registry.Register("TestNestedEvent", "nothing", Array.Empty<string>(), (_, _) =>
        {
            result.Runtime.DispatchChat(0, "nested");
            return JassValue.Null;
        });

        result.Runtime.DispatchDialogClick(dialog, button, 0);

        Assert.True(GetBool(result, "NestedEventRan"));
        Assert.True(GetBool(result, "NestedEventContextNull"));
        Assert.Equal("nested", GetString(result, "NestedChatSeen"));
        Assert.True(GetBool(result, "OuterAfterNestedEventOk"));
        Assert.Equal(1, registration.Callbacks);
        Assert.Equal(0, result.Runtime.EventDepth);
    }

    [Fact]
    public void RegistrationsDispatchInRegistrationOrderAfterConditions()
    {
        var result = RunFixture();
        var (dialog, button) = Visible(result);
        var second = GetTrigger(result, "T4");
        result.Runtime.Register(second, "dialog", "test", source: dialog.Handle);

        result.Runtime.DispatchDialogClick(dialog, button, 0);

        Assert.Equal("AB", GetString(result, "Order"));
        Assert.True(GetBool(result, "ActionSawCondition"));
    }

    [Fact]
    public void FailingCallbackRestoresContextAndLeavesBusUsable()
    {
        var result = RunFixture();
        var (dialog, button) = Visible(result);
        var failing = GetTrigger(result, "T5");
        var registration = result.Runtime.Register(failing, "dialog", "test", source: dialog.Handle);

        Assert.Throws<UnsupportedNativeException>(() => result.Runtime.DispatchDialogClick(dialog, button, 0));

        Assert.Equal(1, registration.Failures);
        Assert.Equal(1, result.Runtime.EventCallbacksFailed);
        Assert.Equal(0, result.Runtime.EventDepth);

        result.Runtime.DestroyTrigger(failing);
        dialog.Visible = true;
        Assert.Equal(2, result.Runtime.DispatchDialogClick(dialog, button, 0));
        Assert.Equal(0, result.Runtime.EventDepth);
    }

    [Fact]
    public void SelectingOwnUnitDispatchesPlayerUnitAndUnitScopedEvents()
    {
        var result = RunFixture();
        var unit = GetUnit(result, "U");

        var fired = result.Runtime.SelectSingleUnit(unit, 0);

        Assert.Equal(2, fired);
        Assert.Equal(1, GetInt(result, "SelectionRuns"));
        Assert.Equal(1, GetInt(result, "UnitSelectionRuns"));
        Assert.Equal(0x6F6F6668, GetInt(result, "SelectedUnitId"));
        Assert.Equal(0, GetInt(result, "SelectedPlayerId"));
        Assert.Contains(unit, result.Runtime.SelectedUnits(0));
        Assert.Equal(0, result.Runtime.EventDepth);

        Assert.Equal(0, result.Runtime.SelectSingleUnit(unit, 0));
        Assert.Equal(1, GetInt(result, "SelectionRuns"));
        Assert.Equal(0, result.Runtime.SelectSingleUnit(unit, 1));
        Assert.Equal(1, GetInt(result, "SelectionRuns"));
    }

    [Fact]
    public void JassSelectUnitNativeProducesSelectionEvent()
    {
        var result = RunFixture();
        var unit = GetUnit(result, "U");

        result.Vm.Invoke("SelectUnitJass");

        Assert.Equal(1, GetInt(result, "SelectionRuns"));
        Assert.Equal(1, GetInt(result, "UnitSelectionRuns"));
        Assert.Contains(unit, result.Runtime.SelectedUnits(0));

        result.Vm.Invoke("ClearSelectionJass");
        Assert.Empty(result.Runtime.SelectedUnits(0));
        Assert.Equal(1, GetInt(result, "DeselectionRuns"));
    }

    [Fact]
    public void DeselectingAndRemovingSelectionCleansState()
    {
        var result = RunFixture();
        var unit = GetUnit(result, "U");
        var second = GetUnit(result, "U2");

        result.Runtime.SelectUnit(unit, 0, add: true);
        result.Runtime.SelectUnit(second, 0, add: true);
        Assert.Equal(2, result.Runtime.SelectedUnits(0).Count);
        Assert.Equal(2, GetInt(result, "SelectionRuns"));

        result.Runtime.DeselectUnit(second, 0);
        Assert.Single(result.Runtime.SelectedUnits(0));
        Assert.Equal(1, GetInt(result, "DeselectionRuns"));

        result.Vm.Invoke("RemoveUnit", JassValue.FromHandle(unit.Handle!));
        Assert.True(unit.Removed);
        Assert.Empty(result.Runtime.SelectedUnits(0));
    }

    [Fact]
    public void SelectionFilterBlocksCallback()
    {
        var result = RunFixture();
        var unit = GetUnit(result, "U");
        var filter = result.Vm.Invoke("Filter", JassValue.FromCode("FilterFalse"));
        var registration = result.Runtime.Register(GetTrigger(result, "T11"), "player-unit-event", "test", eventId: 24, playerId: 0, filter: filter);

        result.Runtime.SelectSingleUnit(unit, 0);

        Assert.True(GetBool(result, "FilterRuns"));
        Assert.Equal(0, GetInt(result, "FilterSelectionRuns"));
        Assert.Equal(1, registration.Dispatches);
        Assert.Equal(0, registration.Callbacks);
        Assert.Equal(1, GetInt(result, "SelectionRuns"));
    }

    private static TriggerState GetTrigger(JassMapRunResult result, string name)
    {
        Assert.True(result.Vm.TryGetGlobalValue(name, out var value));
        Assert.IsType<TriggerState>(value.Handle?.State);
        return (TriggerState)value.Handle!.State!;
    }

    private static UnitState GetUnit(JassMapRunResult result, string name)
    {
        Assert.True(result.Vm.TryGetGlobalValue(name, out var value));
        Assert.IsType<UnitState>(value.Handle?.State);
        return (UnitState)value.Handle!.State!;
    }

    private static int GetInt(JassMapRunResult result, string name)
    {
        Assert.True(result.Vm.TryGetGlobalValue(name, out var value));
        return value.Integer;
    }

    private static string GetString(JassMapRunResult result, string name)
    {
        Assert.True(result.Vm.TryGetGlobalValue(name, out var value));
        return value.Text ?? string.Empty;
    }

    private static bool GetBool(JassMapRunResult result, string name)
    {
        Assert.True(result.Vm.TryGetGlobalValue(name, out var value));
        return value.Boolean;
    }
}
