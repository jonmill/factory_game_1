# Factory 1 (working title)

2D factory/colony automation game: Factorio-style depth, DSP-style shine. You lead a small landing party
on a constrained, wrap-around planet, give orders to followers, and build up to full automation.

## Layout

| Path | What |
|---|---|
| `src/Factory.Sim` | **Deterministic simulation.** Pure C#, no engine/Steam references. All rules live here. |
| `src/Factory.Game` | Godot 4.7 .NET client (`project.godot` lives here): rendering, input, UI. Never mutates the sim directly. |
| `src/Factory.Sim/Content/base.json` | All balance data (items, recipes, buildings, vehicles, techs, start kit). |
| `tests/Factory.Sim.Tests` | TUnit tests: determinism/replay, machines, power, followers, trains, progression, bots, playtest framework. |
| `src/Factory.Playtest` | Headless playtesting: bots that play through commands, telemetry, invariant checks, experiments. |
| `tools/Factory.Headless` | CLI for the playtest experiments, bot traces and replay verification. |

## Architecture rules (keep multiplayer possible)

1. **Commands are the only way to change state** (`src/Factory.Sim/Commands/`). The client calls `World.Submit(cmd)`.
   Each command carries `Player` and is validated (`Validate`) before it is applied. Lockstep
   multiplayer = exchange commands per tick; host-authoritative = send commands to the host.
2. **Determinism:** fixed 60 Hz ticks, integer/fixed-point math only (1 tile = 1024 sub-units), seeded
   `Rng`, sorted iteration. `World.Checksum()` hashes state for desync detection and regression tests.
3. **Replays are saves:** seed + command log (`Replay`). F5/F9 in game. Snapshot saves are a later
   optimization for long games.
4. Planets update independently (`Planet.Update`). Research is colony-wide; units have an `OwnerPlayer`.

## Running

Everything is in one solution, `Factory.sln`; Godot is pointed at it too, so it never generates its own.

```bash
./play.sh                                       # build the client and play
./play.sh --editor                              # open the project in the Godot editor
dotnet build Factory.sln                        # build everything
dotnet run --project tests/Factory.Sim.Tests    # tests (TUnit runs as an executable, not `dotnet test`)
```

`play.sh` expects Godot 4.7 .NET at `/Applications/Godot_mono.app`; set `GODOT=/path/to/Godot` otherwise.

Debug/automation env vars for the client: `FACTORY_DEMO=1` (queues a sample base and fast-forwards
via normal commands), `FACTORY_SCREENSHOT=/path.png` (saves a frame after 3 s and quits),
`FACTORY_SEED=<n>` (pins the map seed), `FACTORY_REPLAY=/path.replay.json` (loads a headless replay
and centres on the lander) and `FACTORY_ZOOM=<f>` (initial camera zoom, used with the replay).

## Headless playtesting

Bots play the real simulation through the same commands a player sends, thousands of times faster
than real time, and write a markdown + JSON report plus a replay for every finding.

```bash
H="dotnet run -c Release --project tools/Factory.Headless --"
$H list                                                   # experiments
$H static                                                 # balance maths straight from base.json (no simulation)
$H balance  --seeds 1-8 --goal followers:1                # reference bot plays each seed; gather rates, starvation, surpluses
$H speedrun --goal tech:mechanical-arms --iterations 24   # search bot strategies for the fastest route to a goal
$H softlock --seeds 1-4 --iterations 24 --chaos-minutes 15 --goal followers:1   # chaos play, then can the bot recover?
$H suite --fail-on high                                   # static + balance + softlock; exit code 1 on bad findings (CI)
$H trace --seeds 1 --goal followers:1 --every 60          # timeline of what the bot and each follower is doing
$H trace --seeds 3 --run 22 --chaos-minutes 15            # replay softlock run 22's chaos, then trace the recovery
$H verify playtest-results/<run>/replays/<name>.replay.json   # re-simulate a replay and check its checksum
```

Goals: `tech:<id>`, `followers:<n>`, `item:<id>:<count>`. Reports go to `playtest-results/<timestamp>/`
(gitignored) unless `--out` is given. The `GoalBot` is a state-driven planner (research path → structures →
demand → crafting → follower jobs) that recovers from a messy base; the `ChaosBot` issues random, often
wasteful commands to shake out softlocks, item dupes/losses and stuck followers.

## Code standards

C# follows the Microsoft coding conventions plus the rules enforced by `.editorconfig` and
`Directory.Build.props` (warnings are errors): explicit types instead of `var`, file-scoped namespaces,
Allman braces with braces on every block, `_camelCase` private instance fields, XML docs on public
members, and recommended CA analyzers. Also: no `!` on booleans (write `x == false`), parenthesize each
comparison inside `&&`/`||`, a blank line before `return` (unless it follows `{` or a comment), one type
per file (partial classes as `Type.Part.cs`), sorted `using`s, and tests named `Subject_Behavior`.

## Steam

`IPlatformServices` abstracts the store; the game runs offline without Steam. Steam is opt-in:
`dotnet build src/Factory.Game/Factory.Game.csproj -p:EnableSteam=true`. This uses Steamworks.NET 2024.8.0,
which ships **x64 only** (Windows/Linux Steam builds; it will not load on Apple Silicon). You also need:
the native `steam_api` library from the Steamworks SDK next to the executable, and `steam_appid.txt`
(480 = Valve's test app until you have a real App ID). Both are gitignored.

## Playtest scope

Included: one 128×128 wrap-around planet, 5 ores + trees + water, 3 followers (Build / Gather / Haul /
Operate train), leader hand-mining, lander fabricator, burner drills, furnaces, steam engines, poles,
lead-acid batteries, labs, rails, rail stops, water towers, hand-pump carts (need an operator, 1200 kg
pull limit), steam locomotives (water + fuel, 12 t pull), mine carts, 5 techs, Colony Beacon → new follower.

Not yet: belts (shown as "coming soon" tech), other planets / interplanetary travel, rail signals
(trains just block each other), train collision avoidance on single track, snapshot saves, audio,
sprite art, multiplayer transport.
