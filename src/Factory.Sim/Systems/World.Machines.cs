namespace Factory.Sim;

public sealed partial class World
{
    /// <summary>Applies an upgrade that changes stored stats at once: Robot Efficiency re-rates every robot.</summary>
    /// <param name="series">The repeatable tech whose level just rose (or any other tech id).</param>
    private void RefreshAfterUpgrade(string series)
    {
        if (series == Factory.Sim.Planet.RobotEfficiency)
        {
            foreach (Unit u in Planets.SelectMany(p => p.Units))
            {
                u.Planet.RefreshRobot(u);
            }
        }
    }

    /// <summary>
    /// Adds a finished research unit to a technology: the one the lab started it for, or the current research
    /// when none is given. Progress belongs to the tech, as in Factorio, so switching research mid-unit doesn't
    /// lose it. If other labs finished that tech in the meantime, the unit counts toward the current research.
    /// </summary>
    /// <param name="techId">The tech the unit was started for, or null for the current research.</param>
    internal void AddResearchUnit(string? techId = null)
    {
        string? id = techId ?? Research.Current;
        if ((id is not null) && Content.Techs.TryGetValue(id, out TechDef? started) && (started.Repeatable == false) && Research.Completed.Contains(id))
        {
            id = Research.Current;
        }

        if (id is null)
        {
            return;
        }

        TechDef tech = Content.Techs[id];
        int done = Research.Progress.GetValueOrDefault(id) + 1;
        Research.Progress[id] = done;
        if (done < Research.UnitsFor(tech))
        {
            return;
        }

        if (tech.Repeatable)
        {
            // Infinite research: bank the level and keep going on the next one.
            Research.Levels[id] = Research.Levels.GetValueOrDefault(id) + 1;
            int level = Research.Level(id);
            Research.Progress[id] = 0;
            Research.Completed.Add(id);
            Stats.RecordResearch(id);
            RefreshAfterUpgrade(id);
            Emit($"Research complete: {tech.Name} {level}");

            return;
        }

        Research.Completed.Add(id);
        RefreshAfterUpgrade(tech.LevelOf ?? id);
        Stats.RecordResearch(id);
        foreach (string u in tech.Unlocks)
        {
            Research.Unlocked.Add(u);
        }

        if (Research.Current == id)
        {
            Research.Current = null;
        }

        Emit($"Research complete: {tech.Name}");
    }
}
