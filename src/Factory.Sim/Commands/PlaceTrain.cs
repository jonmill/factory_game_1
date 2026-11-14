using System.Text.Json.Serialization;

namespace Factory.Sim;

/// <summary>Orders a train to be built on existing track.</summary>
/// <param name="Head">Rail tile for the front vehicle.</param>
/// <param name="Facing">Direction the train faces; remaining vehicles trail behind along the track.</param>
/// <param name="Vehicles">Vehicle definition ids from front to back (1 to 12, at least one locomotive).</param>
public sealed record PlaceTrain(Int2 Head, Dir Facing, string[] Vehicles) : Command
{
    /// <summary>Checks the vehicle list is valid and researched and that enough free connected track exists.</summary>
    public override string? Validate(World w)
    {
        if (ValidPlanet(w) == false)
        {
            return "Bad planet";
        }

        if ((Vehicles.Length == 0) || (Vehicles.Length > 12))
        {
            return "A train needs 1-12 vehicles";
        }

        foreach (string v in Vehicles)
        {
            if (w.Content.Vehicles.ContainsKey(v) == false)
            {
                return $"Unknown vehicle {v}";
            }

            if (w.Research.Unlocked.Contains(v) == false)
            {
                return $"{w.Content.Vehicles[v].Name} has not been researched";
            }
        }

        if (Vehicles.Any(v => w.Content.Vehicles[v].IsLocomotive) == false)
        {
            return "A train needs a locomotive or hand cart";
        }

        List<Int2>? tiles = P(w).LayoutTrain(Head, Facing, Vehicles.Length);
        if (tiles is null)
        {
            return "Not enough connected track for that train";
        }

        if (tiles.Any(P(w).TrainOccupies))
        {
            return "Another train is in the way";
        }

        return null;
    }

    /// <summary>Creates a train construction site costing the combined materials and build time of all vehicles.</summary>
    public override void Apply(World w)
    {
        Planet p = P(w);
        List<Int2> tiles = p.LayoutTrain(Head, Facing, Vehicles.Length)!;
        ItemAmount[] cost = Vehicles.SelectMany(v => w.Content.Vehicles[v].CostList)
            .GroupBy(c => c.Item).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ItemAmount(g.Key, g.Sum(x => x.Count))).ToArray();
        int work = Vehicles.Sum(v => w.Content.Vehicles[v].BuildTicks);
        ConstructionSite site = p.CreateSite(SiteKind.BuildTrain, "train", Head, Facing, 1, 1, cost, work);
        site.Vehicles.AddRange(Vehicles);
        site.TrainTiles.AddRange(tiles);
    }
}
