using Factory.Sim;
using Godot;

namespace Factory.Game.Ui;

/// <summary>
/// The research tree as a node graph: technologies are laid out in columns by how many prerequisites
/// deep they are, with elbow links from each prerequisite. Cards show status at a glance (researched,
/// researching, available, locked); clicking one selects it.
/// </summary>
public partial class TechTreeView : Control
{
    private const float CardW = 236;
    private const float CardH = 66;
    private const float ColGap = 70;
    private const float RowGap = 22;
    private const float Pad = 16;

    private const float HeaderH = 30;

    private readonly Dictionary<string, Rect2> _cards = [];
    private readonly List<(AgeDef Age, int First, int Last)> _bands = [];
    private string? _hover;

    /// <summary>Content with the technologies.</summary>
    public ContentDb Content { get; set; } = null!;

    /// <summary>Current research state.</summary>
    public System.Func<ResearchState> Research { get; set; } = null!;

    /// <summary>Draws a technology's icon into a rect.</summary>
    public System.Action<CanvasItem, Rect2, TechDef> DrawIcon { get; set; } = null!;

    /// <summary>The selected technology id.</summary>
    public string? Selected { get; set; }

    /// <summary>Raised when a card is clicked.</summary>
    public event System.Action<string>? Picked;

    /// <summary>Lays the cards out from the content; call once after setting <see cref="Content"/>.</summary>
    public void Layout()
    {
        // Ages run left to right; within an age, a tech sits one column right of its deepest prerequisite.
        Dictionary<string, int> column = [];
        Dictionary<int, int> rows = [];
        _cards.Clear();
        _bands.Clear();
        int ageStart = 0;
        List<AgeDef> ages = Content.AgeList.Count > 0 ? Content.AgeList : [new AgeDef { Id = "" }];
        foreach (AgeDef age in ages)
        {
            List<TechDef> techs = Content.TechList.Where(t => (Content.AgeList.Count == 0) || (Content.AgeIndex(t.Age) == Content.AgeIndex(age.Id))).ToList();
            if (techs.Count == 0)
            {
                continue;
            }

            int Col(TechDef t)
            {
                if (column.TryGetValue(t.Id, out int c))
                {
                    return c;
                }

                c = System.Math.Max(ageStart, t.Prereqs.Length == 0 ? 0 : t.Prereqs.Max(p => Col(Content.Techs[p]) + 1));
                column[t.Id] = c;

                return c;
            }

            int last = ageStart;
            foreach (TechDef t in techs)
            {
                int col = Col(t);
                int row = rows.GetValueOrDefault(col);
                rows[col] = row + 1;
                _cards[t.Id] = new Rect2(Pad + (col * (CardW + ColGap)), Pad + HeaderH + (row * (CardH + RowGap)), CardW, CardH);
                last = System.Math.Max(last, col);
            }

            _bands.Add((age, ageStart, last));
            ageStart = last + 1;
        }

        float w = _cards.Values.Max(r => r.End.X) + Pad;
        float h = _cards.Values.Max(r => r.End.Y) + Pad;
        CustomMinimumSize = new Vector2(w, h);
        MouseFilter = MouseFilterEnum.Stop;
        QueueRedraw();
    }

