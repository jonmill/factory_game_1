using Factory.Sim;

namespace Factory.Game;

/// <summary>One file in the saves folder, as the save and load lists show it (read from its header only).</summary>
/// <param name="Stem">File name without the extension; what loading and deleting go by.</param>
/// <param name="Name">The name the player gave it (the file name for older saves).</param>
/// <param name="SavedAt">When it was written: the header's time, or the file's for older saves.</param>
/// <param name="Ticks">In-game time played, in ticks; null if the file couldn't be read.</param>
/// <param name="Planet">The planet being viewed when it was saved, if recorded.</param>
/// <param name="Build">The game build that wrote it, if recorded (format 2 and later).</param>
/// <param name="Version">Save format version; 0 if the file couldn't be read.</param>
/// <param name="Check">Whether this build can load it, and why not.</param>
public sealed record SaveSlot(string Stem, string Name, DateTimeOffset SavedAt, long? Ticks, string? Planet, string? Build, int Version, SaveCheck Check);
