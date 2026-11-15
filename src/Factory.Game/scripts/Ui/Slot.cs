using Godot;

namespace Factory.Game.Ui;

/// <summary>
/// A square icon tile: recessed well, procedural icon, count in the corner and an optional key hint
/// and progress ring. Used by the build bar, inventory and replicator alike. Slots given a
/// <see cref="DragSource"/> can be dragged onto slots that accept the stack; a plain left click on
/// them fires on release instead of press, so it doesn't fight with starting a drag.
/// </summary>
public partial class Slot : Control
{
    private bool _hover;
    private bool _armed;
    private bool _dragged;

    /// <summary>Raised when a drag starts or ends, so slots can show whether they would take the stack.</summary>
    private static event System.Action? DragChanged;

    /// <summary>The stack being dragged, if any.</summary>
    public static ItemDrag? Dragging { get; private set; }

    /// <summary>What dragging this slot picks up, or null when it can't be dragged right now.</summary>
    public System.Func<ItemDrag?>? DragSource { get; set; }

    /// <summary>Whether a dragged stack may be dropped here.</summary>
    public System.Func<ItemDrag, bool>? CanDrop { get; set; }

    /// <summary>Raised when a stack is dropped here.</summary>
    public event System.Action<ItemDrag>? Dropped;

    /// <summary>A faint icon for an empty slot, showing what belongs there.</summary>
    public bool Ghost { get; set; }

    /// <summary>Draws the icon into the given rect, or null for an empty slot.</summary>
    public System.Action<CanvasItem, Rect2>? Icon { get; set; }

    /// <summary>Stack count shown bottom-right, or null for none.</summary>
    public int? Count { get; set; }

    /// <summary>Key hint shown top-left, such as "3".</summary>
    public string? KeyHint { get; set; }

    /// <summary>Selected or active: framed in orange.</summary>
    public bool Selected { get; set; }

    /// <summary>Unavailable: drawn faded.</summary>
    public bool Dim { get; set; }

    /// <summary>Progress from 0 to 1 drawn as a ring, or below 0 for none.</summary>
    public float Progress { get; set; } = -1;

    /// <summary>Raised on click with the mouse button and whether Shift was held.</summary>
    public event System.Action<MouseButton, bool>? Clicked;

    /// <summary>Creates a slot of the given edge length.</summary>
    /// <param name="size">Edge length in pixels.</param>
    public Slot(float size)
    {
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += () =>
        {
            _hover = true;
            QueueRedraw();
        };
        MouseExited += () =>
        {
            _hover = false;
            QueueRedraw();
        };
    }

    /// <summary>Parameterless constructor required by Godot.</summary>
    public Slot()
        : this(48)
    {
    }

    /// <inheritdoc />
    public override void _EnterTree() => DragChanged += QueueRedraw;

    /// <inheritdoc />
    public override void _ExitTree() => DragChanged -= QueueRedraw;

    /// <inheritdoc />
    public override void _GuiInput(InputEvent @event)
    {
        if ((@event is not InputEventMouseButton mb) || (mb.ButtonIndex is not (MouseButton.Left or MouseButton.Right)))
        {
            return;
        }

        if ((DragSource is null) || (mb.ButtonIndex == MouseButton.Right))
        {
            if (mb.Pressed)
            {
                Clicked?.Invoke(mb.ButtonIndex, mb.ShiftPressed);
                AcceptEvent();
            }

            return;
        }

        if (mb.Pressed)
        {
            _armed = true;
            _dragged = false;
        }
        else if (_armed)
        {
            _armed = false;
            if (_dragged == false)
            {
                Clicked?.Invoke(mb.ButtonIndex, mb.ShiftPressed);
            }

            AcceptEvent();
        }
    }

    /// <inheritdoc />
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (DragSource?.Invoke() is not { } drag)
        {
            return default;
        }

        _dragged = true;
        Dragging = drag;
        Control holder = new Control { MouseFilter = MouseFilterEnum.Ignore };
        Slot preview = new Slot(Size.X) { Icon = Icon, Count = drag.Count, Position = -Size / 2, Modulate = new Color(1, 1, 1, 0.85f) };
        holder.AddChild(preview);
        SetDragPreview(holder);
        DragChanged?.Invoke();

        return "item-stack";
    }

    /// <inheritdoc />
    public override bool _CanDropData(Vector2 atPosition, Variant data) => (Dragging is { } d) && (CanDrop?.Invoke(d) == true);

    /// <inheritdoc />
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (Dragging is { } d)
        {
            Dropped?.Invoke(d);
        }
    }

    /// <inheritdoc />
    public override void _Notification(int what)
    {
        if ((what == NotificationDragEnd) && (Dragging is not null))
        {
            Dragging = null;
            DragChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        Rect2 r = new Rect2(Vector2.Zero, Size);
        bool target = (Dragging is { } d) && (CanDrop?.Invoke(d) == true);
        Color border = Selected ? HudStyle.Orange : (_hover || target) ? HudStyle.LineBright : HudStyle.Line;
        DrawStyleBox(HudStyle.Box(Selected ? new Color(0.16f, 0.1f, 0.04f, 0.95f) : HudStyle.Well, border, 3, 0, 0), r);
        if (target)
        {
            DrawRect(r.Grow(-1), new Color(0.45f, 0.72f, 0.9f, 0.12f));
        }

        if (Icon is not null)
        {
            Icon(this, r.Grow(-Size.X * 0.1f));
            if (Ghost)
            {
                DrawRect(r.Grow(-1), new Color(0.015f, 0.03f, 0.05f, 0.75f));
            }
            else if (Dim)
            {
                DrawRect(r.Grow(-1), new Color(0.015f, 0.03f, 0.05f, 0.62f));
            }
        }

        Font font = ThemeDB.FallbackFont;
        if (Count is { } n)
        {
            string text = n >= 10000 ? $"{n / 1000}k" : $"{n}";
            Vector2 at = new Vector2(2, Size.Y - 4);
            DrawStringOutline(font, at, text, HorizontalAlignment.Right, Size.X - 5, 12, 4, new Color(0, 0, 0, 0.9f));
            DrawString(font, at, text, HorizontalAlignment.Right, Size.X - 5, 12, Colors.White);
        }

        if (KeyHint is { } key)
        {
            DrawString(font, new Vector2(4, 12), key, HorizontalAlignment.Left, -1, 10, HudStyle.Header);
        }

        if (Progress >= 0)
        {
            Vector2 c = r.GetCenter();
            float rad = (Size.X * 0.5f) - 3;
            DrawArc(c, rad, 0, Mathf.Tau, 32, new Color(0, 0, 0, 0.5f), 3);
            DrawArc(c, rad, -Mathf.Pi / 2, (-Mathf.Pi / 2) + (Mathf.Tau * Mathf.Clamp(Progress, 0, 1)), 32, HudStyle.Orange, 3);
        }
    }

    /// <summary>Updates the visible state and redraws only if something changed.</summary>
    /// <param name="count">New count.</param>
    /// <param name="selected">New selected state.</param>
    /// <param name="dim">New dim state.</param>
    /// <param name="progress">New progress.</param>
    public void Set(int? count, bool selected = false, bool dim = false, float progress = -1)
    {
        if ((count == Count) && (selected == Selected) && (dim == Dim) && Mathf.IsEqualApprox(progress, Progress))
        {
            return;
        }

        Count = count;
        Selected = selected;
        Dim = dim;
        Progress = progress;
        QueueRedraw();
    }
}
