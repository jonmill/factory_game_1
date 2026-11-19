using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// Terraforming on the planet strip: the system-wide settler readiness meter, and in its details this
/// planet's stats (atmosphere, hydrosphere, biosphere) as bars. Every planet has to reach the bar before a Settler
/// Uplink can call the settlers; once it has, a window announces the win.
/// </summary>
public partial class Hud
{
    private ProgressBar _readinessBar = null!;
    private Label _readinessText = null!;
    private readonly List<(PlanetStatDef Stat, ProgressBar Bar, Label Value)> _statRows = [];
    private bool _victoryShown;

    private void BuildTerraform(VBoxContainer into, VBoxContainer details)
    {
        // The readiness meter always shows; this planet's stats are in the details the strip opens.
        HBoxContainer head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 6);
        Label title = HudStyle.Caption("Settlers", HudStyle.TextDim, 10);
        _readinessBar = HudStyle.Bar(120, 4);
        _readinessBar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _readinessBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        HudStyle.Tint(_readinessBar, new Color(1f, 0.85f, 0.4f));
        _readinessText = HudStyle.Caption("", HudStyle.Text, 10);
        head.AddChild(title);
        head.AddChild(_readinessBar);
        head.AddChild(_readinessText);
        into.AddChild(head);
        into = details;
        into.AddChild(new Control { CustomMinimumSize = new Vector2(0, 3) });
        foreach (PlanetStatDef stat in C.Terraform.Stats)
        {
            HBoxContainer row = new HBoxContainer { TooltipText = $"{stat.Name}: {stat.Description}", MouseFilter = Control.MouseFilterEnum.Stop };
            row.AddThemeConstantOverride("separation", 6);
            Label name = HudStyle.Caption(stat.Name, HudStyle.TextDim, 10);
            name.CustomMinimumSize = new Vector2(72, 0);
            name.MouseFilter = Control.MouseFilterEnum.Pass;
            ProgressBar bar = HudStyle.Bar(90, 4);
            bar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            bar.MouseFilter = Control.MouseFilterEnum.Pass;
            HudStyle.Tint(bar, WorldView.Hex(stat.Color));
            Label value = HudStyle.Caption("", HudStyle.TextDim, 10);
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
        _readinessText.Text = $"{system}%  {ready}/{need}";
        _planetCard.TooltipText = "Settler readiness (click for this planet's details)\n" + $"Bring any {need} of the {targets.Count} terraformable planets to {bar}% readiness, then call the settlers from a Settler Uplink. " +
                                    $"The meter averages your {need} most terraformed planets.\n" +
                                    string.Join("\n", targets.Select(p => $"{p.Name}: {p.Readiness}%"));
        foreach ((PlanetStatDef stat, ProgressBar b, Label value) in _statRows)
        {
            int pct = P.StatPercent(stat);
            double exact = stat.Target <= 0 ? 100 : System.Math.Min(100.0, P.Stats.GetValueOrDefault(stat.Id) * 100.0 / stat.Target);
            b.Value = exact / 100.0;
            value.Text = $"{exact:0.00}%";
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
