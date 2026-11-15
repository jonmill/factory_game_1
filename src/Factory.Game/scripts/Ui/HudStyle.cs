using Godot;

namespace Factory.Game.Ui;

/// <summary>
/// The HUD's look: dark translucent navy panels with thin cyan hairlines and uppercase headers, orange
/// reserved for selection and the thing the player is acting on. Shared colours and small widget
/// factories live here so every panel stays consistent.
/// </summary>
public static class HudStyle
{
    /// <summary>Panel fill.</summary>
    public static readonly Color Bg = new(0.035f, 0.065f, 0.1f, 0.96f);

    /// <summary>Fill for centred windows: opaque, since HDR 2D blends in linear light and any see-through shows strongly.</summary>
    public static readonly Color WindowBg = new(0.03f, 0.055f, 0.085f, 1f);

    /// <summary>Recessed fill for slots and wells inside panels.</summary>
    public static readonly Color Well = new(0.015f, 0.03f, 0.05f, 0.92f);

    /// <summary>Hairline borders and dividers.</summary>
    public static readonly Color Line = new(0.45f, 0.72f, 0.9f, 0.32f);

    /// <summary>Brighter hairline for hovered or framed elements.</summary>
    public static readonly Color LineBright = new(0.55f, 0.85f, 1f, 0.85f);

    /// <summary>Body text.</summary>
    public static readonly Color Text = new(0.84f, 0.91f, 0.98f);

    /// <summary>Secondary text.</summary>
    public static readonly Color TextDim = new(0.52f, 0.62f, 0.72f);

    /// <summary>Headers and highlighted values.</summary>
    public static readonly Color Header = new(0.6f, 0.85f, 1f);

    /// <summary>Selection, the active tool and research in progress.</summary>
    public static readonly Color Orange = new(1f, 0.62f, 0.22f);

    /// <summary>Done, enough, powered.</summary>
    public static readonly Color Good = new(0.45f, 0.92f, 0.6f);

    /// <summary>Missing, unpowered, blocked.</summary>
    public static readonly Color Bad = new(1f, 0.42f, 0.36f);

    /// <summary>Builds the theme applied to the whole HUD.</summary>
    /// <returns>The theme.</returns>
    public static Theme BuildTheme()
    {
        Theme theme = new Theme { DefaultFontSize = 13 };
        theme.SetStylebox("panel", "PanelContainer", Box(Bg, Line, 2, 10, 8));
        theme.SetStylebox("panel", "TooltipPanel", Box(new Color(0.02f, 0.04f, 0.07f, 0.97f), LineBright, 2, 8, 6));
        theme.SetColor("font_color", "TooltipLabel", Text);

        foreach (string type in new[] { "Button", "OptionButton" })
        {
            theme.SetStylebox("normal", type, Box(new Color(0.06f, 0.11f, 0.17f, 0.95f), Line, 2, 8, 3));
            theme.SetStylebox("hover", type, Box(new Color(0.09f, 0.17f, 0.25f, 0.95f), LineBright, 2, 8, 3));
            theme.SetStylebox("pressed", type, Box(new Color(0.25f, 0.16f, 0.06f, 0.95f), Orange, 2, 8, 3));
            theme.SetStylebox("disabled", type, Box(new Color(0.04f, 0.06f, 0.08f, 0.9f), new Color(0.3f, 0.35f, 0.4f, 0.3f), 2, 8, 3));
            theme.SetStylebox("focus", type, new StyleBoxEmpty());
            theme.SetColor("font_color", type, Text);
            theme.SetColor("font_hover_color", type, Colors.White);
            theme.SetColor("font_disabled_color", type, TextDim);
        }

        theme.SetColor("font_color", "Label", Text);
        theme.SetStylebox("normal", "LineEdit", Box(Well, Line, 2, 6, 3));
        theme.SetStylebox("panel", "PopupMenu", Box(new Color(0.03f, 0.06f, 0.1f, 0.98f), LineBright, 2, 4, 4));
        theme.SetStylebox("hover", "PopupMenu", Box(new Color(0.12f, 0.22f, 0.32f), LineBright, 1, 4, 2));

        return theme;
    }

