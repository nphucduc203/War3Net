# Support matrix

Target baseline: Warcraft III: The Frozen Throne 1.27d, MPQ era (`D:/Games/Warcraft 3.2`).
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
| Terrain mesh + ground textures | Working (no splat blending) | 12,288 tris, 7 Cityscape textures |
| Cliff models / ramps / water surface | Not implemented | tiers currently stretch ground quads |
| Doodads (incl. destructible trees) | Working | 54 placements -> 13 visible (pathing-only skipped) |
| Destructible replaceable textures (texID/texFile) | Working | AshenTree textures resolve |
| Units placement | Implemented, no unit in fixture | UnitUI.slk file/modelScale |
| Model variations (File<N>.mdx) | Working | AshenTree1/4 |
| JASS script for fixture map | Blocked | map is protected (no war3map.j/.lua) |
| CASC / Reforged | Not started | — |

## Local paths

- WC3 install: `D:/Games/Warcraft 3.2` (War3.exe reports 1.27d; MPQs + extracted `ui/`).
- Settings: `app/settings.json` (override with `WAR3ENGINE_WARCRAFT_PATH` / `WAR3ENGINE_MAP_PATH`).
