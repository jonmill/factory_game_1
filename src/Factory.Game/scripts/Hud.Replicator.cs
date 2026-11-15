using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The replicator: queue crafts on the lander's fabricator from anywhere. Recipes on the left, the
/// selected recipe's inputs (what the lander has, plus your pockets when you are close enough to feed
/// it) on the right, and the queue with live progress along the bottom.
/// </summary>
public partial class Hud
{
    private GridContainer _recipeGrid = null!;
    private VBoxContainer _recipeDetail = null!;
    private HBoxContainer _queueRow = null!;
    private Label _queueStatus = null!;
    private string? _selectedRecipe;
    private string? _replicatorSig;
    private Slot? _queueHead;

    private PanelContainer BuildReplicator()
    {
        PanelContainer win = MakeWindow(HudWindow.Replicator, "Replicator  ·  lander fabricator", "C", out VBoxContainer body);
        HBoxContainer cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 18);
        body.AddChild(cols);

        VBoxContainer left = new VBoxContainer();
        left.AddChild(HudStyle.Title("Recipes"));
        _recipeGrid = new GridContainer { Columns = 6 };
        _recipeGrid.AddThemeConstantOverride("h_separation", 4);
        _recipeGrid.AddThemeConstantOverride("v_separation", 4);
        left.AddChild(_recipeGrid);
        left.AddChild(HudStyle.Caption("Click: queue 1   ·   Right-click: queue 5   ·   Shift-click: queue 10", HudStyle.TextDim, 11));
        cols.AddChild(left);

        cols.AddChild(new ColorRect { Color = HudStyle.Line, CustomMinimumSize = new Vector2(1, 0) });

        _recipeDetail = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        _recipeDetail.AddThemeConstantOverride("separation", 6);
        cols.AddChild(_recipeDetail);

        body.AddChild(HudStyle.Divider());
        HBoxContainer qh = new HBoxContainer();
        qh.AddChild(HudStyle.Title("Queue"));
        _queueStatus = HudStyle.Caption("", HudStyle.TextDim, 12);
        _queueStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        qh.AddChild(_queueStatus);
        qh.AddChild(HudStyle.Button("Clear queue", () => Root.Send(new CancelQueuedCrafts(P.LanderId))));
        body.AddChild(qh);
        _queueRow = new HBoxContainer { CustomMinimumSize = new Vector2(0, 44) };
        _queueRow.AddThemeConstantOverride("separation", 4);
        body.AddChild(_queueRow);

