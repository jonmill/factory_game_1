namespace Factory.Sim;

public sealed partial class World
{
    internal void AddResearchUnit()
    {
        string? id = Research.Current;
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
            int level = Research.Level(id) + 1;
            Research.Levels[id] = level;
            Research.Progress[id] = 0;
            Research.Completed.Add(id);
            Emit($"Research complete: {tech.Name} {level}");

            return;
        }

        Research.Completed.Add(id);
        foreach (string u in tech.Unlocks)
        {
            Research.Unlocked.Add(u);
        }

        Research.Current = null;
        Emit($"Research complete: {tech.Name}");
    }
}
