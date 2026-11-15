using System.Text;
using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>The inspector, right side: details and actions for the selected building, unit, train or construction site.</summary>
public partial class Hud
{
    private PanelContainer _inspector = null!;
    private Label _inspTitle = null!, _inspBody = null!;
    private VBoxContainer _inspActions = null!, _inspGrids = null!;
    private string _inspSig = "";
    private string? _inspGridSig;

    private void BuildInspector()
    {
        _inspector = new PanelContainer();
        _ui.AddChild(_inspector);
        VBoxContainer v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        _inspector.AddChild(v);
        _inspTitle = HudStyle.Title("", 14);
        _inspBody = HudStyle.Caption("", null, 12);
        _inspBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _inspBody.CustomMinimumSize = new Vector2(320, 0);
        _inspGrids = new VBoxContainer();
        _inspActions = new VBoxContainer();
        v.AddChild(_inspTitle);
        v.AddChild(HudStyle.Divider());
        v.AddChild(_inspBody);
        v.AddChild(_inspGrids);
        v.AddChild(_inspActions);
    }

    private static string Inv(Inventory inv, ContentDb c) =>
        inv.IsEmpty ? "  (empty)" : string.Join("\n", inv.Items.Select(kv => $"  {c.Item(kv.Key).Name} ×{kv.Value}"));

    private void UpdateInspector()
    {
        // With nothing selected, the panel shows whatever is under the mouse (read-only), as an entity
        // info panel does; selecting something pins it and brings back its buttons.
        int id = Root.SelectedId;
        bool hovered = false;
        if ((id == 0) && (_open == HudWindow.None))
        {
            id = Root.HoverEntityId;
            hovered = id != 0;
        }

        _inspActions.Visible = hovered == false;
        StringBuilder sb = new StringBuilder();
        string title = "";
        string sig = $"{id}";
        List<(string Title, Inventory Inv)> grids = [];
        Entity? e = W.Get<Building>(id) ?? W.Get<Unit>(id) ?? W.Get<Train>(id) ?? (Entity?)W.Get<ConstructionSite>(id);
        // A centred window takes over; the panel comes back when it closes.
        if ((e is null) || (_open != HudWindow.None))
        {
            _inspector.Visible = false;

            return;
        }

        _inspector.Visible = true;

        switch (e)
        {
            case Building b:
                {
                    title = BuildingTitle(b);
                    sb.Append(DescribeBuilding(b));
                    if (b.Def.Slots > 0)
                    {
                        grids.Add((b.Def.IsStorage ? $"Contents  {b.Inv.UsedSlots}/{b.Inv.Slots}" : "Input and fuel", b.Inv));
                    }

                    if ((b.Def.IsStorage == false) && (b.Def.OutSlots > 0) && (b.Out.IsEmpty == false))
                    {
                        grids.Add(("Output", b.Out));
                    }

                    sig += $"|{b.RecipeId}";
                    if (sig != _inspSig)
                    {
                        ClearActions();
                        BuildBuildingActions(_inspActions, b);
                    }

                    break;
                }
            case Unit u:
                {
                    title = u.IsLeader ? $"{u.Name} (you)" : u.Name;
                    sb.AppendLine(u.Status);
                    sb.AppendLine(u.Robot.Name);
                    if (u.IsLeader == false)
                    {
                        sb.AppendLine($"Job: {u.Job.Kind}" + (u.Job.Resource is { } res ? $" ({C.Resources[res].Name})" : ""));
                    }

                    sb.AppendLine($"Modules: {(u.Modules.Count == 0 ? "none" : string.Join(", ", u.Modules.Select(ItemName)))}  ({u.Modules.Count}/{P.ModuleSlots(u)})");
                    grids.Add(($"Carrying  {u.Carry.UsedSlots}/{u.Carry.Slots}", u.Carry));
                    if (sig != _inspSig)
                    {
                        ClearActions();
                    }

                    break;
                }
            case Train t:
                {
                    title = t.Name;
                    Vehicle? loco = t.Locomotive;
                    sb.AppendLine($"State: {WorldView.StateText(t.State)}   Speed: {t.Speed * 60 / Fixed.One} tiles/s");
                    if (loco is not null)
                    {
                        sb.AppendLine($"Load: {Planet.PulledMass(t)} / {loco.Def.MaxPull} kg pull limit");
                        if (loco.Def.Kind == VehicleKind.Steam)
                        {
                            sb.AppendLine($"Water: {t.Water * 100 / System.Math.Max(1, loco.Def.WaterCapacity)}%   Fuel: {t.FuelJ / 1000} kJ + {Inv(loco.Cargo, C).Trim()}");
                        }

                        if (loco.Def.Kind == VehicleKind.HandCart)
                        {
                            sb.AppendLine(P.HasOperator(t) ? "Operator aboard" : "Needs a helper bot to operate it (left panel → Operate train)");
                        }
                    }

                    int ci = 0;
                    foreach (Vehicle? v in t.Vehicles.Where(v => v.Def.Kind == VehicleKind.Cart))
                    {
                        sb.AppendLine($"Cart {++ci}: " + Inv(v.Cargo, C).Trim());
                    }

                    sb.AppendLine("Schedule:");
                    if (t.Schedule.Count == 0)
                    {
                        sb.AppendLine("  (none — add stops below)");
                    }

                    sig += "|" + string.Join(",", t.Schedule.Select(s => $"{s.StopId}{s.Action}"));
                    if (sig != _inspSig)
                    {
                        BuildTrainActions(t);
                    }

                    break;
                }
            case ConstructionSite s:
                {
                    title = s.Kind == SiteKind.Deconstruct ? $"Dismantle {P.SiteLabel(s)}" : $"Construction: {P.SiteLabel(s)}";
                    List<ItemAmount> missing = s.Missing().ToList();
                    sb.AppendLine(missing.Count == 0 ? "All materials delivered" : "Still needs: " + Costs(missing));
                    sb.AppendLine($"Work: {s.WorkDone * 100 / System.Math.Max(1, s.WorkNeeded)}%");
                    Unit? builder = W.Get<Unit>(s.ClaimedBy);
                    sb.AppendLine(builder is null ? "No builder assigned (set a helper bot to Build)" : $"Builder: {builder.Name}");
                    if (sig != _inspSig)
                    {
                        ClearActions();
                        _inspActions.AddChild(HudStyle.Button("Cancel", () => Root.Send(new CancelSite(s.Id))));
                    }

                    break;
                }
        }

        _inspSig = sig;
        _inspTitle.Text = title.ToUpperInvariant();
        _inspBody.Text = sb.ToString().TrimEnd();
        UpdateInspectorGrids(grids);
    }

