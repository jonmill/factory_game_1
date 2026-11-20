# Rebalance plan: crafting tree, tech tree and research costs

Scope: analysis and a data-level change list against `src/Factory.Sim/Content/base.json`. Nothing in the repo was edited.
Every "new" number below was computed by applying the change list to a copy of base.json and re-running the same analyser used for the diagnosis:

- `/Users/jonmill/.claude/jobs/bf978ed8/tmp/rb/an.py`: raw cost, machine time, chain depth, tech totals for any content file
- `/Users/jonmill/.claude/jobs/bf978ed8/tmp/rb/propose.py`: the change list as code (applies sections 3.1-3.6 to base.json)
- `/Users/jonmill/.claude/jobs/bf978ed8/tmp/rb/proposed.json`: the result. A numeric reference, not a drop-in file: it has no comments or descriptions for new entries, carries a helper `_star` field, and the `levelOf` field needs the small sim change in 4.1.
- `paths.py` (win path / bot path / win bill) and `split.py` (machine seconds per core by category) in the same folder.

Conventions: "raw" = ore units (orbital helium-3 and osmiridium count 1 each); "time" = sum of recipe seconds at machine speed 1, excluding orbital collection; 60 ticks = 1 s.

## 1. Diagnosis

### 1.1 Science cores

| Core | Raw (ours) | Step | Time s (ours) | Step | Depth | Factorio raw / s | DSP raw / s |
|---|---|---|---|---|---|---|---|
| survey | 3.0 | - | 15 | - | 3 | red 3 / 15 | blue 3 / 7.5 |
| analysis | 5.5 | 1.83x | 28 | 1.9x | 4 | green 7 / 31 | red 6 / 10 |
| synthesis | 9.0 | 1.64x | 36 | 1.3x | 4 | blue 59.5 / 120 (military 21 / 52 between) | yellow 17 / 48 |
| stellar | 11.0 | 1.22x | 40 | 1.1x | 3 | purple 155 / 431 | purple 50 / 103 |

- Tier growth collapses: 1.83x, 1.64x, 1.22x against 2.6x (Factorio) and 2.3x (DSP). The last core costs 3.7x the first; the references are at 17-50x by their fourth tier.
- The stellar core is the *shallowest* core (depth 3: ore, plate, core) and takes barely longer than a synthesis core. It is four smelted plates and one lattice.
- One research unit of the last age costs 28.5 raw (all four cores). The first age costs 3. The whole game spans a 9.5x range; Factorio red to purple spans ~75x.

### 1.2 Crafting tree

- 48 items, 46 recipes. Only four general-purpose intermediates exist (drive-cog 2 raw, cupral-filament 0.5, logic-board 2.5, charge-cell 2) plus polymer-resin 1, photonic-wafer 2 and promethium-lattice 5.
- Maximum chain depth is 4, and that is reached in the Salvage Age (ore, bar, filament, logic board, core). Nothing later is deeper. Factorio and DSP end at 4-7 levels with a 3-10x cost step per level (2.5, 28.5, 115 for the three circuit tiers).
- There is no second or third "circuit" tier and no structural tier (steel analogue) or motion tier (motor analogue). The 2.5-raw logic board is the most advanced component in a Plasma Fabricator, a Fusion Reactor and the Space Elevator.
- Brimstone, deuterium and lanthanide each have exactly one consumer. Titanium, tantalum and osmiridium are only ever used as bare plates.

### 1.3 Buildings

| Line | Ours (raw) | Steps | Factorio | DSP |
|---|---|---|---|---|
| Fabricator Mk1 / 2 / 3 | 26.5 / 67 / 102 | 2.5x, 1.5x | 26.5 / 54 / 728 (2x, 13x) | 18 / 108 / 532 (6x, 5x) |
| Smelter burner / arc / plasma | 5 / 42.5 / 75 | 8.5x, 1.8x | 5 / 50 / 213 | 15 / - / 440 |
| Drill burner / electric / plasma | 19 / 27.5 / 85 | 1.4x, 3.1x | 27.5 (one tier) | 12 / 1495 |
| Logistics station planetary / orbital | 170 / 400 | | | 1430 / 2620 |
| Launch pad + ferry rocket | 165 + 170 | | silo 35k | |
| Fusion reactor (12 MW) | 345 | 5.3x a 0.9 MW generator for 13x the power | reactor 17.8k | |
| Space elevator | 600 | | | |
| Settler uplink + call | 155 + 440 | | silo + rocket 346k | 717k |

- Upgrade chains are front-loaded or flat (Mk2 to Mk3 is 1.5x, arc to plasma 1.8x); the references back-load them.
- Milestone buildings are 6-23x a Fabricator. The references put them at 100-1000x a basic building.
- Every building, to the end of the game, is plates + cogs + logic boards (+ a few bare exotic plates), so no building ever pulls on a new production line.
- First-tier machines are in a sound band already (19-38 raw; Factorio 26-51).

### 1.4 Tech tree and research volume

| Age | Techs (non-repeatable) | Units | Median units | Unit time | Raw per unit | Raw total |
|---|---|---|---|---|---|---|
| salvage | 6 | 215 | 40 | 10 s | 3 | 645 |
| combustion | 12 | 860 | 50 | 10 s | 8.5 | 7,310 |
| clean | 12 | 1,060 | 75 | 10 s | 17.5 | 18,550 |
| stellar | 9 | 1,060 | 100 | 10 s | 28.5 | 30,210 |
| total | 39 (+5 repeatable) | 3,195 | | | | 56,715 |

