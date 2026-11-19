using Factory.Game.Ui;
using Godot;

namespace Factory.Game;

/// <summary>
/// The planet map, as Factorio's: opened by clicking the minimap, a big view of everything explored on
/// the planet you're looking at. Drag to pan, wheel to zoom, hover for what's there.
/// </summary>
public partial class Hud
{
    private MapView _mapView = null!;

    private PanelContainer BuildMap()
    {
        PanelContainer win = MakeWindow(HudWindow.Map, "Map", null, out VBoxContainer body, out _);
        _mapView = new MapView { Root = Root, FollowCamera = false, Interactive = true, RepaintSeconds = 0.5 };
        body.AddChild(_mapView);
        HBoxContainer foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", 8);
        foot.AddChild(HudStyle.Caption("Drag to pan  ·  wheel to zoom  ·  the white frame is your screen", HudStyle.TextDim, 11));
        foot.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        foot.AddChild(HudStyle.Button("Centre on view", CentreMap));
        foot.AddChild(HudStyle.Button("Whole planet", () =>
        {
            _mapView.Center = new Vector2(Root.Planet.Width, Root.Planet.Height) / 2;
            _mapView.TilesAcross = Root.Planet.Width;
        }));
        foot.AddChild(HudStyle.Button("Star system  [M]", () => Toggle(HudWindow.System)));
        body.AddChild(foot);

        return win;
    }

    /// <summary>Puts the map back on what the screen shows, zoomed to see the area round it.</summary>
    private void CentreMap()
    {
        _mapView.Center = Root.Cam;
        _mapView.TilesAcross = Mathf.Min(96, Root.Planet.Width);
        _mapView.Refresh();
    }

    /// <summary>The map fills most of the screen, whatever its size.</summary>
    private void FitMap(Vector2 room)
    {
        if (_windows[HudWindow.Map].Visible)
        {
            _mapView.CustomMinimumSize = new Vector2(Mathf.Max(200, room.X - 60), Mathf.Max(150, room.Y - 110)).Floor();
        }
    }
}
