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
        if (done < tech.Units)
        {
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
