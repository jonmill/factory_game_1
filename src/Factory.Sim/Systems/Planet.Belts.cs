namespace Factory.Sim;

/// <summary>
/// Conveyor belts, in the usual factory-game pattern: each tile carries up to four items toward its
/// front and hands them to the belt it faces. Sorters load and unload belts, and a drill drops its ore
/// onto a belt in front of it. A belt's items are kept in its inventory as well as in its lane, so
/// saving, dismantling and item accounting treat them like any other contents.
/// </summary>
public sealed partial class Planet
{
    /// <summary>The closest two items may ride on one belt, in sub-units (four to a tile).</summary>
    public const int BeltGap = Fixed.One / 4;

    private void UpdateBelt(Building b)
    {
        if (b.Lane.Count == 0)
        {
            return;
        }

        int speed = b.Def.Speed;
        Building? next = BuildingAt(b.FrontTile()) is { Def.Kind: BuildingKind.Belt } n && (n != b) && (n.FrontTile() != b.Origin) ? n : null;
        int limit = int.MaxValue;
        for (int i = 0; i < b.Lane.Count; i++)
        {
            BeltItem it = b.Lane[i];
            int to = System.Math.Min(it.Pos + speed, limit);
            if ((i == 0) && (to >= Fixed.One))
            {
                if ((next is not null) && BeltHasRoom(next))
                {
                    b.Lane.RemoveAt(0);
                    b.Inv.Remove(it.Item, 1);
                    next.Lane.Add(new BeltItem { Item = it.Item, Pos = to - Fixed.One });
                    next.Inv.AddUnchecked(it.Item, 1);
                    i--;
                    limit = int.MaxValue;
                    continue;
                }

                // Blocked: wait just short of the edge, so a tile holds four items.
                to = Fixed.One - 1;
            }

            it.Pos = System.Math.Max(it.Pos, to);
            limit = it.Pos - BeltGap;
        }
    }

    /// <summary>True if an item can be put on the back of the belt.</summary>
    private static bool BeltHasRoom(Building belt) => (belt.Lane.Count == 0) || (belt.Lane[^1].Pos >= BeltGap);

    /// <summary>Puts an item on the back of a belt; the caller has checked there is room.</summary>
    private static void PutOnBelt(Building belt, string item)
    {
        belt.Lane.Add(new BeltItem { Item = item, Pos = 0 });
        belt.Inv.AddUnchecked(item, 1);
    }

    /// <summary>Takes the frontmost item off a belt that the destination accepts, or null.</summary>
    private string? TakeFromBelt(Building belt, Building dst)
    {
        for (int i = 0; i < belt.Lane.Count; i++)
        {
            string item = belt.Lane[i].Item;
            if (AcceptAmount(dst, item) > 0)
            {
                belt.Lane.RemoveAt(i);
                belt.Inv.Remove(item, 1);

                return item;
            }
        }

        return null;
    }

    /// <summary>A drill facing a belt drops its output straight onto it.</summary>
    private void DrillOntoBelt(Building drill)
    {
        if ((drill.Out.IsEmpty == false) && (BuildingAt(drill.FrontTile()) is { Def.Kind: BuildingKind.Belt } belt) && BeltHasRoom(belt))
        {
            string item = drill.Out.Items.First().Key;
            drill.Out.Remove(item, 1);
            PutOnBelt(belt, item);
        }
    }
}
