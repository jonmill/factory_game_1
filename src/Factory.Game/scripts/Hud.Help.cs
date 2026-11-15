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
            ("1 – 6", "Open a build category, then 1 – 0 to pick a building."),
            ("LMB", "Place. Drag to lay rail."),
            ("R", "Rotate. Arrows on drills, stops and sorters show their front."),
            ("X", "Dismantle tool."),
            ("Q", "Pipette: pick the building under the cursor to build another (or clear the tool)."),
            ("Esc", "Step back: tool, category or window, selection, then the game menu."),
        ]),
        ("Windows",
        [
            ("E / I / Tab", "Pockets, with the replicator and colony storage beside them."),
            ("C", "Replicator tab: queue crafts on the lander."),
            ("LMB", "Click a machine or crate to open it beside your pockets. Hover for quick info."),
            ("Drag", "Move stacks.  Shift-click: a stack.  Ctrl/Cmd-click: all.  RMB: half."),
            ("T", "Technology tree."),
            ("H", "This help."),
            ("Space", "Pause.   + / –  game speed.   F5 / F9  save / load."),
        ]),
    ];

    private static readonly (string Title, string Text)[] _helpRight =
    [
        ("Goal", "Research the Colony Beacon, build it, and call your first new follower."),
        ("Followers", "Give each follower a job in the left panel: Build, Gather a resource, Haul between two buildings, or Operate a train. Builders construct every ghost you place, fetching materials from the lander and crates."),
        ("Your pockets", "While you're nearby, whatever you carry supplies construction sites and the lander's queued crafts, so you can build straight from what you mined."),
        ("Power", "Steam engines must touch water and burn wood or coal. Copper poles carry power to machines around them; batteries store the surplus."),
        ("Trains", "Lay track, place a Rail Stop facing it, then a train. Select the train to set its schedule. Hand carts need a follower to pump them; steam locomotives need water (a Water Tower near a stop) and fuel (coal in the stop)."),
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
