# Dependency and license inventory

| Dependency | Version | License | Use |
|---|---|---|---|
| .NET | 10.0 (LTS) | MIT | Runtime/SDK |
| MonoGame.Framework.WindowsDX | 3.8.6 | MS-PL | Window, game loop, input, graphics device, effects |
| MonoGame.Content.Builder.Task / dotnet-mgcb | 3.8.6 | MS-PL | Shader compilation |
| War3Net (source, `War3Net-master`) | MIT | MIT | MPQ, BLP, MDX/MDL parsing, IO compression |
| — War3Net.IO.Mpq | source | MIT | Archive reading |
| — War3Net.IO.Compression | source | MIT | MPQ compression (via SharpZipLib, System.IO.Hashing) |
| — War3Net.Drawing.Blp | source | MIT | BLP decoding (via JpegLibrary) |
| — War3Net.Modeling | source | MIT | MDX/MDL parsing |
| — War3Net.Common | source | MIT | Shared utilities |
| xunit / runner / test sdk | 2.9.3 / 2.8.2 / 17.12.0 | Apache-2.0 / Apache-2.0 / MIT | Tests |

Reference-only repositories (not linked, not copied code):
- `open-realm-main` — architecture and format behavior reference (blend modes, animation
  interpolation, coordinate contract). GPL-family license; used as documentation of
  observable behavior only.
- `openwarcraft3-main` — supplementary research reference.

Game assets (MPQs, maps) are not included in the repository; they are read from the
user's local installation and fixture paths at runtime.
