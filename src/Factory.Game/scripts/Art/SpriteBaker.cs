using Godot;
using SimDir = Factory.Sim.Dir;

namespace Factory.Game.Art;

/// <summary>A building's baked images and where they sit relative to its footprint.</summary>
public sealed class BakedSprite
{
    /// <summary>The lit model, one image per animation frame (one if it doesn't move).</summary>
    public Texture2D[] Frames { get; init; } = [];

    /// <summary>The glowing parts per frame, drawn brighter while the machine works; empty if nothing glows.</summary>
    public Texture2D[] Glow { get; init; } = [];

    /// <summary>The shadow it casts, black with alpha.</summary>
    public Texture2D? Shadow { get; init; }

    /// <summary>Top-left of the images in tiles, relative to the footprint's top-left corner.</summary>
    public Vector2 Offset { get; init; }

    /// <summary>Size of the images in tiles.</summary>
    public Vector2 Size { get; init; }

    /// <summary>Animation frames per second while working.</summary>
    public float Fps { get; init; }

    /// <summary>Where wires attach, in tiles from the footprint's top-left (poles only).</summary>
    public Vector2? Anchor { get; init; }

    /// <summary>Where smoke rises, in tiles from the footprint's top-left; for burner models, which turn, given facing north.</summary>
    public Vector2? Smoke { get; init; }
}

/// <summary>
/// Renders every <see cref="Models"/> model into sprites when the game starts. Each model is lit by one
/// sun from the north-west, sky light for ambience and reflections, and screen-space ambient occlusion, and
/// seen by an orthographic camera tilted <see cref="Tilt"/> degrees from straight down. The model's
/// north-south axis is stretched by 1/cos(tilt) so its footprint still lands on the map's square tiles, while
/// height rises up the screen, which gives the 3/4 view. Three passes per frame: colour, shadow and glow.
/// Until a model is baked, the world draws its old flat art.
/// </summary>
public partial class SpriteBaker : Node
{
    /// <summary>Pixels per tile in the baked images; the world scales them to the zoom.</summary>
    public const int PxPerTile = 80;

    /// <summary>Camera tilt from vertical, in degrees.</summary>
    public const float Tilt = 40f;

    private const int Batch = 24;
    private const float Margin = 0.12f;

    private static readonly Vector3 _sun = new Vector3(0.55f, -1f, 0.45f).Normalized();

    private readonly Dictionary<(string, SimDir), BakedSprite> _sprites = [];

    /// <summary>The running baker, for interface code that draws building icons.</summary>
    public static SpriteBaker? Current { get; private set; }

    /// <summary>The game root, for the content's colours.</summary>
    public GameRoot Root { get; set; } = null!;

    /// <summary>True once every model is baked.</summary>
    public bool Baked { get; private set; }

    /// <summary>How tall a model of height <paramref name="h"/> tiles stands on screen, in tiles.</summary>
    /// <param name="h">Height in tiles.</param>
    /// <returns>Its projected height.</returns>
    public static float Rise(float h) => h * Mathf.Sin(Mathf.DegToRad(Tilt));

    /// <summary>The baked sprite for a building, if its model has been baked.</summary>
    /// <param name="defId">Building id.</param>
    /// <param name="rot">Its facing; ignored by models that don't turn.</param>
    /// <param name="sprite">The sprite.</param>
    /// <returns>Whether there is one.</returns>
    public bool TryGet(string defId, SimDir rot, out BakedSprite sprite) =>
        _sprites.TryGetValue((defId, rot), out sprite!) || _sprites.TryGetValue((defId, SimDir.North), out sprite!);

    /// <inheritdoc />
    public override void _Ready()
    {
        Current = this;
        if (System.Environment.GetEnvironmentVariable("FACTORY_FLAT_ART") == "1")
        {
            Baked = true;

            return;
        }

        BakeAll();
    }

    private sealed record Job(ModelDef Model, SimDir Rot, BakePass Pass, int Frame, SubViewport View);

