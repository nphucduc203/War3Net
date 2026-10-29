# Target runtime profile

- Warcraft III: 1.27.0.52240 (verified from War3.exe/game.dll).
- YDWE: WorldEdit v1.2.9C, YDWEConfig 2.0.0.2485; KKWE sets 2263/2332/2362/2485.
- Runtime plugin: dz_w3_plugin.dll 1.0.0.51 (variants 1.0.0.248, 2.0.0.0), dznep.dll, version.dll proxy.
- Lua: target 5.3.6 (`luacore.dll`, PE i386); engine bridge NLua/Lua 5.4 (exact 5.3.6 DLL cannot load in x64).
- Script: JASS only (`war3map.j`, no `war3map.lua`); common.j + Blizzard.j + map merged at load.
- Natives: 1425 signatures known, 470 implemented, 69/265 map extensions implemented.
- Data: 3 custom field queries, 3 object-data hits, 2912 wts entries.
- Events: 1911 registrations across 13 types; timer/chat/init/dialog/selection dispatched; 34 trigger executions.
- Scenario: 120 s simulated, dialog click at 3000 ms, 16202 timer callbacks, 11 units alive, failure=none.
