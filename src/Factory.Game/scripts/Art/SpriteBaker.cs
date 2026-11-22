using Godot;
using SimDir = Factory.Sim.Dir;

namespace Factory.Game.Art;

/// <summary>A building's baked images and where each sits relative to its footprint, in tiles.</summary>
public sealed class BakedSprite
{
    /// <summary>The lit model, one image per animation frame (one if it doesn't move).</summary>
    public Texture2D[] Frames { get; init; } = [];

    /// <summary>The light it gives off while working, per frame, drawn additively; empty if it gives none.</summary>
    public Texture2D[] Lights { get; init; } = [];

    /// <summary>Top-left of the light images.</summary>
    public Vector2 LightOffset { get; init; }

    /// <summary>Size of the light images.</summary>
    public Vector2 LightSize { get; init; }

    /// <summary>Whether its light flickers like fire.</summary>
    public bool Flicker { get; init; }

    /// <summary>Where smoke, steam, sparks or dust come from while it works, in tiles from the footprint's top-left.</summary>
    public (Vector2 At, Emit Kind)[] Emitters { get; init; } = [];

    /// <summary>The cast shadow per frame: a white mask the world tints and draws into one shared shadow layer.</summary>
    public Texture2D[] Shadows { get; init; } = [];

    /// <summary>A soft, ragged mask of scuffed ground under and around the building, tinted by the world.</summary>
    public Texture2D? Decal { get; init; }

    /// <summary>Top-left of the frames, relative to the footprint's top-left corner.</summary>
    public Vector2 Offset { get; init; }

    /// <summary>Size of the frames.</summary>
    public Vector2 Size { get; init; }

    /// <summary>Top-left of the shadow images.</summary>
    public Vector2 ShadowOffset { get; init; }

    /// <summary>Size of the shadow images.</summary>
    public Vector2 ShadowSize { get; init; }

    /// <summary>Top-left of the ground decal.</summary>
    public Vector2 DecalOffset { get; init; }

    /// <summary>Size of the ground decal.</summary>
    public Vector2 DecalSize { get; init; }

    /// <summary>Animation frames per second while working.</summary>
    public float Fps { get; init; }

    /// <summary>Where wires attach, in tiles from the footprint's top-left (poles only).</summary>
    public Vector2? Anchor { get; init; }

    /// <summary>The animation frame to show.</summary>
    /// <param name="time">Game clock in seconds.</param>
    /// <param name="seed">Per-building offset so neighbours don't move in step.</param>
    /// <param name="working">Whether the machine is running; idle machines hold still.</param>
    /// <returns>Frame index.</returns>
    public int FrameAt(float time, int seed, bool working) => (working && (Frames.Length > 1)) ? (int)((time * Fps) + seed) % Frames.Length : 0;
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

    private const int Batch = 32;
    private const float Margin = 0.2f;
    private const float DecalMargin = 0.8f;
    private const int DecalPx = 40;
    private const int LightPx = 40;

    /// <summary>
    /// The sun's direction in model space (x east, y up, z south): low and from the west-north-west, so
    /// shadows fall about one height long toward the screen's lower right. Trees, ore and units use the same.
    /// </summary>
    public static readonly Vector3 Sun = new Vector3(0.9f, -1f, 0.35f).Normalized();

