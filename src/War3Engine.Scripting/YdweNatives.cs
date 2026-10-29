namespace War3Engine.Scripting;

/// <summary>
/// YDWE/DzAPI extension natives used by the target map. Behavior is stateful where the
/// engine can model it; platform-only queries return documented headless values.
/// </summary>
public static class YdweNatives
{
    private const int RootFrame = 1;

    public static void Register(JassNativeRegistry registry, JassGameRuntime runtime)
    {
        void N(string name, string returns, string[] parameters, JassNativeImplementation impl, string status = "implemented", string sideEffects = "")
        {
            registry.Register(new JassNative(name, returns, parameters, (vm, args) =>
            {
                runtime.CountNative(name);
                return impl(vm, args);
            }, status, sideEffects));
        }

        var none = Array.Empty<string>();

        // Init-path natives ----------------------------------------------------
        N("DzLoadToc", "nothing", new[] { "string" }, (_, a) =>
        {
            runtime.LoadedTocs.Add(a[0].Text ?? string.Empty);
            return JassValue.Null;
        }, sideEffects: "Records loaded TOC file names.");
        N("DzTriggerRegisterKeyEvent", "nothing", new[] { "trigger", "integer", "integer", "boolean", "string" },
            (_, a) => Register(a, $"key-event:{a[1].Integer}:{a[2].Integer}:{a[3].Boolean}:{a[4].Text}"));
        N("DzTriggerRegisterKeyEventByCode", "nothing", new[] { "trigger", "integer", "integer", "boolean", "code" },
            (_, a) => Register(a, $"key-event-code:{a[1].Integer}:{a[2].Integer}:{a[3].Boolean}"));
        N("DzTriggerRegisterSyncData", "nothing", new[] { "trigger", "string", "boolean" },
            (_, a) => Register(a, $"sync:{a[1].Text}:{a[2].Boolean}"));
        N("DzTriggerRegisterMouseEvent", "nothing", new[] { "trigger", "integer", "integer", "boolean", "string" },
            (_, a) => Register(a, $"mouse:{a[1].Integer}:{a[2].Integer}:{a[3].Boolean}:{a[4].Text}"));
        N("DzTriggerRegisterMouseEventByCode", "nothing", new[] { "trigger", "integer", "integer", "boolean", "code" },
            (_, a) => Register(a, $"mouse-code:{a[1].Integer}:{a[2].Integer}:{a[3].Boolean}"));
        N("DzTriggerRegisterMouseWheelEvent", "nothing", new[] { "trigger", "boolean", "string" },
            (_, a) => Register(a, $"wheel:{a[1].Boolean}:{a[2].Text}"));
        N("DzTriggerRegisterMouseWheelEventByCode", "nothing", new[] { "trigger", "boolean", "code" },
            (_, a) => Register(a, $"wheel-code:{a[1].Boolean}"));
        N("DzTriggerRegisterMouseMoveEvent", "nothing", new[] { "trigger", "boolean", "string" },
            (_, a) => Register(a, $"move:{a[1].Boolean}:{a[2].Text}"));
        N("DzTriggerRegisterMouseMoveEventByCode", "nothing", new[] { "trigger", "boolean", "code" },
            (_, a) => Register(a, $"move-code:{a[1].Boolean}"));
        N("DzTriggerRegisterWindowResizeEvent", "nothing", new[] { "trigger", "boolean", "string" },
            (_, a) => Register(a, $"resize:{a[1].Boolean}:{a[2].Text}"));
        N("DzTriggerRegisterWindowResizeEventByCode", "nothing", new[] { "trigger", "boolean", "code" },
            (_, a) => Register(a, $"resize-code:{a[1].Boolean}"));
        N("DzGetMouseX", "integer", none, (_, _) => JassValue.FromInteger(0));
        N("DzGetMouseY", "integer", none, (_, _) => JassValue.FromInteger(0));
        N("DzGetMouseXRelative", "integer", none, (_, _) => JassValue.FromInteger(0));
        N("DzGetMouseYRelative", "integer", none, (_, _) => JassValue.FromInteger(0));
        N("DzGetMouseTerrainX", "real", none, (_, _) => JassValue.FromReal(0f));
        N("DzGetMouseTerrainY", "real", none, (_, _) => JassValue.FromReal(0f));
        N("DzGetMouseTerrainZ", "real", none, (_, _) => JassValue.FromReal(0f));
        N("DzIsMouseOverUI", "boolean", none, (_, _) => JassValue.FromBoolean(false));
        N("DzIsKeyDown", "boolean", new[] { "integer" }, (_, _) => JassValue.FromBoolean(false));
        N("DzSetMousePos", "nothing", new[] { "integer", "integer" }, (_, _) => JassValue.Null);

        // Headless-safe platform/input queries ---------------------------------
        N("DzIsWindowActive", "boolean", none, (_, _) => JassValue.FromBoolean(true), "implemented", "Headless client is always active.");
        N("DzGetWindowWidth", "integer", none, (_, _) => JassValue.FromInteger(1920));
        N("DzGetWindowHeight", "integer", none, (_, _) => JassValue.FromInteger(1080));
        N("DzGetWindowX", "integer", none, (_, _) => JassValue.FromInteger(0));
        N("DzGetWindowY", "integer", none, (_, _) => JassValue.FromInteger(0));
        N("DzGetLocale", "string", none, (_, _) => JassValue.FromString("enUS"));
        N("DzGetWheelDelta", "integer", none, (_, _) => JassValue.FromInteger(0));
        N("DzGetTriggerKey", "integer", none, (_, _) => JassValue.FromInteger(0));
        N("DzGetTriggerKeyPlayer", "player", none, (_, _) => JassValue.FromHandle(runtime.Player(0)));
        N("DzGetTriggerSyncData", "string", none, (_, _) => JassValue.FromString(string.Empty));
        N("DzGetTriggerSyncPlayer", "player", none, (_, _) => JassValue.FromHandle(runtime.Player(0)));
        N("DzSyncData", "nothing", new[] { "string", "string" },
            (_, a) => { runtime.SyncLog.Add($"{a[0].Text}|{a[1].Text}"); return JassValue.Null; });
        N("DzSyncDataImmediately", "nothing", new[] { "string", "string" },
            (_, a) => { runtime.SyncLog.Add($"{a[0].Text}|{a[1].Text}"); return JassValue.Null; });
        N("DzSyncBuffer", "nothing", new[] { "string", "string", "integer" },
            (_, a) => { runtime.SyncLog.Add($"{a[0].Text}|{a[1].Text}"); return JassValue.Null; });
        N("DzEnableWideScreen", "nothing", new[] { "boolean" }, (_, _) => JassValue.Null);
        N("DzAPI_Map_HasMallItem", "boolean", new[] { "player", "string" }, (_, _) => JassValue.FromBoolean(false),
            "implemented", "No platform backend: player has no mall items.");
        N("DzSetUnitModel", "nothing", new[] { "unit", "string" },
            (_, a) => { Unit(a[0]).Model = a[1].Text ?? string.Empty; return JassValue.Null; });

        // Platform data queries used by YDWE libraries: documented empty values.
        var extraParams = new[] { "integer", "player", "string", "string", "boolean", "integer", "integer", "integer" };
        N("RequestExtraIntegerData", "integer", extraParams, (_, _) => JassValue.FromInteger(0), "implemented", "No platform backend.");
        N("RequestExtraBooleanData", "boolean", extraParams, (_, _) => JassValue.FromBoolean(false), "implemented", "No platform backend.");
        N("RequestExtraStringData", "string", extraParams, (_, _) => JassValue.FromString(string.Empty), "implemented", "No platform backend.");
        N("RequestExtraRealData", "real", extraParams, (_, _) => JassValue.FromReal(0f), "implemented", "No platform backend.");

        // Frames ---------------------------------------------------------------
        N("DzGetGameUI", "integer", none, (_, _) => JassValue.FromInteger(RootFrame));
        N("DzCreateFrame", "integer", new[] { "string", "integer", "integer" },
            (_, a) => JassValue.FromInteger(CreateFrame(runtime, a[0].Text ?? string.Empty, string.Empty, a[1].Integer)));
        N("DzCreateFrameByTagName", "integer", new[] { "string", "string", "integer", "string", "integer" },
            (_, a) => JassValue.FromInteger(CreateFrame(runtime, a[0].Text ?? string.Empty, a[1].Text ?? string.Empty, a[2].Integer)));
        N("DzFrameFindByName", "integer", new[] { "string", "integer" }, (_, a) => JassValue.FromInteger(FindFrame(runtime, a[0].Text, a[1].Integer)));
        N("DzSimpleFrameFindByName", "integer", new[] { "string", "integer" }, (_, a) => JassValue.FromInteger(FindFrame(runtime, a[0].Text, a[1].Integer)));
        N("DzSimpleFontStringFindByName", "integer", new[] { "string", "integer" }, (_, a) => JassValue.FromInteger(FindFrame(runtime, a[0].Text, a[1].Integer)));
        N("DzFrameSetPoint", "nothing", new[] { "integer", "integer", "integer", "integer", "real", "real" },
            (_, a) => SetFrame(runtime, a[0].Integer, f =>
            {
                f.Point = a[1].Integer;
                f.RelativeFrame = a[2].Integer;
                f.RelativePoint = a[3].Integer;
                f.PointX = a[4].Real;
                f.PointY = a[5].Real;
            }));
        N("DzFrameSetAbsolutePoint", "nothing", new[] { "integer", "integer", "real", "real" },
            (_, a) => SetFrame(runtime, a[0].Integer, f => { f.Point = a[1].Integer; f.PointX = a[2].Real; f.PointY = a[3].Real; }));
        N("DzFrameClearAllPoints", "nothing", new[] { "integer" }, (_, a) => SetFrame(runtime, a[0].Integer, f => { f.Point = 0; f.RelativeFrame = 0; }));
        N("DzFrameShow", "nothing", new[] { "integer", "boolean" }, (_, a) => SetFrame(runtime, a[0].Integer, f => f.Visible = a[1].Boolean));
        N("DzFrameSetEnable", "nothing", new[] { "integer", "boolean" }, (_, a) => SetFrame(runtime, a[0].Integer, f => f.Enabled = a[1].Boolean));
        N("DzFrameIsVisible", "boolean", new[] { "integer" }, (_, a) => JassValue.FromBoolean(GetFrame(runtime, a[0].Integer).Visible));
        N("DzFrameSetText", "nothing", new[] { "integer", "string" }, (_, a) => SetFrame(runtime, a[0].Integer, f => f.Text = a[1].Text ?? string.Empty));
        N("DzFrameSetTexture", "nothing", new[] { "integer", "string", "integer" }, (_, a) => SetFrame(runtime, a[0].Integer, f => f.Texture = a[1].Text ?? string.Empty));
        N("DzFrameSetSize", "nothing", new[] { "integer", "real", "real" }, (_, a) => SetFrame(runtime, a[0].Integer, f => { f.Width = a[1].Real; f.Height = a[2].Real; }));
        N("DzFrameSetFont", "nothing", new[] { "integer", "string", "real", "integer" }, (_, a) => SetFrame(runtime, a[0].Integer, f => f.Font = a[1].Text ?? string.Empty));
        N("DzFrameSetTextAlignment", "nothing", new[] { "integer", "integer" }, (_, a) => SetFrame(runtime, a[0].Integer, f => f.TextAlignment = a[1].Integer));
        N("DzFrameSetClip", "nothing", new[] { "integer", "boolean" }, (_, a) => SetFrame(runtime, a[0].Integer, _ => { }));
        N("DzFrameSetModel", "nothing", new[] { "integer", "string", "integer", "integer" }, (_, a) => SetFrame(runtime, a[0].Integer, f => f.Texture = a[1].Text ?? string.Empty));
        N("DzFrameSetScriptByCode", "nothing", new[] { "integer", "integer", "code", "boolean" },
            (_, a) => SetFrame(runtime, a[0].Integer, f => f.Scripts[a[1].Integer] = a[2]));
        N("DzFrameSetUpdateCallbackByCode", "nothing", new[] { "code" }, (_, a) => { runtime.Globals["dz_update_callback"] = a[0]; return JassValue.Null; });
        N("DzGetTriggerUIEventFrame", "integer", none, (_, _) => JassValue.FromInteger(0));
        N("DzFrameGetCommandBarButton", "integer", new[] { "integer", "integer" }, (_, _) => JassValue.FromInteger(CreateFrame(runtime, "CommandBarButton", string.Empty, RootFrame)));
        N("DzFrameGetHeroBarButton", "integer", new[] { "integer" }, (_, _) => JassValue.FromInteger(CreateFrame(runtime, "HeroBarButton", string.Empty, RootFrame)));
        N("DzFrameGetUpperButtonBarButton", "integer", new[] { "integer" }, (_, _) => JassValue.FromInteger(CreateFrame(runtime, "UpperButtonBarButton", string.Empty, RootFrame)));
    }

