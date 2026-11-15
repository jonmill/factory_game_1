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
        int id = Root.SelectedId;
        StringBuilder sb = new StringBuilder();
        string title = "";
        string sig = $"{id}";
        List<(string Title, Inventory Inv)> grids = [];
        Entity? e = W.Get<Building>(id) ?? W.Get<Unit>(id) ?? W.Get<Train>(id) ?? (Entity?)W.Get<ConstructionSite>(id);
        if (e is null)
        {
            _inspector.Visible = false;

            return;
        }

        _inspector.Visible = true;

        switch (e)
        {
            case Building b:
                {
                    title = b.Name == b.Def.Name ? b.Def.Name : $"{b.Name} ({b.Def.Name})";
                    sb.AppendLine(b.Def.Description);
                    sb.AppendLine($"Status: {b.Status}");
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

                    if ((b.Def.Kind == BuildingKind.Drill) && (P.FindMinable(b) is { } t))
                    {
                        sb.AppendLine($"Mining {P.ResourceAt(t)!.Name} ({P.AmountAt(t)} left on tile)");
                    }

                    if (b.Def.Slots > 0)
                    {
                        grids.Add((b.Def.IsStorage ? $"Contents  {b.Inv.UsedSlots}/{b.Inv.Slots}" : "Input and fuel", b.Inv));
                    }

                    if ((b.Def.IsStorage == false) && (b.Def.OutSlots > 0) && (b.Out.IsEmpty == false))
                    {
                        grids.Add(("Output", b.Out));
                    }

                    bool reach = P.InLeaderReach(Root.Leader, b.Origin);
                    sig += $"|{b.RecipeId}|{reach}";
                    if (sig != _inspSig)
                    {
                        BuildBuildingActions(b, reach);
                    }

                    break;
                }
            case Unit u:
                {
                    title = u.IsLeader ? $"{u.Name} (you)" : u.Name;
                    sb.AppendLine(u.Status);
                    sb.AppendLine($"Job: {u.Job.Kind}" + (u.Job.Resource is { } res ? $" ({C.Resources[res].Name})" : ""));
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
                            sb.AppendLine(P.HasOperator(t) ? "Operator aboard" : "Needs a follower to operate (left panel → Operate train)");
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
                    sb.AppendLine(builder is null ? "No builder assigned (set a follower to Build)" : $"Builder: {builder.Name}");
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

    private void BuildBuildingActions(Building b, bool reach)
    {
        ClearActions();
        Unit leader = Root.Leader;
        if (b.Def.Kind == BuildingKind.Lander)
        {
            _inspActions.AddChild(HudStyle.Button("Open replicator  [C]", () => Toggle(HudWindow.Replicator)));
        }

        if (b.Def.Kind == BuildingKind.Assembler)
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
            _inspActions.AddChild(opt);
        }

        if (b.Def.Kind == BuildingKind.RailStop)
        {
            LineEdit edit = new LineEdit { Text = b.Name, PlaceholderText = "Stop name" };
            edit.TextSubmitted += txt =>
            {
                Root.Send(new RenameBuilding(b.Id, txt));
                edit.ReleaseFocus();
            };
            _inspActions.AddChild(edit);
        }

        if (reach && (b.Def.Kind is not (BuildingKind.Pole or BuildingKind.Rail)))
        {
            HBoxContainer row = new HBoxContainer();
            row.AddChild(HudStyle.Button("Deposit all", () => Root.Send(new DepositToBuilding(leader.Id, b.Id)), "Put what you're carrying into this building (only what it accepts)"));
            row.AddChild(HudStyle.Button("Take all", () => Root.Send(new TakeFromBuilding(leader.Id, b.Id))));
            _inspActions.AddChild(row);
        }
        else if (b.Def.Slots > 0)
        {
            _inspActions.AddChild(new Label { Text = "(walk closer to move items by hand)" });
        }

        if (b.Def.Kind != BuildingKind.Lander)
        {
            _inspActions.AddChild(HudStyle.Button("Dismantle", () => Root.Send(new DeconstructEntity(b.Id))));
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
