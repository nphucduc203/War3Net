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
   environment state, YDWE frames and sync log.
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
- 68 of the target map's 265 declared extension natives are bound (DzAPI frames/events,
  platform queries, RequestExtra*, init natives). All three extension natives reachable
  from `main` (`DzLoadToc`, `DzTriggerRegisterKeyEvent`, `DzTriggerRegisterSyncData`) are
  implemented.
- Standard runtime implemented: hashtables (int/real/bool/string/handles), triggers,
  timers + scheduler, units, destructables, items, effects, groups, forces, rects,
  regions, locations, dialogs, sounds, camera fields, player/game state, tech/ability
  state, math/string utilities, enum constants (`Convert*`).
- `EXExecuteScript` (Lua JASS-SLK query) is **not implemented**; reaching it fails loudly.

## Verified target-map result

`War3Engine.Inspector runmap` (map `811.ThoiDaiBS.w3x`, game 1.27.0.52240):

```
scripts: common.j=166690 blizzard.j=451847 war3map.j=1749409
functions=2130 natives=1425 globals=1443 types=91
globals initialized
main completed (steps ~56k)
state: units=14 triggers=228 timers=16 frames=8 tocs=1
native calls: 6437
```

This is real execution: units are created by map code, 228 triggers are constructed and
registered, 16 timers are scheduled, and YDWE frames are created/configured.

Advancing timers by 3 s enters a map UI trigger that calls `EXExecuteScript` with
`(require'jass.slk').item[...]...` Lua queries, which is the next blocker for a full
gameplay scenario.

## Tests

- `JassVmTests` — globals/arrays/functions/recursion/loops, natives, code refs, nulls,
  strings, type errors, step limits, global handle initialization, ExecuteFunc reachability.
- `JassMapRuntimeTests` — target map initialization through the VM; timer scheduling with
  one-shot and periodic callbacks on real runtime state.

Commands:

```
dotnet run --project src/War3Engine.Inspector -- jassparse
dotnet run --project src/War3Engine.Inspector -- runmap
dotnet run --project src/War3Engine.Inspector -- runmap "" 5000000 3000   # advances timers, shows next blocker
dotnet run --project src/War3Engine.Inspector -- mapaudit                 # writes the dependency reports
```

Generated reports: `docs/target-map-jass-dependencies.md`,
`docs/target-map-native-status.json`.
