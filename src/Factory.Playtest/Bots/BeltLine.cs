using Factory.Sim;

namespace Factory.Playtest.Bots;

/// <summary>
/// A belt-fed production line, the layout an experienced player builds once belts and long arms arrive: a row of
/// fabricators on one recipe with a belt per ingredient, so a belt that backs up never blocks another ingredient
/// (a mixed belt jams as soon as one ingredient runs ahead). Each input belt starts at a source crate emptied
/// onto it by an arm; arms above and below each fabricator take from the belts, and a long arm puts its output on
/// the output belt, which runs into an output crate. Helpers only keep the source crates stocked.
/// <code>
/// row 0   B >  > > > > > > > > > > >          third input (long arms reach it over belt A)
/// row 1   A >  > > > > > > > > > > >          first input
/// row 2     P    v L v   v L v                arms down (L: long arm from row 0)
/// row 3-5        [fab]   [fab]                fabricators, poles in the gaps (row 4)
/// row 6     P    ^ L ^   ^ L ^      P         arms up from belt C; long arms down to the output belt
/// row 7   C >  > > > > > > > > > > >          second input
/// row 8          > > > > > > > > >  ] O       output belt, arm, output crate
/// </code>
/// </summary>
/// <param name="Recipe">The recipe every fabricator on the line makes.</param>
/// <param name="Origin">Top-left tile of the layout.</param>
/// <param name="Inputs">The recipe's ingredients (one to three), one belt and source crate each, in order.</param>
/// <param name="Count">How many fabricators.</param>
/// <param name="FabSize">The fabricator's width and height (square, at least 3).</param>
public sealed record BeltLine(string Recipe, Int2 Origin, string[] Inputs, int Count, int FabSize)
{
    /// <summary>The most ingredients a line can carry: two belts above (one through a long arm), one below.</summary>
    public const int MaxInputs = 3;

    private const int FabStart = 2;

    private const int FabTop = 3;

    /// <summary>Rows the layout takes.</summary>
    public int Height => OutRow + 1;

    /// <summary>Columns the layout takes.</summary>
    public int Width => End + 2;

    private int K => Inputs.Length;

    /// <summary>Column of the output arm, just past the last fabricator's gap.</summary>
    private int End => FabStart + ((FabSize + 1) * Count) + 1;

    private int BottomArmRow => FabTop + FabSize;

    private int OutRow => BottomArmRow + 2;

    private Int2 At(int c, int r) => Origin + new Int2(c, r);

    /// <summary>The row of input <paramref name="i"/>'s belt: the first above, the second below, the third two above.</summary>
    private int BeltRow(int i) => i switch
    {
        0 => FabTop - 2,
        1 => BottomArmRow + 1,
        _ => FabTop - 3,
    };

    /// <summary>Where the source crate for input <paramref name="i"/> stands.</summary>
    /// <param name="i">Index into <see cref="Inputs"/>.</param>
    /// <returns>The tile.</returns>
    public Int2 SourceCrate(int i) => At(0, BeltRow(i));

    /// <summary>The output crate at the end of the output belt.</summary>
    public Int2 OutputCrate => At(End + 1, OutRow);

    /// <summary>Top-left tile of fabricator <paramref name="i"/>.</summary>
    /// <param name="i">0-based index along the line.</param>
    /// <returns>The tile.</returns>
    public Int2 Fabricator(int i) => At(FabStart + 1 + ((FabSize + 1) * i), FabTop);

    /// <summary>Every building of the line, with where it goes and which way it faces.</summary>
    /// <param name="fab">The fabricator to use.</param>
    /// <param name="arm">The sorter arm (reaching one tile).</param>
    /// <param name="longArm">The long sorter arm (reaching two).</param>
    /// <param name="pole">The power pole to use (one supplying two tiles round it).</param>
    /// <returns>(building id, origin, rotation) for each part.</returns>
    public IEnumerable<(string Id, Int2 Origin, Dir Rot)> Parts(string fab, string arm, string longArm, string pole)
    {
        for (int i = 0; i < K; i++)
        {
            int row = BeltRow(i);
            yield return ("chest", At(0, row), Dir.North);
            yield return (arm, At(1, row), Dir.East);
            // To the last fabricator's edge: items on tiles past its arms would just sit there.
            for (int c = 2; c < End - 1; c++)
            {
                yield return ("conveyor-belt", At(c, row), Dir.East);
            }
        }

        yield return (pole, At(1, FabTop - 1), Dir.North);
        yield return (pole, At(1, BottomArmRow), Dir.North);
        yield return (pole, At(End, BottomArmRow), Dir.North);
        int mid = FabSize / 2;
        for (int i = 0; i < Count; i++)
        {
            Int2 f = Fabricator(i);
            yield return (fab, f, Dir.North);
            for (int x = 0; x < FabSize; x++)
            {
                Int2 top = new Int2(f.X + x, f.Y - 1);
                Int2 bottom = new Int2(f.X + x, f.Y + FabSize);
                yield return ((K == MaxInputs) && (x == mid) ? longArm : arm, top, Dir.South);
                if ((x == mid) || (K == 1))
                {
                    // Output: a long arm from inside the fabricator, over belt C, onto the output belt.
                    yield return (longArm, bottom, Dir.South);
                }
                else
                {
                    yield return (arm, bottom, Dir.North);
                }
            }
        }

        for (int i = 0; i <= Count; i++)
        {
            yield return (pole, At(FabStart + ((FabSize + 1) * i), FabTop + mid), Dir.North);
        }

        for (int c = FabStart; c < End; c++)
        {
            yield return ("conveyor-belt", At(c, OutRow), Dir.East);
        }

        yield return (arm, At(End, OutRow), Dir.East);
        yield return ("chest", OutputCrate, Dir.North);
    }
}
