namespace War3Engine.Scripting;

/// <summary>
/// Standard Warcraft III natives required by the target map's initialization path.
/// Every registration here has observable state or a documented headless behavior;
/// anything else stays unregistered and fails loudly.
/// </summary>
public static class StandardNatives
{
    public static void Register(JassNativeRegistry registry, JassGameRuntime runtime)
    {
        void N(string name, string returns, string[] parameters, JassNativeImplementation impl, string sideEffects = "")
        {
            var native = new JassNative(name, returns, parameters, (vm, args) =>
            {
                runtime.CountNative(name);
                return impl(vm, args);
            }, "implemented", sideEffects);
            registry.Register(native);
        }

        var noParams = Array.Empty<string>();

        // Handles / hashtables -------------------------------------------------
        N("InitHashtable", "hashtable", noParams, (_, _) => Handle("hashtable", new HashtableState()));
        N("StringHash", "integer", new[] { "string" }, (_, a) => JassValue.FromInteger(Fnv1a(a[0].Text ?? string.Empty)));
        N("GetHandleId", "integer", new[] { "handle" }, (_, a) => JassValue.FromInteger(a[0].Handle?.Id ?? 0));
        N("SaveInteger", "nothing", new[] { "hashtable", "integer", "integer", "integer" },
            (_, a) => Save(a, (t, key) => t.Integers[key] = a[3].Integer));
        N("LoadInteger", "integer", new[] { "hashtable", "integer", "integer" },
            (_, a) => JassValue.FromInteger(Hashtable(a[0]).Integers.TryGetValue(HashtableState.Key(a[1].Integer, a[2].Integer), out var v) ? v : 0));
        N("HaveSavedInteger", "boolean", new[] { "hashtable", "integer", "integer" },
            (_, a) => JassValue.FromBoolean(Hashtable(a[0]).Integers.ContainsKey(HashtableState.Key(a[1].Integer, a[2].Integer))));
        N("RemoveSavedInteger", "nothing", new[] { "hashtable", "integer", "integer" },
            (_, a) => { Hashtable(a[0]).Integers.Remove(HashtableState.Key(a[1].Integer, a[2].Integer)); return JassValue.Null; });
        N("FlushChildHashtable", "nothing", new[] { "hashtable", "integer" },
            (_, a) =>
            {
                Hashtable(a[0]).RemovePrefix(a[1].Integer);
                return JassValue.Null;
            });
        N("FlushParentHashtable", "nothing", new[] { "hashtable" },
            (_, a) =>
            {
                var table = Hashtable(a[0]);
                table.Integers.Clear();
                table.Reals.Clear();
                table.Booleans.Clear();
                table.Strings.Clear();
                table.Handles.Clear();
                return JassValue.Null;
            });
        N("SaveReal", "nothing", new[] { "hashtable", "integer", "integer", "real" },
            (_, a) => Save(a, (t, key) => t.Reals[key] = a[3].Real));
        N("LoadReal", "real", new[] { "hashtable", "integer", "integer" },
            (_, a) => JassValue.FromReal(Hashtable(a[0]).Reals.TryGetValue(HashtableState.Key(a[1].Integer, a[2].Integer), out var v) ? v : 0f));
        N("SaveBoolean", "nothing", new[] { "hashtable", "integer", "integer", "boolean" },
            (_, a) => Save(a, (t, key) => t.Booleans[key] = a[3].Boolean));
        N("LoadBoolean", "boolean", new[] { "hashtable", "integer", "integer" },
            (_, a) => JassValue.FromBoolean(Hashtable(a[0]).Booleans.TryGetValue(HashtableState.Key(a[1].Integer, a[2].Integer), out var v) && v));
        N("SaveStr", "nothing", new[] { "hashtable", "integer", "integer", "string" },
            (_, a) => Save(a, (t, key) => t.Strings[key] = a[3].Text ?? string.Empty));
        N("LoadStr", "string", new[] { "hashtable", "integer", "integer" },
            (_, a) => JassValue.FromString(Hashtable(a[0]).Strings.TryGetValue(HashtableState.Key(a[1].Integer, a[2].Integer), out var v) ? v : string.Empty));

        // Players --------------------------------------------------------------
        N("Player", "player", new[] { "integer" }, (_, a) => JassValue.FromHandle(runtime.Player(a[0].Integer)));
        N("GetPlayerId", "integer", new[] { "player" }, (_, a) => JassValue.FromInteger(PlayerId(a[0])));
        N("GetPlayerName", "string", new[] { "player" }, (_, a) => JassValue.FromString($"Player {PlayerId(a[0])}"));
        N("GetLocalPlayer", "player", noParams, (_, _) => JassValue.FromHandle(runtime.Player(0)));
        N("GetPlayerController", "playermapcontrol", new[] { "player" }, (_, _) => JassValue.FromHandle(JassGameRuntime.EnumHandle("playermapcontrol", 0)));
        N("GetPlayerSlotState", "playerslotstate", new[] { "player" }, (_, _) => JassValue.FromHandle(JassGameRuntime.EnumHandle("playerslotstate", 2)));

        // Triggers -------------------------------------------------------------
        N("CreateTrigger", "trigger", noParams, (_, _) =>
        {
            var state = new TriggerState();
            var handle = new JassHandle("trigger", state);
            state.Handle = handle;
            runtime.Triggers.Add(state);
            return JassValue.FromHandle(handle);
        });
        N("DestroyTrigger", "nothing", new[] { "trigger" }, (_, a) => { runtime.DestroyTrigger(Trigger(a[0])); return JassValue.Null; });
        N("TriggerAddAction", "triggeraction", new[] { "trigger", "code" },
            (_, a) => { Trigger(a[0]).Actions.Add(a[1]); return Handle("triggeraction", null); });
        N("TriggerAddCondition", "triggercondition", new[] { "trigger", "boolexpr" },
            (_, a) => { Trigger(a[0]).Conditions.Add(a[1]); return Handle("triggercondition", null); });
        N("Condition", "conditionfunc", new[] { "code" }, (_, a) => Handle("conditionfunc", new ConditionState(a[0])));
        N("Filter", "filterfunc", new[] { "code" }, (_, a) => Handle("filterfunc", new ConditionState(a[0])));
        N("DestroyBoolExpr", "nothing", new[] { "boolexpr" }, (_, _) => JassValue.Null);
        N("TriggerEvaluate", "boolean", new[] { "trigger" }, (_, a) => JassValue.FromBoolean(runtime.EvaluateConditions(Trigger(a[0]))));
        N("TriggerExecute", "nothing", new[] { "trigger" }, (_, a) => { runtime.ExecuteTrigger(Trigger(a[0])); return JassValue.Null; });
        N("EnableTrigger", "nothing", new[] { "trigger" }, (_, a) => { Trigger(a[0]).Enabled = true; return JassValue.Null; });
        N("DisableTrigger", "nothing", new[] { "trigger" }, (_, a) => { Trigger(a[0]).Enabled = false; return JassValue.Null; });
        N("IsTriggerEnabled", "boolean", new[] { "trigger" }, (_, a) => JassValue.FromBoolean(Trigger(a[0]).Enabled));
        N("TriggerRegisterEnterRegion", "event", new[] { "trigger", "region", "boolexpr" },
            (_, a) =>
            {
                runtime.Register(Trigger(a[0]), "enter-region", "TriggerRegisterEnterRegion", source: a[1].Handle, filter: a[2]);
                return JassValue.Null;
            });
        N("TriggerRegisterLeaveRegion", "event", new[] { "trigger", "region", "boolexpr" },
            (_, a) =>
            {
                runtime.Register(Trigger(a[0]), "leave-region", "TriggerRegisterLeaveRegion", source: a[1].Handle, filter: a[2]);
                return JassValue.Null;
            });
        N("TriggerRegisterPlayerChatEvent", "event", new[] { "trigger", "player", "string", "boolean" },
            (_, a) =>
            {
                runtime.Register(Trigger(a[0]), "chat", "TriggerRegisterPlayerChatEvent", eventId: 16, playerId: PlayerId(a[1]), key: a[2].Text ?? string.Empty, exact: a[3].Boolean);
                return JassValue.Null;
            });
        N("TriggerRegisterPlayerEvent", "event", new[] { "trigger", "player", "playerevent" },
            (_, a) =>
            {
                runtime.Register(Trigger(a[0]), "player-event", "TriggerRegisterPlayerEvent", eventId: EnumId(a[2]), playerId: PlayerId(a[1]));
                return JassValue.Null;
            });
        N("TriggerRegisterPlayerUnitEvent", "event", new[] { "trigger", "player", "playerunitevent", "boolexpr" },
            (_, a) =>
            {
                runtime.Register(Trigger(a[0]), "player-unit-event", "TriggerRegisterPlayerUnitEvent", eventId: EnumId(a[2]), playerId: PlayerId(a[1]), filter: a[3]);
                return JassValue.Null;
            });
        N("TriggerRegisterTimerEvent", "event", new[] { "trigger", "real", "boolean" },
            (_, a) =>
            {
                var timer = new TimerState
                {
                    RemainingMs = a[1].Real * 1000.0,
                    PeriodMs = a[1].Real * 1000.0,
                    Periodic = a[2].Boolean,
                    Running = true,
                };
                timer.Handle = new JassHandle("timer", timer);
                timer.HandleId = timer.Handle.Id;
                runtime.Timers.Add(timer);
                runtime.Register(Trigger(a[0]), "timer", "TriggerRegisterTimerEvent", source: timer.Handle, key: $"{a[1].Real}:{a[2].Boolean}");
                return JassValue.Null;
            });
        N("TriggerRegisterTimerExpireEvent", "event", new[] { "trigger", "timer" },
            (_, a) =>
            {
                runtime.Register(Trigger(a[0]), "timer-expire", "TriggerRegisterTimerExpireEvent", source: a[1].Handle);
                return JassValue.Null;
            });

        // Timers ---------------------------------------------------------------
        N("CreateTimer", "timer", noParams, (_, _) =>
        {
            var state = new TimerState();
            var handle = (JassHandle)Handle("timer", state).Handle!;
            state.HandleId = handle.Id;
            state.Handle = handle;
            return JassValue.FromHandle(handle);
        });
        N("DestroyTimer", "nothing", new[] { "timer" }, (_, a) => { Timer(a[0]).Destroyed = true; Timer(a[0]).Running = false; return JassValue.Null; });
        N("TimerStart", "nothing", new[] { "timer", "real", "boolean", "code" },
            (_, a) =>
            {
                var timer = Timer(a[0]);
                timer.Callback = a[3];
                timer.RemainingMs = a[1].Real * 1000.0;
                timer.PeriodMs = a[1].Real * 1000.0;
                timer.Periodic = a[2].Boolean;
                timer.Running = true;
                timer.Destroyed = false;
                if (!runtime.Timers.Contains(timer))
                {
                    runtime.Timers.Add(timer);
                }

                return JassValue.Null;
            });
        N("PauseTimer", "nothing", new[] { "timer" }, (_, a) => { Timer(a[0]).Running = false; return JassValue.Null; });
        N("ResumeTimer", "nothing", new[] { "timer" }, (_, a) => { Timer(a[0]).Running = true; return JassValue.Null; });
        N("TimerGetElapsed", "real", new[] { "timer" }, (_, _) => JassValue.FromReal((float)(runtime.TimeMs / 1000.0)));
        N("TimerGetRemaining", "real", new[] { "timer" }, (_, a) => JassValue.FromReal((float)(Timer(a[0]).RemainingMs / 1000.0)));
        N("TimerGetTimeout", "real", new[] { "timer" }, (_, a) => JassValue.FromReal((float)(Timer(a[0]).PeriodMs / 1000.0)));

        // Units ----------------------------------------------------------------
        N("CreateUnit", "unit", new[] { "player", "integer", "real", "real", "real" },
            (_, a) =>
            {
                var unit = new UnitState { TypeId = a[1].Integer, OwnerId = PlayerId(a[0]), X = a[2].Real, Y = a[3].Real, Facing = a[4].Real };
                var handle = new JassHandle("unit", unit);
                unit.Handle = handle;
                runtime.Units.Add(unit);
                return JassValue.FromHandle(handle);
            });
        N("CreateUnitAtLoc", "unit", new[] { "player", "integer", "location", "real" },
            (_, a) =>
            {
                var location = State<LocationState>(a[2], "location");
                var unit = new UnitState { TypeId = a[1].Integer, OwnerId = PlayerId(a[0]), X = location.X, Y = location.Y, Z = location.Z, Facing = a[3].Real };
                var handle = new JassHandle("unit", unit);
                unit.Handle = handle;
                runtime.Units.Add(unit);
                return JassValue.FromHandle(handle);
            });
        N("RemoveUnit", "nothing", new[] { "unit" }, (_, a) => { runtime.RemoveUnit(State<UnitState>(a[0], "unit")); return JassValue.Null; });
        N("SelectUnit", "nothing", new[] { "unit", "boolean" }, (_, a) => { runtime.SelectUnit(State<UnitState>(a[0], "unit"), 0, a[1].Boolean); return JassValue.Null; });
        N("ClearSelection", "nothing", noParams, (_, _) => { runtime.ClearSelection(0); return JassValue.Null; });
        N("GetUnitX", "real", new[] { "unit" }, (_, a) => JassValue.FromReal(State<UnitState>(a[0], "unit").X));
        N("GetUnitY", "real", new[] { "unit" }, (_, a) => JassValue.FromReal(State<UnitState>(a[0], "unit").Y));
        N("GetUnitFacing", "real", new[] { "unit" }, (_, a) => JassValue.FromReal(State<UnitState>(a[0], "unit").Facing));
        N("GetUnitTypeId", "integer", new[] { "unit" }, (_, a) => JassValue.FromInteger(State<UnitState>(a[0], "unit").TypeId));
        N("GetOwningPlayer", "player", new[] { "unit" }, (_, a) => JassValue.FromHandle(runtime.Player(State<UnitState>(a[0], "unit").OwnerId)));
        N("SetUnitX", "nothing", new[] { "unit", "real" }, (_, a) => { State<UnitState>(a[0], "unit").X = a[1].Real; return JassValue.Null; });
        N("SetUnitY", "nothing", new[] { "unit", "real" }, (_, a) => { State<UnitState>(a[0], "unit").Y = a[1].Real; return JassValue.Null; });
        N("SetUnitPosition", "nothing", new[] { "unit", "real", "real" },
            (_, a) => { var u = State<UnitState>(a[0], "unit"); u.X = a[1].Real; u.Y = a[2].Real; return JassValue.Null; });
        N("SetUnitFacing", "nothing", new[] { "unit", "real" }, (_, a) => { State<UnitState>(a[0], "unit").Facing = a[1].Real; return JassValue.Null; });
        N("GetUnitState", "real", new[] { "unit", "unitstate" },
            (_, a) =>
            {
                var unit = State<UnitState>(a[0], "unit");
                return JassValue.FromReal(EnumId(a[1]) switch
                {
                    0 => unit.Hp,
                    1 => unit.MaxHp,
                    2 => unit.Mana,
                    _ => 0f,
                });
            });
        N("SetUnitState", "nothing", new[] { "unit", "unitstate", "real" },
            (_, a) =>
            {
                var unit = State<UnitState>(a[0], "unit");
                switch (EnumId(a[1]))
                {
                    case 0: unit.Hp = a[2].Real; break;
                    case 1: unit.MaxHp = a[2].Real; break;
                    case 2: unit.Mana = a[2].Real; break;
                }

                return JassValue.Null;
            });

        // Groups ---------------------------------------------------------------
        N("CreateGroup", "group", noParams, (_, _) => Handle("group", new GroupState()));
        N("DestroyGroup", "nothing", new[] { "group" }, (_, a) => { State<GroupState>(a[0], "group").Units.Clear(); return JassValue.Null; });
        N("GroupAddUnit", "nothing", new[] { "group", "unit" },
            (_, a) => { State<GroupState>(a[0], "group").Units.Add(a[1].Handle!); return JassValue.Null; });
        N("GroupRemoveUnit", "nothing", new[] { "group", "unit" },
            (_, a) => { State<GroupState>(a[0], "group").Units.Remove(a[1].Handle!); return JassValue.Null; });
        N("GroupClear", "nothing", new[] { "group" }, (_, a) => { State<GroupState>(a[0], "group").Units.Clear(); return JassValue.Null; });
        N("GroupEnumUnitsInRect", "nothing", new[] { "group", "rect", "boolexpr" },
            (vm, a) =>
            {
                var group = State<GroupState>(a[0], "group");
                var rect = State<RectState>(a[1], "rect");
                var filter = a[2].IsNull ? null : State<ConditionState>(a[2], "boolexpr");
                foreach (var unit in runtime.Units.Where(u => !u.Removed && rect.Contains(u.X, u.Y)))
                {
                    var handle = unit.Handle;
                    if (handle is null)
                    {
                        continue;
                    }

                    if (filter is not null)
                    {
                        runtime.FilterUnit = JassValue.FromHandle(handle);
                        var result = vm.InvokeCode(filter.Code);
                        if (result.Kind != JassValueKind.Boolean || !result.Boolean)
                        {
                            continue;
                        }
                    }

                    group.Units.Add(handle);
                }

                runtime.FilterUnit = JassValue.Null;
                return JassValue.Null;
            });
        N("FirstOfGroup", "unit", new[] { "group" },
            (_, a) =>
            {
                var group = State<GroupState>(a[0], "group");
                if (group.Units.Count == 0)
                {
                    return JassValue.Null;
                }

                var unit = group.Units[0];
                group.Units.RemoveAt(0);
                return JassValue.FromHandle(unit);
            });
        N("ForGroup", "nothing", new[] { "group", "code" },
            (vm, a) =>
            {
                foreach (var unit in State<GroupState>(a[0], "group").Units.ToArray())
                {
                    runtime.EnumUnit = JassValue.FromHandle(unit);
                    vm.InvokeCode(a[1]);
                }

                runtime.EnumUnit = JassValue.Null;
                return JassValue.Null;
            });
        N("GetEnumUnit", "unit", noParams, (_, _) => runtime.EnumUnit);
        N("GetFilterUnit", "unit", noParams, (_, _) => runtime.FilterUnit);

        // Forces ---------------------------------------------------------------
        N("CreateForce", "force", noParams, (_, _) => Handle("force", new ForceState()));
        N("DestroyForce", "nothing", new[] { "force" }, (_, a) => { State<ForceState>(a[0], "force").Players.Clear(); return JassValue.Null; });
        N("ForceAddPlayer", "nothing", new[] { "force", "player" },
            (_, a) => { State<ForceState>(a[0], "force").Players.Add(PlayerId(a[1])); return JassValue.Null; });
        N("ForceRemovePlayer", "nothing", new[] { "force", "player" },
            (_, a) => { State<ForceState>(a[0], "force").Players.Remove(PlayerId(a[1])); return JassValue.Null; });
        N("ForForce", "nothing", new[] { "force", "code" },
            (vm, a) =>
            {
                foreach (var id in State<ForceState>(a[0], "force").Players.ToArray())
                {
                    runtime.EnumPlayer = JassValue.FromHandle(runtime.Player(id));
                    vm.InvokeCode(a[1]);
                }

                runtime.EnumPlayer = JassValue.Null;
                return JassValue.Null;
            });
        N("GetEnumPlayer", "player", noParams, (_, _) => runtime.EnumPlayer);

        // Regions / rects / locations -----------------------------------------
        N("Rect", "rect", new[] { "real", "real", "real", "real" },
            (_, a) => Handle("rect", new RectState { MinX = a[0].Real, MinY = a[1].Real, MaxX = a[2].Real, MaxY = a[3].Real }));
        N("RectFromLoc", "rect", new[] { "location", "location" },
            (_, a) =>
            {
                var min = State<LocationState>(a[0], "location");
                var max = State<LocationState>(a[1], "location");
                return Handle("rect", new RectState { MinX = min.X, MinY = min.Y, MaxX = max.X, MaxY = max.Y });
            });
        N("RemoveRect", "nothing", new[] { "rect" }, (_, _) => JassValue.Null);
        N("GetRectCenterX", "real", new[] { "rect" }, (_, a) => JassValue.FromReal(State<RectState>(a[0], "rect").CenterX));
        N("GetRectCenterY", "real", new[] { "rect" }, (_, a) => JassValue.FromReal(State<RectState>(a[0], "rect").CenterY));
        N("GetRectMinX", "real", new[] { "rect" }, (_, a) => JassValue.FromReal(State<RectState>(a[0], "rect").MinX));
        N("GetRectMinY", "real", new[] { "rect" }, (_, a) => JassValue.FromReal(State<RectState>(a[0], "rect").MinY));
        N("GetRectMaxX", "real", new[] { "rect" }, (_, a) => JassValue.FromReal(State<RectState>(a[0], "rect").MaxX));
        N("GetRectMaxY", "real", new[] { "rect" }, (_, a) => JassValue.FromReal(State<RectState>(a[0], "rect").MaxY));
        N("GetWorldBounds", "rect", noParams, (_, _) => Handle("rect", new RectState { MinX = -8192f, MinY = -8192f, MaxX = 8192f, MaxY = 8192f }));
        N("CreateRegion", "region", noParams, (_, _) => Handle("region", new RegionState()));
        N("RemoveRegion", "nothing", new[] { "region" }, (_, a) => { State<RegionState>(a[0], "region").Rects.Clear(); return JassValue.Null; });
        N("RegionAddRect", "nothing", new[] { "region", "rect" },
            (_, a) => { State<RegionState>(a[0], "region").Rects.Add(State<RectState>(a[1], "rect")); return JassValue.Null; });
        N("Location", "location", new[] { "real", "real" }, (_, a) => Handle("location", new LocationState { X = a[0].Real, Y = a[1].Real }));
        N("RemoveLocation", "nothing", new[] { "location" }, (_, _) => JassValue.Null);
        N("GetLocationX", "real", new[] { "location" }, (_, a) => JassValue.FromReal(State<LocationState>(a[0], "location").X));
        N("GetLocationY", "real", new[] { "location" }, (_, a) => JassValue.FromReal(State<LocationState>(a[0], "location").Y));
        N("SetLocation", "nothing", new[] { "location", "real", "real" },
            (_, a) => { var l = State<LocationState>(a[0], "location"); l.X = a[1].Real; l.Y = a[2].Real; return JassValue.Null; });

        // Camera / environment -------------------------------------------------
        N("SetCameraBounds", "nothing", new[] { "real", "real", "real", "real", "real", "real", "real", "real" },
            (_, a) =>
            {
                for (var i = 0; i < 8; i++)
                {
                    runtime.CameraBounds[i] = a[i].Real;
                }

                return JassValue.Null;
            });
        N("GetCameraMargin", "real", new[] { "integer" }, (_, _) => JassValue.FromReal(0f));
        N("GetCameraBoundMinX", "real", noParams, (_, _) => JassValue.FromReal(runtime.CameraBounds[0]));
        N("GetCameraBoundMinY", "real", noParams, (_, _) => JassValue.FromReal(runtime.CameraBounds[1]));
        N("GetCameraBoundMaxX", "real", noParams, (_, _) => JassValue.FromReal(runtime.CameraBounds[2]));
        N("GetCameraBoundMaxY", "real", noParams, (_, _) => JassValue.FromReal(runtime.CameraBounds[3]));

        N("SetDayNightModels", "nothing", new[] { "string", "string" },
            (_, a) => { runtime.DayNightModels[0] = a[0].Text ?? string.Empty; runtime.DayNightModels[1] = a[1].Text ?? string.Empty; return JassValue.Null; });
        N("SetWaterBaseColor", "nothing", new[] { "integer", "integer", "integer", "integer" },
            (_, a) => { for (var i = 0; i < 4; i++) runtime.WaterColor[i] = a[i].Integer; return JassValue.Null; });
        N("NewSoundEnvironment", "nothing", new[] { "string" }, (_, a) => { runtime.SoundEnvironment = a[0].Text ?? string.Empty; return JassValue.Null; });
        N("SetMapMusic", "nothing", new[] { "string", "boolean", "integer" }, (_, a) => { runtime.MapMusic = a[0].Text ?? string.Empty; return JassValue.Null; });

        // Dialogs --------------------------------------------------------------
        N("DialogCreate", "dialog", noParams, (_, _) =>
        {
            var dialog = new DialogState();
            var handle = new JassHandle("dialog", dialog);
            dialog.Handle = handle;
            runtime.Dialogs.Add(dialog);
            return JassValue.FromHandle(handle);
        });
        N("DialogSetMessage", "nothing", new[] { "dialog", "string" }, (_, a) => { State<DialogState>(a[0], "dialog").Message = a[1].Text ?? string.Empty; return JassValue.Null; });
        N("DialogAddButton", "button", new[] { "dialog", "string", "integer" },
            (_, a) =>
            {
                var dialog = State<DialogState>(a[0], "dialog");
                var button = new DialogButtonState { Dialog = dialog, Label = a[1].Text ?? string.Empty, Hotkey = a[2].Integer };
                var handle = new JassHandle("button", button);
                button.Handle = handle;
                dialog.Buttons.Add(button);
                return JassValue.FromHandle(handle);
            });
        N("DialogDisplay", "nothing", new[] { "player", "dialog", "boolean" },
            (_, a) =>
            {
                var dialog = State<DialogState>(a[1], "dialog");
                dialog.PlayerId = PlayerId(a[0]);
                dialog.Visible = a[2].Boolean;
                return JassValue.Null;
            });
        N("DialogDestroy", "nothing", new[] { "dialog" },
            (_, a) =>
            {
                var dialog = State<DialogState>(a[0], "dialog");
                dialog.Destroyed = true;
                dialog.Visible = false;
                dialog.Buttons.Clear();
                runtime.Dialogs.Remove(dialog);
                return JassValue.Null;
            });

        N("ExecuteFunc", "nothing", new[] { "string" }, (vm, a) =>
        {
            vm.Invoke(a[0].Text ?? string.Empty);
            return JassValue.Null;
        });
    }