    private async void BakeAll()
    {
        ulong started = Time.GetTicksMsec();
        List<(ModelDef Model, SimDir Rot, BakePass Pass, int Frame)> work = [];
        foreach (ModelDef m in Models.All)
        {
            foreach (SimDir rot in m.Turns ? new[] { SimDir.North, SimDir.East, SimDir.South, SimDir.West } : new[] { SimDir.North })
            {
                work.Add((m, rot, BakePass.Shadow, 0));
                for (int f = 0; f < m.Frames; f++)
                {
                    work.Add((m, rot, BakePass.Color, f));
                    if (m.Glows)
                    {
                        work.Add((m, rot, BakePass.Glow, f));
                    }
                }
            }
        }

        Dictionary<(string, SimDir), (Image? Shadow, Image[] Frames, Image[] Glow)> images = [];
        string cache = CacheDir();
        bool cached = work.All(w => Godot.FileAccess.FileExists(CachePath(cache, w.Model.Id, w.Rot, w.Pass, w.Frame)));
        if (cached)
        {
            foreach ((ModelDef m, SimDir rot, BakePass pass, int frame) in work)
            {
                Image img = Image.LoadFromFile(ProjectSettings.GlobalizePath(CachePath(cache, m.Id, rot, pass, frame)));
                Store(images, m, rot, pass, frame, img);
            }

            work.Clear();
        }

        for (int i = 0; i < work.Count; i += Batch)
        {
            List<Job> jobs = [];
            foreach ((ModelDef m, SimDir rot, BakePass pass, int frame) in work.Skip(i).Take(Batch))
            {
                SubViewport view = Stage(m, rot, pass, frame);
                AddChild(view);
                jobs.Add(new Job(m, rot, pass, frame, view));
            }

            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            foreach (Job job in jobs)
            {
                Image raw = job.View.GetTexture().GetImage();
                job.View.QueueFree();
                Image img = job.Pass switch { BakePass.Shadow => ToShadow(raw), BakePass.Glow => ToGlow(raw), _ => Unpremultiply(raw) };
                img.SavePng(CachePath(cache, job.Model.Id, job.Rot, job.Pass, job.Frame));
                Store(images, job.Model, job.Rot, job.Pass, job.Frame, img);
            }
        }

        // FACTORY_ART_DUMP=<dir> saves every baked image, to look at the art without running the game.
        string? dump = System.Environment.GetEnvironmentVariable("FACTORY_ART_DUMP");
        foreach (((string id, SimDir rot), (Image? shadow, Image[] frames, Image[] glow)) in images)
        {
            if (dump is not null)
            {
                frames[0].SavePng(System.IO.Path.Combine(dump, $"{id}-{rot}.png"));
                shadow?.SavePng(System.IO.Path.Combine(dump, $"{id}-{rot}-shadow.png"));
                if (glow.Length > 0)
                {
                    glow[0].SavePng(System.IO.Path.Combine(dump, $"{id}-{rot}-glow.png"));
                }
            }

            ModelDef m = Models.All.First(x => x.Id == id);
            (Vector2 offset, Vector2 size) = Frame(m);
            _sprites[(id, rot)] = new BakedSprite
            {
                Frames = [.. frames.Select(Texture)],
                Glow = [.. glow.Select(Texture)],
                Shadow = shadow is null ? null : Texture(shadow),
                Offset = offset,
                Size = size,
                Fps = m.Fps,
                Anchor = m.Anchor is { } a ? new Vector2(a.X, a.Z - Rise(a.Y)) : null,
                Smoke = m.Smoke is { } sm ? Turned(m, rot, sm) : null,
            };
        }

        if (dump is not null)
        {
            ContactSheet(images, dump);
        }

        Baked = true;
        GD.Print(cached ? $"Loaded {_sprites.Count} baked sprites from the cache in {Time.GetTicksMsec() - started} ms" : $"Baked {_sprites.Count} sprites from {work.Count} renders in {Time.GetTicksMsec() - started} ms");
    }

