using Godot;

namespace Factory.Game.Audio;

/// <summary>
/// Builds every sound in the game from code: sine partials, filtered noise and envelopes, rendered once
/// into 16-bit PCM when the game starts. Nothing is loaded from files, so the sounds are original and
/// can be tuned here. Loops are made seamless by using only frequencies that fit a whole number of cycles
/// into the loop and by cross-fading the noise tail into its head.
/// </summary>
public static class Synth
{
    /// <summary>Sample rate of every generated sound.</summary>
    public const int Rate = 32000;

    private const float Tau = Mathf.Tau;

    /// <summary>The names of every one-shot sound <see cref="Make"/> knows.</summary>
    public static readonly string[] OneShots = ["click", "open", "place", "dismantle", "built", "research", "craft", "error", "mine", "launch", "win", "pickup"];

    /// <summary>Renders the named sound.</summary>
    /// <param name="name">A name from <see cref="OneShots"/>, or one of the loops: <c>wind</c>, <c>pad</c>, <c>hum</c>.</param>
    /// <returns>The sound, looping if it is a loop.</returns>
    public static AudioStreamWav Make(string name) => name switch
    {
        "click" => Click(),
        "open" => Open(),
        "place" => Place(),
        "dismantle" => Dismantle(),
        "built" => Built(),
        "research" => Research(),
        "craft" => Craft(),
        "error" => Error(),
        "mine" => Mine(),
        "launch" => Launch(),
        "win" => Win(),
        "pickup" => Pickup(),
        "wind" => Wind(),
        "pad" => Pad(),
        "hum" => Hum(),
        _ => throw new System.ArgumentException($"No sound called {name}", nameof(name)),
    };