    /// <summary>Shows inventories as slot grids, rebuilt only when their contents change.</summary>
    private void UpdateInspectorGrids(List<(string Title, Inventory Inv)> grids)
    {
        string sig = string.Join("|", grids.Select(g => g.Title + ":" + string.Join(",", g.Inv.Items.Select(kv => $"{kv.Key}={kv.Value}"))));
        if (sig == _inspGridSig)
        {
            return;
        }

        _inspGridSig = sig;
        Clear(_inspGrids);
        foreach ((string title, Inventory inv) in grids)
        {
            _inspGrids.AddChild(HudStyle.Title(title, 11));
            HFlowContainer flow = new HFlowContainer { CustomMinimumSize = new Vector2(300, 0) };
            flow.AddThemeConstantOverride("h_separation", 3);
            flow.AddThemeConstantOverride("v_separation", 3);
            foreach ((string item, int n) in inv.Items)
            {
                flow.AddChild(ItemSlot(item, n, 38));
            }

            if (inv.IsEmpty)
            {
                flow.AddChild(HudStyle.Caption("(empty)", HudStyle.TextDim, 12));
            }

            _inspGrids.AddChild(flow);
        }
    }

    private void ClearActions()
    {
        foreach (Node? c in _inspActions.GetChildren())
        {
            c.QueueFree();
        }
    }

