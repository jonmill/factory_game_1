using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Progress rings, the same everywhere something takes time: a machine's craft, a lab's research unit, a
/// drill's next ore, the fuel burning in a burner (counting down), and a robot mining, gathering or
/// building. Each helper returns 0 to 1, or -1 when nothing is under way (no ring).
/// </summary>
public partial class Hud
{
    /// <summary>What a robot is busy with: an icon, a tooltip and how far along it is.</summary>
    private sealed record Activity(System.Action<CanvasItem, Rect2> Icon, string What, float Progress);

    private static float Fraction(long done, long total) => total <= 0 ? -1 : Mathf.Clamp(done / (float)total, 0, 1);

    /// <summary>How far along a building's current cycle is: a craft, a research unit or the next ore.</summary>
    private float WorkProgress(Building b)
    {
        if (b.Def.Kind == BuildingKind.Lab)
        {
            return b.Crafting && (W.Research.Current is { } tech) && C.Techs.TryGetValue(tech, out TechDef? t) ? Fraction(b.Progress, t.UnitTicks * 100L) : -1;
        }

        if (b.Def.Kind == BuildingKind.Drill)
        {
            return (b.Progress > 0) || (b.Status == MachineStatus.Working) ? Fraction(b.Progress, b.Def.MiningTicks * 100L) : -1;
        }

        return b.Crafting && (b.Recipe is { } r) ? Fraction(b.Progress, r.Ticks * 100L) : -1;
    }

    /// <summary>How much of the fuel item now burning is left: 1 just after it's put on, counting down to 0.</summary>
    private float FuelLeft(Building b) =>
        (b.FuelJ > 0) && (b.BurningFuel is { } fuel) ? Fraction(b.FuelJ, C.Item(fuel).FuelKj * 1000L) : -1;

    /// <summary>What a robot is doing that takes time (mining, gathering, building or dismantling), or null.</summary>
    private Activity? UnitActivity(Unit u)
    {
        Planet p = u.Planet;
        if ((u.BuildingSiteId != 0) && (W.Get<ConstructionSite>(u.BuildingSiteId) is { WorkNeeded: > 0 } site) &&
            C.Buildings.TryGetValue(site.DefId, out BuildingDef? def))
        {
            string verb = site.Kind == SiteKind.Deconstruct ? "Dismantling" : "Building";

            return new Activity((ci, r) => Icons.Building(ci, r, def), $"{verb} {def.Name}", Fraction(site.WorkDone, site.WorkNeeded));
        }

        if (u.IsLeader && (u.MiningTile is { } tile) && (p.ResourceAt(tile) is { } res))
        {
            return new Activity(ItemIcon(res.Item), $"Mining {res.Name}", Fraction(u.MiningProgress, p.HandMiningTicks(u, res)));
        }

        if ((u.IsLeader == false) && (u.Phase == UnitPhase.Work) && (u.Job.Kind == JobKind.Gather) && (p.ResourceAt(u.TargetTile) is { } gathered))
        {
            return new Activity(ItemIcon(gathered.Item), $"Gathering {gathered.Name}", Fraction(u.WorkTicks, p.HandMiningTicks(u, gathered)));
        }

        return null;
    }

    /// <summary>Shows a robot's activity in a slot: its icon and progress ring, hidden when it's idle.</summary>
    private void ShowActivity(Slot slot, Unit u)
    {
        Activity? a = UnitActivity(u);
        // Keep the slot's space while hidden so the panel doesn't jump as work starts and stops.
        slot.Modulate = a is null ? Colors.Transparent : Colors.White;
        if (a is null)
        {
            slot.TooltipText = "";

            return;
        }

        if (slot.TooltipText != a.What)
        {
            slot.Icon = a.Icon;
            slot.TooltipText = a.What;
            slot.QueueRedraw();
        }

        slot.Set(null, false, false, a.Progress);
    }

    /// <summary>Sets the ring on a slot, keeping everything else as it is.</summary>
    private static void Ring(Slot slot, float progress) => slot.Set(slot.Count, slot.Selected, slot.Dim, progress);
}
