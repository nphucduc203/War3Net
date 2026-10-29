# Project status (2026-10-29)

Target baseline: WC3 TFT 1.27d MPQ install at `D:/Games/Warcraft 3.2`; fixture map
`C:/Users/admin/Downloads/Map Tong Hop v49.0 SS2.w3x` (protected, no JASS/Lua script).

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
- Evidence: `artifacts/map_overview_final.png`, `artifacts/map_ground_final.png`,
  `artifacts/interactive_window.png` (windowed client running the map).

## Partially working / next

- Terrain fidelity: ground is one atlas cell per tile; WC3 splat blending, cliff
  models/ramps and water surfaces are not rendered yet (gaps documented).
- Model runtime: billboard nodes, texture UV animation, ParticleEmitter(2), ribbons,
  global blended-layer sort by priority plane.
- Fixture map has no script (protected); JASS execution needs another fixture.
- Simulation, UI, audio, fog, networking: not started.

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
