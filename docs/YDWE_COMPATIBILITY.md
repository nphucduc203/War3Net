# YDWE compatibility

The engine targets standard Warcraft III first; YDWE/extended runtimes are an optional
layer (`War3Engine.Compat`) that never loads `YDWEConfig.exe` and never mutates the game
installation.

## Target installation inventory (verified 2026-10-29)

| Component | Version | Evidence |
|---|---|---|
| Warcraft III client | 1.27.0.52240 (f7ba2a3) | `War3.exe` / `game.dll` file version |
| YDWE editor bundle | WorldEdit v1.2.9C, YDWEConfig 2.0.0.2485 | `4_we_WorldEdit v1.2.9c/WorldEdit v1.2.9C/bin/YDWEConfig.exe` |
| KKWE plugin sets | 2263 / 2332 / 2362 / 2485 (folders) | `KKWE*` directories |
| DzAPI runtime plugin | dz_w3_plugin.dll 1.0.0.51 (+ variants 1.0.0.248, 2.0.0.0) | game root and `dz_w3_plugin*` folders |
| NEP plugin | dznep.dll (no version resource) | game root |
| DLL proxy | version.dll 1.0.0.1 | game root |

The editor-side components (luacore, LuaEngine, ydbase, YDWEStartup, YDClient, …) are not
needed to run the engine's parsers/renderer.

## Map dependency report

`War3Engine.Inspector compat [mapPath] [report.txt]` prints:

- game version and detected components,
- script language and YDWE libraries used (`//globals from ...`),
- every extension native declared by the compiled script with call counts,
- provider classification (DzAPI / YDWE-JAPI / YDWE-Extra),
- `war3map.imp` resources resolved vs missing,
- whether compatibility profiles are enabled.

Fixture: `811.ThoiDaiBS.w3x` (243.9 MB)
- JASS (no Lua), 22,072 lines; 25 YDWE libraries including DzAPI, BzAPI, LBKKAPI,
  YDWEBase, YDWETimerSystem, YDWEJapiEffect.
- 265 extension natives declared, 69 called: DzAPI 229 declared / 43 called,
  YDWE-JAPI 31 / 22, YDWE-Extra 5 / 4.
- 2,024 imported files, 1 missing (`8x8Line.tga`).
- Representative natives: `DzGetMouseTerrainX`, `DzCreateFrame`, `DzSetUnitTexture`,
  `DzTriggerRegisterKeyEvent`, `EXGetUnitAbility`, `EXSetAbilityDataString`,
  `EXExecuteScript`, `RequestExtraIntegerData`.

## Profiles and toggle

`CompatibilityProfiles` declares: classic 1.27, `ydwe` (YDWE-JAPI + Extra), `kkwe`,
`dzapi`. Enable with `"enableYdweCompat": true` in `app/settings.json`. When enabled the
report marks required natives as covered by *stubs*; it does **not** claim the map runs.
When disabled they are reported as unsupported.

## Status

- Inventory and classification: implemented and tested against the fixture.
- Map script execution: not implemented (no JASS VM yet). The compatibility layer's job
  today is dependency reporting and explicit, togglable provider registration points.
- Rendering the fixture statically: works with imported map assets; 139 of 1,091 doodads
  currently resolve to no model (custom ids / text MDL), tracked in the report.