    private static void Store(Dictionary<(string, SimDir), (Image? Shadow, Image[] Frames, Image[] Glow)> images, ModelDef m, SimDir rot, BakePass pass, int frame, Image img)
    {
        if (images.TryGetValue((m.Id, rot), out (Image? Shadow, Image[] Frames, Image[] Glow) set) == false)
        {
            set = (null, new Image[m.Frames], new Image[m.Glows ? m.Frames : 0]);
        }

        switch (pass)
        {
            case BakePass.Shadow:
                set.Shadow = img;
                break;
            case BakePass.Glow:
                set.Glow[frame] = img;
                break;
            default:
                set.Frames[frame] = img;
                break;
        }

        images[(m.Id, rot)] = set;
    }

    /// <summary>
    /// The cache folder for this build of the game: baked images are reused until the code changes (a new
    /// build has a new module id), and older builds' folders are removed. FACTORY_ART_REBAKE=1 ignores it.
    /// </summary>
    private static string CacheDir()
    {
        string build = typeof(Models).Assembly.ManifestModule.ModuleVersionId.ToString("N");
        string root = "user://art";
        DirAccess.MakeDirRecursiveAbsolute($"{root}/{build}");
        foreach (string old in DirAccess.GetDirectoriesAt(root).Where(d => d != build))
        {
            foreach (string f in DirAccess.GetFilesAt($"{root}/{old}"))
            {
                DirAccess.RemoveAbsolute($"{root}/{old}/{f}");
            }

            DirAccess.RemoveAbsolute($"{root}/{old}");
        }

        if (System.Environment.GetEnvironmentVariable("FACTORY_ART_REBAKE") == "1")
        {
            foreach (string f in DirAccess.GetFilesAt($"{root}/{build}"))
            {
                DirAccess.RemoveAbsolute($"{root}/{build}/{f}");
            }
        }

        return $"{root}/{build}";
    }

    private static string CachePath(string dir, string id, SimDir rot, BakePass pass, int frame) => $"{dir}/{id}-{rot}-{pass}-{frame}.png";

    /// <summary>Every model's first frame with its shadow, on a patch of ground, in one image.</summary>
    private static void ContactSheet(Dictionary<(string, SimDir), (Image? Shadow, Image[] Frames, Image[] Glow)> images, string dir)
    {
        List<(Image? Shadow, Image Frame)> cells = [.. images.Where(kv => kv.Key.Item2 is SimDir.North or SimDir.East).Select(kv => (kv.Value.Shadow, kv.Value.Frames[0]))];
        int cell = 400, cols = 5, rows = (cells.Count + cols - 1) / cols;
        Image sheet = Image.CreateEmpty(cell * cols, cell * rows, false, Image.Format.Rgba8);
        sheet.Fill(new Color(0.34f, 0.3f, 0.21f));
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2I at = new Vector2I(i % cols * cell, i / cols * cell) + new Vector2I(10, 10);
            if (cells[i].Shadow is { } sh)
            {
                sheet.BlendRect(sh, new Rect2I(Vector2I.Zero, sh.GetSize()), at);
            }

            sheet.BlendRect(cells[i].Frame, new Rect2I(Vector2I.Zero, cells[i].Frame.GetSize()), at);
        }

