namespace War3Engine.Scripting;

/// <summary>
/// Additional standard natives with real runtime state, registered with the exact
/// signatures declared in common.j.
/// </summary>
public static class RuntimeNatives
{
    public static void Register(JassNativeRegistry registry, JassProgramModel program, JassGameRuntime runtime)
    {
        void Auto(string name, JassNativeImplementation impl, string sideEffects = "")
        {
            if (!program.Natives.TryGetValue(name, out var native))
            {
                return;
            }

            registry.Register(new JassNative(
                name,
                native.ReturnType,
                native.Parameters.Select(p => p.Type).ToArray(),
                (vm, args) =>
                {
                    runtime.CountNative(name);
                    return impl(vm, args);
                },
                "implemented",
                sideEffects));
        }

        JassValue Event(
            JassValue triggerValue,
            string kind,
            string function,
            int eventId = -1,
            int playerId = -1,
            JassHandle? source = null,
            JassValue filter = default,
            string key = "")
        {
            runtime.Register(State<TriggerState>(triggerValue, "trigger"), kind, function, eventId, playerId, source, filter, key);
            return JassValue.Null;
        }

        Auto("CreateSound", (_, a) => Sound(a, a[0].Text), "Creates a sound handle; playback is headless.");
        Auto("CreateSoundFromLabel", (_, a) => Sound(a, a[0].Text), "Creates a sound handle; playback is headless.");
        Auto("CreateMIDISound", (_, a) => Sound(a, a[0].Text), "Creates a sound handle; playback is headless.");
        Auto("StartSound", (_, a) => { SoundState(a[0]).Playing = true; return JassValue.Null; });
        Auto("StopSound", (_, a) => { SoundState(a[0]).Playing = false; return JassValue.Null; });
        Auto("KillSoundWhenDone", (_, _) => JassValue.Null);
        Auto("SetSoundVolume", (_, a) => { SoundState(a[0]).Volume = a[1].Integer; return JassValue.Null; });
        Auto("SetSoundPitch", (_, a) => { SoundState(a[0]).Pitch = (int)a[1].Real; return JassValue.Null; });
        Auto("GetTimeOfDay", (_, _) => JassValue.FromReal(12f), "Headless clock is fixed at noon.");
        Auto("VersionGet", (_, _) => JassValue.FromHandle(new JassHandle("version", runtime.Version)));
        Auto("IsFogEnabled", (_, _) => JassValue.FromBoolean(runtime.FogEnabled));
        Auto("IsFogMaskEnabled", (_, _) => JassValue.FromBoolean(runtime.FogMaskEnabled));
        Auto("FogEnable", (_, a) => { runtime.FogEnabled = a[0].Boolean; return JassValue.Null; });
        Auto("FogMaskEnable", (_, a) => { runtime.FogMaskEnabled = a[0].Boolean; return JassValue.Null; });
        Auto("GetGameSpeed", (_, _) => JassValue.FromHandle(JassGameRuntime.EnumHandle("gamespeed", 0)));
        Auto("SetPlayerAlliance", (_, a) =>
        {
            runtime.Alliances[(PlayerId(a[0]), PlayerId(a[1]), EnumId(a[2]))] = a[3].Boolean;
            return JassValue.Null;
        }, "Stores alliance flags in runtime state.");
        Auto("SetPlayerName", (_, a) => { runtime.Globals[$"player-name:{PlayerId(a[0])}"] = JassValue.FromString(a[1].Text ?? string.Empty); return JassValue.Null; });
        Auto("ForceEnumPlayers", (vm, a) =>
        {
            var force = State<ForceState>(a[0], "force");
            var filter = a[1].IsNull ? null : State<ConditionState>(a[1], "boolexpr");
            for (var i = 0; i < 16; i++)
            {
                if (filter is not null)
                {
                    runtime.EnumPlayer = JassValue.FromHandle(runtime.Player(i));
                    var result = vm.InvokeCode(filter.Code);
                    if (result.Kind != JassValueKind.Boolean || !result.Boolean)
                    {
                        continue;
                    }
                }

                force.Players.Add(i);
            }

            runtime.EnumPlayer = JassValue.Null;
            return JassValue.Null;
        });
        Auto("GetFilterPlayer", (_, _) => runtime.EnumPlayer);
        Auto("IsPlayerInForce", (_, a) => JassValue.FromBoolean(State<ForceState>(a[1], "force").Players.Contains(PlayerId(a[0]))));
        Auto("IsUnitInForce", (_, _) => JassValue.FromBoolean(false));
        Auto("GroupEnumUnitsOfPlayer", (vm, a) =>
        {
            var group = State<GroupState>(a[0], "group");
            var player = PlayerId(a[1]);
            var filter = a[2].IsNull ? null : State<ConditionState>(a[2], "boolexpr");
            foreach (var unit in runtime.Units.Where(u => !u.Removed && u.OwnerId == player).ToArray())
            {
                if (unit.Handle is null)
                {
                    continue;
                }

                if (filter is not null)
                {
                    runtime.FilterUnit = JassValue.FromHandle(unit.Handle);
                    var result = vm.InvokeCode(filter.Code);
                    if (result.Kind != JassValueKind.Boolean || !result.Boolean)
                    {
                        continue;
                    }
                }

                group.Units.Add(unit.Handle);
            }

            runtime.FilterUnit = JassValue.Null;
            return JassValue.Null;
        });
        Auto("GroupEnumUnitsInRange", (vm, a) =>
        {
            var group = State<GroupState>(a[0], "group");
            var x = a[1].Real;
            var y = a[2].Real;
            var radius = a[3].Real;
            var filter = a[4].IsNull ? null : State<ConditionState>(a[4], "boolexpr");
            foreach (var unit in runtime.Units.Where(u => !u.Removed).ToArray())
            {
                var dx = unit.X - x;
                var dy = unit.Y - y;
                if ((dx * dx) + (dy * dy) > radius * radius || unit.Handle is null)
                {
                    continue;
                }

                if (filter is not null)
                {
                    runtime.FilterUnit = JassValue.FromHandle(unit.Handle);
                    var result = vm.InvokeCode(filter.Code);
                    if (result.Kind != JassValueKind.Boolean || !result.Boolean)
                    {
                        continue;
                    }
                }

                group.Units.Add(unit.Handle);
            }

            runtime.FilterUnit = JassValue.Null;
            return JassValue.Null;
        });
        Auto("GroupEnumUnitsInRangeOfLoc", (vm, a) =>
        {
            var group = State<GroupState>(a[0], "group");
            var loc = State<LocationState>(a[1], "location");
            var radius = a[2].Real;
            var filter = a[3].IsNull ? null : State<ConditionState>(a[3], "boolexpr");
            foreach (var unit in runtime.Units.Where(u => !u.Removed).ToArray())
            {
                var dx = unit.X - loc.X;
                var dy = unit.Y - loc.Y;
                if ((dx * dx) + (dy * dy) > radius * radius || unit.Handle is null)
                {
                    continue;
                }

                if (filter is not null)
                {
                    runtime.FilterUnit = JassValue.FromHandle(unit.Handle);
                    var result = vm.InvokeCode(filter.Code);
                    if (result.Kind != JassValueKind.Boolean || !result.Boolean)
                    {
                        continue;
                    }
                }

                group.Units.Add(unit.Handle);
            }

            runtime.FilterUnit = JassValue.Null;
            return JassValue.Null;
        });
        Auto("GroupEnumUnitsOfType", (vm, a) =>
        {
            var group = State<GroupState>(a[0], "group");
            var typeId = a[1].Integer;
            var filter = a[2].IsNull ? null : State<ConditionState>(a[2], "boolexpr");
            foreach (var unit in runtime.Units.Where(u => !u.Removed && u.TypeId == typeId).ToArray())
            {
                if (unit.Handle is null)
                {
                    continue;
                }

                if (filter is not null)
                {
                    runtime.FilterUnit = JassValue.FromHandle(unit.Handle);
                    var result = vm.InvokeCode(filter.Code);
                    if (result.Kind != JassValueKind.Boolean || !result.Boolean)
                    {
                        continue;
                    }
                }

                group.Units.Add(unit.Handle);
            }

            runtime.FilterUnit = JassValue.Null;
            return JassValue.Null;
        });
        Auto("CountUnitsInGroup", (_, a) => JassValue.FromInteger(State<GroupState>(a[0], "group").Units.Count));
        Auto("IsUnitInGroup", (_, a) => JassValue.FromBoolean(State<GroupState>(a[1], "group").Units.Contains(a[0].Handle!)));
        Auto("GroupPickRandomUnit", (_, a) =>
        {
            var units = State<GroupState>(a[0], "group").Units;
            return units.Count == 0 ? JassValue.Null : JassValue.FromHandle(units[Random.Shared.Next(units.Count)]);
        });
        Auto("CreateMultiboard", (_, _) => JassValue.FromHandle(new JassHandle("multiboard", new MultiboardState())));
        Auto("DestroyMultiboard", (_, a) => { Multi(a[0]).Items.Clear(); return JassValue.Null; });
        Auto("MultiboardDisplay", (_, a) => { Multi(a[0]).Visible = a[1].Boolean; return JassValue.Null; });
        Auto("MultiboardMinimize", (_, a) => { Multi(a[0]).Minimized = a[1].Boolean; return JassValue.Null; });
        Auto("MultiboardClear", (_, a) => { Multi(a[0]).Items.Clear(); return JassValue.Null; });
        Auto("MultiboardSetTitleText", (_, a) => { Multi(a[0]).Title = a[1].Text ?? string.Empty; return JassValue.Null; });
        Auto("MultiboardGetTitleText", (_, a) => JassValue.FromString(Multi(a[0]).Title));
        Auto("MultiboardSetTitleTextColor", (_, _) => JassValue.Null);
        Auto("MultiboardGetRowCount", (_, a) => JassValue.FromInteger(Multi(a[0]).Rows));
        Auto("MultiboardGetColumnCount", (_, a) => JassValue.FromInteger(Multi(a[0]).Columns));
        Auto("MultiboardSetColumnCount", (_, a) => { Multi(a[0]).Columns = a[1].Integer; return JassValue.Null; });
        Auto("MultiboardSetRowCount", (_, a) => { Multi(a[0]).Rows = a[1].Integer; return JassValue.Null; });
        Auto("MultiboardSetItemsStyle", (_, a) => { var m = Multi(a[0]); m.ShowValues = a[1].Boolean; m.ShowIcons = a[2].Boolean; return JassValue.Null; });
        Auto("MultiboardSetItemsValue", (_, a) => { Multi(a[0]).ItemsValue = a[1].Text ?? string.Empty; return JassValue.Null; });
        Auto("MultiboardSetItemsValueColor", (_, _) => JassValue.Null);
        Auto("MultiboardSetItemsWidth", (_, a) => { Multi(a[0]).ItemsWidth = a[1].Real; return JassValue.Null; });
        Auto("MultiboardSetItemsIcon", (_, a) => { Multi(a[0]).ItemsIcon = a[1].Text ?? string.Empty; return JassValue.Null; });
        Auto("MultiboardGetItem", (_, a) =>
        {
            var board = Multi(a[0]);
            var key = (a[1].Integer, a[2].Integer);
            if (!board.Items.TryGetValue(key, out var item))
            {
                item = new MultiboardItemState { Board = board, Row = key.Item1, Column = key.Item2 };
                board.Items[key] = item;
            }

            return JassValue.FromHandle(new JassHandle("multiboarditem", item));
        });
        Auto("MultiboardReleaseItem", (_, _) => JassValue.Null);
        Auto("MultiboardSetItemStyle", (_, a) => { var i = MultiItem(a[0]); i.ShowValue = a[1].Boolean; i.ShowIcon = a[2].Boolean; return JassValue.Null; });
        Auto("MultiboardSetItemValue", (_, a) => { MultiItem(a[0]).Value = a[1].Text ?? string.Empty; return JassValue.Null; });
        Auto("MultiboardSetItemValueColor", (_, _) => JassValue.Null);
        Auto("MultiboardSetItemWidth", (_, a) => { MultiItem(a[0]).Width = a[1].Real; return JassValue.Null; });
        Auto("MultiboardSetItemIcon", (_, a) => { MultiItem(a[0]).Icon = a[1].Text ?? string.Empty; return JassValue.Null; });
        Auto("MultiboardSuppressDisplay", (_, a) => { runtime.MultiboardSuppressed = a[0].Boolean; return JassValue.Null; });
        Auto("DisplayTextToPlayer", (_, a) => { runtime.DisplayMessages.Add(a.Length > 3 ? a[3].Text ?? string.Empty : string.Empty); return JassValue.Null; });
        Auto("DisplayTimedTextToPlayer", (_, a) => { runtime.DisplayMessages.Add(a.Length > 4 ? a[4].Text ?? string.Empty : string.Empty); return JassValue.Null; });
        Auto("DisplayTimedTextToForce", (_, a) => { runtime.DisplayMessages.Add(a.Length > 2 ? a[2].Text ?? string.Empty : string.Empty); return JassValue.Null; });
        Auto("DisplayTextToForce", (_, a) => { runtime.DisplayMessages.Add(a.Length > 1 ? a[1].Text ?? string.Empty : string.Empty); return JassValue.Null; });
        Auto("GetTriggeringTrigger", (_, _) => runtime.Context?.Trigger?.Handle is { } h ? JassValue.FromHandle(h) : JassValue.Null);
        Auto("GetExpiredTimer", (_, _) => runtime.Context?.ExpiredTimer?.Handle is { } t ? JassValue.FromHandle(t) : JassValue.Null);
        Auto("GetTriggerPlayer", (_, _) => runtime.Context?.Event.Player ?? JassValue.Null);
        Auto("GetEventPlayerChatString", (_, _) => JassValue.FromString(runtime.Context?.Event.ChatString ?? string.Empty));
        Auto("GetEventPlayerChatStringMatched", (_, _) => JassValue.FromString(runtime.Context?.Event.ChatMatched ?? string.Empty));
        Auto("GetClickedButton", (_, _) => runtime.Context?.Event.ClickedButton ?? JassValue.Null);
        Auto("GetClickedDialog", (_, _) => runtime.Context?.Event.ClickedDialog ?? JassValue.Null);
        Auto("GetPlayerUnitTypeCount", (_, _) => JassValue.FromInteger(0));
        Auto("GetUnitUserData", (_, _) => JassValue.FromInteger(0));
        Auto("SetUnitUserData", (_, _) => JassValue.Null);
        foreach (var handleType in new[] { "Unit", "Effect", "Trigger", "Group", "Timer", "Destructable", "Item", "Region", "Rect", "Location", "Sound", "Lightning", "Image", "TextTag", "Dialog", "Button", "Quest", "Multiboard", "Leaderboard", "Trackable", "Ubersplat", "FogModifier", "Player" })
        {
            var saveName = $"Save{handleType}Handle";
            var loadName = $"Load{handleType}Handle";
            if (program.Natives.ContainsKey(saveName))
            {
                Auto(saveName, (_, a) =>
                {
                    var table = Hashtable(a[0]);
                    table.Handles[HashtableState.Key(a[1].Integer, a[2].Integer)] = a[3].Handle;
                    return JassValue.FromBoolean(true);
                });
            }

            if (program.Natives.ContainsKey(loadName))
            {
                Auto(loadName, (_, a) =>
                {
                    var table = Hashtable(a[0]);
                    return table.Handles.TryGetValue(HashtableState.Key(a[1].Integer, a[2].Integer), out var handle) && handle is not null
                        ? JassValue.FromHandle(handle)
                        : JassValue.Null;
                });
            }
        }
        Auto("SetSoundParamsFromLabel", (_, a) => { SoundState(a[0]).Label = a[1].Text ?? string.Empty; return JassValue.Null; });
        Auto("SetSoundDistanceCutoff", (_, _) => JassValue.Null);
        Auto("SetSoundChannel", (_, _) => JassValue.Null);
        Auto("SetSoundPlayPosition", (_, _) => JassValue.Null);
        Auto("SetSoundPosition", (_, _) => JassValue.Null);
        Auto("SetSoundDuration", (_, _) => JassValue.Null);
        Auto("AttachSoundToUnit", (_, _) => JassValue.Null);
        Auto("SetSoundParams", (_, _) => JassValue.Null);
        Auto("TriggerRegisterGameStateEvent", (_, a) => Event(a[0], "game-state", "TriggerRegisterGameStateEvent", EnumId(a[1]), key: $"{EnumId(a[2])}:{a[3].Real}"));
        Auto("TriggerRegisterGameEvent", (_, a) => Event(a[0], "game-event", "TriggerRegisterGameEvent", EnumId(a[1])));
        Auto("TriggerRegisterPlayerAllianceChange", (_, a) => Event(a[0], "alliance", "TriggerRegisterPlayerAllianceChange", EnumId(a[2]), PlayerId(a[1])));
        Auto("TriggerRegisterPlayerStateEvent", (_, a) => Event(a[0], "player-state", "TriggerRegisterPlayerStateEvent", EnumId(a[2]), PlayerId(a[1]), key: $"{EnumId(a[3])}:{a[4].Real}"));
        Auto("TriggerRegisterUnitStateEvent", (_, a) => Event(a[0], "unit-state", "TriggerRegisterUnitStateEvent", EnumId(a[2]), source: a[1].Handle, key: $"{EnumId(a[3])}:{a[4].Real}"));
        Auto("TriggerRegisterUnitEvent", (_, a) => Event(a[0], "unit-event", "TriggerRegisterUnitEvent", EnumId(a[2]), source: a[1].Handle));
        Auto("TriggerRegisterDeathEvent", (_, a) => Event(a[0], "death", "TriggerRegisterDeathEvent", source: a[1].Handle));
        Auto("TriggerRegisterDialogEvent", (_, a) => Event(a[0], "dialog", "TriggerRegisterDialogEvent", source: a[1].Handle));
        Auto("TriggerRegisterDialogButtonEvent", (_, a) => Event(a[0], "dialog-button", "TriggerRegisterDialogButtonEvent", source: a[1].Handle));
        Auto("TriggerRegisterCommandEvent", (_, a) => Event(a[0], "command", "TriggerRegisterCommandEvent", a[2].Integer, source: a[1].Handle));
        Auto("TriggerRegisterUpgradeCommandEvent", (_, a) => Event(a[0], "upgrade-command", "TriggerRegisterUpgradeCommandEvent", a[2].Integer, source: a[1].Handle));
        Auto("TriggerRegisterTrackableHitEvent", (_, a) => Event(a[0], "trackable-hit", "TriggerRegisterTrackableHitEvent", source: a[1].Handle));
        Auto("TriggerRegisterTrackableTrackEvent", (_, a) => Event(a[0], "trackable-track", "TriggerRegisterTrackableTrackEvent", source: a[1].Handle));
        Auto("TriggerRegisterVariableEvent", (_, a) => Event(a[0], "variable", "TriggerRegisterVariableEvent", key: $"{a[1].Text}:{EnumId(a[2])}:{a[3].Real}"));
        Auto("GetFloatGameState", (_, a) =>
        {
            var state = EnumId(a[0]);
            // GAME_STATE_TIME_OF_DAY defaults to noon; other float states default to 0.
            return JassValue.FromReal(runtime.FloatGameState.TryGetValue(state, out var value) ? value : state == 1 ? 12f : 0f);
        });
        Auto("SetFloatGameState", (_, a) => { runtime.FloatGameState[EnumId(a[0])] = a[1].Real; return JassValue.Null; });
        Auto("GetIntegerGameState", (_, a) => JassValue.FromInteger(runtime.IntegerGameState.TryGetValue(EnumId(a[0]), out var value) ? value : 0));
        Auto("SetIntegerGameState", (_, a) => { runtime.IntegerGameState[EnumId(a[0])] = a[1].Integer; return JassValue.Null; });
        Auto("SetPlayerState", (_, a) => { runtime.PlayerStates[(PlayerId(a[0]), EnumId(a[1]))] = a[2].Integer; return JassValue.Null; });
        Auto("GetPlayerState", (_, a) => JassValue.FromInteger(runtime.PlayerStates.TryGetValue((PlayerId(a[0]), EnumId(a[1])), out var value) ? value : 0));
        Auto("SetPlayerColor", (_, a) => { runtime.PlayerColors[PlayerId(a[0])] = EnumId(a[1]); return JassValue.Null; });
        Auto("GetPlayerColor", (_, a) => JassValue.FromInteger(runtime.PlayerColors.TryGetValue(PlayerId(a[0]), out var value) ? value : 0));
        Auto("SetPlayerRacePreference", (_, a) => { runtime.PlayerRaces[PlayerId(a[0])] = EnumId(a[1]); return JassValue.Null; });
        Auto("GetPlayerRace", (_, a) => JassValue.FromInteger(runtime.PlayerRaces.TryGetValue(PlayerId(a[0]), out var value) ? value : 0));
        Auto("SetPlayerTeam", (_, a) => { runtime.PlayerTeams[PlayerId(a[0])] = a[1].Integer; return JassValue.Null; });
        Auto("GetPlayerTeam", (_, a) => JassValue.FromInteger(runtime.PlayerTeams.TryGetValue(PlayerId(a[0]), out var value) ? value : 0));
        Auto("SetPlayerStartLocation", (_, a) => { runtime.PlayerStartLocations[PlayerId(a[0])] = a[1].Integer; return JassValue.Null; });
        Auto("GetPlayerStartLocation", (_, a) => JassValue.FromInteger(runtime.PlayerStartLocations.TryGetValue(PlayerId(a[0]), out var value) ? value : 0));
        Auto("GetPlayerHandicap", (_, _) => JassValue.FromReal(1f));
        Auto("SetPlayerHandicap", (_, _) => JassValue.Null);
        Auto("GetPlayerHandicapXP", (_, _) => JassValue.FromReal(1f));
        Auto("SetPlayerHandicapXP", (_, _) => JassValue.Null);
        Auto("GetPlayerTaxRate", (_, _) => JassValue.FromInteger(0));
        Auto("SetPlayerTaxRate", (_, _) => JassValue.Null);
        Auto("SetPlayerAbilityAvailable", (_, _) => JassValue.Null);
        Auto("SetPlayerTechResearched", (_, _) => JassValue.Null);
        Auto("GetPlayerTechCount", (_, _) => JassValue.FromInteger(0));
        Auto("SetPlayerFlag", (_, _) => JassValue.Null);
        Auto("GetPlayerTechResearched", (_, a) => JassValue.FromBoolean(GetTech(runtime.TechResearched, PlayerId(a[0]), a[1].Integer) > 0));
        Auto("SetPlayerTechResearched", (_, a) => { runtime.TechResearched[(PlayerId(a[0]), a[1].Integer)] = a[2].Integer; return JassValue.Null; });
        Auto("GetPlayerTechCount", (_, a) => JassValue.FromInteger(GetTech(runtime.TechResearched, PlayerId(a[0]), a[1].Integer)));
        Auto("GetPlayerTechMaxAllowed", (_, a) => JassValue.FromInteger(GetTech(runtime.TechMaxAllowed, PlayerId(a[0]), a[1].Integer)));
        Auto("SetPlayerTechMaxAllowed", (_, a) => { runtime.TechMaxAllowed[(PlayerId(a[0]), a[1].Integer)] = a[2].Integer; return JassValue.Null; });
        Auto("UnitAddAbility", (_, a) => { Unit(a[0]).Abilities[a[1].Integer] = 1; return JassValue.Null; });
        Auto("UnitRemoveAbility", (_, a) => { Unit(a[0]).Abilities.Remove(a[1].Integer); return JassValue.Null; });
        Auto("SetUnitAbilityLevel", (_, a) => { Unit(a[0]).Abilities[a[1].Integer] = a[2].Integer; return JassValue.FromInteger(a[2].Integer); });
        Auto("GetUnitAbilityLevel", (_, a) => JassValue.FromInteger(Unit(a[0]).Abilities.TryGetValue(a[1].Integer, out var level) ? level : 0));
        Auto("UnitMakeAbilityPermanent", (_, _) => JassValue.FromBoolean(true));
        Auto("UnitAddType", (_, a) => { Unit(a[0]).Types.Add(EnumId(a[1])); return JassValue.FromBoolean(true); });
        Auto("UnitRemoveType", (_, a) => { Unit(a[0]).Types.Remove(EnumId(a[1])); return JassValue.FromBoolean(true); });
        Auto("IsUnitType", (_, a) => JassValue.FromBoolean(Unit(a[0]).Types.Contains(EnumId(a[1]))));
        Auto("SetAllItemTypeSlots", (_, a) => { runtime.AllItemTypeSlots = a[0].Integer; return JassValue.Null; });
        Auto("SetAllUnitTypeSlots", (_, a) => { runtime.AllUnitTypeSlots = a[0].Integer; return JassValue.Null; });
        Auto("GetAllItemTypeSlots", (_, _) => JassValue.FromInteger(runtime.AllItemTypeSlots));
        Auto("GetAllUnitTypeSlots", (_, _) => JassValue.FromInteger(runtime.AllUnitTypeSlots));
        Auto("GetPlayerUnitCount", (_, a) => JassValue.FromInteger(runtime.Units.Count(u => !u.Removed && u.OwnerId == PlayerId(a[0]))));
        Auto("GetPlayerStructureCount", (_, _) => JassValue.FromInteger(0));
        Auto("SetPlayerUnitAvailable", (_, _) => JassValue.Null);
        Auto("GetPlayerUnitAvailable", (_, _) => JassValue.FromBoolean(true));
        Auto("SetUnitCreepGuard", (_, _) => JassValue.Null);
        Auto("I2R", (_, a) => JassValue.FromReal(a[0].Integer));
        Auto("R2I", (_, a) => JassValue.FromInteger((int)a[0].Real));
        Auto("I2S", (_, a) => JassValue.FromString(a[0].Integer.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Auto("R2S", (_, a) => JassValue.FromString(a[0].Real.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Auto("S2I", (_, a) => JassValue.FromInteger(ParseLeadingInt(a[0].Text)));
        Auto("S2R", (_, a) => JassValue.FromReal(ParseLeadingFloat(a[0].Text)));
        Auto("B2S", (_, a) => JassValue.FromString(a[0].Boolean ? "true" : "false"));
        Auto("S2B", (_, a) => JassValue.FromBoolean(string.Equals(a[0].Text, "true", StringComparison.OrdinalIgnoreCase)));
        Auto("StringLength", (_, a) => JassValue.FromInteger(a[0].Text?.Length ?? 0));
        Auto("SubString", (_, a) => JassValue.FromString(SubString(a[0].Text, a[1].Integer, a[2].Integer)));
        Auto("StringCase", (_, a) => JassValue.FromString(a[1].Boolean ? (a[0].Text ?? string.Empty).ToUpperInvariant() : (a[0].Text ?? string.Empty).ToLowerInvariant()));
        Auto("ModuloInteger", (_, a) => JassValue.FromInteger(a[1].Integer == 0 ? 0 : a[0].Integer % a[1].Integer));
        Auto("ModuloReal", (_, a) => JassValue.FromReal(a[1].Real == 0 ? 0f : a[0].Real % a[1].Real));
        Auto("Pow", (_, a) => JassValue.FromReal(MathF.Pow(a[0].Real, a[1].Real)));
        Auto("SquareRoot", (_, a) => JassValue.FromReal(MathF.Sqrt(a[0].Real)));
        Auto("Sin", (_, a) => JassValue.FromReal(MathF.Sin(a[0].Real)));
        Auto("Cos", (_, a) => JassValue.FromReal(MathF.Cos(a[0].Real)));
        Auto("Tan", (_, a) => JassValue.FromReal(MathF.Tan(a[0].Real)));
        Auto("Atan2", (_, a) => JassValue.FromReal(MathF.Atan2(a[0].Real, a[1].Real)));
        Auto("Atan", (_, a) => JassValue.FromReal(MathF.Atan(a[0].Real)));
        Auto("Asin", (_, a) => JassValue.FromReal(MathF.Asin(a[0].Real)));
        Auto("Acos", (_, a) => JassValue.FromReal(MathF.Acos(a[0].Real)));
        Auto("Deg2Rad", (_, a) => JassValue.FromReal(a[0].Real * MathF.PI / 180f));
        Auto("Rad2Deg", (_, a) => JassValue.FromReal(a[0].Real * 180f / MathF.PI));
        Auto("GetRandomInt", (_, a) => JassValue.FromInteger(Random.Shared.Next(a[0].Integer, Math.Max(a[0].Integer + 1, a[1].Integer + 1))));
        Auto("GetRandomReal", (_, a) => JassValue.FromReal(a[0].Real + ((float)Random.Shared.NextDouble() * (a[1].Real - a[0].Real))));
        Auto("SetRandomSeed", (_, _) => JassValue.Null);
        Auto("GetUnitName", (_, _) => JassValue.FromString("unit"));
        Auto("GetUnitPointValue", (_, _) => JassValue.FromInteger(0));
        Auto("GetTriggerUnit", (_, _) => runtime.Context?.Event.TriggerUnit ?? JassValue.Null);
        Auto("GetDyingUnit", (_, _) => runtime.Context?.Event.DyingUnit ?? JassValue.Null);
        Auto("GetKillingUnit", (_, _) => runtime.Context?.Event.KillingUnit ?? JassValue.Null);
        Auto("GetAttacker", (_, _) => runtime.Context?.Event.Attacker ?? JassValue.Null);
        Auto("GetEventDamageSource", (_, _) => runtime.Context?.Event.DamageSource ?? JassValue.Null);
        Auto("GetEventDamage", (_, _) => JassValue.FromReal(runtime.Context?.Event.Damage ?? 0f));
        Auto("GetSpellAbilityUnit", (_, _) => runtime.Context?.Event.SpellAbilityUnit ?? JassValue.Null);
        Auto("GetSpellAbilityId", (_, _) => JassValue.FromInteger(runtime.Context?.Event.SpellAbilityId ?? 0));
        Auto("GetSpellTargetUnit", (_, _) => runtime.Context?.Event.SpellTargetUnit ?? JassValue.Null);
        Auto("GetSpellTargetX", (_, _) => JassValue.FromReal(runtime.Context?.Event.SpellTargetX ?? 0f));
        Auto("GetSpellTargetY", (_, _) => JassValue.FromReal(runtime.Context?.Event.SpellTargetY ?? 0f));
        Auto("GetOrderedUnit", (_, _) => runtime.Context?.Event.OrderedUnit ?? JassValue.Null);
        Auto("GetIssuedOrderId", (_, _) => JassValue.FromInteger(runtime.Context?.Event.IssuedOrderId ?? 0));
        Auto("GetOrderTargetUnit", (_, _) => runtime.Context?.Event.OrderTargetUnit ?? JassValue.Null);
        Auto("GetOrderPointX", (_, _) => JassValue.FromReal(runtime.Context?.Event.OrderPointX ?? 0f));
        Auto("GetOrderPointY", (_, _) => JassValue.FromReal(runtime.Context?.Event.OrderPointY ?? 0f));
        Auto("SetCameraField", (_, a) => { runtime.CameraFields[EnumId(a[0])] = a[1].Real; return JassValue.Null; });
        Auto("GetCameraField", (_, a) => JassValue.FromReal(runtime.CameraFields.TryGetValue(EnumId(a[0]), out var v) ? v : 0f));
        Auto("GetCameraTargetPositionX", (_, _) => JassValue.FromReal(runtime.CameraX));
        Auto("GetCameraTargetPositionY", (_, _) => JassValue.FromReal(runtime.CameraY));
        Auto("GetCameraTargetPositionZ", (_, _) => JassValue.FromReal(runtime.CameraZ));
        Auto("GetCameraEyePositionX", (_, _) => JassValue.FromReal(runtime.CameraX));
        Auto("GetCameraEyePositionY", (_, _) => JassValue.FromReal(runtime.CameraY));
        Auto("GetCameraEyePositionZ", (_, _) => JassValue.FromReal(runtime.CameraZ));
        Auto("SetCameraPosition", (_, a) => { runtime.CameraX = a[0].Real; runtime.CameraY = a[1].Real; return JassValue.Null; });
        Auto("SetCameraQuickPosition", (_, a) => { runtime.CameraX = a[0].Real; runtime.CameraY = a[1].Real; return JassValue.Null; });
        Auto("ResetToGameCamera", (_, _) => JassValue.Null);
        Auto("CreateItem", (_, a) =>
        {
            var item = new ItemState { TypeId = a[0].Integer, X = a[1].Real, Y = a[2].Real };
            var handle = new JassHandle("item", item);
            item.Handle = handle;
            runtime.Items.Add(item);
            return JassValue.FromHandle(handle);
        });
        Auto("RemoveItem", (_, a) => JassValue.Null);
        Auto("GetItemX", (_, a) => JassValue.FromReal(State<ItemState>(a[0], "item").X));
        Auto("GetItemY", (_, a) => JassValue.FromReal(State<ItemState>(a[0], "item").Y));
        Auto("GetItemTypeId", (_, a) => JassValue.FromInteger(State<ItemState>(a[0], "item").TypeId));
        Auto("SetItemPosition", (_, a) => { var i = State<ItemState>(a[0], "item"); i.X = a[1].Real; i.Y = a[2].Real; return JassValue.Null; });
        Auto("CreateDestructable", (_, a) =>
        {
            var d = new DestructableState { TypeId = a[0].Integer, X = a[1].Real, Y = a[2].Real, Z = a[3].Real };
            var handle = new JassHandle("destructable", d);
            d.Handle = handle;
            runtime.Destructables.Add(d);
            return JassValue.FromHandle(handle);
        });
        Auto("CreateDestructableZ", (_, a) =>
        {
            var d = new DestructableState { TypeId = a[0].Integer, X = a[1].Real, Y = a[2].Real, Z = a[3].Real };
            var handle = new JassHandle("destructable", d);
            d.Handle = handle;
            runtime.Destructables.Add(d);
            return JassValue.FromHandle(handle);
        });
        Auto("GetDestructableX", (_, a) => JassValue.FromReal(State<DestructableState>(a[0], "destructable").X));
        Auto("GetDestructableY", (_, a) => JassValue.FromReal(State<DestructableState>(a[0], "destructable").Y));
        Auto("GetDestructableTypeId", (_, a) => JassValue.FromInteger(State<DestructableState>(a[0], "destructable").TypeId));
        Auto("SetDestructableLife", (_, a) => { State<DestructableState>(a[0], "destructable").Life = a[1].Real; return JassValue.Null; });
        Auto("GetDestructableLife", (_, a) => JassValue.FromReal(State<DestructableState>(a[0], "destructable").Life));
        Auto("KillDestructable", (_, _) => JassValue.Null);
        Auto("RemoveDestructable", (_, _) => JassValue.Null);
        Auto("AddSpecialEffect", (_, a) => JassValue.FromHandle(new JassHandle("effect", new EffectState { Model = a[0].Text ?? string.Empty, X = a[1].Real, Y = a[2].Real })));
        Auto("AddSpecialEffectTarget", (_, a) => JassValue.FromHandle(new JassHandle("effect", new EffectState { Model = a[0].Text ?? string.Empty })));
        Auto("DestroyEffect", (_, _) => JassValue.Null);
        Auto("SetUnitOwner", (_, a) => { Unit(a[0]).OwnerId = PlayerId(a[1]); return JassValue.Null; });
        Auto("SetUnitScale", (_, _) => JassValue.Null);
        Auto("SetUnitTimeScale", (_, _) => JassValue.Null);
        Auto("SetUnitAnimation", (_, _) => JassValue.Null);
        Auto("QueueUnitAnimation", (_, _) => JassValue.Null);
        Auto("SetUnitAnimationByIndex", (_, _) => JassValue.Null);
        Auto("SetUnitVertexColor", (_, _) => JassValue.Null);
        Auto("KillUnit", (_, a) => { Unit(a[0]).Hp = 0; return JassValue.Null; });
    }

    private static string SubString(string? text, int start, int end)
    {
        if (text is null || start < 0 || end <= start || start >= text.Length)
        {
            return string.Empty;
        }

        return text.Substring(start, Math.Min(end, text.Length) - start);
    }

    private static HashtableState Hashtable(JassValue value)
    {
        if (value.Kind != JassValueKind.Handle || value.Handle?.State is not HashtableState table)
        {
            throw new JassRuntimeException("expected hashtable handle");
        }

        return table;
    }

    // JASS S2I/S2R follow C atoi/atof: parse the numeric prefix and stop at the first
    // non-numeric character ("126.0" -> 126), unlike strict int.TryParse.
    private static int ParseLeadingInt(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var index = 0;
        var sign = 1;
        if (text[index] is '+' or '-')
        {
            sign = text[index] == '-' ? -1 : 1;
            index++;
        }

        var value = 0;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            value = (value * 10) + (text[index] - '0');
            index++;
        }

        return sign * value;
    }

    private static float ParseLeadingFloat(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var index = 0;
        if (text[index] is '+' or '-')
        {
            index++;
        }

        while (index < text.Length && (char.IsAsciiDigit(text[index]) || text[index] is '.' or 'e' or 'E' or '+' or '-'))
        {
            if (float.TryParse(text[..(index + 1)], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                index++;
                continue;
            }

            break;
        }

        return float.TryParse(text[..index], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0f;
    }

    private static MultiboardState Multi(JassValue value)
    {
        if (value.Kind != JassValueKind.Handle || value.Handle?.State is not MultiboardState board)
        {
            throw new JassRuntimeException("expected multiboard handle");
        }

        return board;
    }

    private static MultiboardItemState MultiItem(JassValue value)
    {
        if (value.Kind != JassValueKind.Handle || value.Handle?.State is not MultiboardItemState item)
        {
            throw new JassRuntimeException("expected multiboarditem handle");
        }

        return item;
    }

    private static UnitState Unit(JassValue value)
    {
        if (value.Kind != JassValueKind.Handle || value.Handle?.State is not UnitState unit)
        {
            throw new JassRuntimeException("expected unit handle");
        }

        return unit;
    }

    private static int GetTech(Dictionary<(int Player, int Tech), int> table, int player, int tech) =>
        table.TryGetValue((player, tech), out var value) ? value : 0;

    private static JassValue Sound(JassValue[] args, string? label)
    {
        return JassValue.FromHandle(new JassHandle("sound", new SoundState { Label = label ?? string.Empty }));
    }

    private static SoundState SoundState(JassValue value)
    {
        if (value.Kind != JassValueKind.Handle || value.Handle?.State is not SoundState state)
        {
            throw new JassRuntimeException("expected sound handle");
        }

        return state;
    }

    private static T State<T>(JassValue value, string type)
    {
        if (value.Kind != JassValueKind.Handle || value.Handle?.State is not T state)
        {
            throw new JassRuntimeException($"expected {type} handle, got {value}");
        }

        return state;
    }

    private static int PlayerId(JassValue value)
    {
        if (value.Handle?.State is int id)
        {
            return id;
        }

        throw new JassRuntimeException("expected player handle");
    }

    private static int EnumId(JassValue value)
    {
        if (value.Kind == JassValueKind.Integer)
        {
            return value.Integer;
        }

        return value.Handle?.State is int id ? id : value.Handle?.Id ?? 0;
    }
}