    /// <summary>A flat box with a hairline border.</summary>
    /// <param name="fill">Background colour.</param>
    /// <param name="border">Border colour.</param>
    /// <param name="radius">Corner radius in pixels.</param>
    /// <param name="padX">Horizontal content margin.</param>
    /// <param name="padY">Vertical content margin.</param>
    /// <returns>The style box.</returns>
    public static StyleBoxFlat Box(Color fill, Color border, int radius, int padX, int padY)
    {
        StyleBoxFlat s = new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            ContentMarginLeft = padX,
            ContentMarginRight = padX,
            ContentMarginTop = padY,
            ContentMarginBottom = padY,
        };
        s.SetBorderWidthAll(1);
        s.SetCornerRadiusAll(radius);

        return s;
    }

    /// <summary>An uppercase section header.</summary>
    /// <param name="text">Header text; it is shown in capitals.</param>
    /// <param name="size">Font size.</param>
    /// <returns>The label.</returns>
    public static Label Title(string text, int size = 13)
    {
        Label l = new Label { Text = text.ToUpperInvariant() };
        l.AddThemeColorOverride("font_color", Header);
        l.AddThemeFontSizeOverride("font_size", size);

        return l;
    }

    /// <summary>A label in a given colour and size.</summary>
    /// <param name="text">Label text.</param>
    /// <param name="color">Text colour, or the default body colour.</param>
    /// <param name="size">Font size, or the theme default.</param>
    /// <returns>The label.</returns>
    public static Label Caption(string text, Color? color = null, int? size = null)
    {
        Label l = new Label { Text = text };
        if (color is { } c)
        {
            l.AddThemeColorOverride("font_color", c);
        }

        if (size is { } s)
        {
            l.AddThemeFontSizeOverride("font_size", s);
        }

        return l;
    }

    /// <summary>A button that never takes keyboard focus, so game keys keep working.</summary>
    /// <param name="text">Caption.</param>
    /// <param name="onPress">Action to run.</param>
    /// <param name="tooltip">Optional tooltip.</param>
    /// <returns>The button.</returns>
    public static Button Button(string text, System.Action onPress, string? tooltip = null)
    {
        Button b = new Button { Text = text, TooltipText = tooltip ?? "", FocusMode = Control.FocusModeEnum.None };
        b.Pressed += onPress;

        return b;
    }

    /// <summary>A thin horizontal divider.</summary>
    /// <returns>The divider.</returns>
    public static Control Divider()
    {
        ColorRect r = new ColorRect { Color = Line, CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };

        return r;
    }

    /// <summary>A horizontal progress bar drawn as a well with a coloured fill.</summary>
    /// <param name="width">Minimum width.</param>
    /// <param name="height">Height.</param>
    /// <returns>The bar.</returns>
    public static ProgressBar Bar(float width, float height = 6)
    {
        ProgressBar bar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(width, height),
            ShowPercentage = false,
            MaxValue = 1,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bar.AddThemeStyleboxOverride("background", Box(Well, Line, 1, 0, 0));
        bar.AddThemeStyleboxOverride("fill", Box(Header, Header, 1, 0, 0));

        return bar;
    }

    /// <summary>Sets a bar's fill colour.</summary>
    /// <param name="bar">The bar.</param>
    /// <param name="c">Fill colour.</param>
    public static void Tint(ProgressBar bar, Color c) => bar.AddThemeStyleboxOverride("fill", Box(c, c, 1, 0, 0));

    /// <summary>A small key-cap label such as <c>[T]</c>.</summary>
    /// <param name="key">The key name.</param>
    /// <returns>The label.</returns>
    public static Label Key(string key)
    {
        Label l = new Label { Text = key, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(20, 0) };
        l.AddThemeStyleboxOverride("normal", Box(Well, LineBright, 3, 5, 0));
        l.AddThemeColorOverride("font_color", Header);
        l.AddThemeFontSizeOverride("font_size", 11);

        return l;
    }
}