        return win;
    }

    private void UpdateReplicator()
    {
        if (W.Get<Building>(P.LanderId) is not { } lander)
        {
            return;
        }

        List<RecipeDef> recipes = C.RecipeList.Where(r => lander.Def.Categories.Contains(r.Category)).ToList();
        _selectedRecipe ??= recipes.FirstOrDefault(r => W.Research.Unlocked.Contains(r.Id))?.Id;
        bool reach = P.InLeaderReach(Root.Leader, lander.Origin);
        string inv = _selectedRecipe is { } sel
            ? string.Join(",", C.Recipes[sel].Inputs.Select(i => $"{lander.Inv.Count(i.Item)}+{Root.Leader.Carry.Count(i.Item)}"))
            : "";
        string sig = $"{W.Research.Unlocked.Count}|{_selectedRecipe}|{string.Join(",", lander.CraftQueue)}|{inv}|{reach}";
        if (sig != _replicatorSig)
        {
            _replicatorSig = sig;
            RebuildRecipes(lander, recipes);
            RebuildRecipeDetail(lander, reach);
            RebuildQueue(lander);
        }

        if (_queueHead is not null)
        {
            RecipeDef head = C.Recipes[lander.CraftQueue[0]];
            float f = lander.Crafting ? lander.Progress / (float)System.Math.Max(1, head.Ticks * 100L) : 0;
            _queueHead.Set(_queueHead.Count, true, false, f);
        }

        _queueStatus.Text = lander.CraftQueue.Count == 0 ? "Empty" :
            lander.Crafting ? $"Making {C.Recipes[lander.CraftQueue[0]].Name}" :
            lander.Status == MachineStatus.NoInput ? "Waiting for ingredients" : lander.Status.ToString();
    }

    private void RebuildRecipes(Building lander, List<RecipeDef> recipes)
    {
        Clear(_recipeGrid);
        foreach (RecipeDef r in recipes)
        {
            RecipeDef recipe = r;
            bool unlocked = W.Research.Unlocked.Contains(r.Id);
            Slot slot = new Slot(52)
            {
                Icon = RecipeIcon(r),
                Selected = r.Id == _selectedRecipe,
                Dim = unlocked == false,
                TooltipText = unlocked ? $"{r.Name}\n{Costs(r.Inputs)} → {(r.Outputs.Length > 0 ? Costs(r.Outputs) : r.Effect)}" : $"{r.Name}\nNot researched yet",
            };
            slot.Clicked += (button, shift) =>
            {
                _selectedRecipe = recipe.Id;
                if (unlocked)
                {
                    Root.Send(new QueueCraft(lander.Id, recipe.Id, button == MouseButton.Right ? 5 : shift ? 10 : 1));
                }

                _replicatorSig = null;
            };
            _recipeGrid.AddChild(slot);
        }
    }

    private System.Action<CanvasItem, Rect2> RecipeIcon(RecipeDef r)
    {
        if (r.Outputs.Length > 0)
        {
            return ItemIcon(r.Outputs[0].Item);
        }

        // Recipes with an effect instead of an output, such as calling a colonist, use the first input.
        return r.Inputs.Length > 0 ? ItemIcon(r.Inputs[0].Item) : (_, _) => { };
    }

    private void RebuildRecipeDetail(Building lander, bool reach)
    {
        Clear(_recipeDetail);
        if (_selectedRecipe is null)
        {
            _recipeDetail.AddChild(HudStyle.Caption("Pick a recipe.", HudStyle.TextDim));

            return;
        }

        RecipeDef r = C.Recipes[_selectedRecipe];
        bool unlocked = W.Research.Unlocked.Contains(r.Id);
        HBoxContainer head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(new Slot(64) { Icon = RecipeIcon(r), Count = r.Outputs.Length > 0 ? r.Outputs[0].Count : null });
        VBoxContainer hv = new VBoxContainer();
        hv.AddChild(HudStyle.Caption(r.Name, HudStyle.Header, 16));
        double seconds = r.Ticks * 100.0 / System.Math.Max(1, lander.Def.Speed) / World.TicksPerSecond;
        hv.AddChild(HudStyle.Caption($"{seconds:0.#} s per craft", HudStyle.TextDim, 12));
        hv.AddChild(HudStyle.Caption(unlocked ? (r.Outputs.Length > 0 ? $"Makes {Costs(r.Outputs)}" : $"Effect: {r.Effect}") : "Not researched yet", unlocked ? HudStyle.Text : HudStyle.Bad, 12));
        head.AddChild(hv);
        _recipeDetail.AddChild(head);

        _recipeDetail.AddChild(HudStyle.Title("Needs per craft"));
        foreach (ItemAmount input in r.Inputs)
        {
            int inLander = lander.Inv.Count(input.Item);
            int inPockets = reach ? Root.Leader.Carry.Count(input.Item) : 0;
            HBoxContainer row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(ItemSlot(input.Item, input.Count, 34));
            Label name = HudStyle.Caption(ItemName(input.Item));
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(name);
            string have = inPockets > 0 ? $"{inLander} + {inPockets} carried" : $"{inLander}";
            Label count = HudStyle.Caption($"{have} / {input.Count}", inLander + inPockets >= input.Count ? HudStyle.Good : HudStyle.Bad, 12);
            count.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(count);
            _recipeDetail.AddChild(row);
        }

        Label note = HudStyle.Caption(reach ? "You're close enough: your pockets top up missing ingredients." : "Ingredients come from the lander. Stand near it to add from your pockets.", HudStyle.TextDim, 11);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.CustomMinimumSize = new Vector2(300, 0);
        _recipeDetail.AddChild(note);
        if (unlocked)
        {
            HBoxContainer buttons = new HBoxContainer();
            foreach (int n in new[] { 1, 5, 10 })
            {
                int count = n;
                buttons.AddChild(HudStyle.Button($"Queue {n}", () => Root.Send(new QueueCraft(lander.Id, r.Id, count))));
            }

            _recipeDetail.AddChild(buttons);
        }
    }

    private void RebuildQueue(Building lander)
    {
        Clear(_queueRow);
        _queueHead = null;
        if (lander.CraftQueue.Count == 0)
        {
            _queueRow.AddChild(HudStyle.Caption("Nothing queued.", HudStyle.TextDim));

            return;
        }

        // Group consecutive entries so a queue of twenty gears is one tile.
        int i = 0;
        while ((i < lander.CraftQueue.Count) && (_queueRow.GetChildCount() < 14))
        {
            string id = lander.CraftQueue[i];
            int run = 1;
            while ((i + run < lander.CraftQueue.Count) && (lander.CraftQueue[i + run] == id))
            {
                run++;
            }

            RecipeDef r = C.Recipes[id];
            Slot slot = new Slot(44) { Icon = RecipeIcon(r), Count = run, TooltipText = $"{r.Name} ×{run}" };
            _queueRow.AddChild(slot);
            _queueHead ??= slot;
            i += run;
        }
    }
}
