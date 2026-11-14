using Factory.Playtest.Bots;
using Factory.Sim;

namespace Factory.Playtest.Experiments;

/// <summary>A kind of headless playtest task. Add new ones to <see cref="Experiments.All"/>.</summary>
public interface IExperiment
{
    /// <summary>Short identifier used on the command line and for report file names.</summary>
    string Name { get; }
    /// <summary>One-line explanation of what the experiment does.</summary>
    string Description { get; }
    /// <summary>Runs the experiment and returns its report.</summary>
    /// <param name="ctx">Shared inputs such as content, seeds and time budget.</param>
    Report Run(ExperimentContext ctx);
}
