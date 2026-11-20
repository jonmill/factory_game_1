# Factorio 2.0 / DSP 0.10 balance reference (from game data + wikis, researched 2026-11-19)

Raw = ore/crude 1:1 (Factorio oil products 1.026 crude). Time = sum of all recipe steps at speed 1.

## Science per tier
Factorio: red 3 raw/15 s, green 7/31, military 21/52, blue 59.5/120 (21 solid ore), purple 155/431, yellow 196/396, space 361/528.
  ~2.6x raw and ~2.0x time per tier. Pack recipes: red = copper + gear (5 s); green = inserter + belt (6 s); blue = 2 engines + 3 red circuits + sulfur -> 2 (24 s).
DSP: blue 3/7.5, red 6/10, yellow 17/48, purple 50/103, green 103/198, white 179/383 (white = 1 of each + antimatter).
  ~2.3x raw, 2.2x time per tier. Each new matrix introduces a new resource/chain (hydrogen, titanium/diamond, silicon processors, deuterium/graviton).

## Research volume
Factorio 2.0: 187 techs, 50k units; red 15 techs (median 25 units, 10-100), green 60 (median 100), blue 54 (median 100, 50-800), purple 17 (median 300), yellow 34 (median 500).
  5-60 s per unit. Minimum path to rocket silo: 44 techs, 5,960 units, ~702k raw, ~62 lab-hours (~6% of full tree raw).
  Cheap early techs (10-50 units red) unlock things often; mid-game techs 100-300 units; late 500-1000+.
  Many techs need SEVERAL pack types at once (a blue tech costs red+green+blue per unit).
DSP: blue 23 techs (median 120 units), red 31 (500), yellow 21 (800), purple 9 (1000), green 10 (2000), Mission 4000 white. ~10 s/unit/lab.
  Finite upgrades (mecha, drive, vein utilization 1-?, research speed...) are a separate pool 8x the tech tree, mostly optional.
Infinite research: Factorio mining productivity 2500*(L-3) linear; worker robot speed 1000*2^(L-6). DSP Veins Utilization 4000*(L-5), Research Speed 8000*(L-3).
  DSP Vein Utilization: early finite levels with blue/red/yellow... matrices, then infinite with white.

## Win cost
Factorio: 100 rocket parts x 3,114 raw = 311k + silo 35k = ~346k raw = ~0.5x minimum research path.
DSP: 4000 white x 179 = 717k raw = ~0.2x tech tree raw.

## Crafting tree / item costs (raw / seconds)
Factorio: plate 1/3.2, gear 2/6.9, cable 0.5, green circuit 2.5/9.2, plastic 10.8, red circuit 28.5/34, blue circuit 115/264, engine 9/56, electric engine 29, battery 33, robot frame 108, LDS 84, rocket fuel 113, module 155.
  belts 1.5 -> 11.5 -> 52; inserters 5.5 -> 12.5 -> 108; assembler 1/2/3: 26.5/54/728; furnaces stone 5 / steel 50 / electric 213; drill 27.5; lab 51; refinery 150; solar 67.5; reactor 17.8k; silo 35k.
DSP: ingot 1, magnet 1, coil 1.5, circuit 1.5, gear 1, steel 3, glass 2, graphite 2, Ti ingot 2, silicon 2, plastic 4, graphene 4.75, nanotube 8, motor 4.5, turbine 12, super-magnetic ring 29, processor 13, Ti crystal 15, Ti alloy 12, broadband 24, particle container 35, graviton lens 91, quantum chip 115, frame 46.5, sail 6, rocket 746.
  belts 1/5/16; sorters 2.5/4.75/10.75; assembler Mk1/2/3 18/108/532; arc->plane smelter 15->440; miner 12 -> adv 1495; lab 28; PLS 1430, ILS 2620.
Patterns:
- Intermediates step 3-10x per chain level (green->red->blue circuit 2.5->28.5->115; board->processor->quantum 1.5->13->115). Chains are 4-7 levels deep at the end.
- First-tier buildings = 10-25 basic intermediates (5-55 raw). Upgrade chains back-loaded (assembler 2x then 13x). Top machine ~3-5 top science packs of raw.
- Milestone buildings 100-1000x a basic building.
- Time ~1.5-3 s per raw unit (smelting dominates).

## Throughput / scale
Factorio machine speeds: drill 0.5 ore/s, furnace 1/2, assembler 0.5/0.75/1.25, lab 1. 45 SPM red+green+blue = ~52 assembler-2s, ~41 steel furnaces, ~47 drills, 23-45 labs.
DSP: miner 0.5/s per vein; assembler 0.75/1/1.5; 1 matrix/s needs 3/6/8/10/12/15 labs per tier.

## Playthrough lengths
Factorio any% record 1:18:56 (1.1), 1:42 on 2.x, default-settings 1:59, 100% 4:12; first playthrough 40-50 h; "There is no spoon" = rocket < 8 h.
DSP Mission Completed: 25-50 h dev estimate; 60-100 h casual.
