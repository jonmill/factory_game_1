using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>The commander's pockets: an always-visible bar of carried items above the hotbar.</summary>
public partial class Hud
{
    private PanelContainer _pocketPanel = null!;
    private Label _pocketTitle = null!;
    private HFlowContainer _pocketItems = null!;
    private string? _pocketSig;

    private void BuildPockets()
    {
        _pocketPanel = Panel(_ui);
        VBoxContainer v = new VBoxContainer();
        _pocketPanel.AddChild(v);
        _pocketTitle = Title("");
        _pocketItems = new HFlowContainer { CustomMinimumSize = new Vector2(300, 0) };
        v.AddChild(_pocketTitle);
        v.AddChild(_pocketItems);
    }

    private void UpdatePockets()
    {
        Unit leader = Root.Leader;
        Inventory carry = leader.Carry;
        _pocketTitle.Text = $"POCKETS  {carry.UsedSlots}/{carry.Slots}   ·   {leader.Status}";

        string sig = string.Join(",", carry.Items.Select(kv => $"{kv.Key}:{kv.Value}"));
        if (sig == _pocketSig)
        {
            return;
        }

        _pocketSig = sig;
        foreach (Node c in _pocketItems.GetChildren())
        {
            c.QueueFree();
        }

        if (carry.IsEmpty)
        {
            _pocketItems.AddChild(new Label { Text = "(empty — right-click ore or trees to mine)", Modulate = new Color(1, 1, 1, 0.6f) });

            return;
        }

        foreach ((string item, int count) in carry.Items)
        {
            ItemDef def = C.Item(item);
            HBoxContainer chip = new HBoxContainer { TooltipText = def.Name, MouseFilter = Control.MouseFilterEnum.Stop };
            chip.AddChild(new ColorRect
            {
                Color = WorldView.Hex(def.Color),
                CustomMinimumSize = new Vector2(14, 14),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            chip.AddChild(new Label { Text = $"{def.Name} ×{count}  " });
            _pocketItems.AddChild(chip);
        }
    }
}