    /// <summary>How far a shadow falls per tile of height, east and south, in tiles.</summary>
    public static Vector2 ShadowLean => new(Sun.X / -Sun.Y, Sun.Z / -Sun.Y);

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
                work.Add((m, rot, BakePass.Footprint, 0));
                for (int f = 0; f < m.Frames; f++)
                {
                    work.Add((m, rot, BakePass.Color, f));
                    work.Add((m, rot, BakePass.Shadow, f));
                    if (m.Glows)
                    {
                        work.Add((m, rot, BakePass.Light, f));
                    }
                }
            }
        }

        Dictionary<(string, SimDir), Images> images = [];
        string cache = CacheDir();
        bool cached = work.All(w => Godot.FileAccess.FileExists(CachePath(cache, w.Model.Id, w.Rot, w.Pass, w.Frame)));
        if (cached)
        {
            foreach ((ModelDef m, SimDir rot, BakePass pass, int frame) in work)
            {
                Image img = ResourceLoader.Load<Image>(CachePath(cache, m.Id, rot, pass, frame), null, ResourceLoader.CacheMode.Ignore);
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
                Image img = job.Pass switch { BakePass.Shadow => ToMask(raw), BakePass.Footprint => ToDecal(raw), BakePass.Light => ToLight(raw), _ => Unpremultiply(raw) };
                Pack(img);
                ResourceSaver.Save(img, CachePath(cache, job.Model.Id, job.Rot, job.Pass, job.Frame), ResourceSaver.SaverFlags.Compress);
                Store(images, job.Model, job.Rot, job.Pass, job.Frame, img);
            }
        }

        // FACTORY_ART_DUMP=<dir> saves every baked image, to look at the art without running the game.
        string? dump = System.Environment.GetEnvironmentVariable("FACTORY_ART_DUMP");
        foreach (((string id, SimDir rot), Images set) in images)
        {
            if (dump is not null)
            {
                Plain(set.Frames[0]).SavePng(System.IO.Path.Combine(dump, $"{id}-{rot}.png"));
                Plain(set.Shadows[0]).SavePng(System.IO.Path.Combine(dump, $"{id}-{rot}-shadow.png"));
                (set.Decal is null ? null : Plain(set.Decal))?.SavePng(System.IO.Path.Combine(dump, $"{id}-{rot}-decal.png"));
                if (set.Lights.Length > 0)
                {
                    Plain(set.Lights[0]).SavePng(System.IO.Path.Combine(dump, $"{id}-{rot}-light.png"));
                }
            }

            ModelDef m = Models.All.First(x => x.Id == id);
            Rect2 frame = Area(m, BakePass.Color), shadow = Area(m, BakePass.Shadow), decal = Area(m, BakePass.Footprint), light = Area(m, BakePass.Light);
            _sprites[(id, rot)] = new BakedSprite
            {
                Frames = [.. set.Frames.Select(Texture)],
                Lights = Textures(set.Lights),
                Shadows = Textures(set.Shadows),
                LightOffset = light.Position,
                LightSize = light.Size,
                Flicker = m.Flicker,
                Emitters = [.. m.Emitters.Select(e => (Turned(m, rot, e.At), e.Kind))],
                Decal = set.Decal is null ? null : Texture(set.Decal),
                Offset = frame.Position,
                Size = frame.Size,
                ShadowOffset = shadow.Position,
                ShadowSize = shadow.Size,
                DecalOffset = decal.Position,
                DecalSize = decal.Size,
                Fps = m.Fps,
                Anchor = m.Anchor is { } a ? new Vector2(a.X, a.Z - Rise(a.Y)) : null,
            };
        }

        if (dump is not null)
        {
            ContactSheet(images, dump);
        }

        long bytes = _sprites.Values.SelectMany(sp => sp.Frames.Concat(sp.Shadows).Concat(sp.Lights).Append(sp.Decal)).OfType<Texture2D>().Distinct()
            .Sum(t => (long)t.GetWidth() * t.GetHeight() * 4 * 4 / 3 / (t.GetImage().IsCompressed() ? 4 : 1));
        GD.Print($"Baked art uses about {bytes / (1024 * 1024)} MB of textures");
        Baked = true;
        GD.Print(cached ? $"Loaded {_sprites.Count} baked sprites from the cache in {Time.GetTicksMsec() - started} ms" : $"Baked {_sprites.Count} sprites from {work.Count} renders in {Time.GetTicksMsec() - started} ms");
    }

    /// <summary>One model facing's images, filled in as renders finish.</summary>
    private sealed class Images(ModelDef m)
    {
        public Image[] Frames { get; } = new Image[m.Frames];

        public Image[] Shadows { get; } = new Image[m.Frames];

        public Image[] Lights { get; } = new Image[m.Glows ? m.Frames : 0];

        public Image? Decal { get; set; }
    }

    private static void Store(Dictionary<(string, SimDir), Images> images, ModelDef m, SimDir rot, BakePass pass, int frame, Image img)
    {
        if (images.TryGetValue((m.Id, rot), out Images? set) == false)
        {
            set = new Images(m);
            images[(m.Id, rot)] = set;
        }

        switch (pass)
        {
            case BakePass.Shadow:
                set.Shadows[frame] = img;
                break;
            case BakePass.Footprint:
                set.Decal = img;
                break;
            case BakePass.Light:
                set.Lights[frame] = img;
                break;
            default:
                set.Frames[frame] = img;
                break;
        }
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

    private static string CachePath(string dir, string id, SimDir rot, BakePass pass, int frame) => $"{dir}/{id}-{rot}-{pass}-{frame}.res";

    /// <summary>Every model's first frame with its decal and shadow, on a patch of ground, in one image.</summary>
    private static void ContactSheet(Dictionary<(string, SimDir), Images> images, string dir)
    {
        List<(ModelDef M, Images S)> cells = [.. images.Where(kv => kv.Key.Item2 is SimDir.North or SimDir.East).Select(kv => (Models.All.First(x => x.Id == kv.Key.Item1), kv.Value))];
        int cell = 440, cols = 5, rows = (cells.Count + cols - 1) / cols;
        Image sheet = Image.CreateEmpty(cell * cols, cell * rows, false, Image.Format.Rgba8);
        sheet.Fill(new Color(0.36f, 0.31f, 0.22f));
        for (int i = 0; i < cells.Count; i++)
        {
            (ModelDef m, Images set) = cells[i];
            Vector2 origin = new Vector2(i % cols * cell, i / cols * cell) + new Vector2(60, 60);
            Vector2I At(BakePass pass) => (Vector2I)(origin + (Area(m, pass).Position * PxPerTile));
            if (set.Decal is { } decal)
            {
                Image big = Plain(decal);
                big.Resize(decal.GetWidth() * PxPerTile / DecalPx, decal.GetHeight() * PxPerTile / DecalPx, Image.Interpolation.Bilinear);
                Tint(big, new Color(0.13f, 0.1f, 0.06f), 1f);
                sheet.BlendRect(big, new Rect2I(Vector2I.Zero, big.GetSize()), At(BakePass.Footprint));
            }

            Image sh = Plain(set.Shadows[0]);
            Tint(sh, new Color(0.1f, 0.08f, 0.04f), 0.55f);
            sheet.BlendRect(sh, new Rect2I(Vector2I.Zero, sh.GetSize()), At(BakePass.Shadow));
            Image frame = Plain(set.Frames[0]);
            frame.Convert(Image.Format.Rgba8);
            sheet.BlendRect(frame, new Rect2I(Vector2I.Zero, frame.GetSize()), At(BakePass.Color));
        }

        sheet.SavePng(System.IO.Path.Combine(dir, "_sheet.png"));
    }

    /// <summary>Recolours a white mask to <paramref name="c"/>, scaling its alpha.</summary>
    private static void Tint(Image img, Color c, float alpha)
    {
        img.Convert(Image.Format.Rgba8);
        for (int y = 0; y < img.GetHeight(); y++)
        {
            for (int x = 0; x < img.GetWidth(); x++)
            {
                img.SetPixel(x, y, new Color(c.R, c.G, c.B, img.GetPixel(x, y).A * alpha));
            }
        }
    }

    /// <summary>A model point turned to a facing and projected to tiles from the footprint's top-left.</summary>
    private static Vector2 Turned(ModelDef m, SimDir rot, Vector3 p)
    {
        Vector3 centre = new Vector3(m.W / 2f, 0, m.H / 2f);
        float yaw = rot switch { SimDir.East => -Mathf.Pi / 2, SimDir.South => Mathf.Pi, SimDir.West => Mathf.Pi / 2, _ => 0 };
        Vector3 q = centre + (new Basis(Vector3.Up, yaw) * (p - centre));

        return new Vector2(q.X, q.Z - Rise(q.Y));
    }

    /// <summary>Textures for a run of frames, sharing one texture between frames whose images are identical.</summary>
    private static Texture2D[] Textures(Image[] frames)
    {
        Texture2D[] result = new Texture2D[frames.Length];
        List<(byte[] Data, Texture2D Tex)> seen = [];
        for (int i = 0; i < frames.Length; i++)
        {
            byte[] data = frames[i].GetData();
            Texture2D? same = seen.FirstOrDefault(s => s.Data.AsSpan().SequenceEqual(data)).Tex;
            if (same is null)
            {
                same = Texture(frames[i]);
                seen.Add((data, same));
            }

            result[i] = same;
        }

        return result;
    }

    /// <summary>
    /// Readies a baked image for the GPU: mipmaps, and colour and light images block-compressed (a quarter of
    /// the memory) where the GPU supports it. Done before caching, so loading from the cache is quick.
    /// </summary>
    private static void Pack(Image img)
    {
        img.GenerateMipmaps();
        if ((img.GetFormat() != Image.Format.La8) && RenderingServer.HasOsFeature("s3tc"))
        {
            img.Compress(Image.CompressMode.S3Tc);
        }
    }

    /// <summary>An uncompressed, mipmap-free copy, for saving and compositing.</summary>
    private static Image Plain(Image img)
    {
        Image copy = (Image)img.Duplicate();
        if (copy.IsCompressed())
        {
            copy.Decompress();
        }

        copy.ClearMipmaps();

        return copy;
    }

    private static ImageTexture Texture(Image img) => ImageTexture.CreateFromImage(img);

    /// <summary>The ground area each pass's image covers, in tiles relative to the footprint's top-left (screen axes).</summary>
    private static Rect2 Area(ModelDef m, BakePass pass)
    {
        // A ground point (x, z) lands at screen (x, z); a point h up rises Rise(h) up the screen.
        (float u0, float v0, float u1, float v1, int px) = pass switch
        {
            BakePass.Shadow => (-Margin, -Margin, m.W + Margin + (m.Height * ShadowLean.X), m.H + Margin + (m.Height * ShadowLean.Y), PxPerTile),
            BakePass.Footprint => (-DecalMargin, -DecalMargin, m.W + DecalMargin, m.H + DecalMargin, DecalPx),
            BakePass.Light => (-m.Reach, -m.Reach - Rise(m.Height), m.W + m.Reach, m.H + m.Reach, LightPx),
            _ => (-Margin, -Margin - Rise(m.Height), m.W + Margin, m.H + Margin, PxPerTile),
        };
        Vector2 size = new Vector2(Mathf.Ceil((u1 - u0) * px), Mathf.Ceil((v1 - v0) * px)) / px;

        return new Rect2(u0, v0, size);
    }

    private static int PxOf(BakePass pass) => pass switch { BakePass.Footprint => DecalPx, BakePass.Light => LightPx, _ => PxPerTile };

    /// <summary>Builds the scene for one render: camera, lights, environment and the model.</summary>
    private SubViewport Stage(ModelDef m, SimDir rot, BakePass pass, int frame)
    {
        Rect2 area = Area(m, pass);
        int ppt = PxOf(pass);
        Vector2I px = new Vector2I(Mathf.RoundToInt(area.Size.X * ppt), Mathf.RoundToInt(area.Size.Y * ppt));
        bool mask = pass is BakePass.Shadow or BakePass.Footprint;
        SubViewport view = new SubViewport
        {
            Size = px,
            TransparentBg = pass != BakePass.Light,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa8X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };

        // Screen v (down) is -dot(p, up); the camera looks along -back from 30 tiles away.
        float tilt = Mathf.DegToRad(Tilt);
        float k = 1f / Mathf.Cos(tilt);
        Vector3 right = Vector3.Right, up = new Vector3(0, Mathf.Sin(tilt), -Mathf.Cos(tilt)), back = new Vector3(0, Mathf.Cos(tilt), Mathf.Sin(tilt));
        Vector2 c = area.GetCenter();
        Camera3D cam = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = area.Size.Y,
            Near = 0.05f,
            Far = 80,
            Transform = new Transform3D(new Basis(right, up, back), (right * c.X) + (up * -c.Y) + (back * 30)),
            Environment = Environment(pass),
        };
        view.AddChild(cam);
        if (pass == BakePass.Color)
        {
            Lights(view, k, pass);
        }

        // The model lives under a node that stretches north-south (so the footprint projects square). For the
        // masks, a shear first flattens it onto the ground: along the sun for the shadow, straight down for
        // the footprint. Inside, it turns about its centre for its facing.
        Node3D stretch = new Node3D { Scale = new Vector3(1, 1, k) };
        view.AddChild(stretch);
        Node3D parent = stretch;
        if (mask)
        {
            Vector2 lean = pass == BakePass.Shadow ? ShadowLean : Vector2.Zero;
            Node3D flatten = new Node3D { Transform = new Transform3D(new Basis(Vector3.Right, new Vector3(lean.X, 0.002f, lean.Y), Vector3.Back), Vector3.Zero) };
            stretch.AddChild(flatten);
            parent = flatten;
        }

        Vector3 centre = new Vector3(m.W / 2f, 0, m.H / 2f);
        float yaw = rot switch { SimDir.East => -Mathf.Pi / 2, SimDir.South => Mathf.Pi, SimDir.West => Mathf.Pi / 2, _ => 0 };
        Basis turn = new Basis(Vector3.Up, yaw);
        Node3D model = new Node3D { Transform = new Transform3D(turn, centre - (turn * centre)) };
        parent.AddChild(model);
        Kit kit = new Kit(model, pass, m.Frames > 1 ? frame / (float)m.Frames : 0);
        m.Build(kit, Accent(m));
        if (pass == BakePass.Light)
        {
            // The machine's own lamps and fires, lighting it and a patch of ground (stretched like the model).
            stretch.AddChild(new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(m.W + 12, m.H + 12) },
                Position = centre,
                MaterialOverride = Palette.Paint(new Color(0.5f, 0.44f, 0.36f), 0f, 1f),
            });
            foreach (Kit.PointLight l in kit.Lights)
            {
                Vector3 p = model.Transform * l.At;
                view.AddChild(new OmniLight3D
                {
                    Position = new Vector3(p.X, p.Y, p.Z * k),
                    LightColor = l.Color,
                    LightEnergy = l.Energy,
                    OmniRange = l.Range,
                    OmniAttenuation = 1.4f,
                    ShadowEnabled = l.Shadows,
                });
            }
        }

        return view;
    }

    /// <summary>
    /// The light rig: a warm, fairly low sun casting crisp shadows, and a faint cool rim light from the
    /// opposite side to pick out silhouettes. Ambient light comes from the sky above and warm ground below.
    /// </summary>
    private static void Lights(SubViewport view, float k, BakePass pass)
    {
        bool glow = pass == BakePass.Light;
        Vector3 sunWorld = new Vector3(Sun.X, Sun.Y, Sun.Z * k).Normalized();
        DirectionalLight3D sun = new DirectionalLight3D
        {
            LightEnergy = 1.35f,
            LightColor = new Color(1f, 0.9f, 0.74f),
            ShadowEnabled = glow == false,
            ShadowBlur = 0.6f,
            ShadowBias = 0.02f,
            ShadowNormalBias = 0.6f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            DirectionalShadowMaxDistance = 50,
        };
        sun.Transform = new Transform3D(Basis.LookingAt(sunWorld, Vector3.Up), Vector3.Zero);
        view.AddChild(sun);
        DirectionalLight3D rim = new DirectionalLight3D
        {
            LightEnergy = 0.25f,
            LightColor = new Color(0.7f, 0.8f, 1f),
            ShadowEnabled = false,
        };
        rim.Transform = new Transform3D(Basis.LookingAt(new Vector3(-0.75f, -0.45f, -0.5f).Normalized(), Vector3.Up), Vector3.Zero);
        view.AddChild(rim);
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
                SkyTopColor = new Color(0.4f, 0.41f, 0.44f),
                SkyHorizonColor = new Color(0.5f, 0.5f, 0.5f),
                GroundHorizonColor = new Color(0.48f, 0.4f, 0.3f),
                GroundBottomColor = new Color(0.36f, 0.29f, 0.2f),
            },
        };
        if (pass == BakePass.Light)
        {
            env = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Colors.Black,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.Black,
                AmbientLightEnergy = 0,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
            };
            _envs[pass] = env;

            return env;
        }

        env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            Sky = sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.55f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = 1f,
            TonemapWhite = 4f,
            SsaoEnabled = pass == BakePass.Color,
            SsaoRadius = 0.22f,
            SsaoIntensity = 2.6f,
            SsaoPower = 1.6f,
            SsaoDetail = 0.9f,
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

    /// <summary>The render's coverage as a white mask (white, with alpha), two bytes a pixel.</summary>
    private static Image ToMask(Image img)
    {
        img.Convert(Image.Format.Rgba8);
        byte[] d = img.GetData();
        byte[] o = new byte[d.Length / 2];
        for (int i = 0, j = 0; i < d.Length; i += 4, j += 2)
        {
            o[j] = 255;
            o[j + 1] = d[i + 3];
        }

        return Image.CreateFromData(img.GetWidth(), img.GetHeight(), false, Image.Format.La8, o);
    }

    /// <summary>
    /// The ground decal from the footprint mask: spread out and softened past the edges, strongest right at
    /// the contact, and broken up by noise so it reads as scuffed, trodden earth rather than a halo.
    /// </summary>
    private static Image ToDecal(Image img)
    {
        img.Convert(Image.Format.Rgba8);
        int w = img.GetWidth(), h = img.GetHeight();
        byte[] d = img.GetData();
        float[] a = new float[w * h];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = d[(i * 4) + 3] / 255f;
        }

        // How close each pixel is to the footprint, blurred out to about a third of a tile, then cut at a
        // noisy threshold so the patch has a ragged, trodden edge instead of a soft halo.
        float[] spread = Blur(a, w, h, 9, 2);
        uint seed = (uint)((w * 31) + h);
        byte[] o = new byte[w * h * 2];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w) + x;
                float n = (0.6f * Noise2(x / 5f, y / 5f, seed)) + (0.4f * Noise2(x / 1.6f, y / 1.6f, seed + 7));
                float edge = 0.1f + (n * 0.38f);
                float v = Mathf.SmoothStep(edge - 0.04f, edge + 0.04f, spread[i]);
                float grain = 0.75f + (0.25f * Noise2(x / 0.8f, y / 0.8f, seed + 13));
                o[i * 2] = 255;
                o[(i * 2) + 1] = (byte)(Mathf.Clamp(v * grain, 0, 1) * 0.8f * 255);
            }
        }

        return Image.CreateFromData(w, h, false, Image.Format.La8, o);
    }

    private static float[] Blur(float[] src, int w, int h, int radius, int passes)
    {
        float[] a = (float[])src.Clone(), b = new float[a.Length];
        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        sum += a[(y * w) + System.Math.Clamp(x + dx, 0, w - 1)];
                    }

                    b[(y * w) + x] = sum / ((2 * radius) + 1);
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        sum += b[(System.Math.Clamp(y + dy, 0, h - 1) * w) + x];
                    }

                    a[(y * w) + x] = sum / ((2 * radius) + 1);
                }
            }
        }

        return a;
    }

    private static float Noise2(float x, float y, uint seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float tx = x - x0, ty = y - y0;
        tx = tx * tx * (3 - (2 * tx));
        ty = ty * ty * (3 - (2 * ty));
        float C(int cx, int cy) => (GroundPainter.Hash(cx, cy, seed) & 0xffff) / 65535f;

        return Mathf.Lerp(Mathf.Lerp(C(x0, y0), C(x0 + 1, y0), tx), Mathf.Lerp(C(x0, y0 + 1), C(x0 + 1, y0 + 1), tx), ty);
    }

    /// <summary>The light render as plain colour; black adds nothing when drawn additively.</summary>
    private static Image ToLight(Image img)
    {
        img.Convert(Image.Format.Rgb8);

        return img;
    }
}
