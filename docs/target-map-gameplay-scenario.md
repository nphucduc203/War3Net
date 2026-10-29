# Target-map gameplay scenario (Phase 1–7 report)

Map: `C:/Users/admin/Downloads/811.ThoiDaiBS.w3x`
Game: Warcraft III 1.27.0.52240, YDWE runtime Lua **5.3.6** (verified from
`luacore.dll` strings in the local YDWE 1.2.9C install).

## Phase 1 — Exact failure trace (before the fix)

Reproduction: `Inspector runmap <map> 5000000 3000` (initializes globals, runs `main`,
advances timers 3 s).

Observed path:

```
main -> RunInitializationTriggers -> ConditionalTriggerExecute -> TriggerExecute
     -> Trig_mnzy02Actions -> EXExecuteScript("(require'jass.slk').item[...]...")
```

- Timer state: map init created 16 timers (0-time trigger timers plus YDWE timers); the
  3-second tick fired `Trig_mnzy02Actions` through the timer scheduler.
- JASS argument: `(require'jass.slk').item[<custom item id>].class/prio/Name/Ubertip`
  and `(require'jass.slk').unit[<unit id>].weapsOn`.
- Lua environment at that point: none in the engine; `EXExecuteScript` was unregistered.
- Classification: **missing native + missing Lua bridge/module**, not a timer or string
  conversion failure. Reference implementation
  (`YDWE/.../yd_lua_engine/lua_engine/lua_loader.cpp`) wraps the trigstring-decoded input
  as `return (<expr>)`, evaluates it in a persistent Lua state, and returns a JASS string
  only when `lua_isstring` is true; syntax/runtime errors return JASS null.

Additional failure found immediately after the bridge was added: `GetExpiredTimer`
missing inside timer callbacks. Implemented with real timer context. Then the 3 s advance
completed all 380 callbacks with 3 Lua calls and no errors.

## Phase 2–3 — EXExecuteScript bridge

Implemented in `src/War3Engine.Scripting/Lua`:

- `NLuaBridge` — persistent Lua state, `return (<trigstring-decoded expression>)`
  evaluation, JASS-string conversion for Lua strings/numbers, null for other types,
  syntax/runtime errors recorded and returned as JASS null (matching YDWE error behavior).
- `JassTrigString` — removes `|cAARRGGBB`/`|r` color codes and converts `|n`.
- `require('jass.slk')` module with lazy `item/unit/ability/buff/doodad/destructable/upgrade`
  tables, backed by `JassSlkQueryProvider` (real `Units/ItemData.slk`,
  `Units/UnitWeapons.slk`, `Units/ItemStrings.txt`).
- Lua version note: target uses Lua 5.3.6; the engine bridge uses NLua/Lua 5.4 because the
  target luacore.dll is 32-bit. Numeric conformance tests cover integer/float tostring (2 vs
  2.0), //, %, scientific notation, 14-digit precision, string.format, tonumber, 64-bit
  wrap, and locale independence. Bit-identical 5.3.6 behavior remains a documented risk.
- `jass.slk` resolves map object data (w3t/w3u) before base SLKs and resolves TRIGSTR via
  war3map.wts. The map's 3 custom Ubertip queries hit object data; 2 TRIGSTR references
  (I033/I010) are missing from wts and stay literal. Missing fields return nil, never
  fabricated values.

Tests (`LuaBridgeTests`): string/number results, raw and module-backed expressions,
trigstring decoding, nil/boolean/table → null, missing field logging, syntax error,
runtime error, 20 repeated calls, JASS caller receiving the value/null.

## Phase 4 — Scheduler / coroutines

- Timer callbacks are dispatched synchronously in due-time order; `GetExpiredTimer`,
  trigger and chat event contexts are saved/restored around callbacks.
