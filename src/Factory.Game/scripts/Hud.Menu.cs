using Factory.Game.Ui;
using Godot;

namespace Factory.Game;

/// <summary>
/// The game menu, opened with Esc once there is nothing else to close. The game pauses while it is
/// open and picks up where it was (paused or running) when it closes.
/// </summary>
public partial class Hud
{
    private bool _pausedBeforeMenu;

    private PanelContainer BuildMenu()
    {
        PanelContainer win = MakeWindow(HudWindow.Menu, "Paused", "Esc", out VBoxContainer body);
        body.CustomMinimumSize = new Vector2(280, 0);
        body.AddThemeConstantOverride("separation", 6);
        Button MenuButton(string text, System.Action onPress)
        {
            Button b = HudStyle.Button(text, onPress);
            b.CustomMinimumSize = new Vector2(0, 34);
            body.AddChild(b);

            return b;
        }

        MenuButton("Resume", () => ShowOnly(HudWindow.None));
        MenuButton("Save game", () => ShowOnly(HudWindow.Save));
        MenuButton("Load game", () => ShowOnly(HudWindow.Load));
        Label quick = HudStyle.Caption("F5 quicksave  ·  F9 quickload", HudStyle.TextDim, 11);
        quick.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(quick);
        MenuButton("Controls and how to play   [H]", () => ShowOnly(HudWindow.Help));
        body.AddChild(HudStyle.Divider());

        // Settings, until there is an options screen: the guide, the interface size and the volumes.
        Button hints = MenuButton("", () => { });
        void ShowHintsState() => hints.Text = GameSettings.ShowHints ? "Guide and tips: on   [G]" : "Guide and tips: off   [G]";
        hints.Pressed += () =>
        {
            SetHints(GameSettings.ShowHints == false);
            if (GameSettings.ShowHints)
            {
                GameSettings.ResetTips();
            }

            ShowHintsState();
        };
        ShowHintsState();

        HBoxContainer size = new HBoxContainer();
        size.AddThemeConstantOverride("separation", 6);
        Label sizeLabel = HudStyle.Caption("");
        sizeLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        void ShowSize() => sizeLabel.Text = $"Interface size   {Mathf.RoundToInt(GameSettings.UiScale * 100)}%";
        Button StepButton(string text, float step)
        {
            Button b = HudStyle.Button(text, () =>
            {
                GameSettings.UiScale += step;
                ShowSize();
            });
            b.CustomMinimumSize = new Vector2(34, 30);

            return b;
        }

        size.AddChild(sizeLabel);
        size.AddChild(StepButton("−", -0.1f));
        size.AddChild(StepButton("+", 0.1f));
        body.AddChild(size);
        ShowSize();

        // Sound: three volume rows, each a label and − / + steps of 10%.
        void VolumeRow(string name, System.Func<float> get, System.Action<float> set)
        {
            HBoxContainer row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            Label label = HudStyle.Caption("");
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            void Show() => label.Text = $"{name}   {Mathf.RoundToInt(get() * 100)}%";
            Button Step(string text, float step)
            {
                Button b = HudStyle.Button(text, () =>
                {
                    set(get() + step);
                    Audio.SoundPlayer.ApplyVolumes();
                    Show();
                });
                b.CustomMinimumSize = new Vector2(34, 30);

                return b;
            }

            row.AddChild(label);
            row.AddChild(Step("−", -0.1f));
            row.AddChild(Step("+", 0.1f));
            body.AddChild(row);
            Show();
        }

        VolumeRow("Volume", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        VolumeRow("Effects", () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);
        VolumeRow("Ambience", () => GameSettings.AmbientVolume, v => GameSettings.AmbientVolume = v);
        body.AddChild(HudStyle.Divider());
        MenuButton("Quit to desktop", () => Root.GetTree().Quit());

        return win;
    }
}
