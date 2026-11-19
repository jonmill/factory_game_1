using Factory.Sim;

namespace Factory.Game.Ui;

/// <summary>
/// The deposits on a planet as the player knows them, as in DSP's veins: each explored ore patch (tiles of
/// the same ore touching side by side) with how much is left in it. Trees aren't grouped; each tree stands
/// alone. Client-side only: the patch map is rebuilt when tiles are mined out or more ground is explored
/// (at most once a second), and totals are summed from the patch's tiles at most twice a second.
/// </summary>
public sealed class Deposits
{
    /// <summary>One explored ore patch.</summary>
    public sealed class Patch
    {
        /// <summary>The ore.</summary>
        public required ResourceDef Resource { get; init; }

        /// <summary>Tile indices (y * width + x) in the patch.</summary>
        public List<int> Tiles { get; } = [];

        /// <summary>The middle of the patch in tiles, possibly outside 0..size where it wraps.</summary>
        public Godot.Vector2 Center { get; set; }

        /// <summary>Ore left in the patch, as of the last <see cref="Deposits.Refresh"/>.</summary>
        public long Total { get; set; }
    }

    private readonly Planet _p;
    private int[] _patchOf = [];
    private readonly List<Patch> _patches = [];
    private int _terrainVersion = -1, _fogVersion = -1;
    private double _labelAt = double.MinValue, _totalAt = double.MinValue;

    /// <summary>Creates the deposit index for a planet.</summary>
    /// <param name="p">The planet.</param>
    public Deposits(Planet p) => _p = p;

    /// <summary>The explored patches.</summary>
    public IReadOnlyList<Patch> Patches => _patches;

    /// <summary>Brings the patches and their totals up to date; cheap to call every frame.</summary>
    public void Refresh()
    {
        double now = Godot.Time.GetTicksMsec() / 1000.0;
        bool changed = (_p.TerrainVersion != _terrainVersion) || (_p.FogVersion != _fogVersion);
        if (changed && (now - _labelAt >= 1.0))
        {
            _labelAt = now;
            _terrainVersion = _p.TerrainVersion;
            _fogVersion = _p.FogVersion;
            Label();
            _totalAt = double.MinValue;
        }

        if (now - _totalAt >= 0.5)
        {
            _totalAt = now;
            foreach (Patch patch in _patches)
            {
                long sum = 0;
                foreach (int i in patch.Tiles)
                {
                    sum += _p.AmountAt(new Int2(i % _p.Width, i / _p.Width));
                }

                patch.Total = sum;
            }
        }
    }

    /// <summary>The patch a tile belongs to, if it is explored ore.</summary>
    /// <param name="t">The tile.</param>
    /// <returns>The patch, or null.</returns>
    public Patch? At(Int2 t)
    {
        t = _p.Torus.Wrap(t);
        int i = (t.Y * _p.Width) + t.X;

        return (i < _patchOf.Length) && (_patchOf[i] > 0) ? _patches[_patchOf[i] - 1] : null;
    }

    private void Label()
    {
        int w = _p.Width, h = _p.Height;
        if (_patchOf.Length != w * h)
        {
            _patchOf = new int[w * h];
        }
        else
        {
            System.Array.Clear(_patchOf);
        }

        _patches.Clear();
        Stack<int> todo = new Stack<int>();
        for (int start = 0; start < _patchOf.Length; start++)
        {
            Int2 st = new Int2(start % w, start / w);
            if ((_patchOf[start] != 0) || (_p.KnownResourceAt(st) is not { IsTree: false } res) || (_p.AmountAt(st) <= 0))
            {
                continue;
            }

            // Flood the patch out from here across the wrap, summing offsets from the first tile for the centre.
            Patch patch = new Patch { Resource = res };
            _patches.Add(patch);
            int id = _patches.Count;
            _patchOf[start] = id;
            todo.Push(start);
            long sx = 0, sy = 0;
            while (todo.Count > 0)
            {
                int i = todo.Pop();
                Int2 t = new Int2(i % w, i / w);
                patch.Tiles.Add(i);
                Int2 d = _p.Torus.Delta(st, t);
                sx += d.X;
                sy += d.Y;
                foreach (Int2 step in (ReadOnlySpan<Int2>)[new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1)])
                {
                    Int2 n = _p.Torus.Wrap(new Int2(t.X + step.X, t.Y + step.Y));
                    int ni = (n.Y * w) + n.X;
                    if ((_patchOf[ni] == 0) && (_p.KnownResourceAt(n) == res) && (_p.AmountAt(n) > 0))
                    {
                        _patchOf[ni] = id;
                        todo.Push(ni);
                    }
                }
            }

            patch.Center = new Godot.Vector2(st.X + 0.5f + (sx / (float)patch.Tiles.Count), st.Y + 0.5f + (sy / (float)patch.Tiles.Count));
        }
    }

    /// <summary>A count the way DSP labels veins: 812, 12.4k, 1.25M.</summary>
    /// <param name="n">The amount.</param>
    /// <returns>Short text.</returns>
    public static string Brief(long n) => n switch
    {
        >= 1_000_000 => $"{n / 1_000_000.0:0.##}M",
        >= 10_000 => $"{n / 1000.0:0.#}k",
        >= 1_000 => $"{n / 1000.0:0.##}k",
        _ => $"{n}",
    };
}
