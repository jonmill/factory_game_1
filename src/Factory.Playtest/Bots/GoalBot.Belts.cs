using Factory.Playtest.Analysis;
using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>
/// Belt-fed production lines, the way an experienced player builds once conveyor belts are researched: a row
/// of fabricators on one recipe, fed from an input belt and emptied onto an output belt (see
/// <see cref="BeltLine"/>). Helpers stop carrying to each machine; they keep the line's source crates
/// stocked a load at a time, and the belts and arms do the rest.
/// </summary>
public sealed partial class GoalBot
{
    /// <summary>Most fabricators the bot puts on one line.</summary>
    private const int MaxLineLength = 6;

    private readonly List<(int PlanetId, BeltLine Line)> _lines = [];

    /// <summary>Source crates of the lines on the focused planet: line stock, not general storage.</summary>
    private HashSet<int> _lineCrates = [];

    /// <summary>Output crates of the lines: storage to take from, but never to put things into.</summary>
    private HashSet<int> _lineOutputs = [];

    /// <summary>The fabricators standing on lines.</summary>
    private HashSet<int> _lineFabs = [];

    private bool BeltsReady() => Unlocked("conveyor-belt") && Unlocked("sorter") && Unlocked("long-sorter") && Unlocked("power-pole");

    /// <summary>The lines on the focused planet.</summary>
    private IEnumerable<BeltLine> Lines() => _lines.Where(l => l.PlanetId == P.Id).Select(l => l.Line);

    /// <summary>General storage: every depot except the lines' own source crates.</summary>
    private IEnumerable<Building> Stores() => WorldProbe.Storages(P).Where(b => _lineCrates.Contains(b.Id) == false);

    /// <summary>How much of an item general storage holds.</summary>
    private long Stock(string item) => Stores().Sum(b => (long)b.Inv.Count(item));

    /// <summary>Per-item totals across general storage.</summary>
    private Dictionary<string, long> StockTotals()
    {
        Dictionary<string, long> d = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (Building b in Stores())
        {
            foreach ((string k, int v) in b.Inv.Items)
            {
                d[k] = d.GetValueOrDefault(k) + v;
            }
        }

        return d;
    }

    /// <summary>Finds the buildings of each line on the focused planet (they may be ghosts still, or gone).</summary>
    private void RefreshLines()
    {
        _lineCrates = [];
        _lineOutputs = [];
        _lineFabs = [];
        foreach (BeltLine l in Lines())
        {
            for (int i = 0; i < l.Inputs.Length; i++)
            {
                AddIf(_lineCrates, l.SourceCrate(i), BuildingKind.Chest);
            }

            AddIf(_lineOutputs, l.OutputCrate, BuildingKind.Chest);
            for (int i = 0; i < l.Count; i++)
            {
                AddIf(_lineFabs, l.Fabricator(i), BuildingKind.Assembler);
            }
        }

        void AddIf(HashSet<int> set, Int2 at, BuildingKind kind)
        {
            if (P.BuildingAt(P.Torus.Wrap(at)) is { } b && (b.Def.Kind == kind))
            {
                set.Add(b.Id);
            }
        }
    }

    /// <summary>Fabricators on lines making a recipe, built or not.</summary>
    private int LineFabsFor(string recipe) => Lines().Where(l => l.Recipe == recipe).Sum(l => l.Count);

    /// <summary>
    /// Lays out a whole line for a recipe in one go, in the first clear rectangle (with a walkway round it) in
    /// rings out from the lander.
    /// </summary>
    private bool PlaceBeltLine(BotPlan plan, Building lander, BuildingDef fab, RecipeDef r, int count)
    {
        string[] inputs = r.Inputs.Select(i => i.Item).ToArray();
        if ((inputs.Length is 0 or > BeltLine.MaxInputs) || (fab.W != fab.H))
        {
            return false;
        }

        Int2 home = Placement.Center(lander);
        BeltLine probe = new BeltLine(r.Id, home, inputs, count, fab.W);
        for (int ring = 8; ring <= 60; ring++)
        {
            foreach (Int2 d in Placement.Ring(ring))
            {
                Int2 origin = P.Torus.Wrap(home + d);
                if (ClearArea(origin, probe.Width, probe.Height) == false)
                {
                    continue;
                }

                BeltLine line = probe with { Origin = origin };
                List<(string Id, Int2 Origin, Dir Rot)> parts = line.Parts(fab.Id, "sorter", "long-sorter", "power-pole").Select(p => (p.Id, P.Torus.Wrap(p.Origin), p.Rot)).ToList();
                if (parts.Any(p => P.CanPlace(C.Buildings[p.Id], p.Origin, p.Rot) is not null))
                {
                    continue;
                }

                foreach ((string id, Int2 at, Dir rot) in parts)
                {
                    Send(new PlaceBuilding(id, at, rot));
                    plan.Placed.Add(id);
                    plan.PlannedCosts.AddRange(C.Buildings[id].CostList);
                }

                _lines.Add((P.Id, line));

                return true;
            }
        }

        plan.Problems.Add("no room for a belt line");

        return false;
    }

