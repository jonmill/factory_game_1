using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>A launch pad's controls in its machine window: where to fly, which helper bots come along, and Launch.</summary>
public partial class Hud
{
    private void BuildLaunchActions(Container into, Building pad)
    {
        List<Planet> targets = W.Planets.Where(p => p != pad.Planet).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        into.AddChild(HudStyle.Title("Flight", 11));
        HBoxContainer row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        OptionButton dest = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(180, 0) };
        foreach (Planet p in targets)
        {
            dest.AddItem($"{p.Name}  ({p.Def.Type})");
        }

        row.AddChild(dest);
        into.AddChild(row);

        List<(Unit Bot, CheckBox Box)> passengers = [];
        foreach (Unit u in pad.Planet.Units.Where(u => (u.IsLeader == false) && (u.OwnerPlayer == GameRoot.LocalPlayer)))
        {
            CheckBox box = new CheckBox { Text = $"Take {u.Name}", FocusMode = Control.FocusModeEnum.None };
            box.AddThemeFontSizeOverride("font_size", 12);
            into.AddChild(box);
            passengers.Add((u, box));
        }

        row.AddChild(HudStyle.Button("Launch", () =>
        {
            Planet to = targets[dest.Selected];
            Root.Send(new LaunchFerry(Root.Leader.Id, pad.Id, to.Id, passengers.Where(p => p.Box.ButtonPressed).Select(p => p.Bot.Id).ToArray()));
        }, "Fly the ferry, you, the ticked helper bots and the pad's cargo to the chosen planet."));
    }
}
