using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Playtest.Experiments;

/// <summary>A titled table of string cells included in a report.</summary>
/// <param name="Title">Heading shown above the table.</param>
/// <param name="Columns">Column headers.</param>
/// <param name="Rows">Row cells, one array per row in column order.</param>
public sealed record Table(string Title, string[] Columns, List<string[]> Rows);
