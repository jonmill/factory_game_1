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
            ("M", "Map of this planet: everything explored. Drag to pan, wheel to zoom. Clicking the minimap opens it too."),
            ("P", "Planetary map of the star system: every planet, its readiness and resources. View one from there."),
            ("H", "This help."),
            ("G", "Show or hide the guide: your next objective, and tips."),
            ("Space", "Pause.   + / –  game speed."),
            ("F5 / F9", "Quicksave / quickload (the \"Quicksave\" slot). Esc, then Save game or Load game, for named saves."),
        ]),
    ];

    private static readonly (string Title, string Text)[] _helpRight =
    [
        ("Goal", "You are a robot sent ahead of human settlers. Terraform the system: raise the Atmosphere, Hydrosphere and Biosphere of any three of the four terraformable planets until they are ready, then call the settlers from a Settler Uplink (a Stellar Age tech). Terraformers turn catalysts, hydro cartridges and seed pods into progress: build more of them and feed them faster to go faster, and Payload Launchers (up to four a planet) fire the same consumables at other planets. Climate Engines (Stellar Age) get three times as much from each consumable, four times as fast."),
        ("Screen", "Top left: the planet and settler readiness (click for details) and your helper bots. Top right: the minimap (click it or press M for the full map, with how much ore is left in each deposit), research and power, then the guide and info on whatever you point at. Bottom: your commander, the build categories (1-7) and shortcuts to every window."),
        ("Getting started", "The lander lands empty. Set your helper bots to gather (regolith builds the first smelter, biomass is fuel), mine alongside them, then let a helper Haul… for the smelter and put Burner Drills on ore so the base runs itself."),
        ("Building", "You build every ghost within reach by yourself, using materials from your pockets or a lander or crate nearby. Helper bots with a Construction Module build anywhere."),
        ("Exploring", "Only the ground around the lander has been seen. You and your robots uncover the map as you move, and buildings see around them. Nothing can be built or mined in unexplored ground and helper bots only gather deposits that have been found, so set a helper bot to Explore: it walks an outward spiral from the lander."),
        ("Helper bots", "Give each helper bot a job with its job button in the list on the left: Gather a resource, Explore, or Haul between two buildings. Modules teach them more: Build, Operate train. Click a bot to fit modules."),
        ("Research", "Craft Survey Cores and feed them to a Research Station. Every age's research costs one of each core so far, and each core sits on a deeper chain: Survey (bar and cog), Analysis (around a Torque Actuator), Synthesis (around a Photonic Processor) and Stellar (a Phase Processor, Gyro Rotor and Keel Plating). Analytical Engines unlocks Analysis Cores and the Combustion Age: electric machines, railways and robotics. Synthesis Cores open the Clean Age and terraforming: the Atmosphere Processor, Hydrosphere Seeder and Bio Incubator."),
        ("Upgrades", "Research unlocks modules. Craft them, then drag them into your own slots (E) or a helper bot's. Later, a Robot Assembly Bay builds new helper bots."),
        ("Your pockets", "While you're nearby, whatever you carry supplies construction sites and the lander's queued crafts, so you can build straight from what you mined."),
        ("Power", "Steam engines must touch water and burn biomass or compacted carbon. Power poles carry power to machines around them; batteries store the surplus. In the Combustion Age, Kerogen Generators need no water and waste less fuel, and Lattice Pylons carry power across the planet. In the Clean Age, Solar Panels (stronger nearer the sun) and Wind Turbines (stronger in a thick atmosphere) burn nothing."),
        ("Other planets", "Refine crude kerogen (from fen shale) into fuel. Research Orbital Ferry, build a Launch Pad and load it with a Ferry Rocket, 20 Kerogen Fuel and any cargo, then pick a planet and launch. The landed ferry can always fly you home. Synthesis Cores need silica sand from Dune. A Hover Module lets a robot float over water."),
        ("Logistics", "From the late Combustion Age, belts carry items four to a tile: drag to lay a run, and use Sorter Arms to load and unload them (a drill facing a belt drops its ore on it). Give each ingredient its own belt: one belt carrying several jams as soon as one runs ahead. A Long Sorter Arm reaches two tiles, so it takes from the second belt over the first. Click a crate to limit its slots or make it take only one item, so a crate feeding a belt holds a stack of its ingredient and nothing else. Logistics Stations each supply or demand a few items; their drones fly supplies across the planet. Orbital Stations' vessels fly them between planets."),
        ("Endless research", "Upgrades come in levels, as in DSP: the first few are ordinary techs in the early ages (Vein Utilization I-III, Robot Efficiency I-III, Research Speed I-II, Terraforming Efficiency I-II), then each one repeats forever in the Stellar Age, costing more every level. Vein Utilization makes deposits last 10% longer per level, Robot Efficiency makes every robot 15% faster, Research Speed makes stations a fifth faster and Terraforming Efficiency makes every consumable terraform a fifth more. Run them whenever your stations would otherwise wait for cores."),
        ("Stellar Age", "An Orbital Collector siphons helium-3 from Vesper or mines osmiridium in the Shards. With Phase Processors (lattice, tantalum and brimstone etchant), Gyro Rotors (osmiridium) and Keel Plating (Cinder's titanium) they make Stellar Cores, which research plasma smelters and drills, fusion reactors, the Space Elevator (free launches, faster collectors and vessels), the Payload Launcher (fires catalyst packs, hydro cartridges and seed pods to terraform another planet from home), Climate Engines (fast terraforming) and the endless upgrades."),
        ("Trains", "Lay track, place a Rail Stop facing it, then a train. Select the train to set its schedule. Hand carts need a helper bot to pump them; steam locomotives need water (a Water Tower near a stop) and fuel (compacted carbon in the stop)."),
        ("Saving", "Esc opens the game menu: Save game names a save (or pick one in the list to save over it), Load game lists every save with when it was made, time played and the planet. F5 quicksaves and F9 loads the quicksave. The game also autosaves every 10 minutes of play, taking turns between three Autosave slots. Saves are kept in the game's user folder, in \"saves\" (the full path is at the bottom of the Save window). Loading is quick however long you've played."),
    ];

    private ScrollContainer _helpScroll = null!;
    private Control _helpCols = null!;

    /// <summary>Gives the help as much height as the window has room for; the rest scrolls.</summary>
    /// <param name="room">Space available for a centred window, in HUD units.</param>
    private void FitHelp(Vector2 room)
    {
        if (_windows[HudWindow.Help].Visible == false)
        {
            return;
        }

        Vector2 content = _helpCols.GetCombinedMinimumSize();
        _helpScroll.CustomMinimumSize = new Vector2(content.X, 0);
        float chrome = _windows[HudWindow.Help].GetCombinedMinimumSize().Y;
        float height = Mathf.Min(content.Y, Mathf.Max(160, room.Y - chrome));
        // Leave room for the scroll bar when there is one.
        float bar = height < content.Y ? 14 : 0;
        _helpScroll.CustomMinimumSize = new Vector2(content.X + bar, height);
    }

    private PanelContainer BuildHelp()
    {
        PanelContainer win = MakeWindow(HudWindow.Help, "How to play", "H", out VBoxContainer body);
        _helpScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(_helpScroll);
        HBoxContainer cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 24);
        _helpCols = cols;
        _helpScroll.AddChild(cols);

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
