using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>The tech tree window: the node graph on the left and the selected technology's details on the right.</summary>
public partial class Hud
{
    private TechTreeView _techView = null!;
    private VBoxContainer _techDetail = null!;
    private string? _techSig;

    private PanelContainer BuildTechTree()
    {
        PanelContainer win = MakeWindow(HudWindow.Tech, "Technology", "T", out VBoxContainer body);
        HBoxContainer cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 18);
        body.AddChild(cols);

        PanelContainer canvas = new PanelContainer();
        canvas.AddThemeStyleboxOverride("panel", HudStyle.Box(new Color(0.01f, 0.025f, 0.045f, 0.95f), HudStyle.Line, 3, 0, 0));
        _techView = new TechTreeView
        {
            Content = C,
            Research = () => W.Research,
            DrawIcon = DrawTechIcon,
        };
        _techView.Picked += _ =>
        {
            _techSig = null;
        };
        _techView.Layout();
        // The tree is wider than the screen now, so it scrolls sideways (drag the bar or Shift-scroll).
        ScrollContainer scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(900, System.Math.Min(_techView.CustomMinimumSize.Y + 14, 470)),
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        scroll.AddChild(_techView);
        canvas.AddChild(scroll);
        cols.AddChild(canvas);

        _techDetail = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        _techDetail.AddThemeConstantOverride("separation", 6);
        cols.AddChild(_techDetail);

        body.AddChild(HudStyle.Caption("Labs consume the unit materials and need power. Helper bots can haul materials in, or deposit them by hand.", HudStyle.TextDim, 11));

        return win;
    }

    /// <summary>A technology's icon: the first building it unlocks, else the first recipe's output.</summary>
    private void DrawTechIcon(CanvasItem ci, Rect2 r, TechDef t)
    {
        foreach (string id in t.Unlocks)
        {
            if (C.Buildings.TryGetValue(id, out BuildingDef? b))
            {
                Icons.Building(ci, r, b);

                return;
            }

            if (C.Recipes.TryGetValue(id, out RecipeDef? rec) && (rec.Outputs.Length > 0))
            {
                Icons.Item(ci, r, C.Item(rec.Outputs[0].Item));

                return;
            }
        }

        if (t.UnitCostList.Length > 0)
        {
            Icons.Item(ci, r, C.Item(t.UnitCostList[0].Item));
        }
    }

    private void UpdateTechTree()
    {
        ResearchState rs = W.Research;
        _techView.Selected ??= rs.Current ?? C.TechList.FirstOrDefault(t => (rs.Completed.Contains(t.Id) == false) && t.Prereqs.All(rs.Completed.Contains))?.Id;
        string sig = $"{_techView.Selected}|{rs.Current}|{rs.Completed.Count}|{rs.UnitsDone}";
        if (sig == _techSig)
        {
            return;
        }

        _techSig = sig;
        _techView.QueueRedraw();
        Clear(_techDetail);
        if (_techView.Selected is not { } id)
        {
            return;
        }

        TechDef t = C.Techs[id];
        bool done = rs.Completed.Contains(id);
        bool current = rs.Current == id;
        bool available = (done == false) && (t.ComingSoon == false) && t.Prereqs.All(rs.Completed.Contains);
        int units = rs.Progress.GetValueOrDefault(id);

        HBoxContainer head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(new Slot(64) { Icon = (ci, r) => DrawTechIcon(ci, r, t) });
        VBoxContainer hv = new VBoxContainer();
        Label title = HudStyle.Caption(t.Name, HudStyle.Header, 17);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.CustomMinimumSize = new Vector2(240, 0);
        hv.AddChild(title);
        string state = done ? "Researched" : current ? "Researching" : t.ComingSoon ? "Coming soon" : available ? "Available" : "Locked";
        hv.AddChild(HudStyle.Caption(state, done ? HudStyle.Good : current ? HudStyle.Orange : available ? HudStyle.Header : HudStyle.TextDim, 12));
        head.AddChild(hv);
        _techDetail.AddChild(head);

        Label desc = HudStyle.Caption(t.Description, HudStyle.Text, 12);
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.CustomMinimumSize = new Vector2(320, 0);
        _techDetail.AddChild(desc);

        if (t.Prereqs.Length > 0)
        {
            _techDetail.AddChild(HudStyle.Caption("Requires: " + string.Join(", ", t.Prereqs.Select(p => C.Techs[p].Name)), HudStyle.TextDim, 12));
        }

        _techDetail.AddChild(HudStyle.Divider());
        _techDetail.AddChild(HudStyle.Title($"Cost  ·  {t.Units} units × {t.UnitTicks / World.TicksPerSecond}s"));
        HBoxContainer cost = new HBoxContainer();
        foreach (ItemAmount c in t.UnitCostList)
        {
            Slot s = ItemSlot(c.Item, c.Count, 40);
            s.TooltipText = $"{ItemName(c.Item)} ×{c.Count} per unit ({c.Count * t.Units} total)";
            cost.AddChild(s);
        }

        _techDetail.AddChild(cost);
        if ((units > 0) && (done == false))
        {
            ProgressBar bar = HudStyle.Bar(320);
            HudStyle.Tint(bar, HudStyle.Orange);
            bar.Value = units / (double)System.Math.Max(1, t.Units);
            _techDetail.AddChild(bar);
            _techDetail.AddChild(HudStyle.Caption($"{units} / {t.Units} units done", HudStyle.TextDim, 11));
        }

        if (t.Unlocks.Length > 0)
        {
            _techDetail.AddChild(HudStyle.Title("Unlocks"));
            HBoxContainer unlocks = new HBoxContainer();
            foreach (string u in t.Unlocks)
            {
                Slot s = new Slot(40);
                if (C.Buildings.TryGetValue(u, out BuildingDef? b))
                {
                    s.Icon = (ci, r) => Icons.Building(ci, r, b);
                    s.TooltipText = $"{b.Name}\n{b.Description}";
                }
                else if (C.Recipes.TryGetValue(u, out RecipeDef? rec))
                {
                    s.Icon = RecipeIcon(rec);
                    s.TooltipText = $"Recipe: {rec.Name}";
                }

                unlocks.AddChild(s);
            }

            _techDetail.AddChild(unlocks);
        }

        _techDetail.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        if (available && (current == false))
        {
            _techDetail.AddChild(HudStyle.Button("Start research", () => Root.Send(new SetResearch(id))));
        }
        else if (current)
        {
            _techDetail.AddChild(HudStyle.Caption("Labs are working on this.", HudStyle.Orange, 12));
        }
    }
}
