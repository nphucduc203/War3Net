# War3Engine Architecture

Warcraft III-compatible engine in C# / .NET 10, built as independent modules around
verified reference behavior rather than guessed format semantics.

## Modules

| Project | Responsibility |
|---|---|
| `src/War3Engine.Core` | Engine-independent math: WC3↔render coordinate basis, keyframe sampling, node hierarchy evaluation, runtime model data, settings. No graphics or file dependencies. |
| `src/War3Engine.Assets` | Virtual filesystem over game MPQs + map archive, BLP decoding, MDX→runtime model conversion (via War3Net parsers). |
| `src/War3Engine.Rendering` | MonoGame (WindowsDX) renderer: skinned vertex format, explicit blend/depth/raster states, material-layer draw passes, orbit camera. |
| `src/War3Engine.Client` | Executable viewer: window, input, animation playback, deterministic screenshot mode. |
| `src/War3Engine.Inspector` | CLI diagnostics: archive listing, MDX validation, BLP statistics, pose dump, map listing, asset extraction. |
| `tests/War3Engine.Tests` | xUnit tests: sampler math vs. reference formulas, pivot/transform behavior, blend-state mapping, real-asset parser + map checks. |

## Coordinate contract

Warcraft III model space: X east, Y north, Z up, right-handed. Rendering uses MonoGame
space: X right, Y up, Z toward the viewer. The basis change is a -90° rotation about X,
applied once at model load (`Wc3Space`):

- positions/translations: `(x, y, z) → (x, z, -y)`
- scaling tracks: `(sx, sy, sz) → (sx, sz, sy)`
- rotations: `Q' = B⁻¹ Q B` via matrix basis change (handedness preserved, winding preserved)

Pivots are indexed by **node object id**, not enumeration order. This is easy to get
wrong because War3Net yields nodes grouped by type; a regression test pins it.

## Model runtime

- MDX v800 (classic 1.27-era) parsed with War3Net.Modeling; converted to `ModelData`
  (positions, normals, UV sets, 4-influence rigid skin weights, materials/layers,
  geoset animations, sequences, node tracks).
- Classic MDX skinning: each vertex references a matrix group; bones in a group share
  the weight equally (no explicit weight array in the format). Groups larger than four
  are truncated to the first four and renormalized.
- Node local transform: `T(-pivot) · S · R · T(pivot + translation)` (row-vector).
  Rest pose is identity for every node, so skin matrices are node world matrices.
- Sampling: linear/hermite/bezier per reference formulas; quaternions use slerp and
  squad for hermite/bezier; global sequences wrap by their own duration (ms).

## Renderer

- Pass order: all opaque layers (filter `None`/`Transparent`), then blended layers.
  Blended layers read depth but do not write it; depth function is `LessEqual`.
- Blend modes (verified against open-realm `MDLX_SetLayerBlend`):
  - None: blend off, depth write
  - Transparent: alpha test (shader clip at 0.75), depth write
  - Blend: `SRC_ALPHA / INV_SRC_ALPHA`
  - Additive: `ONE / ONE`
  - AddAlpha: `SRC_ALPHA / ONE`
  - Modulate: `DST_COLOR / ZERO`
  - Modulate2x: `DST_COLOR / SRC_COLOR`
- Per-layer: two-sided, no-depth-test, no-depth-set flags map to explicit states.
- Layer alpha = static or animated track; multiplied by geoset animation alpha and
  geoset animation color. Replaceable textures 1/2 resolve to team color/glow.

## Assets

- Priority: map files (directory or MPQ) → `War3xlocal` → `patch` → `War3Patch` →
  `War3x` → `War3`. MPQ lookup is by hash, so archives without listfiles still resolve.
- W3X `HM3W` header (512 bytes) is skipped before MPQ parsing.
- BLP: paletted/DXT via War3Net; JPEG-content BLPs (team colors) get an explicit
  BGRA→RGBA fix validated against the WC3 team-color palette.

## Deliberate limits (current)

See `docs/KNOWN_LIMITATIONS.md`.
