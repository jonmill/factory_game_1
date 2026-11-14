using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Factory.Playtest.Experiments;

/// <summary>How urgently a finding needs attention, from lowest to highest.</summary>
public enum Severity
{
    /// <summary>Informational only; nothing needs to change.</summary>
    Info,

    /// <summary>Minor issue worth a look when convenient.</summary>
    Low,

    /// <summary>Noticeable issue that affects play or balance.</summary>
    Medium,

    /// <summary>Serious issue such as a bug or a broken progression path.</summary>
    High,

    /// <summary>Game-breaking issue such as a crash or a confirmed softlock.</summary>
    Critical,
}
