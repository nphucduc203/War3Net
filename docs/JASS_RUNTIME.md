# JASS runtime

Real interpreter for the target map's JASS initialization, built on War3Net.CodeAnalysis.Jass
as the parser. Parsing, program modeling, execution, native dispatch and game state are
separate components.

## Pipeline

1. `JassProgram.Parse` — War3Net parser → syntax tree (no execution).
2. `JassProgramModel.Build` — functions, native declarations, globals, declared types and a
   direct-call graph (calls + `ExecuteFunc("...")` targets; function references are recorded
   separately as callbacks).
3. `JassProgramMerge` — common.j + Blizzard.j + war3map.j, later declarations win.
4. `JassVirtualMachine` — tree-walking interpreter with step/depth limits, call stack and
   strict native dispatch.
5. `JassNativeRegistry` + runtime services (`JassGameRuntime`): hashtables, players, units,
   triggers, timers, groups, forces, regions, rects, locations, dialogs, sounds, camera /
   environment state, YDWE frames and sync log. Event registrations are structured
   `JassEventRegistration` records dispatched through a typed `JassEvent` payload and a scoped
   `JassEventContext` stack.
6. `JassMapHost.Run` — loads the three scripts, initializes globals, runs `main`, advances
   timers, and returns state + failure diagnostics (shared by CLI and tests).

## Language coverage

- Primitives: integer, real, boolean, string, handle, code, null; user `type` aliases.
- Globals, constants, global arrays (0..8191), local variables and local arrays.
- Functions, parameters, returns, recursion, call depth limit.
- `if / elseif / else`, `loop` + `exitwhen`, `set`, `call`, `return`.
- Arithmetic `+ - * /` (integer truncation, IEEE real division, divide-by-zero diagnostic),
  comparisons, `and`/`or` (short-circuit), `not`, unary `-`.
- String concatenation, decimal/hex/octal/real/fourcc/char literals, string unescaping.
- `function Foo` references and native-driven invocation (`RunCode` style).
- `debug` blocks are skipped in release semantics by default (option to execute).

Not implemented: coroutines/`TriggerSleepAction`, precise handle-type inheritance when
common.j types are absent (lenient mode by default), and source line numbers (the parser
exposes no spans; diagnostics report function + native + call stack).

## Native coverage

- 1,425 native signatures are known from the merged scripts; implementations are explicit
  and missing natives throw `UnsupportedNativeException` with signature + call stack.
- 69 of the target map's 265 declared extension natives are bound (DzAPI frames/events,
  platform queries, RequestExtra*, init natives, EXExecuteScript through the Lua bridge);
  data fields resolve map object data (w3t/w3u) before base SLKs. All three extension natives reachable
  from `main` (`DzLoadToc`, `DzTriggerRegisterKeyEvent`, `DzTriggerRegisterSyncData`) are
  implemented.
- Standard runtime implemented: hashtables (int/real/bool/string/handles), triggers,
  timers + scheduler, units, destructables, items, effects, groups, forces, rects,
  regions, locations, dialogs, sounds, camera fields, player/game state, tech/ability
  state, math/string utilities, enum constants (`Convert*`).
- `EXExecuteScript` is implemented as the YDWE Lua bridge: trigstring decoding,
  `return (<expr>)`, persistent Lua state (NLua/Lua 5.4), string/number
  results, null for other types, errors recorded and returned as JASS null.
  `require('jass.slk')` is backed by base-game ItemData/UnitWeapons/ItemStrings data plus
  map object data (w3t/w3u) and war3map.wts TRIGSTR resolution.

## Event dispatch

- `JassEventRegistration` stores kind, exact target-version event ID, player, source handle
  (dialog/button/timer/region/unit), optional filter, owning trigger, registration function,
  site/call chain and dispatch/callback/failure counters.
- `JassGameRuntime.Dispatch(select, expiredTimer)` snapshots registrations in order, pushes a
  typed `JassEvent` context per registration, evaluates the filter, then conditions, then
  actions. `TriggerExecute` swaps only `Context.Trigger` and restores it; nested triggers see
  their own `GetTriggeringTrigger` while the caller keeps its event payload.
- Destroyed triggers are removed from the registration table; disabled triggers are skipped;
  callback exceptions restore the context, increment failure counters and rethrow.
- Producers implemented: timer scheduler (`timer`, `timer-expire`), engine chat adapter
  (`EVENT_PLAYER_CHAT`), dialog clicks (`dialog`, `dialog-button`) and unit selection
  (`EVENT_PLAYER_UNIT_SELECTED`/`DESELECTED`, unit-scoped `EVENT_UNIT_SELECTED`/`DESELECTED`).
  Selection is owner-only, deduplicated and published from the runtime selection state used by
  viewer picking and the JASS `SelectUnit`/`ClearSelection` natives. All other registered
  families remain level 1 in `docs/event-dispatch-coverage.json`.
- Response natives read only the current context (`GetClickedButton`, `GetClickedDialog`,
  `GetTriggerPlayer`, `GetTriggeringTrigger`, `GetExpiredTimer`, `GetTriggerUnit`, damage,
  spell and order getters); outside an event they return null/zero instead of fabricated data.

## Verified target-map result

`War3Engine.Inspector runmap` (map `811.ThoiDaiBS.w3x`, game 1.27.0.52240):

```
scripts: common.j=166690 blizzard.j=451847 war3map.j=1749409
functions=2130 natives=1425 globals=1443 types=91
globals initialized
main completed (steps ~56k)
state: units=14 triggers=229 timers=22 frames=701 tocs=2
native calls: 44183 (3 s advance, 380 timer callbacks, 3 Lua calls)
```

This is real execution: units are created by map code, triggers are constructed and
registered, timers fire, YDWE frames are created/configured, and the Lua bridge answers
the map's `jass.slk` queries. Chat command `T5` dispatches the map's command processor and
sets `udg_guaibs=5` with a display message. A real dialog click dispatches the map's startup
mode-selection trigger and changes `udg_yxms`/messages/nested triggers. Runtime units are
rendered by the client (`--run-jass`). Detailed reports:
`docs/target-map-gameplay-scenario.md`, `docs/event-dispatch-roadmap.md`.

## Tests

- `JassVmTests` — globals/arrays/functions/recursion/loops, natives, code refs, nulls,
  strings, type errors, step limits, global handle initialization, ExecuteFunc reachability.
- `JassMapRuntimeTests` — target map initialization through the VM; timer scheduling with
  one-shot and periodic callbacks on real runtime state.
- `EventDispatchTests` — dialog registration and click, nested TriggerExecute/event context
  restoration, filters, disabled/destroyed triggers, exact dialog-button matching, timer
  context, selection/deselection, owner and player scoping, JASS SelectUnit path, and the
  engine input adapter paths.
- `MapGameplayScenarioTests` — real map: init, timers, Lua, chat T5, startup dialog click and
  unit selection with hashtable verification.

Commands:

```
dotnet run --project src/War3Engine.Inspector -- jassparse
dotnet run --project src/War3Engine.Inspector -- runmap
dotnet run --project src/War3Engine.Inspector -- runmap "" 5000000 3000   # advances timers, shows next blocker
dotnet run --project src/War3Engine.Inspector -- mapaudit                 # writes the dependency reports
```

Generated reports: `docs/target-map-jass-dependencies.md`,
`docs/target-map-native-status.json`.
