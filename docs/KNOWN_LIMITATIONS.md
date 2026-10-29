# Known limitations (current)

## Rendering

- No global blended-layer sort across geosets yet: opaque layers draw first per geoset,
  then blended layers per geoset. Priority planes are parsed but not used for ordering.
- Billboarded nodes (including billboard lock axes, camera anchored) are treated as
  ordinary nodes; particle/ribbon billboards do not exist yet.
- Texture coordinate animation (`KTAT/KTAR/KTAS` on TextureAnimations) is parsed but
  not applied. Texture id animation is applied.
- Fog of war, terrain fog, shadows, and team glow tinting are not implemented.
- Shader alpha-key threshold is a fixed 0.75 clip; not compared against the original
  client's multisample coverage behavior yet.
- Sampler state is fixed to wrap/linear per model; per-texture wrap flags are parsed
  but not applied.
- Node scale is converted by axis permutation `(x,z,y)`; models with non-uniform
  animated scaling plus rotation are not cross-checked against the original client.

## Terrain

- Ground uses one full-coverage atlas cell per tile. WC3's splat blending (masked
  corner cells) is not implemented, so tile borders are hard and textures repeat.
- Cliff and ramp models are not rendered; height discontinuities currently show as
  stretched ground quads. Water tiles are parsed (WaterHeight, flags) but no water
  surface is drawn.
- Terrain lighting uses the model Lambert shader; WC3 light-environment tables are not used.
- Doodads are drawn without distance culling/instancing; very large maps will exceed
  the 2000-placement cap (`MapSceneLoader` parameter).
- Pathing-only doodads (destructible category P) are skipped; no other doodad filters.
- Object data overrides (war3map.w3u/w3b/w3d) are parsed by War3Net but not yet applied
  to art lookup; custom unit/doodad models may resolve to defaults.

## Assets

- BLP2 (Reforged) and MDX v900/v1000 models are untested; parsers may accept them but
  rendering semantics differ (HD layers).
- JPEG-content BLPs are decoded through War3Net's JPEG path and channel order is fixed
  empirically (BGRA→RGBA). 4-component CMYK JPEG semantics are not formally verified.
- The fixture map is protected: `war3map.j`/`war3map.lua` are missing, so script
  execution for that map cannot be validated. Trigger metadata (`wtg`, `wct`) exists.
- MPQ `(listfile)` in the fixture map is unreadable (protected); known map filenames
  are probed by hash instead.

## Engine

- Bone palette is capped at 128 nodes (shader array) and 4 influences per vertex.
- No save/load, UI, audio, or networking.
- Fixed-step simulation does not exist yet (viewer advances animation by frame time).

## Validation gaps

- Animation poses were sanity-checked visually (stand/walk/attack/death/defend) but not
  yet pixel-compared with the original client on identical timestamps.
- Only two unit models and one doodad-grade asset set have been exercised end to end.