    /// <summary>Turns an enum name like "NoFuel" into "No fuel".</summary>
    private static string Spaced(string name)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char ch in name)
        {
            if (char.IsUpper(ch) && (sb.Length > 0))
            {
                sb.Append(' ').Append(char.ToLowerInvariant(ch));
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static string BuildingTitle(Building b) => b.Name == b.Def.Name ? b.Def.Name : $"{b.Name} ({b.Def.Name})";

    /// <summary>A building's description and live status lines, shared by the inspector and the machine window.</summary>
    private string DescribeBuilding(Building b)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(b.Def.Description);
        sb.AppendLine($"Status: {Spaced(b.Status.ToString())}");
        if (b.Def.Energy == EnergyKind.Electric)
        {
            sb.AppendLine(b.NetworkId == 0 ? "Not connected to power" : $"Power: {b.Satisfaction / 10}%");
        }

        if (b.Def.Kind == BuildingKind.SteamEngine)
        {
            sb.AppendLine($"Output: {b.LastOutputJ * World.TicksPerSecond / 1000} kW");
        }

        if (b.Def.Kind == BuildingKind.Battery)
        {
            sb.AppendLine($"Charge: {b.StoredJ / 1000} / {b.Def.CapacityKj} kJ");
        }

        if ((b.Recipe is { } r) && (b.Def.Kind != BuildingKind.Lab))
        {
            sb.AppendLine($"Recipe: {r.Name}  ({Costs(r.Inputs)} → {(r.Outputs.Length > 0 ? Costs(r.Outputs) : r.Effect)})");
        }

        if ((b.Def.Kind == BuildingKind.Lander) && (b.CraftQueue.Count > 0))
        {
            sb.AppendLine("Queue: " + string.Join(", ", b.CraftQueue.GroupBy(x => x).Select(g => $"{C.Recipes[g.Key].Name} ×{g.Count()}")));
        }

        if (b.Def.Kind == BuildingKind.LaunchPad)
        {
            sb.AppendLine("To launch: " + string.Join(", ", b.Def.LaunchFuelList.Prepend(new ItemAmount(b.Def.Vessel!, 1))
                .Select(f => $"{ItemName(f.Item)} {System.Math.Min(f.Count, b.Inv.Count(f.Item))}/{f.Count}")) + ". Everything else in the pad flies as cargo.");
        }

        if ((b.Def.Kind == BuildingKind.Drill) && (P.FindMinable(b) is { } t))
        {
            sb.AppendLine($"Mining {P.ResourceAt(t)!.Name} ({P.AmountAt(t)} left on tile)");
        }

        return sb.ToString();
    }

    /// <summary>Controls for a building (recipe, rename, replicator, dismantle), added to the given container.</summary>
    private void BuildBuildingActions(Container into, Building b)
    {
        if (b.Def.Kind == BuildingKind.Lander)
        {
            into.AddChild(HudStyle.Button("Open replicator  [C]", () => Toggle(HudWindow.Replicator)));
        }

        if (b.Def.Kind is BuildingKind.Assembler or BuildingKind.RobotBay)
        {
            OptionButton opt = new OptionButton { FocusMode = Control.FocusModeEnum.None };
            List<RecipeDef> recipes = C.RecipeList.Where(r => b.Def.Categories.Contains(r.Category) && W.Research.Unlocked.Contains(r.Id)).ToList();
            opt.AddItem("(choose recipe)");
            foreach (RecipeDef? r in recipes)
            {
                opt.AddItem(r.Name);
            }

            opt.Select(b.RecipeId is null ? 0 : recipes.FindIndex(r => r.Id == b.RecipeId) + 1);
            opt.ItemSelected += i => Root.Send(new SetRecipe(b.Id, i == 0 ? null : recipes[(int)i - 1].Id));
            into.AddChild(opt);
        }

        if (b.Def.Kind == BuildingKind.RailStop)
        {
            LineEdit edit = new LineEdit { Text = b.Name, PlaceholderText = "Stop name" };
            edit.TextSubmitted += txt =>
            {
                Root.Send(new RenameBuilding(b.Id, txt));
                edit.ReleaseFocus();
            };
            into.AddChild(edit);
        }

        if (b.Def.Kind == BuildingKind.LaunchPad)
        {
            BuildLaunchActions(into, b);
        }

        if (b.Def.Kind == BuildingKind.Ferry)
        {
            (Planet home, Building store) = b.Planet.HomeOf(b);
            into.AddChild(HudStyle.Button($"Fly home to {home.Name}", () => Root.Send(new ReturnFerry(Root.Leader.Id, b.Id)),
                $"Lands on {store.Name} with the hold's cargo, you and every one of your helper bots on {b.Planet.Name}."));
        }

        if (b.Def.Kind is not (BuildingKind.Lander or BuildingKind.Ferry))
        {
            into.AddChild(HudStyle.Button("Dismantle", () => Root.Send(new DeconstructEntity(b.Id))));
        }
    }

    private void BuildTrainActions(Train t)
    {
        ClearActions();
        for (int i = 0; i < t.Schedule.Count; i++)
        {
            int index = i;
            ScheduleStop stop = t.Schedule[i];
            HBoxContainer row = new HBoxContainer();
            row.AddChild(new Label { Text = $"{i + 1}. {W.Get<Building>(stop.StopId)?.Name ?? "(removed)"}", CustomMinimumSize = new Vector2(120, 0) });
            OptionButton opt = new OptionButton { FocusMode = Control.FocusModeEnum.None };
            opt.AddItem("Load");
            opt.AddItem("Unload");
            opt.AddItem("Pass");
            opt.Select((int)stop.Action);
            opt.ItemSelected += a =>
            {
                ScheduleStop[] stops = t.Schedule.ToArray();
                stops[index] = stops[index] with
                {
                    Action = (StopAction)(int)a
                };
                Root.Send(new SetSchedule(t.Id, stops));
            };
            row.AddChild(opt);
            row.AddChild(HudStyle.Button("✕", () => Root.Send(new SetSchedule(t.Id, t.Schedule.Where((_, k) => k != index).ToArray()))));
            _inspActions.AddChild(row);
        }

        _inspActions.AddChild(HudStyle.Button("Add stops (click rail stops on map)", () => Root.SetTool(ToolKind.PickStop)));
        _inspActions.AddChild(HudStyle.Button("Clear schedule", () => Root.Send(new SetSchedule(t.Id, []))));
        _inspActions.AddChild(HudStyle.Button("Dismantle train", () => Root.Send(new DeconstructEntity(t.Id))));
    }
}
