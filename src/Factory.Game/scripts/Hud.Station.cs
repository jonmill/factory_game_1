using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>A logistics station's controls: each item slot's item and supply / demand setting, and how many carriers are home.</summary>
public partial class Hud
{
    private void BuildStationActions(Container into, Building station)
    {
        into.AddChild(HudStyle.Title("Logistics", 11));
        string carriers = $"Drones home: {station.Planet.FreeCarriers(station, false)}/{station.Def.Drones}";
        if (station.Def.Vessels > 0)
        {
            carriers += $"   Vessels home: {station.Planet.FreeCarriers(station, true)}/{station.Def.Vessels}";
        }

        into.AddChild(HudStyle.Caption(carriers, HudStyle.TextDim, 12));

        // Offer items the colony has made so far, plus whatever is already set, so the lists stay short.
        List<string> items = W.Stats.AllItems().Union(station.StationSlots.Select(s => s.Item))
            .Where(id => C.Items.ContainsKey(id)).OrderBy(id => C.Items[id].Name, System.StringComparer.Ordinal).ToList();
        for (int i = 0; i < station.Def.StationSlots; i++)
        {
            int index = i;
            StationSlot? slot = i < station.StationSlots.Count ? station.StationSlots[i] : null;
            if ((slot is null) && (i > station.StationSlots.Count))
            {
                break; // one empty row at a time
            }

            HBoxContainer row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            OptionButton item = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(170, 0) };
            item.AddItem("(empty)");
            foreach (string id in items)
            {
                item.AddItem(C.Items[id].Name);
            }

            item.Select(slot is null ? 0 : items.IndexOf(slot.Item) + 1);
            OptionButton mode = new OptionButton { FocusMode = Control.FocusModeEnum.None };
            mode.AddItem("Supply");
            mode.AddItem("Demand");
            mode.Select(slot?.Mode == StationMode.Demand ? 1 : 0);
            mode.Disabled = slot is null;
            item.ItemSelected += sel => Root.Send(new SetStationSlot(station.Id, index, sel == 0 ? null : items[(int)sel - 1], mode.Selected == 1 ? StationMode.Demand : StationMode.Supply));
            mode.ItemSelected += sel =>
            {
                if (slot is not null)
                {
                    Root.Send(new SetStationSlot(station.Id, index, slot.Item, sel == 1 ? StationMode.Demand : StationMode.Supply, slot.Max));
                }
            };
            row.AddChild(item);
            row.AddChild(mode);
            if (slot is not null)
            {
                // The slot's limit, like a station's max-stock slider: 0 keeps up to the station's capacity.
                SpinBox max = new SpinBox { MinValue = 0, MaxValue = station.Def.ItemCap, Step = 100, Value = slot.Max, FocusMode = Control.FocusModeEnum.Click, TooltipText = "Most to keep (0 = full capacity)" };
                max.ValueChanged += v => Root.Send(new SetStationSlot(station.Id, index, slot.Item, slot.Mode, (int)v));
                row.AddChild(max);
                row.AddChild(HudStyle.Caption($"{station.Inv.Count(slot.Item)}/{Planet.StationCap(station, slot)}", HudStyle.TextDim, 12));
            }

            into.AddChild(row);
        }
    }
}
