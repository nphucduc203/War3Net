# War3Engine

A Warcraft III-compatible engine in C# (.NET 10, MonoGame) that loads original MDX/BLP
assets and W3X maps directly — no Blender/GLB pipeline, no game engine shortcuts.

Current state (see `docs/STATUS.md`):
- Milestone A: animated MDX model viewer with WC3 material/blend semantics — done.
- Milestone C: static W3X map viewer (terrain + doodads) — core done, cliffs/water pending.
- YDWE compatibility layer and UI: in progress.

## Requirements

- .NET 10 SDK (pinned by `global.json`; Visual Studio 2026).
- A local Warcraft III 1.27d installation (MPQ era) for original assets.

## Setup

1. Copy `app/settings.example.json` to `app/settings.json` and set your game/map paths.
2. Build and inspect:

```
dotnet build War3Engine.slnx
dotnet test tests/War3Engine.Tests/War3Engine.Tests.csproj
dotnet run --project src/War3Engine.Inspector -- model
dotnet run --project src/War3Engine.Inspector -- mapdata
```

3. View a model or a map (window opens; `--screenshot` renders one deterministic frame):

```
dotnet run --project src/War3Engine.Client -- --model "units\human\footman\footman.mdx" --anim Walk --time 250
dotnet run --project src/War3Engine.Client -- --map "C:\path\to\map.w3x"
```

## Layout

| Path | Purpose |
|---|---|
| `src/War3Engine.Core` | Coordinates, keyframe sampling, node hierarchy, model/map data types |
| `src/War3Engine.Assets` | MPQ/BLP/MDX/map/SLK reading, map scene building |
| `src/War3Engine.Rendering` | MonoGame renderer: skinned models, material states, terrain |
| `src/War3Engine.Client` | Viewer application (model viewer, map viewer, screenshot mode) |
| `src/War3Engine.Inspector` | CLI diagnostics (assets, model, blp, pose, map, mapdata, slk) |
| `tests/War3Engine.Tests` | xUnit tests, including real-asset checks |

Game assets and maps are read from the configured local installation and are not
distributed with this repository. `War3Net-master/src` is vendored under its MIT license.
