using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>Status cards: the planet, clock and terraforming top-left, research and power top-right.</summary>
public partial class Hud
{
    private PanelContainer _planetCard = null!, _researchCard = null!, _powerCard = null!;
    private Label _planetName = null!, _clock = null!;
    private Label _researchName = null!, _researchUnits = null!;
    private ProgressBar _researchBar = null!;
    private Label _powerText = null!, _batteryText = null!;
    private ProgressBar _powerBar = null!, _batteryBar = null!;

    private void BuildStatus()
    {
        _planetCard = new PanelContainer();
        _ui.AddChild(_planetCard);
        VBoxContainer pv = new VBoxContainer();
        pv.AddThemeConstantOverride("separation", 0);
        _planetCard.AddChild(pv);
        _planetName = HudStyle.Caption("", HudStyle.Header, 16);
        _clock = HudStyle.Caption("", HudStyle.TextDim, 12);
        pv.AddChild(_planetName);
        pv.AddChild(_clock);
        BuildTerraform(pv);

        _researchCard = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, TooltipText = "Open the tech tree (T)" };
        _ui.AddChild(_researchCard);
        _researchCard.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Toggle(HudWindow.Tech);
            }
        };
        VBoxContainer rv = new VBoxContainer();
        rv.AddThemeConstantOverride("separation", 3);
        _researchCard.AddChild(rv);
        HBoxContainer rh = new HBoxContainer();
        rh.AddChild(HudStyle.Title("Research"));
        rh.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        rh.AddChild(HudStyle.Key("T"));
        rv.AddChild(rh);
        _researchName = HudStyle.Caption("");
        _researchBar = HudStyle.Bar(240);
        HudStyle.Tint(_researchBar, HudStyle.Orange);
        _researchUnits = HudStyle.Caption("", HudStyle.TextDim, 11);
        rv.AddChild(_researchName);
        rv.AddChild(_researchBar);
        rv.AddChild(_researchUnits);

        _powerCard = new PanelContainer();
        _ui.AddChild(_powerCard);
        VBoxContainer ev = new VBoxContainer();
        ev.AddThemeConstantOverride("separation", 3);
        _powerCard.AddChild(ev);
        ev.AddChild(HudStyle.Title("Power"));
        _powerText = HudStyle.Caption("", null, 12);
        _powerBar = HudStyle.Bar(240);
        _batteryText = HudStyle.Caption("", HudStyle.TextDim, 11);
        _batteryBar = HudStyle.Bar(240, 4);
        HudStyle.Tint(_batteryBar, new Color(0.75f, 1f, 0.35f));
        ev.AddChild(_powerText);
        ev.AddChild(_powerBar);
        ev.AddChild(_batteryText);
        ev.AddChild(_batteryBar);
    }

    private void UpdateStatus()
    {
        long secs = W.Tick / World.TicksPerSecond;
        int followers = P.Units.Count(u => u.IsLeader == false);
        _planetName.Text = P.Name.ToUpperInvariant();
        UpdateTerraform();
        _clock.Text = $"{secs / 3600}:{secs / 60 % 60:00}:{secs % 60:00}   {(Root.Paused ? "PAUSED" : $"×{Root.Speed}")}   ·   {followers} helper bot{(followers == 1 ? "" : "s")}" +
                      (W.HelpersBuilt > 0 ? $" (+{W.HelpersBuilt} built)" : "");

        if (W.Research.Current is { } id)
        {
            TechDef tech = C.Techs[id];
            _researchName.Text = tech.Name;
            _researchName.AddThemeColorOverride("font_color", HudStyle.Orange);
            _researchBar.Value = W.Research.UnitsDone / (double)System.Math.Max(1, tech.Units);
            int labs = P.Buildings.Count(b => (b.Def.Kind == BuildingKind.Lab) && (b.Status == MachineStatus.Working));
            _researchUnits.Text = $"{W.Research.UnitsDone} / {tech.Units} units   ·   {labs} lab{(labs == 1 ? "" : "s")} working";
        }
        else
        {
            _researchName.Text = "Nothing being researched";
            _researchName.AddThemeColorOverride("font_color", HudStyle.TextDim);
            _researchBar.Value = 0;
            _researchUnits.Text = "Click to pick a technology";
        }

        long demand = 0, supply = 0, cap = 0, stored = 0, storeCap = 0;
        foreach (PowerNetwork n in P.Networks.Values)
        {
            demand += n.DemandJ;
            supply += n.SuppliedJ;
            cap += n.GenCapacityJ;
            stored += n.StoredJ;
            storeCap += n.StorageCapacityJ;
        }

        static long Kw(long jPerTick) => jPerTick * World.TicksPerSecond / 1000;
        if (P.Networks.Count == 0)
        {
            _powerText.Text = "No grid yet";
            _powerBar.Value = 0;
        }
        else
        {
            _powerText.Text = $"{Kw(supply)} / {Kw(demand)} kW used   ·   {Kw(cap)} kW capacity";
            double sat = demand == 0 ? 1 : supply / (double)demand;
            _powerBar.Value = cap == 0 ? 0 : demand / (double)cap;
            HudStyle.Tint(_powerBar, sat >= 0.999 ? HudStyle.Good : sat > 0.5 ? HudStyle.Orange : HudStyle.Bad);
        }

        bool hasBattery = storeCap > 0;
        _batteryText.Visible = hasBattery;
        _batteryBar.Visible = hasBattery;
        if (hasBattery)
        {
            _batteryText.Text = $"Batteries {stored * 100 / storeCap}%";
            _batteryBar.Value = stored / (double)storeCap;
        }
    }
}
