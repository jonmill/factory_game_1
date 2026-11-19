using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The star system window (P): every body in the system, nearest the star first, with its type, what
/// it holds, and how ready it is for settlers. Surface planets can be viewed from orbit.
/// </summary>
public partial class Hud
{
    private readonly List<(Planet Planet, Label Status, Button View)> _systemRows = [];

    private PanelContainer BuildSystem()
    {
        PanelContainer win = MakeWindow(HudWindow.System, "Star system", "P", out VBoxContainer body);
        body.AddChild(HudStyle.Caption("Any three of the four terraformable planets must reach the readiness bar before the settlers can be called.", HudStyle.TextDim, 11));
        GridContainer grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        body.AddChild(grid);
        foreach (PlanetDef def in C.PlanetList.OrderBy(d => d.Orbit))
        {
            grid.AddChild(PlanetCard(def));
        }

        return win;
    }

    private PanelContainer PlanetCard(PlanetDef def)
    {
        PanelContainer card = new PanelContainer { CustomMinimumSize = new Vector2(330, 0) };
        card.AddThemeStyleboxOverride("panel", HudStyle.Box(HudStyle.Well, HudStyle.Line, 3, 10, 8));
        HBoxContainer row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        card.AddChild(row);
        row.AddChild(new PlanetDisc { Def = def, Content = C, CustomMinimumSize = new Vector2(64, 64), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin });

        VBoxContainer text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 2);
        row.AddChild(text);
        text.AddChild(HudStyle.Caption(def.Name, HudStyle.Header, 15));
        text.AddChild(HudStyle.Caption($"{def.Type}  ·  orbit {def.Orbit / 10.0:0.0}", HudStyle.TextDim, 11));
        Label desc = HudStyle.Caption(def.Description, HudStyle.Text, 11);
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.CustomMinimumSize = new Vector2(240, 0);
        text.AddChild(desc);

        IEnumerable<string> resources = def.Resources.Select(r => r.Resource).Concat(def.Orbital).Distinct();
        HBoxContainer icons = new HBoxContainer();
        icons.AddThemeConstantOverride("separation", 2);
        foreach (string id in resources)
        {
            ResourceDef res = C.Resources[id];
            Slot slot = new Slot(26) { Icon = (ci, r) => Icons.Item(ci, r, C.Items[res.Item]), TooltipText = $"{res.Name}\n{C.Items[res.Item].Name}" };
            icons.AddChild(slot);
        }

        text.AddChild(icons);
        if (def.Biomes.Length > 0)
        {
            text.AddChild(HudStyle.Caption("Biomes: " + string.Join(", ", def.Biomes.Select(b => C.Biomes[b.Biome].Name)), HudStyle.TextDim, 10));
        }

        if (W.Planets.FirstOrDefault(p => p.Def == def) is { } planet)
        {
            Label status = HudStyle.Caption("", HudStyle.Text, 11);
            text.AddChild(status);
            Button view = HudStyle.Button("View", () =>
            {
                Root.ViewPlanet(planet.Id);
                ShowOnly(HudWindow.None);
            }, "Look at this planet from orbit");
            view.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            text.AddChild(view);
            _systemRows.Add((planet, status, view));
        }
        else
        {
            text.AddChild(HudStyle.Caption("No surface: worked from orbit (Stellar Age)", HudStyle.TextDim, 11));
        }

        return card;
    }

    private void UpdateSystem()
    {
        foreach ((Planet planet, Label status, Button view) in _systemRows)
        {
            bool here = Root.Leader.Planet == planet;
            string target = planet.Def.Terraformable ? (planet.IsReady ? "Ready for settlers" : $"Terraformable · readiness {planet.Readiness}%") : "Can't be terraformed";
            status.Text = (here ? "Commander here  ·  " : "") + target;
            status.AddThemeColorOverride("font_color", planet.IsReady && planet.Def.Terraformable ? HudStyle.Good : here ? HudStyle.Orange : HudStyle.Text);
            view.Disabled = Root.PlanetId == planet.Id;
            view.Text = Root.PlanetId == planet.Id ? "Viewing" : "View";
        }
    }
}
