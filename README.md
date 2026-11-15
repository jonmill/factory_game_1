# Factory 1 (working title)

2D factory automation game: Factorio-style depth, DSP-style shine. You are a robot commander sent ahead
of human settlers. With a few simple helper bots you build up a base on a wrap-around planet, upgrade
yourself and your bots with modules, build more bots, and terraform the system (atmosphere, hydrosphere,
biosphere) until every planet is ready. Then you call the settlers.

## Layout

| Path | What |
|---|---|
| `src/Factory.Sim` | **Deterministic simulation.** Pure C#, no engine/Steam references. All rules live here. |
| `src/Factory.Game` | Godot 4.7 .NET client (`project.godot` lives here): rendering, input, UI. Never mutates the sim directly. |
| `src/Factory.Sim/Content/base.json` | All balance data (items, recipes, buildings, vehicles, techs, start kit). |
| `tests/Factory.Sim.Tests` | TUnit tests: determinism/replay, machines, power, helper bots, modules, terraforming, trains, progression, bots, playtest framework. |
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
via normal commands; `FACTORY_DEMO=2` also unlocks everything and stocks the lander to show the late-game
buildings, so it is not a replayable game), `FACTORY_SCREENSHOT=/path.png` (saves a frame after 3 s and quits),
`FACTORY_SEED=<n>` (pins the map seed), `FACTORY_WINDOW=inventory|replicator|tech|help|victory` and
`FACTORY_CATEGORY=<1-7>` (open a HUD window or build category), `FACTORY_SELECT=<building id>` (selects the
nearest building of that type, e.g. `burner-smelter`, or `helper` for a helper bot), `FACTORY_REPLAY=/path.replay.json`
(loads a headless replay and centres on the lander) and `FACTORY_ZOOM=<f>` (initial camera zoom,
used with the replay).

### Controls

| Key | Action |
|---|---|
| WASD | Walk |
| Right-click (drag) | Mine ore or trees; the commander walks over and works through the deposit |
| 1 – 7, then 1 – 0 | Open a build category, then pick a building; left-click places, R rotates. The commander builds ghosts within 8 tiles by itself |
| X · Q | Dismantle tool · pipette (pick the building under the cursor to place another) |
| Esc | Step back out of the tool, category, window or selection; then the game menu |
| E, I or Tab · C · T · M · H | Inventory (pockets + module slots + replicator + colony storage) · Replicator tab · Tech tree · System map · Help |
| Left-click a machine, crate or helper bot | Opens it beside your pockets (a bot shows its module slots); hover anything for quick info |
| Drag · Shift-click · Ctrl/Cmd-click · right-click | Move a stack between pockets and a machine or storage · one stack · all of that item · half |
| F | Unload pockets into the nearest lander or crate |
| Space · + / − · F5 / F9 | Pause · game speed · quicksave / quickload |

## Headless playtesting

Bots play the real simulation through the same commands a player sends, thousands of times faster
than real time, and write a markdown + JSON report plus a replay for every finding.

```bash
H="dotnet run -c Release --project tools/Factory.Headless --"
$H list                                                   # experiments
$H static                                                 # balance maths straight from base.json (no simulation)
$H balance  --seeds 1-8 --goal helpers:1                # reference bot plays each seed; gather rates, starvation, surpluses
$H speedrun --goal tech:mechanical-arms --iterations 24   # search bot strategies for the fastest route to a goal
$H softlock --seeds 1-4 --iterations 24 --chaos-minutes 15 --goal helpers:1   # chaos play, then can the bot recover?
$H suite --fail-on high                                   # static + balance + softlock; exit code 1 on bad findings (CI)
$H balance  --seeds 1-4 --minutes 240 --goal settlers   # the whole game: terraform and call the settlers
$H trace --seeds 1 --goal helpers:1 --every 60          # timeline of what the bot and each helper bot is doing
$H trace --seeds 3 --run 22 --chaos-minutes 15            # replay softlock run 22's chaos, then trace the recovery
$H verify playtest-results/<run>/replays/<name>.replay.json   # re-simulate a replay and check its checksum
```

Goals: `tech:<id>`, `helpers:<n>` (assemble n helper bots), `settlers` (win the game), `item:<id>:<count>`. Reports go to `playtest-results/<timestamp>/`
(gitignored) unless `--out` is given. The `GoalBot` is a state-driven planner (research path → structures →
demand → crafting → helper jobs, plus walking the commander to its builds and fitting modules) that
recovers from a messy base; the `ChaosBot` issues random, often wasteful commands to shake out softlocks,
item dupes/losses and stuck helper bots.

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

Included: a star system of fixed planets with seeded variation (Kepler's Rest, the 128×128 start world,
plus Cinder, Rime, Dune, the gas giant Vesper and the Shards belt), each with its own biomes and its own
share of the raw resources (siderite, cuprite, carbon, regolith, galena, xylo trees, kerogen, brimstone,
titanium, silica, lanthanide, deuterium, hafnium, tantalite, helium-3, osmiridium); a System map (M) to
view them; four tech **ages** (Salvage → Combustion are playable, Clean and Stellar are shown as
"coming soon"); research paid in **Cores** (Survey Cores in the Salvage Age, Analysis Cores from the
Combustion Age). The commander (hand-mining, builds ghosts in reach, 2 module slots, +2 from research) and
2 Helper Bot Mk1s (1 slot; Gather / Haul, with Build and Operate train taught by modules), 5 modules
(construction, servo, cargo, rail operator, mining laser), a Robot Assembly Bay for Mk1 and Mk2 (3 slots)
helper bots, lander fabricator, burner drills and smelters, steam engines, poles, charge cell banks,
research stations; in the Combustion Age electric drills, arc smelters, rails, rail stops, water towers,
hand-pump carts (need an operator, 1500 kg pull limit), steam locomotives (water + fuel, 12 t pull), mine
carts, terraformers (Atmosphere Processor, Hydrosphere Seeder, Bio Incubator), a system readiness meter
and the Settler Uplink to win.

Not yet: belts (shown as "coming soon" tech), travel between planets (other planets can be viewed but
not visited, so only the start planet counts toward readiness for now), rail signals
(trains just block each other), train collision avoidance on single track, snapshot saves, audio,
sprite art, multiplayer transport.