        sheet.SavePng(System.IO.Path.Combine(dir, "_sheet.png"));
    }

    /// <summary>A model point turned to a facing and projected to tiles from the footprint's top-left.</summary>
    private static Vector2 Turned(ModelDef m, SimDir rot, Vector3 p)
    {
        Vector3 centre = new Vector3(m.W / 2f, 0, m.H / 2f);
        float yaw = rot switch { SimDir.East => -Mathf.Pi / 2, SimDir.South => Mathf.Pi, SimDir.West => Mathf.Pi / 2, _ => 0 };
        Vector3 q = centre + (new Basis(Vector3.Up, yaw) * (p - centre));

        return new Vector2(q.X, q.Z - Rise(q.Y));
    }

    private static ImageTexture Texture(Image img)
    {
        img.GenerateMipmaps();

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>The image's top-left and size in tiles relative to the footprint's top-left, with room for the shadow.</summary>
    private static (Vector2 Offset, Vector2 Size) Frame(ModelDef m)
    {
        float k = 1f / Mathf.Cos(Mathf.DegToRad(Tilt));
        float shadowX = m.Height * (_sun.X / -_sun.Y);
        float shadowZ = m.Height * (_sun.Z / -_sun.Y) / k;
        float uMin = -Margin, uMax = m.W + Margin + shadowX;
        float vMax = Rise(m.Height) + Margin, vMin = -m.H - Margin - shadowZ;
        Vector2 size = new Vector2(Mathf.Ceil((uMax - uMin) * PxPerTile), Mathf.Ceil((vMax - vMin) * PxPerTile)) / PxPerTile;

        return (new Vector2(uMin, -vMax), size);
    }

    /// <summary>Builds the scene for one render: camera, lights, environment and the model.</summary>
    private SubViewport Stage(ModelDef m, SimDir rot, BakePass pass, int frame)
    {
        (Vector2 offset, Vector2 size) = Frame(m);
        Vector2I px = new Vector2I(Mathf.RoundToInt(size.X * PxPerTile), Mathf.RoundToInt(size.Y * PxPerTile));
        SubViewport view = new SubViewport
        {
            Size = px,
            TransparentBg = pass != BakePass.Shadow,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa8X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };

        float tilt = Mathf.DegToRad(Tilt);
        float k = 1f / Mathf.Cos(tilt);
        Vector3 right = Vector3.Right, up = new Vector3(0, Mathf.Sin(tilt), -Mathf.Cos(tilt)), back = new Vector3(0, Mathf.Cos(tilt), Mathf.Sin(tilt));
        float uc = offset.X + (size.X / 2), vc = -offset.Y - (size.Y / 2);
        Camera3D cam = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = size.Y,
            Near = 0.05f,
            Far = 400,
            Transform = new Transform3D(new Basis(right, up, back), (right * uc) + (up * vc) + (back * 100)),
            Environment = Environment(pass),
        };
        view.AddChild(cam);

        DirectionalLight3D sun = new DirectionalLight3D
        {
            LightEnergy = pass == BakePass.Shadow ? 1f : 1.7f,
            LightColor = new Color(1f, 0.93f, 0.82f),
            ShadowEnabled = pass != BakePass.Glow,
            ShadowBlur = 2f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            DirectionalShadowMaxDistance = 60,
        };
        sun.Transform = new Transform3D(Basis.LookingAt(_sun, Vector3.Up), Vector3.Zero);
        view.AddChild(sun);

        // The model lives under a node that stretches north-south (so the footprint projects square)
        // and turns it about its centre for its facing.
        Node3D stretch = new Node3D { Scale = new Vector3(1, 1, k) };
        view.AddChild(stretch);
        Vector3 centre = new Vector3(m.W / 2f, 0, m.H / 2f);
        float yaw = rot switch { SimDir.East => -Mathf.Pi / 2, SimDir.South => Mathf.Pi, SimDir.West => Mathf.Pi / 2, _ => 0 };
        Basis turn = new Basis(Vector3.Up, yaw);
        Node3D model = new Node3D { Transform = new Transform3D(turn, centre - (turn * centre)) };
        stretch.AddChild(model);
        if (pass == BakePass.Shadow)
        {
            stretch.AddChild(new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(m.W + 20, m.H + 20) },
                Position = centre,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.White, Roughness = 1, MetallicSpecular = 0 },
            });
        }

        m.Build(new Kit(model, pass, m.Frames > 1 ? frame / (float)m.Frames : 0), Accent(m));

        return view;
    }

    private Color Accent(ModelDef m) => Root.Content.Buildings.TryGetValue(m.Id, out Factory.Sim.BuildingDef? def) ? WorldView.Hex(def.Color) : new Color(0.3f, 0.8f, 1f);

    private static readonly Dictionary<BakePass, Godot.Environment> _envs = [];

    private static Godot.Environment Environment(BakePass pass)
    {
        if (_envs.TryGetValue(pass, out Godot.Environment? env))
        {
            return env;
        }

        Sky sky = new Sky
        {
            SkyMaterial = new ProceduralSkyMaterial
            {
                SkyTopColor = new Color(0.36f, 0.5f, 0.78f),
                SkyHorizonColor = new Color(0.62f, 0.68f, 0.76f),
                GroundHorizonColor = new Color(0.5f, 0.46f, 0.4f),
                GroundBottomColor = new Color(0.26f, 0.22f, 0.18f),
            },
        };
        env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            Sky = sky,
            AmbientLightSource = pass == BakePass.Shadow ? Godot.Environment.AmbientSource.Color : Godot.Environment.AmbientSource.Sky,
            AmbientLightColor = Colors.White,
            AmbientLightEnergy = pass == BakePass.Shadow ? 0.45f : 0.6f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = 1f,
            TonemapWhite = 3f,
            SsaoEnabled = pass != BakePass.Glow,
            SsaoRadius = 0.5f,
            SsaoIntensity = 2.2f,
            SsaoDetail = 0.6f,
        };
        _envs[pass] = env;

        return env;
    }

    /// <summary>Viewports blend edges against transparent black; divide that back out so edges aren't dark fringed.</summary>
    private static Image Unpremultiply(Image img)
    {
        img.Convert(Image.Format.Rgba8);
        byte[] d = img.GetData();
        for (int i = 0; i < d.Length; i += 4)
        {
            int a = d[i + 3];
            if ((a > 0) && (a < 255))
            {
                d[i] = (byte)System.Math.Min(255, d[i] * 255 / a);
                d[i + 1] = (byte)System.Math.Min(255, d[i + 1] * 255 / a);
                d[i + 2] = (byte)System.Math.Min(255, d[i + 2] * 255 / a);
            }
        }

        return Image.CreateFromData(img.GetWidth(), img.GetHeight(), false, Image.Format.Rgba8, d);
    }

    /// <summary>White lit ground → transparent; shadowed or occluded ground → translucent black.</summary>
    private static Image ToShadow(Image img)
    {
        img.Convert(Image.Format.Rgba8);
        byte[] d = img.GetData();
        int max = 1;
        for (int i = 0; i < d.Length; i += 4)
        {
            max = System.Math.Max(max, (d[i] + d[i + 1] + d[i + 2]) / 3);
        }

        for (int i = 0; i < d.Length; i += 4)
        {
            float lum = (d[i] + d[i + 1] + d[i + 2]) / 3f / max;
            float a = Mathf.Clamp((1 - lum) * 1.8f, 0, 1) * 0.72f;
            d[i] = 0;
            d[i + 1] = 0;
            d[i + 2] = 0;
            d[i + 3] = (byte)(a * 255);
        }

        return Image.CreateFromData(img.GetWidth(), img.GetHeight(), false, Image.Format.Rgba8, d);
    }

    /// <summary>Black → transparent; colours keep their hue at full strength with alpha from their brightness.</summary>
    private static Image ToGlow(Image img)
    {
        img.Convert(Image.Format.Rgba8);
        byte[] d = img.GetData();
        for (int i = 0; i < d.Length; i += 4)
        {
            int m = System.Math.Max(d[i], System.Math.Max(d[i + 1], d[i + 2]));
            int a = m * d[i + 3] / 255;
            if (m > 0)
            {
                d[i] = (byte)(d[i] * 255 / m);
                d[i + 1] = (byte)(d[i + 1] * 255 / m);
                d[i + 2] = (byte)(d[i + 2] * 255 / m);
            }

            d[i + 3] = (byte)a;
        }

        return Image.CreateFromData(img.GetWidth(), img.GetHeight(), false, Image.Format.Rgba8, d);
    }
}