    /// <inheritdoc />
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm)
        {
            string? hover = Hit(mm.Position);
            if (hover != _hover)
            {
                _hover = hover;
                QueueRedraw();
            }
        }
        else if ((@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) && (Hit(mb.Position) is { } id))
        {
            Selected = id;
            Picked?.Invoke(id);
            QueueRedraw();
            AcceptEvent();
        }
    }

    /// <inheritdoc />
    public override string _GetTooltip(Vector2 atPosition) =>
        Hit(atPosition) is { } id ? $"{Content.Techs[id].Name}\n{Content.Techs[id].Description}" :
        _bands.FirstOrDefault(b => (atPosition.Y < Pad + HeaderH) && (atPosition.X >= Pad + (b.First * (CardW + ColGap))) && (atPosition.X <= Pad + ((b.Last + 1) * (CardW + ColGap)))).Age?.Description ?? "";

    private string? Hit(Vector2 p) => _cards.FirstOrDefault(kv => kv.Value.HasPoint(p)).Key;

    /// <inheritdoc />
    public override void _Draw()
    {
        ResearchState rs = Research();
        DrawGrid();
        DrawAgeBands();

        // Links first, so cards sit on top of them.
        foreach (TechDef t in Content.TechList)
        {
            Rect2 to = _cards[t.Id];
            foreach (string p in t.Prereqs)
            {
                Rect2 from = _cards[p];
                Color c = rs.Completed.Contains(p) ? new Color(0.45f, 0.75f, 0.95f, 0.8f) : new Color(0.4f, 0.5f, 0.6f, 0.45f);
                Vector2 a = new Vector2(from.End.X, from.GetCenter().Y);
                Vector2 b = new Vector2(to.Position.X, to.GetCenter().Y);
                float midX = (a.X + b.X) / 2;
                DrawPolyline([a, new Vector2(midX, a.Y), new Vector2(midX, b.Y), b], c, 2, true);
                DrawCircle(b, 3, c);
            }
        }

        Font font = ThemeDB.FallbackFont;
        foreach (TechDef t in Content.TechList)
        {
            Rect2 r = _cards[t.Id];
            // Repeatable research is never "done": it shows its level and stays available.
            bool done = rs.Completed.Contains(t.Id) && (t.Repeatable == false);
            int need = rs.UnitsFor(t);
            bool current = rs.Current == t.Id;
            bool available = (done == false) && (t.ComingSoon == false) && t.Prereqs.All(rs.Completed.Contains);
            string name = t.Repeatable && (rs.Level(t.Id) > 0) ? $"{t.Name}  (level {rs.Level(t.Id)})" : t.Name;
            int units = rs.Progress.GetValueOrDefault(t.Id);
            Color border = current ? HudStyle.Orange : done ? HudStyle.Good : available ? HudStyle.LineBright : HudStyle.Line;
            Color fill = current ? new Color(0.14f, 0.09f, 0.04f, 0.95f) : done ? new Color(0.04f, 0.1f, 0.08f, 0.95f) : HudStyle.Well;
            DrawStyleBox(HudStyle.Box(fill, border, 4, 0, 0), r);
            if ((t.Id == Selected) || (t.Id == _hover))
            {
                DrawRect(r.Grow(3), t.Id == Selected ? HudStyle.Orange : HudStyle.LineBright, false, t.Id == Selected ? 2 : 1);
            }

            Rect2 icon = new Rect2(r.Position + new Vector2(8, 9), new Vector2(46, 46));
            DrawStyleBox(HudStyle.Box(new Color(0.02f, 0.04f, 0.07f), HudStyle.Line, 3, 0, 0), icon);
            DrawIcon(this, icon.Grow(-4), t);
            if ((done == false) && (available == false))
            {
                DrawRect(icon, new Color(0.015f, 0.03f, 0.05f, 0.6f));
            }

            Vector2 text = r.Position + new Vector2(62, 24);
            DrawString(font, text, name, HorizontalAlignment.Left, CardW - 70, 13, (done || available || current) ? HudStyle.Text : HudStyle.TextDim);
            string state = done ? "Researched" : current ? $"Researching  {units}/{need}" : t.ComingSoon ? "Coming soon" :
                available ? (units > 0 ? $"Available  {units}/{need}" : "Available") : "Locked";
            Color sc = done ? HudStyle.Good : current ? HudStyle.Orange : available ? HudStyle.Header : HudStyle.TextDim;
            DrawString(font, text + new Vector2(0, 18), state, HorizontalAlignment.Left, CardW - 70, 11, sc);
            if ((units > 0) && (done == false))
            {
                Rect2 bar = new Rect2(r.Position + new Vector2(62, CardH - 14), new Vector2(CardW - 74, 4));
                DrawRect(bar, new Color(0, 0, 0, 0.5f));
                DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * units / System.Math.Max(1, need), bar.Size.Y)), HudStyle.Orange);
            }
        }
    }

    /// <summary>A tinted column band behind each age's techs, with the age's name along the top.</summary>
    private void DrawAgeBands()
    {
        Font font = ThemeDB.FallbackFont;
        foreach ((AgeDef age, int first, int last) in _bands)
        {
            if (age.Id.Length == 0)
            {
                continue;
            }

            Color c = Color.FromString(age.Color, HudStyle.LineBright);
            float x0 = Pad + (first * (CardW + ColGap)) - (ColGap * 0.35f);
            float x1 = Pad + (last * (CardW + ColGap)) + CardW + (ColGap * 0.35f);
            Rect2 band = new Rect2(x0, 4, x1 - x0, Size.Y - 8);
            DrawRect(band, new Color(c, 0.03f));
            DrawRect(new Rect2(band.Position, new Vector2(band.Size.X, 2)), new Color(c, 0.8f));
            DrawString(font, new Vector2(x0 + 10, 4 + 20), age.Name.ToUpperInvariant(), HorizontalAlignment.Left, band.Size.X - 20, 13, c);
        }
    }

    private void DrawGrid()
    {
        Color dot = new Color(0.45f, 0.72f, 0.9f, 0.08f);
        for (float x = 0; x < Size.X; x += 24)
        {
            for (float y = 0; y < Size.Y; y += 24)
            {
                DrawRect(new Rect2(x, y, 1, 1), dot);
            }
        }
    }
}
