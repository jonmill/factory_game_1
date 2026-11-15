namespace Factory.Sim;

/// <summary>
/// Robot upgrades and the commander's own construction. Modules installed in a robot's slots add
/// speed, cargo space and mining speed, and teach helper bots the Build and Operate train jobs. The
/// commander builds any ghost within its build reach by itself once the materials are in, so the
/// early game needs no builder bots.
/// </summary>
public sealed partial class Planet
{
    /// <summary>Jobs a helper bot can take with no modules at all.</summary>
    private static readonly JobKind[] _baseJobs = [JobKind.Idle, JobKind.Gather, JobKind.Haul];

    /// <summary>How many module slots a robot has: its chassis, plus research for the commander.</summary>
    /// <param name="u">The robot.</param>
    /// <returns>The number of slots.</returns>
    public int ModuleSlots(Unit u) => u.Robot.ModuleSlots + (u.IsLeader ? World.CommanderBonusSlots : 0);

    /// <summary>The module definitions installed in a robot, in slot order.</summary>
    /// <param name="u">The robot.</param>
    /// <returns>Its modules.</returns>
    public IEnumerable<ModuleDef> ModulesOf(Unit u) => u.Modules.Select(m => World.Content.Item(m).Module!);

    /// <summary>True if a helper bot can be given a job of this kind with the modules it has.</summary>
    /// <param name="u">The helper bot.</param>
    /// <param name="kind">The job.</param>
    /// <returns>Whether the job is allowed.</returns>
    public bool CanTakeJob(Unit u, JobKind kind) =>
        _baseJobs.Contains(kind) || ModulesOf(u).Any(m => m.Jobs.Contains(kind));

    /// <summary>The module that teaches a job, for messages and the job picker, or null if none does.</summary>
    /// <param name="kind">The job.</param>
    /// <returns>The module's item, if any.</returns>
    public ItemDef? ModuleForJob(JobKind kind) => World.Content.ItemList.FirstOrDefault(i => i.Module?.Jobs.Contains(kind) == true);

    /// <summary>Ticks a robot takes for a hand-mining step that normally takes <paramref name="baseTicks"/>.</summary>
    private int MiningTicks(Unit u, int baseTicks) => System.Math.Max(1, baseTicks * 100 / (100 + ModulesOf(u).Sum(m => m.MiningPct)));

    /// <summary>Recomputes a robot's speed and cargo space from its model and modules.</summary>
    internal void RefreshRobot(Unit u)
    {
        u.Speed = u.Robot.Speed * (100 + ModulesOf(u).Sum(m => m.SpeedPct)) / 100;
        u.Carry.Slots = u.Robot.CarrySlots + ModulesOf(u).Sum(m => m.CarrySlots);
        if ((u.IsLeader == false) && (CanTakeJob(u, u.Job.Kind) == false))
        {
            SetJob(u, Job.Idle);
        }
    }

    /// <summary>
    /// The commander builds the nearest ghost within its build reach that has all its materials, a
    /// step of work per tick, while it walks or mines. Dismantling works the same way, with whatever
    /// comes back going into its pockets.
    /// </summary>
    private void CommanderBuild(Unit u)
    {
        u.BuildingSiteId = 0;
        int reach = u.Robot.BuildReach;
        if (reach <= 0)
        {
            return;
        }

        long reach2 = (long)reach * Fixed.One * reach * Fixed.One;
        SupplyFromNearbyStorage(u, reach2);
        ConstructionSite? best = null;
        long bestDist = long.MaxValue;
        foreach (ConstructionSite s in Sites)
        {
            long d = SubTorus.DistSq(u.Pos, s.CenterSub);
            if (s.MaterialsComplete && (d <= reach2) && (d < bestDist))
            {
                best = s;
                bestDist = d;
            }
        }

        if (best is null)
        {
            return;
        }

        u.BuildingSiteId = best.Id;
        if (best.WorkDone < best.WorkNeeded)
        {
            best.WorkDone++;
        }

        if (best.WorkDone >= best.WorkNeeded)
        {
            CompleteSite(best, u);
        }
    }

    /// <summary>
    /// Ghosts within the commander's build reach draw missing materials straight from any lander or crate
    /// within its reach, so standing by the base is enough to build there, without carrying anything.
    /// </summary>
    private void SupplyFromNearbyStorage(Unit u, long reach2)
    {
        List<Building>? stores = null;
        foreach (ConstructionSite site in Sites)
        {
            if (site.MaterialsComplete || (SubTorus.DistSq(u.Pos, site.CenterSub) > reach2))
            {
                continue;
            }

            stores ??= Buildings.Where(b => b.Def.IsDepot && InLeaderReach(u, b.Origin)).OrderBy(b => b.Id).ToList();
            foreach (ItemAmount m in site.Missing().ToList())
            {
                int left = m.Count;
                foreach (Building store in stores)
                {
                    left -= store.Inv.MoveTo(site.Delivered, m.Item, left);
                    if (left <= 0)
                    {
                        break;
                    }
                }
            }
        }
    }
}
