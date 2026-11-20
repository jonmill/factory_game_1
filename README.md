# Factory 1 (working title)

2D factory automation game: Factorio-style depth, DSP-style shine. You are a robot commander sent ahead
of human settlers. With a few simple helper bots you build up a base on a wrap-around planet, upgrade
yourself and your bots with modules, build more bots, and terraform the system (atmosphere, hydrosphere,
biosphere) until any three of the four terraformable planets are ready. Then you call the settlers.

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
3. **Saves are snapshots, replays are logs:** an in-game save stores the whole world (`WorldState`) and loads
   at once; a replay (seed + command log, `Replay`) re-simulates a game for bug reports and the playtest tools.
   See [Saves](#saves).
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
buildings, so it is not a replayable game; `3` adds the Combustion Age set and a loaded launch pad, `4` also
flies the ferry to Rime, `5` adds the Clean Age set: solar, wind, a payload launcher, a hover module, belts and logistics stations,
`6` the Stellar Age set: fusion, the space elevator, a climate engine, an orbital collector and plasma machines), `FACTORY_PLANET=<planet id>` (view another planet), `FACTORY_SCREENSHOT=/path.png` (saves a frame after 3 s and quits),
`FACTORY_SEED=<n>` (pins the map seed), `FACTORY_WINDOW=inventory|replicator|tech|help|victory|system` and
`FACTORY_CATEGORY=<1-7>` (open a HUD window or build category), `FACTORY_SELECT=<building id>` (selects the
nearest building of that type, e.g. `burner-smelter`, or `helper` for a helper bot), `FACTORY_REPLAY=/path.replay.json`
(loads a headless replay and centres on the lander) `FACTORY_ZOOM=<f>` (initial camera zoom,
used with the replay) and `FACTORY_HINTS=0|1|fresh` (force the guide off or on; `fresh` also ignores and never writes
`settings.cfg`, so every tip shows again). For saves: `FACTORY_WINDOW=save|load`, `FACTORY_SAVE=<name>` (saves once
the game is set up, e.g. after a demo), `FACTORY_LOAD=<name>` (starts loading a save), `FACTORY_SAVE_DIR=<dir>` (use
another saves folder, so automated runs leave the real one alone), `FACTORY_AUTOSAVE_SECONDS=<n>` and
`FACTORY_LOAD_BUDGET_MS=<ms>` (replay time per frame; small values slow loading down to screenshot the bar).

### Saves

Esc opens the game menu; **Save game** lists the saves and takes a name (picking a save in the list, or typing
an existing name, asks before overwriting it), and **Load game** lists them with the time saved, in-game time
played, the planet being viewed and the build that wrote each one, plus Load and Delete (which asks first,
inline). F5 / F9 quicksave and quickload the "Quicksave" slot, and the game autosaves every 10 minutes of
unpaused play into the oldest of "Autosave 1" to "Autosave 3".

Saves are `user://saves/<name>.json`: on Windows `%APPDATA%\Godot\app_userdata\Factory 1 (working title)\saves\`,
on macOS `~/Library/Application Support/Godot/app_userdata/Factory 1 (working title)/saves/`. A quicksave from an
older build (`user://quicksave.replay.json`) shows up as "Quicksave". Each file is one `Replay` as JSON:
`Version`, `Build`, `Meta` (name, real time saved, viewed planet), the world settings, `ContentHash`, `FinalTick`,
`FinalChecksum`, `Commands` (empty in a save) and then `State`, so the lists read just the header
(`Replay.ReadHeader` stops at `Commands`).

`State` is the world itself (`Save/*.State.cs`, `WorldState`): a gzipped binary snapshot, base64 in the JSON,
about 80 KB for a 10-hour game, and it loads in well under a second however long the game has run. It holds
every planet's tile layers as raw arrays, then each building, construction site, robot, train and shipment in
list order, plus research, the ledger, the RNG and the id counters; power networks are rebuilt from the
buildings on the next tick. `WorldState.Format` versions the binary layout separately from the file format.
`WorldStateTests` keeps it honest: a bot plays a real game, the world is snapshotted and restored, the two are
run side by side on the same commands and compared byte for byte every 10 seconds of game time, and a field
inventory fails when a class the snapshot covers gains or loses a field, so a new field can't be forgotten.

