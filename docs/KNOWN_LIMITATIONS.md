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

- Splat blending uses per-tile corner-mask atlas cells but the cell UV orientation is
  approximate; transitions are smooth but corner direction is not verified against the
  original client.
- Cliffs are simplified vertical walls textured with `Textures/Cliff0.blp`. WC3 cliff
  models, ramp models, cliff variation selection and cliff textures per cliff type are
  not implemented.
- Water uses W3E water levels plus per-tileset Water.slk height; shore blending, depth
  color bands, water animation frames and reflections are missing.
- Terrain lighting uses the model Lambert shader; WC3 light-environment tables are not used.
- Doodads are drawn without distance culling/instancing; very large maps hit the
  2000-placement cap (`MapSceneLoader` parameter).
- Pathing-only doodads (destructible category P) are skipped.
- Object-data overrides apply model paths only (`umdl`/`dfil`); scale, team color and
  other widget fields are not applied. Text `.mdl` models are unsupported.
- YDWE fixture map: MapInfo is skipped (invalid weather id "0000"), 139 of 1,091 doodads
  resolve to no model (custom ids / text MDL).

## JASS runtime

- The verified scenario (init, timers, EXExecuteScript, chat `T5`, runtime unit rendering)
  works. Only 69 of the map's 265 declared extension natives are implemented; other
  gameplay paths may fail and are reported loudly.
- `EXExecuteScript` uses NLua/Lua 5.4 while YDWE 1.2.9C ships Lua 5.3.6 (luacore.dll is
  32-bit). Numeric/string conformance tests cover 2 vs 2.0, //, %, precision, string.format,
  tonumber, 64-bit wrap and locale; a bit-identical 5.3.6 runtime is not embedded.
- `jass.slk` resolves map object data (w3t/w3u) before base SLKs and resolves TRIGSTR via
  war3map.wts. Two references used by the map (TRIGSTR_000/002) are missing from wts and
  stay literal; Ubertip falls back to utub then utip. Fields outside the verified set return nil.
- Trigger event dispatch covers initialization triggers, timer expiry, player chat, dialog
  clicks and unit selection/deselection (player-unit 24/25 and unit-scoped 57/58). Selection is
  owner-only and drives the map's selection trigger, which stores the unit handle in the map
  hashtable. The remaining 26 registered families (orders, attack/damage/death, spells, items,
  player/region/state/sync/YDWE key/mouse, 1,800+ registrations) have no event producer yet;
  they are registered only and listed in `docs/event-dispatch-coverage.json` with their missing
  response natives.
- Dialog clicks and unit picking are produced by real mouse hit-tests in the viewer, but
  automated scenarios use the engine input adapter; OS-level mouse input has not been automated.
  Manual procedure: `docs/event-dispatch-roadmap.md` section 7. The target map has no player-0
  units at startup, so the deterministic selection screenshot selects an owner-8 unit via
  `--select-unit`; a player-0 click works once gameplay spawns a player-0 unit.
- The F5 chat hotkey is verified at the engine input-dispatch layer by a deterministic test;
  OS-level key injection could not be automated because Windows foreground locking prevented
  focusing the background game window. Manual focused-window testing is still required.
- `TriggerSleepAction`/`PolledWait` are not implemented (the target map does not use them).
- Runtime units are rendered in their stand pose at the current position/facing; unit
  animations tied to game state, effects and UI frames are state-only.
- Automated OS-level key injection into the game window failed due to foreground locking in
  the test sandbox; F5 chat dispatch is implemented but the actual key press still needs
  manual verification.

## Compatibility / UI

- YDWE extension natives are inventoried and classified but not implemented; there is no
  JASS VM yet, so the YDWE map cannot execute. Enabling the profile only registers
  reported stubs (future runtime hook points).
- UI overlay interaction (clicking the animation list) is implemented but only verified
  through screenshot mode; it was not exercised with a live mouse in this session.
- The map view has no terrain inspector UI; diagnostics stay in the CLI.

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