    private static JassValue Register(JassValue[] args, string description)
    {
        TriggerState trigger;
        if (args[0].IsNull)
        {
            trigger = UiTriggerHolder.Current ??= new TriggerState();
            trigger.Events.Add(description);
            return JassValue.Null;
        }

        if (args[0].Kind != JassValueKind.Handle || args[0].Handle?.State is not TriggerState state)
        {
            throw new JassRuntimeException("expected trigger handle");
        }

        state.Events.Add(description);
        return JassValue.Null;
    }

    private static class UiTriggerHolder
    {
        [ThreadStatic]
        public static TriggerState? Current;
    }

    private static UnitState Unit(JassValue value)
    {
        if (value.Handle?.State is not UnitState unit)
        {
            throw new JassRuntimeException("expected unit handle");
        }

        return unit;
    }

    private static int CreateFrame(JassGameRuntime runtime, string kind, string name, int parent)
    {
        var id = runtime.Frames.Count == 0 ? 2 : runtime.Frames.Keys.Max() + 1;
        runtime.Frames[id] = new FrameState { Id = id, Kind = kind, Name = name, Parent = parent };
        return id;
    }

    private static int FindFrame(JassGameRuntime runtime, string? name, int fallback)
    {
        if (name is not null)
        {
            foreach (var frame in runtime.Frames.Values)
            {
                if (string.Equals(frame.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return frame.Id;
                }
            }
        }

        return fallback;
    }

    private static FrameState GetFrame(JassGameRuntime runtime, int id)
    {
        if (!runtime.Frames.TryGetValue(id, out var frame))
        {
            frame = new FrameState { Id = id, Kind = "unknown", Parent = RootFrame };
            runtime.Frames[id] = frame;
        }

        return frame;
    }

    private static JassValue SetFrame(JassGameRuntime runtime, int id, Action<FrameState> update)
    {
        update(GetFrame(runtime, id));
        return JassValue.Null;
    }
}
