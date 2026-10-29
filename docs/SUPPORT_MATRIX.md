# Support matrix

Target baseline: Warcraft III 1.27.0.52240 (TFT, MPQ era) at `D:/Games/Warcraft 3.2`.
Fixture map: `C:/Users/admin/Downloads/Map Tong Hop v49.0 SS2.w3x` (HM3W, protected).

| Capability | Status | Evidence |
|---|---|---|
| MPQ archives (read) | Working | War3.mpq/War3x.mpq/War3Patch.mpq/patch.mpq/War3xlocal.mpq open; hash lookup without listfile |
| W3X map mount (HM3W+MPQ) | Working | w3i, w3e, w3u, w3b, w3d, w3a, w3t, w3q, w3h, doo, units.doo, wpm, shd, wts, wtg, wct, imp present and readable |
| MDX v800 parse | Working | Footman/peasant: geosets, nodes, materials, sequences, geoset animations |
| MDX v900/v1000 (Reforged) | Not tested | — |
| BLP1 paletted/DXT | Working | Textures\Footman.blp 256×256, many distinct colors |
| BLP1 JPEG (team colors) | Working (BGRA fix) | TeamColor00..04 decode to the WC3 palette (red/blue/teal/…) |
| BLP2 | Not tested | — |
| Skinning (4-influence rigid groups) | Working | Footman walk/death/defend poses; CPU bounds dump (`Inspector pose`) |
| Animation linear/hermite/bezier + squad | Unit-tested | TrackSamplerTests |
| Global sequences | Unit-tested | PoseEvaluator wrap test |
| Material layers + blend modes | Working | Footman 3 materials / 5 layers; RenderStateTests |
| Team color (replaceable 1) | Working | Red team patches in screenshots |
| Team glow (replaceable 2) | Fallback to TeamGlow00 | Not visually verified |
| Alpha-key (Transparent filter) | Working (clip 0.75) | Footman body/sword edges |
| Texture UV animation | Parsed, not applied | — |
| Billboarded nodes | Not implemented | — |
| ParticleEmitter2 | Parsed by War3Net, not simulated | — |
| Ribbons | Parsed by War3Net, not simulated | — |
| W3E parse (dimensions, heights, tiles, layers) | Working | 64x96 tiles, 6305 vertices, tiers -256..128 |
| Terrain splat layers | Working (approximate mask orientation) | 15 groups / 15,396 tris on fixture |
| Cliff / ramp models | Approximate tier walls only | `Textures/Cliff0.blp` skirts, no WC3 cliff models |
| Water surface | Working | W3E levels + `TerrainArt/Water.slk` height, Water00.blp |
| Object data art overrides | Working (model paths only) | `umdl`/`dfil` from w3u/w3b/w3d |
| In-app UI overlay | Working (screenshot-verified) | animation list, stats, map panel |
| Doodads (incl. destructible trees) | Working | 54 placements -> 13 visible (pathing-only skipped) |
| Destructible replaceable textures (texID/texFile) | Working | AshenTree textures resolve |
| Units placement | Implemented, no unit in fixture | UnitUI.slk file/modelScale |
| Model variations (File<N>.mdx) | Working | AshenTree1/4 |
| JASS script for fixture map | Blocked | map is protected (no war3map.j/.lua) |
| YDWE map (811.ThoiDaiBS) static load | Working | 250 instances, 64 models, 139 unresolved custom |
| YDWE extension native inventory | Working | 265 declared / 69 called; DzAPI/JAPI/Extra |
| JASS parser (War3Net) | Working | common.j + Blizzard.j + map (1.7 MB) parse in ~1.3 s |
| JASS VM | Working | VM conformance tests; target map `main` executes |
| Target map initialization | Working | 14 units, 228 triggers, 16 timers, 8 frames, 6,437 native calls |
| YDWE native implementations | Partial | 68/265 bound; all 3 init-reachable implemented |
| Timer callbacks | Working (tested) | one-shot + periodic against runtime state |
| Trigger event dispatch | Partial | registration + init triggers + timer events; gameplay events not dispatched |
| EXExecuteScript (Lua SLK query) | Missing | blocks timer-driven UI triggers |
| Text MDL models | Not supported | binary MDX only |
| CASC / Reforged | Not started | — |

## Local paths

- WC3 install: `D:/Games/Warcraft 3.2` (War3.exe reports 1.27d; MPQs + extracted `ui/`).
- Settings: `app/settings.json` (override with `WAR3ENGINE_WARCRAFT_PATH` / `WAR3ENGINE_MAP_PATH`).