- Win path (prerequisite closure of `settler-uplink`): 19 techs, 1,700 units, 29.8k raw, 4.7 lab-hours. The bot's path (its `_factoryPath` plus the win path): 2,650 units, 47.2k raw.
- What is already right: every tech costs all earlier cores (1/2/3/4 core types per unit by age), as in Factorio. No change needed there.
- Unit counts are flat (medians 40, 50, 75, 100) and unit time never changes (10 s for every tech). Factorio runs 5-60 s and medians 25, 100, 100, 300, 500.
- The Salvage Age has 6 techs and the cheapest is 20 units. Factorio's first tier is 15 techs with a median of 25 and several at 10: early unlocks come every couple of minutes.
- The capstone is the cheapest tech of its age: `settler-uplink` is 60 units (min of the Stellar Age), and `call-settlers` costs 440 raw, about one and a half Climate Engines.
- Repeatables all cost 40 units base, growing 50% of base per level (`UnitsFor = units + units*level/2`). Vein Utilization sits at the very end, behind `plasma-mining`.

### 1.5 Win cost against research

| | Ours | Factorio | DSP |
|---|---|---|---|
| Research on the win path | 29.8k raw | 702k | (tree) |
| Win consumables | 22.9k (all climate engines) to 48.8k (all first terraformers), ~30.7k at a 30/70 mix, + 440 call | 346k | 717k |
| Ratio win : research path | 0.8x to 1.6x | 0.5x | 0.2x of tree |