    private static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];

    private static float Env(float t, float attack, float decay) => (t < attack ? t / attack : 1f) * Mathf.Exp(-t / decay);

    private static float Sin(float f, float t) => Mathf.Sin(Tau * f * t);

    /// <summary>A soft square: a sine pushed into saturation.</summary>
    private static float Square(float f, float t) => Mathf.Tanh(3f * Sin(f, t));

    /// <summary>A bell or struck-metal tone: a few inharmonic partials, the higher ones dying first.</summary>
    private static float Bell(float f, float t)
    {
        if (t < 0)
        {
            return 0;
        }

        return (Sin(f, t) * Env(t, 0.003f, 0.6f)) + (0.45f * Sin(f * 2.01f, t) * Env(t, 0.002f, 0.3f))
            + (0.22f * Sin(f * 3.03f, t) * Env(t, 0.002f, 0.15f)) + (0.1f * Sin(f * 4.2f, t) * Env(t, 0.001f, 0.07f));
    }

    private static float Note(int semitonesFromA4) => 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);

    /// <summary>A one-pole low-pass filter step.</summary>
    private static float LowPass(ref float state, float x, float cutoff)
    {
        float k = 1f - Mathf.Exp(-Tau * cutoff / Rate);
        state += k * (x - state);

        return state;
    }

    private static AudioStreamWav Click()
    {
        float[] s = Buffer(0.05f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            s[i] = (0.45f * Sin(1750, t) * Env(t, 0.001f, 0.007f)) + (0.3f * Sin(880, t) * Env(t, 0.001f, 0.014f));
        }

        return Pcm(s);
    }

    private static AudioStreamWav Open()
    {
        float[] s = Buffer(0.14f);
        System.Random rng = new System.Random(2);
        float lp = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float f = t / 0.14f;
            float noise = LowPass(ref lp, (float)((rng.NextDouble() * 2) - 1), 600 + (3500 * f));
            s[i] = noise * Mathf.Sin(Mathf.Pi * f) * 0.5f;
        }

        return Pcm(s);
    }

    private static AudioStreamWav Place()
    {
        float[] s = Buffer(0.26f);
        System.Random rng = new System.Random(3);
        float lp = 0, phase = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float f = 55 + (95 * Mathf.Exp(-t / 0.04f));
            phase += Tau * f / Rate;
            float thump = Mathf.Sin(phase) * Env(t, 0.002f, 0.07f);
            float dust = LowPass(ref lp, (float)((rng.NextDouble() * 2) - 1), 1400) * Env(t, 0.001f, 0.025f);
            float tick = Sin(2600, t) * Env(t, 0.0005f, 0.008f);
            s[i] = (0.85f * thump) + (0.9f * dust) + (0.18f * tick);
        }

        return Pcm(s);
    }

    private static AudioStreamWav Dismantle()
    {
        float[] s = Buffer(0.32f);
        float phase = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float f = 140 + (520 * Mathf.Exp(-t / 0.08f));
            phase += Tau * f / Rate;
            s[i] = (0.28f * Mathf.Tanh(2.5f * Mathf.Sin(phase)) * Env(t, 0.003f, 0.09f)) + (0.3f * Bell(1180, t - 0.02f) * Env(t, 0.001f, 0.05f));
        }

        return Pcm(s);
    }

    private static AudioStreamWav Built()
    {
        float[] s = Buffer(0.6f);
        System.Random rng = new System.Random(5);
        float lp = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float clank = (0.35f * Bell(392, t)) + (0.25f * Bell(523.5f, t - 0.07f));
            float hit = LowPass(ref lp, (float)((rng.NextDouble() * 2) - 1), 2500) * Env(t, 0.001f, 0.012f);
            float thud = Sin(70, t) * Env(t, 0.002f, 0.06f);
            s[i] = clank + (0.6f * hit) + (0.5f * thud);
        }

        return Pcm(s);
    }

    private static AudioStreamWav Research()
    {
        // A rising major arpeggio of soft bells.
        float[] s = Buffer(1.8f);
        int[] notes = [3, 7, 10, 15];
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float v = 0;
            for (int n = 0; n < notes.Length; n++)
            {
                v += Bell(Note(notes[n]), t - (n * 0.11f)) * (n == notes.Length - 1 ? 0.4f : 0.3f);
            }

            s[i] = v;
        }

        return Pcm(s);
    }

    private static AudioStreamWav Craft()
    {
        float[] s = Buffer(0.16f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            s[i] = (0.35f * Sin(660, t) * Env(t, 0.002f, 0.03f)) + (t > 0.05f ? 0.35f * Sin(990, t) * Env(t - 0.05f, 0.002f, 0.04f) : 0);
        }

        return Pcm(s);
    }

    private static AudioStreamWav Pickup()
    {
        float[] s = Buffer(0.1f);
        float phase = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            phase += Tau * (500 + (900 * t / 0.1f)) / Rate;
            s[i] = 0.3f * Mathf.Sin(phase) * Env(t, 0.003f, 0.03f);
        }

        return Pcm(s);
    }

    private static AudioStreamWav Error()
    {
        float[] s = Buffer(0.3f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float gate = ((t < 0.1f) || ((t > 0.14f) && (t < 0.26f))) ? 1 : 0;
            s[i] = 0.45f * gate * ((Square(150, t) * 0.7f) + (Square(151.5f, t) * 0.3f));
        }

        return Pcm(Smooth(s, 0.004f));
    }

    private static AudioStreamWav Mine()
    {
        float[] s = Buffer(0.12f);
        System.Random rng = new System.Random(7);
        float lp1 = 0, lp2 = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float n = (float)((rng.NextDouble() * 2) - 1);
            float band = LowPass(ref lp1, n, 3200) - LowPass(ref lp2, n, 700);
            s[i] = (1.1f * band * Env(t, 0.0005f, 0.02f)) + (0.25f * Sin(1240, t) * Env(t, 0.0005f, 0.012f)) + (0.2f * Sin(180, t) * Env(t, 0.001f, 0.03f));
        }

        return Pcm(s);
    }

    private static AudioStreamWav Launch()
    {
        float[] s = Buffer(4f);
        System.Random rng = new System.Random(11);
        float lp = 0, lp2 = 0, phase = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float swell = Mathf.Clamp(t / 0.6f, 0, 1) * Mathf.Clamp((4f - t) / 2.2f, 0, 1);
            float n = (float)((rng.NextDouble() * 2) - 1);
            float rumble = LowPass(ref lp2, LowPass(ref lp, n, 260), 180);
            phase += Tau * (80 + (60 * t)) / Rate;
            s[i] = swell * ((3.2f * rumble) + (0.12f * Mathf.Sin(phase)));
        }

        return Pcm(s);
    }

    private static AudioStreamWav Win()
    {
        // Two bright chords and a held third, each a stack of bells over a soft pad.
        float[] s = Buffer(3.2f);
        (float At, int[] Notes)[] chords = [(0f, [-9, -5, -2, 3]), (0.35f, [-7, -3, 0, 5]), (0.8f, [-4, 0, 3, 8, 15])];
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float v = 0;
            foreach ((float at, int[] notes) in chords)
            {
                foreach (int n in notes)
                {
                    float f = Note(n);
                    v += 0.12f * Bell(f, t - at);
                    if (t > at)
                    {
                        v += 0.05f * Sin(f, t) * Env(t - at, 0.08f, 0.9f);
                    }
                }
            }

            s[i] = v;
        }

        return Pcm(s);
    }

    /// <summary>Wind: low-passed noise rising and falling in slow gusts.</summary>
    private static AudioStreamWav Wind()
    {
        const float Seconds = 12f;
        float[] s = Buffer(Seconds + 1f);
        System.Random rng = new System.Random(13);
        float lp = 0, lp2 = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float gust = 0.55f + (0.3f * Mathf.Sin(Tau * t / Seconds)) + (0.15f * Mathf.Sin((Tau * 3 * t / Seconds) + 1f));
            float cutoff = 250 + (350 * gust);
            s[i] = 2.4f * gust * LowPass(ref lp2, LowPass(ref lp, (float)((rng.NextDouble() * 2) - 1), cutoff), cutoff * 1.5f);
        }

        return Loop(s, Seconds);
    }

    /// <summary>A slow, quiet drone: an open chord whose voices swell in turn.</summary>
    private static AudioStreamWav Pad()
    {
        const float Seconds = 24f;
        float[] s = Buffer(Seconds);
        int[] voices = [-24, -17, -12, -5, -1, 2];
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float v = 0;
            for (int k = 0; k < voices.Length; k++)
            {
                // Round each pitch to a whole number of cycles per loop so the loop point is seamless.
                float f = Mathf.Round(Note(voices[k]) * Seconds) / Seconds;
                float swell = 0.5f + (0.5f * Mathf.Sin((Tau * t * (1 + (k % 3)) / Seconds) + (k * 1.7f)));
                float detune = Mathf.Round((f + 0.25f) * Seconds) / Seconds;
                v += swell * ((0.6f * Sin(f, t)) + (0.25f * Sin(detune, t)) + (0.1f * Sin(f * 2, t))) / voices.Length;
            }

            s[i] = 0.55f * v;
        }

        return Pcm(s, true);
    }

    /// <summary>Machinery: a mains hum with its harmonics, a slow chug and a little grit.</summary>
    private static AudioStreamWav Hum()
    {
        const float Seconds = 2f;
        float[] s = Buffer(Seconds + 0.25f);
        System.Random rng = new System.Random(17);
        float lp = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float chug = 0.75f + (0.25f * Mathf.Sin(Tau * 3 * t));
            float tone = (0.5f * Sin(55, t)) + (0.3f * Sin(110, t)) + (0.12f * Sin(165, t)) + (0.06f * Sin(330, t));
            float grit = LowPass(ref lp, (float)((rng.NextDouble() * 2) - 1), 900);
            s[i] = 0.5f * chug * (tone + (0.9f * grit));
        }

        return Loop(s, Seconds);
    }

    /// <summary>Softens hard edges with a short fade at every gate change (a moving average).</summary>
    private static float[] Smooth(float[] s, float seconds)
    {
        int w = System.Math.Max(1, (int)(seconds * Rate));
        float[] o = new float[s.Length];
        float acc = 0;
        for (int i = 0; i < s.Length; i++)
        {
            acc += s[i];
            if (i >= w)
            {
                acc -= s[i - w];
            }

            o[i] = acc / w;
        }

        return o;
    }

    /// <summary>Cross-fades the samples past <paramref name="seconds"/> into the start, then trims to a loop.</summary>
    private static AudioStreamWav Loop(float[] s, float seconds)
    {
        int n = (int)(seconds * Rate);
        int fade = s.Length - n;
        float[] o = new float[n];
        System.Array.Copy(s, o, n);
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            o[i] = (o[i] * k) + (s[n + i] * (1 - k));
        }

        return Pcm(o, true);
    }

    private static AudioStreamWav Pcm(float[] s, bool loop = false)
    {
        // One-shots are held under full scale, and their last few milliseconds fade so they never end on a click.
        if (loop == false)
        {
            float peak = s.Max(System.Math.Abs);
            if (peak > 0.85f)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    s[i] *= 0.85f / peak;
                }
            }

            int tail = System.Math.Min(s.Length, Rate / 200);
            for (int i = 0; i < tail; i++)
            {
                s[s.Length - 1 - i] *= i / (float)tail;
            }
        }

        byte[] data = new byte[s.Length * 2];
        for (int i = 0; i < s.Length; i++)
        {
            short v = (short)(Mathf.Clamp(s[i], -1f, 1f) * 32767);
            data[i * 2] = (byte)(v & 0xff);
            data[(i * 2) + 1] = (byte)((v >> 8) & 0xff);
        }

        AudioStreamWav wav = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
        if (loop)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            wav.LoopBegin = 0;
            wav.LoopEnd = s.Length;
        }

        return wav;
    }
}