Versioning: `Replay.CurrentVersion` (3) is written; `Replay.MinSupportedVersion` (1) is the oldest that loads.
`Replay.Check` runs on the header before the commands are parsed: a format outside that range is refused with a
reason (greyed out in the list), a different `ContentHash` (the game data changed) loads with a warning, and a
checksum that doesn't match `FinalChecksum` after loading is reported. Format 1 is format 2 without `Build` and
`Meta`; format 3 adds `State`. Saves from formats 1 and 2 (no `State`) still load, by replaying. Bump `CurrentVersion` whenever an older build couldn't read what a newer one writes (a new or changed
command, a new world setting), and raise `MinSupportedVersion` only when old saves can no longer replay. `Build`
is `BuildInfo.Version`: `Factory-<date>-<commit>[-dirty]` from git at build time (`package.sh` passes the same name
it writes to `VERSION.txt`), or `dev` without git.

A snapshot save loads at once. An older save, or a replay, replays its command log (`ReplayPlayback`) about
20 ms per frame behind a progress bar, with input
blocked; the old game stays frozen underneath until the new world is ready, and Cancel goes back to it. Stepping
in batches gives exactly the world `Replay.Play` does (a test checks the checksum).

### Interface size and the guide

The HUD is sized from the actual game window (its height, with a floor for the display's DPI), not the
screen or the 1600×900 design size, and windows that still don't fit scroll or shrink. The pause menu (Esc)
has an interface-size setting on top of that and turns the guide on or off; both are kept in
`user://settings.cfg` (`GameSettings`) for the future options screen. The guide (`Hud.Guide.cs`) ticks
objectives off by watching the world, so a loaded game picks up at the right step.

The layout follows Factorio and DSP and keeps the play area clear: a one-line planet strip (click for the
terraform bars) and a slim helper bot list with per-bot job menus top-left; the minimap (a 72-tile local
radar with fog; click it or press M for the full planet map, which pans and zooms and labels each explored deposit with the ore left in it, as DSP does) with research and power lines top-right, the guide and hover/selection
info under it; one dock along the bottom with the commander's activity (pockets only in the inventory, E), the build categories and
window shortcuts; and up to five message lines that fade out.

Ore and trees run out (each tile holds an amount; trees don't regrow). As in DSP, pointing at a deposit shows
what's left in the whole patch and on that tile, and the map labels every explored patch (`Ui/Deposits.cs`
groups explored tiles of the same ore into patches, client-side). Drills warn once when the ore within their
reach drops under 10% of the most they've seen, and again when it runs dry, grouped per planet and ore.

### Controls

| Key | Action |
|---|---|
| WASD | Walk |
| Right-click (drag) | Mine ore or trees; the commander walks over and works through the deposit |
| 1 – 7, then 1 – 0 | Open a build category, then pick a building; left-click places, R rotates. The commander builds ghosts within 8 tiles by itself |
| X · Q | Dismantle tool · pipette (pick the building under the cursor to place another) |
| Esc | Step back out of the tool, category, window or selection; then the game menu |
| E, I or Tab · C · T · M · P · H | Inventory (pockets + module slots + replicator + colony storage) · Replicator tab · Tech tree · Planet map · Star system map · Help |
| G | Show or hide the guide: the next objective and how to do it, plus one-off tips for common snags |
| Left-click a machine, crate or helper bot | Opens it beside your pockets (a bot shows its module slots); hover anything for quick info |
| Drag · Shift-click · Ctrl/Cmd-click · right-click | Move a stack between pockets and a machine or storage · one stack · all of that item · half |
| F | Unload pockets into the nearest lander or crate |
| Space · + / − · F5 / F9 | Pause · game speed · quicksave / quickload (named saves: Esc → Save game / Load game) |

## Packaging a playtest build (Windows)

`./package.sh` exports the Windows build and zips it as `exports/Factory-<date>-<commit>-windows.zip`,
ready to send to playtesters (it includes `tools/playtest/README-PLAYTEST.txt` as the testers' readme).
It needs Godot 4.7.2 .NET's export templates (Editor → Manage Export Templates). The build bundles the
.NET runtime, so testers need nothing installed. It's a standard JIT build: NativeAOT can't cross-compile
from macOS to Windows, so an AOT build has to be exported on Windows (or a Windows CI runner).

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
titanium, silica, lanthanide, deuterium, hafnium, tantalite, helium-3, osmiridium); a star system map (P) to
view them; **fog of war** (only the landing site starts explored; robots uncover the map around them as they
move and buildings around them once built; nothing is built, mined or gathered on unexplored ground; a helper
bot on the Explore job walks an outward spiral from the lander; fog is tracked in 32-tile chunks so it scales
to bigger maps); four tech **ages** (Salvage, Combustion, Clean and Stellar); research paid in **Cores** (Survey Cores in the
Salvage Age, Analysis Cores from the Combustion Age, Synthesis Cores, which need Dune's silica, from the Clean
Age, and Stellar Cores, from Cinder's titanium and hafnium plus orbital helium-3 and osmiridium). The tree is
balanced on Factorio/DSP ratios: each core costs ~2.4x the raw of the one before (3 / 7.5 / 18 / 44) and sits one
crafting level deeper, through tempered girders, torque actuators, photonic and phase processors, gyro rotors and keel
plating; later buildings are built from their age's parts, and research units take 10 / 15 / 20 / 30 s by age. The commander (hand-mining, builds ghosts in reach, 2 module slots, +2 from research) and
2 Helper Bot Mk1s (1 slot; Gather / Haul / Explore, with Build and Operate train taught by modules), landing with an
empty lander (`start.lander` in `base.json`: everything starts from gathering; tests that need stock use
`TestWorld.Supplies`), 5 modules
(construction, servo, cargo, rail operator, mining laser), a Robot Assembly Bay for Mk1 and Mk2 (3 slots)
helper bots, lander fabricator, burner drills and smelters, steam engines, poles, charge cell banks,
research stations; in the Combustion Age electric drills, arc smelters, rails, rail stops, water towers,
hand-pump carts (need an operator, 1500 kg pull limit), steam locomotives (water + fuel, 12 t pull), mine
carts, a Kerogen Refinery (Kerogen Fuel, Polymer Resin), Kerogen Generators (no water, 80% efficient),
Lattice Pylons (20-tile reach), a Launch Pad and reusable Ferry Rocket to fly the commander, helper bots
and cargo to the other planets (a landed ferry always flies home); in the Clean Age Solar Panels (scaled by each planet's sunlight),
Wind Turbines (scaled by its atmosphere), a Hover Module (float over water and lava), the first terraformers
(Atmosphere Processor, Hydrosphere Seeder, Bio Incubator), Conveyor Belts (sorter arms load and unload them), Planetary Logistics
Stations (drones carry supplied items to stations that demand them) and Orbital Logistics Stations (vessels
between planets); in the Stellar Age an Orbital Collector (helium-3 from the gas giant Vesper, osmiridium from
the Shards), a Payload Launcher that terraforms another planet from home, Plasma Smelters and Drills, a Fusion Reactor (12 MW on Fusion Cells), the Space Elevator (free
launches, faster collectors and vessels), Precision and Plasma Fabricators (2x and 4x), the Climate Engine
(terraforms the planet it stands on 60x faster than the first terraformers), Climate Amplifier modules for it
(burn Lanthanide Accelerant from Dune's lanthanide and Cinder's brimstone to double its speed), Climate
Acceleration and Vein Utilization (infinite research; Vein Utilization, Robot Efficiency, Research Speed and Terraforming Efficiency come as one-off levels I-III in the early ages, `levelOf` in base.json, then repeat forever in the Stellar Age), a system readiness meter and the Settler Uplink (Stellar Age) to win.

Win condition: any 3 of the 4 terraformable planets (Kepler's Rest, Cinder, Rime, Dune) ready. Each stat runs
to 10,000 points. The first terraformers and the Payload Launcher add 2 points per 20 s or 10 s and only 2 of
each fit on a planet, so terraforming with them alone takes dozens of hours; Climate Engines on each planet
finish the job.

Every item, building, research and terraform point goes into the sim's ledger (`World.Stats`): who made or
used it, how, on which planet and when, with five-minute time series. It is the basis for achievements and for
the playtest reports that check the bots really automate.

Not yet: splitters and underground belts, rail signals
(trains just block each other), train collision avoidance on single track, snapshot saves, audio,
sprite art, multiplayer transport.
