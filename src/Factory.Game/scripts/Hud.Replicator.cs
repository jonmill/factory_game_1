using Factory.Game.Ui;
using Factory.Sim;
using Godot;

namespace Factory.Game;

/// <summary>
/// The replicator tab of the character screen: queue crafts on the lander's fabricator from anywhere.
/// Recipes on the left, the selected recipe's inputs (what the lander has, plus your pockets when you
/// are close enough to feed it) on the right, and the queue with live progress along the bottom.
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

    private VBoxContainer BuildReplicatorTab()
    {
        VBoxContainer body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        HBoxContainer cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 18);
        body.AddChild(cols);

        VBoxContainer left = new VBoxContainer();
        left.AddChild(HudStyle.Title("Recipes"));
        _recipeGrid = new GridContainer { Columns = 6 };
        _recipeGrid.AddThemeConstantOverride("h_separation", 4);
        _recipeGrid.AddThemeConstantOverride("v_separation", 4);
        left.AddChild(_recipeGrid);
        left.AddChild(HudStyle.Caption("Click a recipe to see what it needs, then queue it on the right.", HudStyle.TextDim, 11));
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

        return body;
    }

    private void UpdateReplicator()
    {
        if (W.Get<Building>(P.LanderId) is not { } lander)
        {
            return;
        }

        List<RecipeDef> recipes = C.RecipeList.Where(r => lander.Def.Categories.Contains(r.Category)).ToList();
        if ((_selectedRecipe is { } picked) && (W.Research.Unlocked.Contains(picked) == false))
        {
            _selectedRecipe = null;
        }

        _selectedRecipe ??= recipes.FirstOrDefault(r => W.Research.Unlocked.Contains(r.Id))?.Id;
        bool reach = Root.InReach(lander.Origin);
        string inv = _selectedRecipe is { } sel
            ? string.Join(",", C.Recipes[sel].Inputs.Select(i => $"{lander.Inv.Count(i.Item)}+{Root.Leader.Carry.Count(i.Item)}"))
            : "";
        string sig = $"{W.Research.Unlocked.Count}|{_selectedRecipe}|{lander.Crafting}:{lander.RecipeId}|{string.Join(",", lander.CraftQueue)}|{inv}|{reach}";
        if (sig != _replicatorSig)
        {
            _replicatorSig = sig;
            RebuildRecipes(recipes);
            RebuildRecipeDetail(lander, reach);
            RebuildQueue(lander);
        }

        // The craft under way has already left the queue: it is the first tile, with its progress.
        if ((_queueHead is not null) && lander.Crafting && (lander.Recipe is { } head))
        {
            _queueHead.Set(_queueHead.Count, true, false, lander.Progress / (float)System.Math.Max(1, head.Ticks * 100L));
        }

        _queueStatus.Text = lander.Crafting && (lander.Recipe is { } making)
            ? $"Making {making.Name}{(lander.CraftingIntermediate ? " (a part)" : "")}"
            : lander.CraftQueue.Count == 0 ? "Empty"
            : lander.Status == MachineStatus.NoInput ? "Waiting for ingredients" : lander.Status.ToString();
    }

    private void RebuildRecipes(List<RecipeDef> recipes)
    {
        Clear(_recipeGrid);
        foreach (RecipeDef r in recipes)
        {
            RecipeDef recipe = r;
            // As in DSP and Factorio, a recipe that hasn't been researched is just an empty tile: nothing to see or pick.
            if (W.Research.Unlocked.Contains(r.Id) == false)
            {
                _recipeGrid.AddChild(new Slot(52) { MouseFilter = Control.MouseFilterEnum.Ignore });
                continue;
            }

            Slot slot = new Slot(52)
            {
                Icon = RecipeIcon(r),
                Selected = r.Id == _selectedRecipe,
                TooltipText = $"{r.Name}\n{Costs(r.Inputs)} → {(r.Outputs.Length > 0 ? Costs(r.Outputs) : r.Effect)}",
            };
            // Clicking only selects; crafting starts from the Queue buttons beside the recipe.
            slot.Clicked += (_, _) =>
            {
                _selectedRecipe = recipe.Id;
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
            bool enough = inLander + inPockets >= input.Count;
            // Missing parts the lander can make are crafted first, from what's in storage.
            bool craftable = (enough == false) && C.RecipeList.Any(x => (x.Effect is null) && lander.Def.Categories.Contains(x.Category) &&
                W.Research.Unlocked.Contains(x.Id) && x.Outputs.Any(o => o.Item == input.Item));
            Label count = HudStyle.Caption($"{have} / {input.Count}{(craftable ? "  · crafted first" : "")}",
                enough ? HudStyle.Good : craftable ? HudStyle.Header : HudStyle.Bad, 12);
            count.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(count);
            _recipeDetail.AddChild(row);
        }

        Label note = HudStyle.Caption(reach ? "Queueing sets the ingredients aside and crafts missing parts first. You're close enough: your pockets top up the rest." : "Queueing sets the ingredients aside from the lander's storage and crafts missing parts first. Stand near it to add from your pockets.", HudStyle.TextDim, 11);
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
        if ((lander.CraftQueue.Count == 0) && (lander.Crafting == false))
        {
            _queueRow.AddChild(HudStyle.Caption("Nothing queued.", HudStyle.TextDim));

            return;
        }

        // The craft under way has left the queue, but it is shown together with the same crafts still
        // waiting behind it: one tile with the progress ring whose count goes down as each one finishes.
        int i = 0;
        if (lander.Crafting && (lander.Recipe is { } current))
        {
            CraftEntry making = new CraftEntry(current.Id, lander.CraftingIntermediate);
            while ((i < lander.CraftQueue.Count) && (lander.CraftQueue[i] == making))
            {
                i++;
            }

            int count = 1 + i;
            _queueHead = new Slot(44)
            {
                Icon = RecipeIcon(current),
                Count = count,
                TooltipText = lander.CraftingIntermediate ? $"Making {current.Name} ×{count}: parts for the next craft" : $"Making {current.Name} ×{count}",
                Modulate = lander.CraftingIntermediate ? new Color(1, 1, 1, 0.6f) : Colors.White,
            };
            _queueRow.AddChild(_queueHead);
        }

        // Group consecutive entries so a queue of twenty gears is one tile. Parts made for a later craft are
        // dimmed: they go to that craft, not to storage.
        while ((i < lander.CraftQueue.Count) && (_queueRow.GetChildCount() < 14))
        {
            CraftEntry e = lander.CraftQueue[i];
            int run = 1;
            while ((i + run < lander.CraftQueue.Count) && (lander.CraftQueue[i + run] == e))
            {
                run++;
            }

            RecipeDef r = C.Recipes[e.RecipeId];
            Slot slot = new Slot(44)
            {
                Icon = RecipeIcon(r),
                Count = run,
                TooltipText = e.Intermediate ? $"{r.Name} ×{run}: parts for the next craft" : $"{r.Name} ×{run}",
                Modulate = e.Intermediate ? new Color(1, 1, 1, 0.6f) : Colors.White,
            };
            _queueRow.AddChild(slot);
            i += run;
        }
    }
}