Terraforming needs 79,200 points on the three cheapest planets (Kepler's Rest 27,700, Rime 23,500, Cinder 28,000). The game is a terraforming grind with a thin research game in front of it: in the 30-hour diagnostic run the bot reached the Stellar Age at 8:44 and won at 26:27, two thirds of the run after the last age started.

### 1.6 What makes it feel like a "kid's version", concretely

1. Flat tier growth: cores 3 / 5.5 / 9 / 11. A late research unit is not meaningfully harder to make than an early one.
2. Shallow chains: depth 4 from the first age on, and the last core is depth 3.
3. One circuit for the whole game; no structural, motion or advanced-logic tiers, so later ages add resources but no production lines.
4. Buildings never require the new tier's parts, and upgrade and milestone costs are flat.
5. Research is small and uniform (10 s units, 50-150 units everywhere, 6 techs in the first age), and the win tech is the cheapest of its age.
6. The win is 0.8-1.6x the research path, so pacing is dominated by feeding terraformers the same three 5-raw consumables.

## 2. Targets

### 2.1 Ratios

| Quantity | Target | Result of the change list |
|---|---|---|
| Core raw per tier | 2.3-2.6x | 3 / 7.5 / 18 / 44 (2.5x, 2.4x, 2.44x) |
| Core machine time per tier | 2.0-2.5x | 15 / 37 / 74 / 159 s (2.45x, 2.0x, 2.15x) |
| Core chain depth | +1 per tier | 3 / 4 / 5 / 6 |
| Raw per research unit by age | Factorio-like compounding | 3 / 10.5 / 28.5 / 72.5 (24x range, was 9.5x) |
| Intermediate step per chain level | 3-10x | logic-board 2.5, photonic-processor 11 (4.4x), phase-processor 23 (2.1x; it is the fourth circuit-like tier of a 4-tier game, kept low to hold the 44-raw core) |
| Unit time by age | 5-60 s band | 10 / 15 / 20 / 30 s |
| Techs | ~60, cheap and frequent early | 9 / 17 / 18 / 10 non-repeatable + 5 infinite = 59 |
| Units per tech | early 15-50, mid 40-120, late 50-200, capstone among the dearest | medians 30 / 60 / 75 / 80; `settler-uplink` 150 |
| First-tier machines | 20-55 raw | unchanged (19-40) |
| Upgrade chains | back-loaded, 3-5x per step | Fabricator 26.5 / 95 / 442; smelter 5 / 56 / 305; drill 19 / 40 / 342 |
| Milestone buildings | 15-150x a Fabricator | pad 380, rocket 375, orbital station 940, reactor 1,210, uplink 1,080, elevator 3,900 |
| Win : research path | 0.3-0.6x | (21.1k to 27.9k consumables + 3.0k call) / 59.5k = 0.41-0.52x |

### 2.2 Budget (raw)

| | Now | Proposed |
|---|---|---|
| Research, win path | 29.8k (1,700 units, 4.7 lab-h) | 59.5k (1,840 units, 10.4 lab-h) |
| Research, bot path | 47.2k (2,650 units) | 85.3k (2,505 units) |
| Research, whole tree (non-repeatable) | 56.7k (3,195 units) | 120.4k (3,635 units) |
| Win consumables, 30/70 mix + call | 31.1k | 30.9k |
| Bot's milestone kit (5 orbital stations, 4 reactors, 4 climate engines, 2 collectors, 6 terraformers, launcher, pad, uplink) | 6.2k | 17.2k |
| Factory kit (15 fabs, 7 labs, 20 drills, 20 arc smelters, 40 arms, 40 panels, 10 Mk2 ...) | 4.4k | 6.2k |
| Skilled-human win path total | ~70k | ~105k |
| Bot total | ~89k | ~140k |

Weight moves from terraforming to research: research becomes two thirds of a win instead of under half. The total grows about 1.5x. That is deliberate (the request is for more depth), and it is the main risk; section 4 gives the knobs to pull it back.

### 2.3 Time arithmetic for a ~4 h skilled run

Machine seconds per research unit, cumulative over all cores an age consumes (from `split.py`): crafting 5.5 / 18.2 / 46.2 / 103.2 s, smelting 9.6 / 33.6 / 73.6 / 169.4 s, refining 0 / 0 / 6 / 12 s.
Machine speeds: Fabricator 0.75 / 1.5 / 3; smelter 1 / 2 / 4; drill 0.75 / 1.5 / 4 ore/s.

| Age | Win-path units x unit time | Labs | Lab wall time | Units/s | Raw/s for science | Smelters | Fabricators | Drills |
|---|---|---|---|---|---|---|---|---|
| salvage | 115 x 10 s | 3 | 6.4 min | 0.30 | 0.9 | 3 burner | 3 Mk1 | 2 burner |
| combustion | 470 x 15 s | 6 | 19.6 min | 0.40 | 4.2 | 14 burner or 7 arc | 10 Mk1 | 3 electric |
| clean | 835 x 20 s | 8 | 34.8 min | 0.40 | 11.4 | 15 arc | 13 Mk2 (25 Mk1) + 3 refineries | 8 electric |
| stellar | 420 x 30 s | 8 | 26.3 min | 0.27 | 19.3 | 23 arc or 12 plasma | 19 Mk2 or 10 Mk3 + 4 refineries | 9 electric + 4 collectors |

- Pure lab time is 87 minutes at those lab counts. A plausible split: Salvage 0:00-0:30 (hand start, first lab ~0:12), Combustion 0:30-1:10, Clean 1:10-2:15 (two ferry trips for silica), Stellar 2:15-3:15, terraforming in parallel from ~1:30 and finishing 3:15-4:00.
- Terraforming time: 79,200 points. A first terraformer gives 1.5 points/s, a Climate Engine 6 points/s bare, more with Climate Amplifiers (+100% each; 18 points/s if two add up, which was not checked in code). Two bare engines per planet finish a 27k-point planet in 38 minutes once fed, amplified ones in as little as 13, so the last 45 minutes are enough if the consumable lines (0.25-0.5 raw per point) were built during the Stellar Age.
- Average over the run: ~105k raw in 4 h = 7.3 raw/s, peaking near 20-25 raw/s. That is a 45-science-per-minute Factorio base in machine count (their figure: ~52 assemblers, ~41 furnaces, ~47 drills), which a speedrunner there stands up in well under two hours with belts from minute one. Here belts arrive in the Clean Age, so 4 h is reachable but tight. If it proves out of reach, pull knob K1 or K5 in section 4.4 rather than flattening the cores again.
- Bot: it averages roughly 1.9 raw/s today (an estimated ~89k raw in 12:26 on seed 1). At the same throughput the proposed ~140k is ~20 h. Target: seed 1 at or under 20 h, every seed at or under 32 h, with diagnostic runs lengthened to 40 h (`--minutes 2400`) until tuned.

## 3. Change list against base.json

Ids not mentioned are unchanged. All new names are original; structures are borrowed (a tempered-metal tier, a motion part, three logic tiers), not recipes.

### 3.1 New items and recipes (7)

Add to `items` (stack 100 for all; suggested mass / colour in brackets) and `recipes`:

| id | Name | Category (machine) | In | Out | Ticks | Raw | Time s | Unlocked by |
|---|---|---|---|---|---|---|---|---|
| tempered-girder | Tempered Girder [2, #8fa3b8] | smelting (any smelter) | siderite-plate 4 | 1 | 420 | 4 | 20 | `tempering` (new, salvage) |
| torque-actuator | Torque Actuator [1, #d9a441] | crafting (lander, Fabricators) | drive-cog 1, cupral-filament 4, siderite-plate 1 | 1 | 120 | 5 | 20 | `mechanical-arms` |
| photonic-processor | Photonic Processor [0, #38d6c8] | crafting | logic-board 2, photonic-wafer 2, polymer-resin 2 | 1 | 360 | 11 | 36 | `synthesis-cores` |
| etchant | Brimstone Etchant [1, #d8e04a] | refining (Kerogen Refinery) | brimstone 2, compacted-carbon 1 | 2 | 180 | 1.5 | 2 | `lattice-synthesis` (new, clean) |
| phase-processor | Phase Processor [0, #b070ff] | crafting | photonic-processor 1, promethium-lattice 1, tantalum-plate 2, etchant 2 | 1 | 600 | 23 | 69 | `lattice-synthesis` |
| gyro-rotor | Gyro Rotor [2, #7fa0c8] | crafting | osmiridium-ingot 2, torque-actuator 1 | 1 | 240 | 9 | 34 | `lattice-synthesis` |
| keel-plating | Keel Plating [3, #a8b8c8] | crafting | titanium-plate 2, tempered-girder 1 | 1 | 300 | 8 | 35 | `exotic-metallurgy` (new, clean) |

Notes:
- `tempered-girder` is a single-input smelting recipe on purpose: smelters refuse mixed inputs ("Don't mix ores in one furnace" in `Planet.AcceptAmount`), so alloys with two inputs must be crafting or refining recipes. `keel-plating` is therefore a crafting recipe.
- No recipe has more than 4 input kinds (Fabricator has 6 slots, refinery 4) and none has two outputs (the bot and `WorldProbe.RecipeFor` assume one recipe per item, first output).
- Roles: girder = structural tier (every Combustion+ building), actuator = motion tier (machines, vehicles, robots, two cores), photonic / phase processor = logic tiers 2 and 3, gyro-rotor and keel-plating give osmiridium and titanium a made part, etchant gives brimstone a second consumer and the refinery a late-game job.
- Item count goes 48 to 55, recipes 46 to 53.

### 3.2 Cores

| Recipe | In (new) | Ticks | Raw | Time s | Depth | Was |
|---|---|---|---|---|---|---|
| survey-core | unchanged: cupral-bar 1, drive-cog 1 | 300 | 3 | 15 | 3 | same |
| analysis-core | charge-cell 1, torque-actuator 1, cupral-filament 1 | 420 | 7.5 | 37 | 4 | logic-board 1, ballast-plate 1, charge-cell 1 (5.5) |
| synthesis-core | photonic-processor 1, torque-actuator 1, polymer-resin 2 | 900 | 18 | 74 | 5 | photonic-wafer 1, polymer-resin 2, logic-board 2 (9) |
| stellar-core | phase-processor 1, gyro-rotor 1, keel-plating 1, helium-3 4 | 1200 | 44 | 159 | 6 | lattice 1 + three plates (11) |

Raw per stellar core: siderite 9, cuprite 5, silica 6, kerogen 1, carbon 2, hafnium 2, tantalite 4, brimstone 2, titanium 4, osmiridium 4, helium-3 5. The set of planets needed is the same as today (Cinder, Dune, Vesper, the Shards; Rime stays the fusion planet); brimstone is new to the core but comes from Cinder alongside titanium and hafnium.

Optional, cosmetic: `survey-core` (bar + cog, 5 s) is structurally the same as Factorio's first pack. If the owner wants distance, `sintered-brick 1 + cupral-filament 2` is also 3 raw / ~12 s; it is referenced by 21 test lines, so only do it if wanted.

Because a stellar core now needs 5 helium-3 and 4 osmiridium ore, raise collector yield so two collectors of each still feed ~0.25 units/s:
- `collect-helium-3`: out helium-3 4 (was 2), ticks 300.
- `collect-osmiridium`: out osmiridium-ore 4 (was 2), ticks 360.

### 3.3 Other recipes

| Recipe | In (new) | Raw new (old) |
|---|---|---|
| catalyst-pack | compacted-carbon 2, polymer-resin 2, cupral-filament 2 | 5 (5) |
| hydro-cartridge | regolith 2, ballast-plate 1, polymer-resin 2 | 5 (5) |
| seed-pod | compacted-carbon 1, sintered-brick 1, logic-board 1, polymer-resin 1 | 6.5 (8.5) |
| call-settlers | stellar-core 50, phase-processor 20, fusion-cell 20, charge-cell 40 (ticks 1800) | 3,020 (440) |
| fusion-cell | deuterium-ice 4, helium-3 2, keel-plating 1 | 14 (8) |
| climate-amplifier | tantalum-plate 10, phase-processor 4, gyro-rotor 2 | 130 (55) |
| ferry-rocket | tempered-girder 30, torque-actuator 20, logic-board 30, polymer-resin 40, charge-cell 20 | 375 (170) |
| helper-mk2 | tempered-girder 5, torque-actuator 8, logic-board 20, charge-cell 8 | 126 (116) |
| hover-module | photonic-processor 2, charge-cell 4, torque-actuator 2, polymer-resin 5 | 45 (29.5) |
| mining-module | logic-board 5, torque-actuator 2, charge-cell 2, cupral-bar 4 | 30.5 (22.5) |
| rail-module | logic-board 4, torque-actuator 2, cupral-bar 4 | 24 (22) |

Unchanged: helper-mk1 (40 raw; Robot Assembly stays an early, cheap Salvage unlock), construction / servo / cargo modules, lanthanide-accelerant, promethium-lattice, all smelting, kerogen-fuel, polymer-resin.

Terraforming effects and ticks are deliberately untouched (`terraform:*:10` at 400 ticks, `payload:*:10` at 300, climate surges 60 at 600, targets 10,000). The consumables keep their price but now all need polymer-resin, so terraforming pulls on the refinery chain instead of raw carbon and regolith. Raw per point stays 0.5-0.65 for first terraformers and 0.23-0.30 for Climate Engines. This keeps `TerraformTests` and `RepeatableResearchTests` (which assert 10/14/60/84/99 points) valid.

Win bill: 79,200 points cost 21.1k raw (all engines), 27.9k (30% first terraformers), 43.6k (no engines), plus 3.0k for the call. Against the 59.5k research path: 0.41x / 0.52x / 0.78x. The cheap path requires the Stellar Age, as intended.

### 3.4 Building and vehicle costs

| Building | Age band | Raw old | Raw new | New cost |
|---|---|---|---|---|
| chest |  | 4 | 4 | (unchanged) |
| burner-drill |  | 19 | 19 | (unchanged) |
| burner-smelter |  | 5 | 5 | (unchanged) |
| steam-engine |  | 38 | 38 | (unchanged) |
| power-pole |  | 3 | 3 | (unchanged) |
| cell-bank |  | 12 | 12 | (unchanged) |
| lab |  | 33 | 33 | (unchanged) |
| assembler |  | 26 | 26 | (unchanged) |
| assembler-mk2 | clean | 67 | 95 | tempered-girder 8, torque-actuator 6, photonic-processor 3 |
| assembler-mk3 | stellar | 102 | 442 | keel-plating 20, phase-processor 8, gyro-rotor 6, photonic-processor 4 |
| rail |  | 2 | 2 | (unchanged) |
| rail-stop |  | 10 | 10 | (unchanged) |
| water-tower |  | 24 | 24 | (unchanged) |
| sorter |  | 6 | 6 | (unchanged) |
| electric-drill |  | 28 | 40 | logic-board 3, torque-actuator 3, tempered-girder 3, siderite-plate 5 |
| arc-smelter | combustion | 42 | 56 | logic-board 5, tempered-girder 6, sintered-brick 10 |
| kerogen-refinery | combustion | 68 | 120 | tempered-girder 10, torque-actuator 5, logic-board 10, sintered-brick 15 |
| kerogen-generator |  | 65 | 92 | tempered-girder 8, torque-actuator 6, cupral-bar 10, sintered-brick 10 |
| lattice-pylon |  | 10 | 11 | tempered-girder 2, cupral-filament 6 |
| launch-pad |  | 165 | 380 | tempered-girder 40, sintered-brick 60, torque-actuator 10, logic-board 20 |
| solar-panel |  | 20 | 40 | photonic-wafer 10, tempered-girder 2, cupral-filament 10, logic-board 3 |
| wind-turbine |  | 55 | 110 | tempered-girder 10, torque-actuator 8, polymer-resin 20, cupral-bar 10 |
| payload-launcher |  | 148 | 427 | tempered-girder 30, photonic-processor 12, torque-actuator 15, polymer-resin 40, sintered-brick 30 |
| conveyor-belt | clean | 3 | 3 | (unchanged) |
| logistics-station | clean | 170 | 367 | tempered-girder 30, photonic-processor 12, torque-actuator 15, charge-cell 20 |
| orbital-station | clean | 400 | 940 | tempered-girder 60, photonic-processor 30, torque-actuator 30, polymer-resin 60, charge-cell 40, sintered-brick 40 |
| orbital-collector | clean | 180 | 465 | tempered-girder 40, photonic-processor 15, torque-actuator 20, polymer-resin 40 |
| plasma-smelter | stellar | 75 | 305 | keel-plating 15, phase-processor 5, promethium-lattice 6, sintered-brick 20 |
| plasma-drill | stellar | 85 | 342 | keel-plating 20, phase-processor 4, gyro-rotor 10 |
| fusion-reactor | stellar | 345 | 1210 | keel-plating 50, osmiridium-ingot 40, phase-processor 20, promethium-lattice 20, gyro-rotor 10, sintered-brick 40 |
| space-elevator | stellar | 600 | 3900 | keel-plating 200, osmiridium-ingot 200, phase-processor 50, gyro-rotor 50, sintered-brick 150 |
| climate-engine | stellar | 365 | 903 | keel-plating 40, phase-processor 15, gyro-rotor 12, promethium-lattice 10, sintered-brick 40 |
| robot-bay |  | 71 | 71 | (unchanged) |
| atmo-processor |  | 85 | 205 | tempered-girder 15, torque-actuator 10, photonic-processor 5, sintered-brick 20 |
| hydro-seeder |  | 75 | 205 | tempered-girder 15, ballast-plate 20, photonic-processor 5, sintered-brick 20, torque-actuator 6 |
| bio-incubator |  | 88 | 204 | tempered-girder 12, photonic-wafer 15, photonic-processor 6, sintered-brick 20, torque-actuator 4 |
| settler-uplink |  | 155 | 1080 | keel-plating 30, phase-processor 20, photonic-processor 20, charge-cell 40, sintered-brick 40 |
| hand-cart |  | 30 | 30 | (unchanged) |
| steam-locomotive |  | 90 | 140 | tempered-girder 15, torque-actuator 10, cupral-bar 10, sintered-brick 10 |
| mine-cart |  | 14 | 14 | (unchanged) |

Rules applied: Salvage machines unchanged; Combustion buildings take girders and actuators; Clean buildings take photonic processors; Stellar buildings take keel plating, phase processors and gyro rotors. `robot-bay`, `lab`, `assembler`, `sorter`, `conveyor-belt`, rails, poles, crates and both burner machines stay as they are, so the bot's opening and the guide's first steps do not move.

Relative to cores: Fabricator Mk1 = 9 survey cores, Mk2 = 5 synthesis cores, Mk3 = 10 stellar cores; Climate Engine = 21 stellar cores; Fusion Reactor = 28; Space Elevator = 89 (optional luxury); Orbital Station = 52 synthesis cores.

### 3.5 Tech list

Unit cost is always one of each core up to the tech's age (unchanged rule). `unitTicks` becomes 600 / 900 / 1200 / 1800 by age. "win path" marks the prerequisite closure of `settler-uplink`. Old unit counts in brackets.

**salvage** (unitCost: survey-core 1; unitTicks 600)

| id | win path | units (old) | prereqs | unlocks | notes |
|---|---|---|---|---|---|
| automation | yes | 15 (30) | - | assembler |  |
| robot-modules |  | 15 (20) | - | construction-module, servo-module |  |
| charge-storage |  | 20 (20) | - | cell-bank |  |
| mechanical-arms | yes | 30 (45) | automation | sorter, torque-actuator | unlocks changed |
| analysis-cores | yes | 50 (60) | mechanical-arms | analysis-core | prereqs changed |
| robotics |  | 40 (40) | automation, robot-modules | robot-bay, helper-mk1 |  |
| tempering | yes | 20 (-) | - | tempered-girder | NEW |
| vein-utilization-1 |  | 30 (-) | automation | - | NEW; levelOf=vein-utilization |
| robot-efficiency-1 |  | 30 (-) | robotics | - | NEW; levelOf=robot-efficiency |

**combustion** (unitCost: survey-core 1, analysis-core 1; unitTicks 900)

| id | win path | units (old) | prereqs | unlocks | notes |
|---|---|---|---|---|---|
| electric-mining |  | 40 (50) | analysis-cores, tempering | electric-drill | prereqs changed |
| arc-smelting |  | 80 (170) | electric-mining | arc-smelter |  |
| rail-logistics | yes | 60 (50) | analysis-cores, tempering | rail, rail-stop, water-tower, hand-cart, mine-cart, steam-locomotive | prereqs changed |
| freight-protocols |  | 40 (40) | rail-logistics, robot-modules | rail-module |  |
| chassis-expansion |  | 50 (50) | robot-modules, analysis-cores | cargo-module |  |
| mining-lasers |  | 40 (50) | electric-mining | mining-module |  |
| advanced-helpers |  | 80 (85) | robotics, analysis-cores, tempering | helper-mk2 | prereqs changed |
| kerogen-refining | yes | 60 (50) | analysis-cores, tempering | kerogen-refinery, kerogen-fuel | prereqs changed; unlocks changed |
| combustion-engines |  | 60 (70) | kerogen-refining | kerogen-generator |  |
| long-range-power |  | 30 (35) | analysis-cores, tempering | lattice-pylon | prereqs changed |
| orbital-ferry | yes | 120 (105) | polymer-synthesis, rail-logistics | launch-pad, ferry-rocket | prereqs changed |
| synthesis-cores | yes | 120 (105) | photonics | photonic-processor, synthesis-core | prereqs changed; unlocks changed |
| polymer-synthesis | yes | 50 (-) | kerogen-refining | polymer-resin | NEW |
| photonics | yes | 60 (-) | orbital-ferry | photonic-wafer | NEW |
| research-speed-1 |  | 60 (-) | analysis-cores | - | NEW; levelOf=research-speed |
| robot-efficiency-2 |  | 80 (-) | robot-efficiency-1, analysis-cores | - | NEW; levelOf=robot-efficiency |
| vein-utilization-2 |  | 80 (-) | vein-utilization-1, electric-mining | - | NEW; levelOf=vein-utilization |

**clean** (unitCost: survey-core 1, analysis-core 1, synthesis-core 1; unitTicks 1200)

| id | win path | units (old) | prereqs | unlocks | notes |
|---|---|---|---|---|---|
| atmospheric-processing | yes | 50 (50) | synthesis-cores | atmo-processor, catalyst-pack, release-catalyst |  |
| hydro-seeding | yes | 60 (70) | atmospheric-processing | hydro-seeder, hydro-cartridge, seed-hydrosphere |  |
| bio-engineering | yes | 75 (85) | hydro-seeding | bio-incubator, seed-pod, seed-biosphere |  |
| solar-power |  | 40 (50) | synthesis-cores, long-range-power | solar-panel |  |
| wind-power |  | 40 (50) | synthesis-cores, combustion-engines | wind-turbine |  |
| hover-modules |  | 50 (65) | synthesis-cores, chassis-expansion | hover-module |  |
| precision-fabrication |  | 60 (75) | synthesis-cores, automation | assembler-mk2 |  |
| conveyor-belts | yes | 40 (75) | synthesis-cores, mechanical-arms | conveyor-belt |  |
| planetary-logistics | yes | 75 (100) | conveyor-belts | logistics-station |  |
| orbital-logistics | yes | 120 (155) | planetary-logistics, orbital-ferry | orbital-station |  |
| orbital-collection | yes | 90 (130) | orbital-logistics | orbital-collector, collect-helium-3, collect-osmiridium |  |
| stellar-cores | yes | 150 (155) | lattice-synthesis | stellar-core | prereqs changed; unlocks changed |
| exotic-metallurgy | yes | 75 (-) | synthesis-cores | titanium-plate, tantalum-plate, keel-plating | NEW |
| lattice-synthesis | yes | 100 (-) | orbital-collection, exotic-metallurgy | osmiridium-ingot, gyro-rotor, promethium-lattice, etchant, phase-processor | NEW |
| research-speed-2 |  | 60 (-) | research-speed-1, synthesis-cores | - | NEW; levelOf=research-speed |
| robot-efficiency-3 |  | 75 (-) | robot-efficiency-2, synthesis-cores | - | NEW; levelOf=robot-efficiency |
| vein-utilization-3 |  | 75 (-) | vein-utilization-2, synthesis-cores | - | NEW; levelOf=vein-utilization |
| terraforming-efficiency-1 |  | 60 (-) | atmospheric-processing | - | NEW; levelOf=terraforming-efficiency |

**stellar** (unitCost: survey-core 1, analysis-core 1, synthesis-core 1, stellar-core 1; unitTicks 1800)

| id | win path | units (old) | prereqs | unlocks | notes |
|---|---|---|---|---|---|
| orbital-terraforming | yes | 50 (75) | stellar-cores, bio-engineering | payload-launcher, launch-catalyst, launch-hydro, launch-seeds |  |
| plasma-smelting |  | 60 (100) | stellar-cores, arc-smelting | plasma-smelter |  |
| plasma-fabrication |  | 80 (100) | stellar-cores, precision-fabrication | assembler-mk3 |  |
| plasma-mining |  | 60 (100) | plasma-smelting, electric-mining | plasma-drill |  |
| fusion-power | yes | 100 (150) | stellar-cores | fusion-reactor, fusion-cell |  |
| space-elevator |  | 200 (200) | fusion-power, orbital-collection | space-elevator |  |
| climate-engineering | yes | 120 (150) | stellar-cores, orbital-terraforming | climate-engine, climate-atmosphere, climate-hydrosphere, climate-biosphere, lanthanide-accelerant |  |
| lanthanide-catalysis |  | 80 (125) | climate-engineering | climate-amplifier |  |
| climate-acceleration |  | 100 (40) | lanthanide-catalysis | - | repeatable (infinite) |
| settler-uplink | yes | 150 (60) | climate-engineering, fusion-power | settler-uplink, call-settlers |  |
| research-speed |  | 100 (40) | research-speed-2, stellar-cores | - | repeatable (infinite); was combustion; prereqs changed |
| robot-efficiency |  | 100 (40) | robot-efficiency-3, stellar-cores | - | repeatable (infinite); was combustion; prereqs changed |
| terraforming-efficiency |  | 100 (40) | terraforming-efficiency-2 | - | repeatable (infinite); was clean; prereqs changed |
| vein-utilization |  | 120 (40) | vein-utilization-3, plasma-mining | - | repeatable (infinite); prereqs changed |
| terraforming-efficiency-2 |  | 80 (-) | terraforming-efficiency-1, stellar-cores | - | NEW; levelOf=terraforming-efficiency |

Prerequisites were chosen so the closure of every tech contains the techs that unlock every ingredient of what it unlocks (the bot's `TechPath` is a pure prerequisite closure, it does not chase ingredient unlocks): hence `tempering` under `electric-mining`, `long-range-power`, `rail-logistics`, `kerogen-refining`, `advanced-helpers`, and `mechanical-arms` under `analysis-cores`.

Totals: 250 / 1,110 / 1,295 / 980 units by age (3,635), 120.4k raw. Win path 24 techs: 115 / 470 / 835 / 420 units (1,840), 59.5k raw, 37,500 lab-seconds.

New techs need names and descriptions; suggestions: `tempering` "Siderite Tempering" (re-fire plates into girders that carry real loads), `polymer-synthesis` "Polymer Synthesis", `photonics` "Photonics" (wafers from Dune's silica), `exotic-metallurgy` "Exotic Metallurgy" (titanium and tantalum, and keel plating), `lattice-synthesis` "Lattice Synthesis" (promethium lattices, etchant, phase processors, osmiridium rotors).

### 3.6 Upgrade series and infinite research (Vein Utilization moves early)

DSP's pattern: a few finite levels priced in the early cores, then an infinite tail in the top core. Modelled here as ordinary one-off techs that count as levels of a series, followed by the existing repeatable tech:

| Series (effect per level, unchanged) | Finite levels (age, units) | Infinite tech (stellar, all four cores, 30 s units) |
|---|---|---|
| vein-utilization (+10% deposit life) | I salvage 30 (prereq `automation`), II combustion 80 (`vein-utilization-1`, `electric-mining`), III clean 75 (`vein-utilization-2`, `synthesis-cores`) | `vein-utilization`: units 120, prereqs `vein-utilization-3`, `plasma-mining` |
| robot-efficiency (+15% move and mine) | I salvage 30 (`robotics`), II combustion 80, III clean 75 | `robot-efficiency`: units 100, prereqs `robot-efficiency-3`, `stellar-cores` |
| research-speed (+20% lab speed) | I combustion 60 (`analysis-cores`), II clean 60 | `research-speed`: units 100, prereqs `research-speed-2`, `stellar-cores` |
| terraforming-efficiency (+20% per consumable) | I clean 60 (`atmospheric-processing`), II stellar 80 | `terraforming-efficiency`: units 100, prereq `terraforming-efficiency-2` |
| climate-acceleration (+25% engine output) | none | `climate-acceleration`: units 100, prereq `lanthanide-catalysis` |

- Infinite cost formula stays as coded: `units + units * n / 2`, n = infinite levels already done (linear, like DSP's 4000 x (L - 5)). Vein Utilization: 120, 180, 240 ... units = 8.7k, 13.1k, 17.4k raw. An infinite level is priced at roughly a third of a whole win, which is what makes it post-game.
- Finite levels are priced to pay back inside a run: Vein Utilization I is 90 raw, II 840, III 2.1k; Terraforming Efficiency I (1.7k raw) saves about 4.6k of consumables.
- Needs one small mechanic (4.1): a `levelOf` field on TechDef so finite techs add to the series level.

### 3.7 Not changed, on purpose

- Ore yields, drill and smelter speeds, plate recipes, power numbers, robot stats, planet resources. Deposits are large (a starting patch holds ~65k ore, far patches ~120k each) against a ~140k-raw game, so no richness change is needed; Vein Utilization matters for how often drill columns must move, not for running out.
- Start unlocks and the empty lander.
- Research Station cost (33 raw) and speed. More labs is the intended response to 15-30 s units.

## 4. Implementation order, risks, verification

### 4.1 Mechanics that do not exist yet (all small)

1. **Series levels (`levelOf`)**. `TechDef.LevelOf` (string, optional). `ResearchState.Level(id)` must return infinite levels plus the number of completed techs whose `LevelOf == id`; `UnitsFor` must keep using only the infinite count, or the first infinite level would be priced as level 4. All five effects read through `Research.Level(...)` (`Planet.cs:160`, `Planet.Machines.cs:470`, `Planet.Robots.cs:52`, `Planet.Terraform.cs:158/161`), so one method changes. Completing a finite robot-efficiency level must also run the `RefreshRobot` loop that `World.AddResearchUnit` runs only in its repeatable branch. Derive the bonus from `Completed` at load so the save format does not change. Tech tree UI: show finite levels as normal nodes and the infinite node with its total level.
2. **Smelter input check ignores unlocks**. `Planet.AcceptAmount` (Furnace case, `Planet.Machines.cs:30`) picks the smelting recipe by input without checking `Research.Unlocked`. Today every smelting input is an ore so it never matters; with `tempered-girder`, a smelter would accept siderite plates before `tempering` is researched and sit on them. Add the unlocked filter there.
3. Nothing else. No multi-output recipes, no new machine kinds, no new categories, no fifth core (labs have 6 slots, 4 are used).

Optional, a design call rather than data: move `conveyor-belts` to late Combustion (survey + analysis). It is the single biggest lever on the 4 h target because every chain is now one or two hauls deeper while helper bots are still the only logistics until the Clean Age.

### 4.2 Order (run tests and a bot playtest after each step, commit per step)

0. Baseline: `static` experiment and the settlers bot run on all seeds at current content; keep ledgers and age times.
1. Mechanics 4.1 (1) and (2) with tests; no content change yet.
2. Items and recipes 3.1, new techs `tempering`, `polymer-synthesis`, `photonics`, `exotic-metallurgy`, `lattice-synthesis` with their unlock moves, prerequisite edits. Cores and costs still old. Bot must still win: this isolates "can the bot make and route the new parts" from "can it afford them".
3. Core recipes 3.2 and collector yields. Biggest pacing step; check per-age arrival times.
4. Building, vehicle, module and rocket costs 3.3 / 3.4.
5. Tech units and unit times 3.5.
6. Series and infinite research 3.6.
7. Consumables and `call-settlers` 3.3; final tuning with the knobs in 4.4.

### 4.3 Risks

Bot (`src/Factory.Playtest/Bots`):
- Throughput, the main one. Helpers carry every input ("they are the belts"); science lines are capped at helpers/3 Fabricators per core, labs at `1 + 2*age` (max 7), Fabricators at `4 + 5*age` plus science lines, helpers at `6 + 4*age`. Cores now have 2-4 more crafted parts beneath them, each wanting a Fabricator cell and hauls. Expect to raise `FabricatorCap`, `HelperTarget` and `LabTarget`, and to add `tempered-girder` and `torque-actuator` to `_standingStock` (`GoalBot.Factory.cs:27`).
- `tempered-girder` is the first smelting recipe whose input is not an ore. `Expand` puts it in `SmeltDeficit` generically, but the hauling side (`SmeltingOf` is keyed by resource, free smelters are sized for carried-in ore) was not traced end to end. Verify first in step 2; if it fails, the fallback is to make it a crafting recipe (same numbers) at the cost of the "second firing" flavour.
- One refinery, three recipes. `TendMachines` uses the first building of a category and switches its recipe when idle. Polymer resin demand rises several-fold (processors, every consumable, rocket, stations) and etchant shares the machine. The bot likely needs one refinery per refining recipe in deficit.
- One orbital collector alternating helium-3 and osmiridium, now 5 + 4 per stellar core. The yield change in 3.2 covers part of it; a second collector is the real fix.
- `_factoryPath` adds `plasma-smelting`, `plasma-mining`, `plasma-fabrication`, `lanthanide-catalysis` (280 stellar units = 20.3k raw, a quarter of the bot's research bill). Re-check that they pay back before the win; dropping `plasma-fabrication` alone saves 5.8k.
- Filler research. The bot runs repeatable techs while the next tech waits on cores. The infinite techs now start in the Stellar Age and the early levels are one-off techs the bot will not pick unless they are on its path. Either add the finite levels to `_factoryPath` (Robot Efficiency I-III and Research Speed I-II are the ones that pay) or let the filler logic pick any affordable `levelOf` tech.
- `Expand` stops at depth 8. Deepest chain is now 6 (7 from a building or the call). Fine, but there is no longer slack for another tier.
- Outposts: the kit per planet (orbital station 940, reactor 1,210, engine 903, fusion cells at 14 with keel plating) is ~2.8x dearer; `_outboundItems` and the hard-coded building ids still exist, so no code breaks, only time.
- `ScienceLines` uses `tech.UnitTicks`; longer units mean fewer core Fabricators per lab, which is correct, but check the lookahead of 15 units per lab does not over-stock 44-raw cores.

Tests (`tests/Factory.Sim.Tests`):
- Anything that researches a tech to unlock an item that moved: `polymer-resin` (now `polymer-synthesis`), `photonic-wafer` (`photonics`), `titanium-plate` / `tantalum-plate` (`exotic-metallurgy`), `osmiridium-ingot` / `promethium-lattice` (`lattice-synthesis`), `stellar-core` alone under `stellar-cores`. Look at `CleanAgeTests`, `StellarAgeTests`, `ProgressionTests`.
- `StellarAgeTests` vein test asserts `UnitsFor == Units + Units/2` after one level: still true only if finite levels are excluded from the cost formula (4.1).
- `RepeatableResearchTests` only needs the three ids to stay repeatable and grow: still true.
- Tests that hand-build with explicit materials or assert costs for changed buildings / recipes (`solar-panel` 20 references, `logistics-station` 13, `atmo-processor` 8, `ferry-rocket` 8, `climate-amplifier` 7, `construction-module` unchanged). Most use `TestWorld.Build`, which places directly; grep for `.Cost` and literal counts.
- `ResearchUnitTests` / `PowerTests` / `MetricsTests` loop over `automation.Units` or `UnitsFor`: fine with 15 units; check any hard-coded 30.

Game side:
- `Hud.Guide.cs` and `Hud.Help.cs` name techs and items in order; the guide needs a step for girders / `tempering` before electric machines, and the Help entry for endless research should mention the finite levels.
- Seven new item icons and five new tech icons (original art).
- `StaticBalanceExperiment` is the cheap guard: its reachability pass walks techs in unlock order and flags any unit cost or building cost that cannot be made. Run it after every step; it must report no Critical or High findings.
- Old saves: recipes and costs are content, but saved research state will lack the new techs (e.g. a save past `kerogen-refining` without `tempering`). Either accept or grant prerequisites on load.

### 4.4 Tuning knobs, in the order to pull them

| Knob | Effect |
|---|---|
| K1. Stellar tech units x0.8 | -6k raw on the win path, -10k on the bot path; ratios untouched |
| K2. `stellar-core` helium-3 4 to 2 | core 44 to 42 raw, collectors halve; use if collectors bottleneck |
| K3. Milestone kit (orbital-station, fusion-reactor, climate-engine) x0.75 | -4k on the bot's bill |
| K4. Bot `_factoryPath` without `plasma-fabrication` / `lanthanide-catalysis` | -5.8k / -5.8k for the bot only |
| K5. Belts in late Combustion | throughput, for humans; no cost change |
| K6. Consumable costs or `terraforming-efficiency` finite levels | win : research ratio, if terraforming still dominates wall time |

Do not tune by lowering the core raw costs or removing intermediates; that undoes the depth. Tune counts (units, building multiples).

### 4.5 Verification

1. `python3 -I an.py <base.json>` (or port it into `StaticBalanceExperiment` as a table) must reproduce: cores 3 / 7.5 / 18 / 44 raw, depth 3 / 4 / 5 / 6, win path 1,840 units / 59.5k raw, tree 3,635 units / 120.4k raw.
2. `dotnet test` green; `static` experiment with no Critical / High findings.
3. Bot: settlers goal on every seed must still win. Targets: seed 1 at or under 20 h (12:26 today), all seeds at or under 32 h, Stellar Age reached by ~45% of the run (it was 33% in diag30: 8:44 of 26:27), so the endgame is no longer two thirds terraforming.
4. Ledger checks per run: every new intermediate is produced by machines rather than the lander (automation proof), all three refining recipes and both orbital recipes show output, no core line starves for more than a few minutes, each of the four planets is used.
5. Human check at small window sizes: the tech tree with 59 nodes and the recipe picker with 7 more items still read well (the earlier playtest feedback was about readability).
