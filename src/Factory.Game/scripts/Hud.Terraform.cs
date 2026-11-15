using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Terraforming on the planet card: the system-wide settler readiness meter, then this planet's stats
/// (atmosphere, hydrosphere, biosphere) as bars. Every planet has to reach the bar before a Settler
/// Uplink can call the settlers; once it has, a window announces the win.
/// </summary>
public partial class Hud
{
    private ProgressBar _readinessBar = null!;
    private Label _readinessText = null!;
    private readonly List<(PlanetStatDef Stat, ProgressBar Bar, Label Value)> _statRows = [];
    private bool _victoryShown;

    private void BuildTerraform(VBoxContainer into)
    {
        into.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        HBoxContainer head = new HBoxContainer();
        head.AddChild(HudStyle.Title("Settler readiness", 11));
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _readinessText = HudStyle.Caption("", HudStyle.Text, 11);
        head.AddChild(_readinessText);
        into.AddChild(head);
        _readinessBar = HudStyle.Bar(250, 7);
        HudStyle.Tint(_readinessBar, new Color(1f, 0.85f, 0.4f));
        into.AddChild(_readinessBar);
        foreach (PlanetStatDef stat in C.Terraform.Stats)
        {
            HBoxContainer row = new HBoxContainer { TooltipText = $"{stat.Name}: {stat.Description}", MouseFilter = Control.MouseFilterEnum.Stop };
            row.AddThemeConstantOverride("separation", 6);
            Label name = HudStyle.Caption(stat.Name, HudStyle.TextDim, 11);
            name.CustomMinimumSize = new Vector2(78, 0);
            name.MouseFilter = Control.MouseFilterEnum.Pass;
            ProgressBar bar = HudStyle.Bar(130, 5);
            bar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            bar.MouseFilter = Control.MouseFilterEnum.Pass;
            HudStyle.Tint(bar, WorldView.Hex(stat.Color));
            Label value = HudStyle.Caption("", HudStyle.TextDim, 11);
            value.MouseFilter = Control.MouseFilterEnum.Pass;
            row.AddChild(name);
            row.AddChild(bar);
            row.AddChild(value);
            into.AddChild(row);
            _statRows.Add((stat, bar, value));
        }
    }

    private void UpdateTerraform()
    {
        int system = W.SystemReadiness;
        int bar = C.Terraform.ReadyBar;
        List<Planet> targets = W.TerraformablePlanets.ToList();
        int ready = W.ReadyPlanetCount;
        int need = W.PlanetsNeeded;
        _readinessBar.Value = system / 100.0;
        _readinessText.Text = $"{system}%   ·   {ready}/{need} planet{(need == 1 ? "" : "s")} ready";
        _readinessBar.TooltipText = $"Bring any {need} of the {targets.Count} terraformable planets to {bar}% readiness, then call the settlers from a Settler Uplink. " +
                                    $"The meter averages your {need} most terraformed planets.\n" +
                                    string.Join("\n", targets.Select(p => $"{p.Name}: {p.Readiness}%"));
        foreach ((PlanetStatDef stat, ProgressBar b, Label value) in _statRows)
        {
            int pct = P.StatPercent(stat);
            b.Value = pct / 100.0;
            value.Text = $"{pct}%";
            value.AddThemeColorOverride("font_color", pct >= 100 ? HudStyle.Good : HudStyle.TextDim);
        }

        if (W.SettlersCalled && (_victoryShown == false))
        {
            _victoryShown = true;
            ShowOnly(HudWindow.Victory);
        }
    }

    private PanelContainer BuildVictory()
    {
        PanelContainer win = MakeWindow(HudWindow.Victory, "The settlers are coming", null, out VBoxContainer body, out _);
        Label text = HudStyle.Caption(
            "Your signal has reached the settler fleet. Every planet in the system can now hold air, water and life, " +
            "and the people who sent you are on their way.\n\nThank you, Commander. You can keep building as long as you like.",
            HudStyle.Text, 14);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = new Vector2(460, 0);
        body.AddChild(text);
        body.AddChild(HudStyle.Button("Keep playing", () => ShowOnly(HudWindow.None)));

        return win;
    }
}
