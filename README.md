# BotsAI

AI bots for ROUNDS — play solo in the LOCAL lobby or online with friends through RoundsWithFriends.
Thunderstore: https://thunderstore.io/c/rounds/p/Shidzenreku13/BotsAI/

See [dist/pkg/README.md](dist/pkg/README.md) for controls, settings and compatibility.

## Layout
- `src/RoundsBots` — the BepInEx plugin (C#, Harmony, UnboundLib).
  - `Logic/` — pure logic without Unity (navigation grid, ballistics, card scoring); covered by unit tests.
  - `Patches/` — Harmony patches; `*Compat.cs` — compatibility with other mods.
- `tests/RoundsBots.Tests` — xUnit tests for `Logic/` (net8, links the sources directly).
- `tools/` — card database generator (`docs/cards.json` → `CardKnowledge.g.cs`), release packaging, pick replay.
- `dist/pkg` — Thunderstore package files (manifest, README, icon).

## Build
Requirements: .NET SDK 8, ROUNDS, an r2modman profile with BepInEx and UnboundLib.

```
dotnet build src/RoundsBots -c Release -p:RoundsDir="<ROUNDS folder>" -p:DevProfile="<r2modman profile folder>"
dotnet test tests/RoundsBots.Tests
```
Paths can also go into a `local.props` file in the repo root (ignored by git).

## License
MIT
