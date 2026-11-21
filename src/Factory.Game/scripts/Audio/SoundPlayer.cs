using Factory.Sim;
using Godot;

namespace Factory.Game.Audio;

/// <summary>
/// Plays the game's sounds: interface clicks, world events near the camera, the commander's pick, and
/// three ambient loops (wind, a quiet drone, and machinery that swells with the number of working
/// machines in view). Sound effects and ambience run on their own buses so the options can set them apart.
/// </summary>
public partial class SoundPlayer : Node
{
    /// <summary>Bus for one-shot effects.</summary>
    public const string SfxBus = "Sfx";

    /// <summary>Bus for the ambient loops.</summary>
    public const string AmbientBus = "Ambient";

    private const int Voices = 14;

    private readonly Dictionary<string, AudioStreamWav> _sounds = [];
    private readonly Dictionary<string, double> _lastPlayed = [];
    private readonly List<AudioStreamPlayer> _voices = [];
    private readonly System.Random _rng = new();
    private int _next;
    private AudioStreamPlayer _hum = null!;
    private float _humLevel;
    private double _mineTimer;
    private double _scanTimer;
    private int _working;

    /// <summary>The game root, for the camera, the planet in view and the commander.</summary>
    public GameRoot Root { get; set; } = null!;

    /// <summary>Creates the effect and ambient buses if they are missing and applies the saved volumes.</summary>
    public static void ApplyVolumes()
    {
        foreach (string bus in new[] { SfxBus, AmbientBus })
        {
            if (AudioServer.GetBusIndex(bus) < 0)
            {
                AudioServer.AddBus();
                int index = AudioServer.BusCount - 1;
                AudioServer.SetBusName(index, bus);
                AudioServer.SetBusSend(index, "Master");
            }
        }

        AudioServer.SetBusVolumeDb(0, Db(GameSettings.MasterVolume));
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(SfxBus), Db(GameSettings.SfxVolume));
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(AmbientBus), Db(GameSettings.AmbientVolume));
    }

    private static float Db(float linear) => linear <= 0.001f ? -80f : Mathf.LinearToDb(linear);

    /// <inheritdoc />
    public override void _Ready()
    {
        ApplyVolumes();
        foreach (string name in Synth.OneShots)
        {
            _sounds[name] = Synth.Make(name);
        }

        // FACTORY_SOUND_DUMP=<dir> writes every sound to WAV files, to listen to or measure outside the game.
        if (System.Environment.GetEnvironmentVariable("FACTORY_SOUND_DUMP") is { } dump)
        {
            foreach (string name in Synth.OneShots.Concat(["wind", "pad", "hum"]))
            {
                Synth.Make(name).SaveToWav(System.IO.Path.Combine(dump, name + ".wav"));
            }
        }

        for (int i = 0; i < Voices; i++)
        {
            AudioStreamPlayer p = new AudioStreamPlayer { Bus = SfxBus };
            AddChild(p);
            _voices.Add(p);
        }

        Ambient("wind", -16f);
        Ambient("pad", -20f);
        _hum = Ambient("hum", -80f);

        // Every button in the interface clicks, including ones built later.
        GetTree().NodeAdded += OnNodeAdded;
        foreach (Node n in GetTree().Root.FindChildren("*", "BaseButton", true, false))
        {
            OnNodeAdded(n);
        }
    }

    /// <inheritdoc />
    public override void _ExitTree() => GetTree().NodeAdded -= OnNodeAdded;

    private AudioStreamPlayer Ambient(string name, float db)
    {
        AudioStreamPlayer p = new AudioStreamPlayer { Bus = AmbientBus, Stream = Synth.Make(name), VolumeDb = db, Autoplay = true };
        AddChild(p);

        return p;
    }

    private void OnNodeAdded(Node n)
    {
        if (n is BaseButton b)
        {
            b.Pressed += () => Play("click", 0.7f);
        }
    }

    /// <summary>Plays a one-shot sound.</summary>
    /// <param name="name">One of <see cref="Synth.OneShots"/>.</param>
    /// <param name="volume">Linear volume, 1 for full.</param>
    /// <param name="pitchJitter">Random pitch spread, so repeated sounds don't sound identical.</param>
    /// <param name="minGap">Seconds that must pass before this sound plays again; extra requests are dropped.</param>
    public void Play(string name, float volume = 1f, float pitchJitter = 0.04f, double minGap = 0.05)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if ((volume <= 0.01f) || (_sounds.TryGetValue(name, out AudioStreamWav? stream) == false)
            || (_lastPlayed.TryGetValue(name, out double last) && (now - last < minGap)))
        {
            return;
        }

        _lastPlayed[name] = now;
        AudioStreamPlayer voice = _voices[_next];
        _next = (_next + 1) % _voices.Count;
        voice.Stream = stream;
        voice.VolumeDb = Mathf.LinearToDb(volume);
        voice.PitchScale = 1f + (((float)_rng.NextDouble() - 0.5f) * 2 * pitchJitter);
        voice.Play();
    }

    /// <summary>Plays a sound that happens somewhere in the world: quieter with distance from the camera, silent off another planet.</summary>
    /// <param name="name">Sound name.</param>
    /// <param name="planetId">Planet it happened on.</param>
    /// <param name="tile">Where; null plays it at full volume.</param>
    /// <param name="volume">Volume at the camera.</param>
    /// <param name="minGap">Seconds before the same sound may play again.</param>
    public void PlayAt(string name, int planetId, Int2? tile, float volume = 1f, double minGap = 0.08)
    {
        if (planetId != Root.Planet.Id)
        {
            return;
        }

        float falloff = 1f;
        if (tile is { } t)
        {
            float d = Root.WrapDelta(Root.Cam, new Vector2(t.X, t.Y)).Length() * Mathf.Max(0.5f, Root.Zoom);
            falloff = Mathf.Clamp(1f - (d / 40f), 0, 1);
        }

        Play(name, volume * falloff * falloff, 0.05f, minGap);
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (Root.World is null)
        {
            return;
        }

        // The commander's pick strikes in rhythm while mining.
        Unit leader = Root.Leader;
        if ((leader.MiningTile is not null) && (leader.Planet == Root.Planet))
        {
            _mineTimer -= delta * Mathf.Max(1, Root.Speed);
            if (_mineTimer <= 0)
            {
                _mineTimer = 0.45;
                PlayAt("mine", Root.Planet.Id, Fixed.ToTile(leader.Pos), 0.55f, 0.1);
            }
        }
        else
        {
            _mineTimer = 0.1;
        }

        // Machinery: count the working machines around the camera a few times a second.
        _scanTimer -= delta;
        if (_scanTimer <= 0)
        {
            _scanTimer = 0.3;
            _working = 0;
            float reach = 18f / Mathf.Max(0.4f, Root.Zoom);
            foreach (Building b in Root.Planet.Buildings)
            {
                if ((b.Status == MachineStatus.Working) && (Root.WrapDelta(Root.Cam, new Vector2(b.Origin.X, b.Origin.Y)).LengthSquared() < reach * reach))
                {
                    _working++;
                }
            }
        }

        float target = Root.Speed <= 0 ? 0 : Mathf.Clamp(_working / 14f, 0, 1);
        _humLevel = Mathf.Lerp(_humLevel, target, Mathf.Min(1f, (float)delta * 1.5f));
        _hum.VolumeDb = _humLevel < 0.01f ? -80f : Mathf.LinearToDb(_humLevel * 0.32f);
    }
}