- Init triggers execute through `RunInitializationTriggers`; chat events are dispatched by
  `DispatchChat` (empty match string means "any chat", matching the map's command processor).
- `TriggerSleepAction`/`PolledWait` are **not used** by the target map (0 occurrences), so
  coroutine suspension is not on the required path. They remain unsupported and tracked.

## Phase 5 — Natives exercised by the scenario

Verified path (after fix) uses: TriggerAddAction/Condition, TriggerRegisterPlayerChatEvent,
TriggerExecute/Evaluate, timer create/start/expiry, GetExpiredTimer, hashing/save/load,
string formatting/substitution (Blizzard helpers), unit creation/queries, groups/forces,
DzFrame* UI setup, EXExecuteScript, DisplayText* and the Convert* enum helpers.

69 of the map's 265 declared extension natives are bound; all three static-init natives
and EXExecuteScript are implemented. Missing natives fail with signature + call stack.
Report: `docs/target-map-native-status.json`.

## Phase 6 — Runtime units in the renderer

- Fixed a real VM bug found here: JASS FourCC literals were parsed big-endian while map
  ids are little-endian, so runtime unit ids never matched UnitUI/object data. Now
  `'hfoo' == 0x6F6F6668`.
- `MapArtCache` resolves custom unit ids through `war3map.w3u` (`umdl`, `.mdl`→`.mdx`) and
  samples terrain height for placement.
- Client mode: `--run-jass --advance-ms 3000 --chat T5` builds the JASS world, renders
  runtime units (14 units, 8 unique models, 0 unresolved), and includes JASS statistics
  in the overlay. F5 dispatches the configured chat command through the event pipeline.
- Evidence: `artifacts/jass_scenario5.png` (runtime boss + terrain + JASS panel).

## Phase 7 — Acceptance results

`MapGameplayScenarioTests.TargetMapRunsTimerLuaBridgeChatAndDialogClick` (real map) asserts:

- map scripts parse and `main` completes;
- timer callbacks > 0 (380 with 3 s advance);
- EXExecuteScript was called (3 calls), no Lua errors;
- chat `T5` fired a real trigger (`chatFired=1`);
- map global `udg_guaibs == 5`;
- display message produced;
- at least one runtime unit resolves a model.

All 53 pre-existing tests plus the 14 event-dispatch tests pass (67 total). Extended runs:
120 s (16,312 callbacks) and 300 s (40,850 callbacks) complete without unsupported natives;
engine hotkey->chat dispatch has a deterministic test.

## Phase 8 — Dialog event dispatch (interactive gameplay slice)

- Event bus: structured `JassEventRegistration` (kind, exact event ID, player, source handle,
  filter, trigger, registration site/call chain, dispatch counters), typed `JassEvent` payload,
  scoped `JassEventContext` stack with push/pop, nested `TriggerExecute` context restoration,
  destroy/disable cleanup, failure counters.
- Producer: the viewer renders the visible dialog for player 0 and hit-tests real mouse clicks;
  `ViewerInput.DispatchDialogClick` -> `JassGameRuntime.DispatchDialogClick` produces
  `GetClickedButton`/`GetClickedDialog`/`GetTriggerPlayer` context for `dialog` and
  `dialog-button` registrations. The map's startup dialog is clickable in the viewer.
- Verified on the actual map: clicking `Mô hình luyện tập` fires the map's dialog trigger,
  sets `udg_yxms=false->true`, appends the practice-mode message, hides the dialog and
  executes the nested `gg_trg_xznd03______u` trigger. Event depth returns to 0, failures 0.
- Evidence: `artifacts/dialog_before.png` (dialog visible) and `artifacts/dialog_after.png`
  (dialog hidden, message in the UI log); `Inspector eventaudit` writes
  `docs/event-dispatch-coverage.json`; `Inspector metrics` includes the dialog click.
- Manual OS-input procedure is documented in `docs/event-dispatch-roadmap.md` section 7;
  automated scenarios use the engine input adapter and are not claimed as OS-level input.

## Phase 9 — Unit selection event (first unit gameplay path)

- Producer: `JassGameRuntime` keeps per-player selection sets. Viewer picking projects runtime
  units to screen space and selects the nearest unit within 45 px of a real left click;
  `SelectUnit`/`ClearSelection` JASS natives publish through the same path. Selection is
  owner-only, deduplicated, and `RemoveUnit` clears stale selected units.
- Dispatch: adding a unit raises `EVENT_PLAYER_UNIT_SELECTED` (24) for the selecting player and
  `EVENT_UNIT_SELECTED` (57) for unit-scoped registrations of that exact unit; removal publishes
  25/58. Payload provides `GetTriggerUnit`, `GetTriggerPlayer`, `GetTriggeringTrigger`.
- Verified on the actual map: selecting `U00O` as owner player 8 fired the registered trigger
  `gg_trg____________________005` once; its action stored the handle in `YDHT` under
  `(GetHandleId(Player(8)), 0x2C1C8DD5)`. Wrong-player selection and duplicate selection
  produced no events; `ClearSelection` emptied the set; event depth returned to 0.
- Evidence: `artifacts/selection_after.png` (green marker and `selected P8 U00O` line);
  `Inspector eventaudit` reports `player-unit-event:24` level 5. `Inspector metrics` includes
  selection and still reports 16,202 timer callbacks / 0 failures.
- Remaining event path: orders -> movement, then attack/damage/death, spells/items. Combat is
  intentionally not faked without an attack/damage simulation.

Remaining gaps:

- OS-level automated keyboard injection could not focus the background game window in this
  sandbox (foreground lock); the F5 chat-dispatch code path and the underlying chat event
  pipeline are verified, but a human key press was not automated. Documented as a test gap.
- `jass.slk` reads base SLKs plus map object data; two TRIGSTR references missing from wts stay literal.
- 196 extension natives remain unbound; gameplay beyond the validated timer/chat path is
  not claimed.
- No coroutine sleeps; dialog and unit selection are dispatched. Orders, attack/damage/death,
  spells, items, player, region, state, sync and YDWE input events remain registered without a
  producer.
- Rendering remains the simplified terrain/cliff/water pipeline; runtime units are rendered
  with their static stand pose and current position/facing.
