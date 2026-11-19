using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>Status: a compact planet strip top-left; the minimap with research and power under it top-right.</summary>
public partial class Hud
{
    private PanelContainer _planetCard = null!, _researchCard = null!;
    private Label _planetName = null!, _clock = null!;
    private Label _researchName = null!, _researchUnits = null!;
    private ProgressBar _researchBar = null!;
    private Label _powerText = null!, _batteryText = null!;
    private ProgressBar _powerBar = null!, _batteryBar = null!;

    private MapView _minimap = null!;
    private VBoxContainer _planetDetails = null!;
    private Label _powerIcon = null!;

    /// <summary>Edge of the minimap in HUD units.</summary>
    private const float MinimapSize = 176;

    private void BuildStatus()
    {
        // Top-left: one compact strip (planet, clock, readiness); click it for the terraforming details.
        _planetCard = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, TooltipText = "Click for terraforming details" };
        _planetCard.AddThemeStyleboxOverride("panel", HudStyle.Box(HudStyle.Bg, HudStyle.Line, 2, 8, 5));
        _ui.AddChild(_planetCard);
        _planetCard.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _planetDetails.Visible = _planetDetails.Visible == false;
            }
        };
        VBoxContainer pv = new VBoxContainer();
        pv.AddThemeConstantOverride("separation", 1);
        _planetCard.AddChild(pv);
        HBoxContainer top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 8);
        _planetName = HudStyle.Caption("", HudStyle.Header, 13);
        _clock = HudStyle.Caption("", HudStyle.TextDim, 11);
        _clock.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        top.AddChild(_planetName);
        top.AddChild(_clock);
        pv.AddChild(top);
        _planetDetails = new VBoxContainer { Visible = false };
        _planetDetails.AddThemeConstantOverride("separation", 0);
        BuildTerraform(pv, _planetDetails);
        pv.AddChild(_planetDetails);

        // Top-right, as in Factorio: the minimap, with research and power as slim lines under it.
        _researchCard = new PanelContainer();
        _researchCard.AddThemeStyleboxOverride("panel", HudStyle.Box(HudStyle.Bg, HudStyle.Line, 2, 5, 5));
        _ui.AddChild(_researchCard);
        VBoxContainer rv = new VBoxContainer();
        rv.AddThemeConstantOverride("separation", 3);
        _researchCard.AddChild(rv);
        _minimap = new MapView { Root = Root, TilesAcross = 72, CustomMinimumSize = new Vector2(MinimapSize, MinimapSize), TooltipText = "Minimap  ·  click to open the map" };
        _minimap.Clicked += () => Toggle(HudWindow.Map);
        rv.AddChild(_minimap);

        VBoxContainer research = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop, TooltipText = "Research: click to open the tech tree (T)" };
        research.AddThemeConstantOverride("separation", 1);
        research.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Toggle(HudWindow.Tech);
            }
        };
        HBoxContainer rh = new HBoxContainer();
        _researchName = HudStyle.Caption("", null, 11);
        _researchName.ClipText = true;
        _researchName.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _researchName.CustomMinimumSize = new Vector2(100, 0);
        _researchName.MouseFilter = Control.MouseFilterEnum.Pass;
        _researchUnits = HudStyle.Caption("", HudStyle.TextDim, 10);
        _researchUnits.MouseFilter = Control.MouseFilterEnum.Pass;
        rh.AddChild(_researchName);
        rh.AddChild(_researchUnits);
        research.AddChild(rh);
        _researchBar = HudStyle.Bar(MinimapSize, 3);
        HudStyle.Tint(_researchBar, HudStyle.Orange);
        research.AddChild(_researchBar);
        rv.AddChild(research);

        VBoxContainer power = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        power.AddThemeConstantOverride("separation", 1);
        HBoxContainer ph = new HBoxContainer();
        _powerIcon = HudStyle.Caption("⚡", HudStyle.Header, 11);
        _powerText = HudStyle.Caption("", null, 11);
        _powerText.MouseFilter = Control.MouseFilterEnum.Pass;
        _batteryText = HudStyle.Caption("", HudStyle.TextDim, 10);
        _batteryText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _batteryText.HorizontalAlignment = HorizontalAlignment.Right;
        ph.AddChild(_powerIcon);
        ph.AddChild(_powerText);
        ph.AddChild(_batteryText);
        power.AddChild(ph);
        _powerBar = HudStyle.Bar(MinimapSize, 3);
        _batteryBar = HudStyle.Bar(MinimapSize, 2);
        HudStyle.Tint(_batteryBar, new Color(0.75f, 1f, 0.35f));
        power.AddChild(_powerBar);
        power.AddChild(_batteryBar);
        rv.AddChild(power);
        _powerTip = power;
    }

    private Control _powerTip = null!;

    private void UpdateStatus()
    {
        long secs = W.Tick / World.TicksPerSecond;
        _planetName.Text = P.Name.ToUpperInvariant();
        UpdateTerraform();
        _clock.Text = $"{secs / 3600}:{secs / 60 % 60:00}:{secs % 60:00}  {(Root.Paused ? "PAUSED" : $"×{Root.Speed}")}";

        if (W.Research.Current is { } id)
        {
            TechDef tech = C.Techs[id];
            _researchName.Text = tech.Name;
            _researchName.AddThemeColorOverride("font_color", HudStyle.Orange);
            _researchBar.Value = W.Research.UnitsDone / (double)System.Math.Max(1, tech.Units);
            int labs = P.Buildings.Count(b => (b.Def.Kind == BuildingKind.Lab) && (b.Status == MachineStatus.Working));
            _researchUnits.Text = $"{W.Research.UnitsDone}/{W.Research.UnitsFor(tech)}";
            _researchName.TooltipText = $"{tech.Name}: {W.Research.UnitsDone} of {W.Research.UnitsFor(tech)} units  ·  {labs} lab{(labs == 1 ? "" : "s")} working";
        }
        else
        {
            _researchName.Text = "No research  ·  T to pick";
            _researchName.AddThemeColorOverride("font_color", HudStyle.TextDim);
            _researchBar.Value = 0;
            _researchUnits.Text = "";
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
            _powerTip.TooltipText = "Power: build a steam engine and poles";
        }
        else
        {
            _powerText.Text = $"{Kw(supply)} / {Kw(cap)} kW";
            _powerTip.TooltipText = $"Power: {Kw(supply)} of {Kw(demand)} kW wanted is supplied  ·  {Kw(cap)} kW capacity";
            double sat = demand == 0 ? 1 : supply / (double)demand;
            _powerBar.Value = cap == 0 ? 0 : demand / (double)cap;
            HudStyle.Tint(_powerBar, sat >= 0.999 ? HudStyle.Good : sat > 0.5 ? HudStyle.Orange : HudStyle.Bad);
        }

        bool hasBattery = storeCap > 0;
        _batteryText.Visible = hasBattery;
        _batteryBar.Visible = hasBattery;
        if (hasBattery)
        {
            _batteryText.Text = $"🔋 {stored * 100 / storeCap}%";
            _batteryBar.Value = stored / (double)storeCap;
        }
    }
}
