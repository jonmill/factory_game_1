using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Playtest.Experiments;

/// <summary>The result of an experiment: findings, tables and notes, renderable as Markdown or JSON.</summary>
public sealed class Report
{
    /// <summary>Name of the experiment that produced the report; also the output file name.</summary>
    public string Experiment { get; init; } = "";
    /// <summary>Short overall summary shown at the top of the report.</summary>
    public string Summary { get; set; } = "";
    /// <summary>Experiment inputs recorded for reproducibility.</summary>
    public Dictionary<string, string> Parameters { get; } = new();
    /// <summary>Issues found, with duplicates merged by <see cref="Add"/>.</summary>
    public List<Finding> Findings { get; } = [];
    /// <summary>Supporting data tables.</summary>
    public List<Table> Tables { get; } = [];
    /// <summary>Free-form notes appended at the end of the report.</summary>
    public List<string> Notes { get; } = [];

    /// <summary>The highest severity among the findings, or <see cref="Severity.Info"/> when there are none.</summary>
    public Severity MaxSeverity => Findings.Count == 0 ? Severity.Info : Findings.Max(f => f.Severity);

    /// <summary>Adds a finding, merging duplicates (same category + title) into an occurrence count.</summary>
    public void Add(Finding f)
    {
        int i = Findings.FindIndex(x => (x.Category == f.Category) && (x.Title == f.Title));
        if (i < 0)
        {
            Findings.Add(f);

            return;
        }

        Finding old = Findings[i];
        Findings[i] = old with
        {
            Occurrences = old.Occurrences + f.Occurrences,
            Severity = (Severity)System.Math.Max((int)old.Severity, (int)f.Severity)
        };
    }

    /// <summary>Renders the report as a Markdown document.</summary>
    public string ToMarkdown()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"# Playtest report: {Experiment}");
        sb.AppendLine();
        if (Summary.Length > 0)
        {
            sb.AppendLine(Summary);
            sb.AppendLine();
        }

        if (Parameters.Count > 0)
        {
            sb.AppendLine("**Parameters:** " + string.Join(", ", Parameters.Select(kv => $"`{kv.Key}={kv.Value}`")));
            sb.AppendLine();
        }

        sb.AppendLine($"## Findings ({Findings.Count})");
        sb.AppendLine();
        if (Findings.Count == 0)
        {
            sb.AppendLine("_None._");
        }

        foreach (Finding? f in Findings.OrderByDescending(f => f.Severity).ThenBy(f => f.Category))
        {
            sb.AppendLine($"### [{f.Severity}] {f.Title}" + (f.Occurrences > 1 ? $" (×{f.Occurrences})" : ""));
            sb.AppendLine($"_{f.Category}_" + (f.Seed is { } s ? $" · seed {s}" : "") + (f.Tick is { } t ? $" · tick {t} ({t / 3600} min)" : "") +
                          (f.Replay is { } r ? $" · replay `{r}`" : ""));
            sb.AppendLine();
            sb.AppendLine(f.Detail);
            sb.AppendLine();
        }

        foreach (Table t in Tables)
        {
            sb.AppendLine($"## {t.Title}");
            sb.AppendLine();
            sb.AppendLine("| " + string.Join(" | ", t.Columns) + " |");
            sb.AppendLine("|" + string.Join("|", t.Columns.Select(_ => "---")) + "|");
            foreach (string[] row in t.Rows)
            {
                sb.AppendLine("| " + string.Join(" | ", row) + " |");
            }

            sb.AppendLine();
        }

        if (Notes.Count > 0)
        {
            sb.AppendLine("## Notes");
            foreach (string n in Notes)
            {
                sb.AppendLine($"- {n}");
            }
        }

        return sb.ToString();
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serializes the report to indented JSON with enums as strings.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, _jsonOptions);

    /// <summary>Writes the report as both <c>.md</c> and <c>.json</c> files named after the experiment.</summary>
    /// <param name="dir">Directory to write into; created if missing.</param>
    public void Write(string dir)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{Experiment}.md"), ToMarkdown());
        File.WriteAllText(Path.Combine(dir, $"{Experiment}.json"), ToJson());
    }
}