    private static JassValue Save(JassValue[] arguments, Action<HashtableState, long> save)
    {
        save(Hashtable(arguments[0]), HashtableState.Key(arguments[1].Integer, arguments[2].Integer));
        return JassValue.Null;
    }

    private static JassValue Handle(string type, object? state) => JassValue.FromHandle(new JassHandle(type, state));

    private static HashtableState Hashtable(JassValue value) => State<HashtableState>(value, "hashtable");

    private static TriggerState Trigger(JassValue value) => State<TriggerState>(value, "trigger");

    private static TimerState Timer(JassValue value) => State<TimerState>(value, "timer");

    private static int EnumId(JassValue value)
    {
        if (value.Kind == JassValueKind.Integer)
        {
            return value.Integer;
        }

        if (value.Kind == JassValueKind.Handle)
        {
            return value.Handle?.State is int state ? state : value.Handle?.Id ?? 0;
        }

        return 0;
    }

    private static int PlayerId(JassValue value)
    {
        if (value.IsNull || value.Handle?.State is not int id)
        {
            throw new JassRuntimeException("expected player handle");
        }

        return id;
    }

    private static T State<T>(JassValue value, string type)
    {
        if (value.Kind != JassValueKind.Handle || value.Handle?.State is not T state)
        {
            throw new JassRuntimeException($"expected {type} handle, got {value}");
        }

        return state;
    }

    private static int Fnv1a(string text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in text)
            {
                hash ^= c;
                hash *= 16777619u;
            }

            return (int)hash;
        }
    }

}