    /// <summary>True if a rectangle and a one-tile walkway round it are free of buildings, rails and ore.</summary>
    private bool ClearArea(Int2 origin, int w, int h)
    {
        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                Int2 t = P.Torus.Wrap(origin + new Int2(x, y));
                if ((P.OccupantAt(t) != 0) || P.IsRail(t) || (P.KnownResourceAt(t) is { IsTree: false }))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Puts each built line fabricator on its line's recipe and limits each source crate to one stack of its ingredient.</summary>
    private void SetLineRecipes()
    {
        foreach (BeltLine l in Lines())
        {
            for (int i = 0; i < l.Inputs.Length; i++)
            {
                if ((P.BuildingAt(P.Torus.Wrap(l.SourceCrate(i))) is { Def.Kind: BuildingKind.Chest } crate) && ((crate.Inv.Slots != 1) || (crate.Inv.Filter != l.Inputs[i])))
                {
                    Send(new SetCrateLimit(crate.Id, 1, l.Inputs[i]));
                }
            }

            for (int i = 0; i < l.Count; i++)
            {
                if ((P.BuildingAt(P.Torus.Wrap(l.Fabricator(i))) is { Def.Kind: BuildingKind.Assembler } f) && (f.RecipeId != l.Recipe))
                {
                    Send(new SetRecipe(f.Id, l.Recipe));
                }
            }
        }
    }

    /// <summary>
    /// Helper jobs for the lines: top up each source crate a load at a time while the line's product is wanted.
    /// </summary>
    private void FeedLines(BotPlan plan, List<Job> needs, Func<string[], int> sourceFor, Building lander, int budget)
    {
        int start = needs.Count;
        Dictionary<string, int> science = ScienceRecipes();
        foreach (BeltLine l in Lines())
        {
            // A line whose product nothing needs now (cores for research long done) idles, as a player's would,
            // instead of taking helpers from what does.
            RecipeDef r = C.Recipes[l.Recipe];
            bool wanted = (science.ContainsKey(r.Id) || plan.CraftDeficit.ContainsKey(r.Outputs[0].Item)) && (Surplus(plan, r) == false);
            for (int i = 0; (i < l.Inputs.Length) && wanted; i++)
            {
                // About ten crafts per fabricator in the crate (more would starve the rest of the base); a
                // helper tops it up once it is down to half.
                string item = l.Inputs[i];
                int perCraft = r.Inputs.First(x => x.Item == item).Count;
                int target = System.Math.Min(C.Items[item].Stack, perCraft * 10 * l.Count);
                if ((P.BuildingAt(P.Torus.Wrap(l.SourceCrate(i))) is { Def.Kind: BuildingKind.Chest } stray)
                    && (stray.Inv.Items.Select(kv => kv.Key).FirstOrDefault(k => k != item) is { } junk) && (StoreFor(junk, lander) is { } away))
                {
                    // Dropped in before the crate's filter was set: out it goes, before it reaches the belt.
                    needs.Add(new Job(JobKind.Haul, FromId: stray.Id, ToId: away.Id, Item: junk));
                }

                if ((P.BuildingAt(P.Torus.Wrap(l.SourceCrate(i))) is { Def.Kind: BuildingKind.Chest } crate) && (crate.Inv.Count(item) < target / 2)
                    && (Stock(item) >= perCraft * 2))
                {
                    needs.Add(new Job(JobKind.Haul, FromId: sourceFor([item]), ToId: crate.Id, Item: item));
                }
            }

            if (needs.Count - start >= budget)
            {
                return;
            }
        }
    }
}
