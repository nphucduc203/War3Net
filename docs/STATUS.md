# Project status (2026-10-29)

Target baseline: Warcraft III **1.27.0.52240** MPQ install at `D:/Games/Warcraft 3.2`
(not 1.27d). Maps:
- `C:/Users/admin/Downloads/Map Tong Hop v49.0 SS2.w3x` - standard-ish fixture, protected (no script).
- `C:/Users/admin/Downloads/811.ThoiDaiBS.w3x` - YDWE map with JASS and 265 extension natives.

Backup remote: `https://github.com/nphucduc203/War3Net` (branch `main`). The working tree
uses the user's `GITHUB_TOKEN` only through a per-command credential helper; no token is
stored in the repository or git config.

.NET/VS: SDK 10.0.401 + Visual Studio 2026 18.10. The "targeting a version of .NET which
is not installed" warning came from vendored War3Net projects multi-targeting net6.0
(no .NET 6 runtime). They are now .NET 10-only and `global.json` pins the SDK.

## Verified working

### Milestone A — native model viewer: DONE
`War3Engine.Client --model ... --anim ... --time ... --screenshot ...`

- Footman (57 nodes, 5 geosets, 3 materials, 13 sequences) loads from War3.mpq with BLP
  textures (paletted + JPEG team colors) and renders skinned animation.
- Poses verified: Stand, Walk, Attack, Death (collapses to ground), Stand Defend (shield up).
- Evidence: `artifacts/footman_stand.png`, `footman_walk_0/250/500.png`,
  `footman_attack.png`, `footman_death.png`, `footman_defend.png`, `peasant_walk.png`.
- Blend/depth state mapping unit-tested for all seven WC3 filter modes.

### Milestone C — static map viewer: core DONE (fidelity gaps listed)
`War3Engine.Client --map <w3x> --screenshot ...`

- W3X (HM3W + MPQ) opens; W3I/W3E/DOO/Units + object data parse via War3Net.Build.Core.
- Terrain: 64x96 tiles, 12,288 triangles, 7 Cityscape ground textures, heights and cliff
  tiers from W3E (worldZ = w3eHeight + layer*128 - 256).
- Doodads: 54 placements -> 13 visible models (pathing blockers skipped), including
  destructible trees with per-destructible replaceable textures, waterfall, cauldron.
- Terrain now uses WC3-style per-layer splat masks (corner-mask atlas cells), tier cliff
  walls (approximate, real cliff models pending), and water surfaces from W3E water levels
  plus `TerrainArt/Water.slk` per-tileset height (-0.7 cityscape / -0.6 ashenvale).
- Doodad/unit art honors `war3map.w3u/w3b/w3d` model overrides (`umdl`/`dfil`).
- Evidence: `artifacts/map_overview_final.png`, `artifacts/map_ground_final.png`,
  `artifacts/map_water2.png` (splat + water + tier walls), `artifacts/interactive_window.png`.

### YDWE fixture - static render + compatibility report
- `811.ThoiDaiBS.w3x` opens (MapInfo skipped due to an invalid weather id "0000"); 128x128
  terrain, 16,384 tiles, 48k+ triangles with splat layers, 1,091 doodads / 26 units,
  250 rendered instances (64 unique models), 139 unresolved custom objects.
- `War3Engine.Inspector compat` reports: 1.27.0.52240, YDWEConfig 2.0.0.2485, DzAPI
  dz_w3_plugin.dll, 25 YDWE libraries, 265 extension natives (229 DzAPI, 31 JAPI,
  5 Extra; 69 actually called), 2,024 imports with 1 missing.
- Evidence: `artifacts/ydwe_map_final.png`; details in `docs/YDWE_COMPATIBILITY.md`.

### UI + tooling
- In-app overlay: title/stats, clickable animation list, paused/time state, controls help;
  map mode shows tileset, counts and water/cliff stats. Rendered into screenshots.
- `War3Engine.Inspector` commands now include `compat` and `mpq` (nested archive probe).
- Tests: 32 passing (adds YDWE inventory, classification, component detection).

### JASS runtime (Phase A/B/C/D partial, verified)
- Real tree-walking JASS VM on War3Net.CodeAnalysis.Jass (parsing/program model/execution/
  native dispatch separated), common.j + Blizzard.j + war3map.j merged.
- Target map initialization **executes through the VM**: 14 units, 228 triggers, 16 timers,
  8 YDWE frames, 6,437 native calls, ~56k VM steps; `main` completes.
- 68/265 declared extension natives bound; all 3 extension natives reachable from `main`
  implemented. Missing natives fail with name, signature and call stack.
- Timer scheduler executes one-shot/periodic callbacks against runtime state (tested).
- Dependency reports: `docs/target-map-jass-dependencies.md`, `docs/target-map-native-status.json`.
- Next blocker: advancing timers 3 s reaches `EXExecuteScript` Lua JASS-SLK queries;
  gameplay scenario not yet complete. Details: `docs/JASS_RUNTIME.md`.

## Partially working / next

- Terrain: splat masks are rendered per tile (orientation approximate), cliffs are
  simplified walls rather than WC3 cliff models/ramps, water has no shore blending,
  depth coloring or animation.
- Model runtime: billboard nodes, texture UV animation, ParticleEmitter(2), ribbons,
  global blended-layer sort by priority plane.
- YDWE: extension natives are only partially implemented (68/265 bound); the map initializes
  but later gameplay/UI trigger paths still hit unimplemented natives (next: EXExecuteScript).
- Text `.mdl` models are unsupported; custom object art resolves `.mdl` names to `.mdx`.
- Simulation, audio, fog, campaign, networking: not started.

## Commands

```
dotnet build War3Engine.slnx
dotnet test tests/War3Engine.Tests/War3Engine.Tests.csproj
dotnet run --project src/War3Engine.Inspector -- model
dotnet run --project src/War3Engine.Inspector -- mapdata
dotnet run --project src/War3Engine.Inspector -- pose "units\human\footman\footman.mdx" Death 2900
dotnet run --project src/War3Engine.Client -- --model "units\human\footman\footman.mdx" --anim "Walk" --time 250 --screenshot artifacts/walk.png
dotnet run --project src/War3Engine.Client -- --map "C:\Users\admin\Downloads\Map Tong Hop v49.0 SS2.w3x" --screenshot artifacts/map.png
```

Tests: 29 passing (sampler math, pivot/hierarchy, blend states, real footman assets,
map terrain/doodad scene). Real-asset tests skip gracefully if game data is missing.

## Verified data facts (reference for next steps)

- W3E vertex grid is x-fastest: index = x + y*(tilesX+1); world origin = (Left, Bottom);
  heights in tiles * 128; doodad tier fit confirmed with 0 median error.
- MDX pivots are indexed by node object id, not node enumeration order.
- WC3 rawcodes are little-endian (first char = low byte).
- Team-color BLPs are JPEG-content BLP1 whose decoded channels arrive BGRA.
- Doodad/unit rotation in .doo is radians; model files use variations (`File<N>.mdx`).
- Destructibles map replaceable texture ids through DestructableData.slk `texID`/`texFile`.
