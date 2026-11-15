using Factory.Game.Ui;
using Godot;

namespace Factory.Game;

/// <summary>The help window: the goal, then controls and how the colony works, in two columns with key caps.</summary>
public partial class Hud
{
    private static readonly (string Title, (string Keys, string What)[] Rows)[] _helpLeft =
    [
        ("Moving and mining",
        [
            ("WASD", "Walk. The planet wraps in every direction."),
            ("RMB", "On ore or trees: walk over and mine, through the whole deposit."),
            ("RMB drag", "Sweep across ore to pick tiles as you go."),
            ("F", "Unload your pockets into the nearest lander or crate."),
            ("Wheel", "Zoom (also PgUp / PgDn)."),
        ]),
        ("Building",
        [
            ("1 – 7", "Open a build category, then 1 – 0 to pick a building."),
            ("LMB", "Place. Drag to lay rail."),
            ("R", "Rotate. Arrows on drills, stops and sorters show their front."),
            ("X", "Dismantle tool."),
            ("Q", "Pipette: pick the building under the cursor to build another (or clear the tool)."),
            ("Esc", "Step back: tool, category or window, selection, then the game menu."),
        ]),
        ("Windows",
        [
            ("E / I / Tab", "Pockets and module slots, with the replicator and colony storage beside them."),
            ("C", "Replicator tab: queue crafts on the lander."),
            ("LMB", "Click a machine, crate or helper bot to open it beside your pockets. Hover for quick info."),
            ("Drag", "Move stacks.  Shift-click: a stack.  Ctrl/Cmd-click: all.  RMB: half."),
            ("T", "Technology tree."),
            ("M", "System map: every planet, its readiness and resources. View one from there."),
            ("H", "This help."),
            ("Space", "Pause.   + / –  game speed.   F5 / F9  save / load."),
        ]),
    ];

    private static readonly (string Title, string Text)[] _helpRight =
    [
        ("Goal", "You are a robot sent ahead of human settlers. Terraform the system: raise every planet's Atmosphere, Hydrosphere and Biosphere until it is ready, then call the settlers from a Settler Uplink."),
        ("Building", "You build every ghost within reach by yourself, using materials from your pockets or a lander or crate nearby. Helper bots with a Construction Module build anywhere."),
        ("Helper bots", "Give each helper bot a job in the left panel: Gather a resource or Haul between two buildings. Modules teach them more: Build, Operate train. Click a bot to fit modules."),
        ("Research", "Craft Survey Cores and feed them to a Research Station. Analytical Engines unlocks Analysis Cores and the Combustion Age: electric machines, railways, robotics and terraforming."),
        ("Upgrades", "Research unlocks modules. Craft them, then drag them into your own slots (E) or a helper bot's. Later, a Robot Assembly Bay builds new helper bots."),
        ("Your pockets", "While you're nearby, whatever you carry supplies construction sites and the lander's queued crafts, so you can build straight from what you mined."),
        ("Power", "Steam engines must touch water and burn biomass or compacted carbon. Power poles carry power to machines around them; batteries store the surplus. In the Combustion Age, Kerogen Generators need no water and waste less fuel, and Lattice Pylons carry power across the planet. In the Clean Age, Solar Panels (stronger nearer the sun) and Wind Turbines (stronger in a thick atmosphere) burn nothing."),
        ("Other planets", "Refine crude kerogen (from fen shale) into fuel. Research Orbital Ferry, build a Launch Pad and load it with a Ferry Rocket, 20 Kerogen Fuel and any cargo, then pick a planet and launch. The landed ferry can always fly you home. Synthesis Cores need silica sand from Dune. A Payload Launcher fires catalyst packs, hydro cartridges and seed pods at a target planet to terraform it from home; a Hover Module lets a robot float over water."),
        ("Logistics", "In the Clean Age, belts carry items four to a tile: drag to lay a run, and use Sorter Arms to load and unload them (a drill facing a belt drops its ore on it). Logistics Stations each supply or demand a few items; their drones fly supplies across the planet. Orbital Stations' vessels fly them between planets."),
        ("Stellar Age", "An Orbital Collector siphons helium-3 from Vesper or mines osmiridium in the Shards. With titanium and hafnium from Cinder they make Stellar Cores, which research plasma smelters and drills, fusion reactors, the Space Elevator (free launches, faster collectors and vessels), Climate Engines (fast terraforming) and Vein Utilization, which you can research forever."),
        ("Trains", "Lay track, place a Rail Stop facing it, then a train. Select the train to set its schedule. Hand carts need a helper bot to pump them; steam locomotives need water (a Water Tower near a stop) and fuel (compacted carbon in the stop)."),
    ];

    private PanelContainer BuildHelp()
    {
        PanelContainer win = MakeWindow(HudWindow.Help, "How to play", "H", out VBoxContainer body);
        HBoxContainer cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 24);
        body.AddChild(cols);

        VBoxContainer left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", 4);
        foreach ((string title, (string Keys, string What)[] rows) in _helpLeft)
        {
            left.AddChild(HudStyle.Title(title));
            foreach ((string keys, string what) in rows)
            {
                HBoxContainer row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 10);
                Label key = HudStyle.Key(keys);
                key.CustomMinimumSize = new Vector2(76, 0);
                row.AddChild(key);
                row.AddChild(HudStyle.Caption(what, null, 12));
                left.AddChild(row);
            }

            left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }

        cols.AddChild(left);
        cols.AddChild(new ColorRect { Color = HudStyle.Line, CustomMinimumSize = new Vector2(1, 0) });

        VBoxContainer right = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        right.AddThemeConstantOverride("separation", 4);
        foreach ((string title, string text) in _helpRight)
        {
            right.AddChild(HudStyle.Title(title));
            Label l = HudStyle.Caption(text, title == "Goal" ? HudStyle.Orange : HudStyle.Text, 12);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(360, 0);
            right.AddChild(l);
            right.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }

        cols.AddChild(right);

        return win;
    }
}
