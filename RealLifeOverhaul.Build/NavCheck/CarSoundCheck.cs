using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// The recorded car sounds (Driving/Sound/CarSoundRules.cs and the Garage's parts in Garage/GarageCatalogue.cs): the
// crossfade covers every rev of every engine on every rev range with no gap and no step, the loops keep their recordings'
// ratios, load, pops, boost and the shop's revs behave; every clip the code names ships in Sounds and has its line in
// SOURCES.md, and every file shipped is named; the sound parts' prices and trade-offs; and, over the sources, that every
// way out gives the game's own sound back and that nothing reaches traffic, the bus or the tow truck.
internal static partial class Program
{
    private static readonly EngineVoice[] SoundVoices = { EngineVoice.Petrol, EngineVoice.V8, EngineVoice.Sport, EngineVoice.Diesel };

    private static void CarSounds()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        SoundCrossfade();
        SoundLoadAndParts();
        SoundFiles(scripts);
        SoundPartsPrices(scripts);
        SoundExits(scripts);
    }

    // ---- the crossfade: every rev covered, the power of the mix one, no step, the recordings' ratios kept.
    private static void SoundCrossfade()
    {
        var ranges = new (float Idle, float Limit)[] { (900f, 4700f), (800f, 7000f), (700f, 6500f), (750f, 3800f), (1000f, 9000f), (0f, 0f) };
        var at = new float[CarSoundRules.MostLoops];
        var gains = new float[CarSoundRules.MostLoops];
        var last = new float[CarSoundRules.MostLoops];
        foreach (EngineVoice voice in SoundVoices)
        {
            EngineLoop[] loops = CarSoundRules.Loops(voice);
            Check(loops.Length >= 3 && loops.Length <= 4, "car sounds: the " + voice + " engine has three or four loops (idle, low, mid, high): " + loops.Length);
            Check(!loops[0].HasOff, "car sounds: the " + voice + " idle loop has no off-throttle layer (idle is idle)");
            int layers = 0;
            for (int i = 0; i < loops.Length; i++)
            {
                layers += loops[i].HasOff ? 2 : 1;
                Check(i == 0 || loops[i].HasOff, "car sounds: the " + voice + " loop " + loops[i].Clip + " above idle has an off-throttle layer");
                Check(i == 0 || loops[i].Rpm > loops[i - 1].Rpm * 1.15f, "car sounds: the " + voice + " loops rise by at least 15 % each: " + loops[i].Clip);
            }
            Check(layers <= CarSoundRules.MostLoops, "car sounds: the " + voice + " engine plays at most " + CarSoundRules.MostLoops + " clips: " + layers);
            foreach ((float idle, float limit) in ranges)
            {
                int n = CarSoundRules.Place(voice, idle, limit, at);
                string where = voice + " on " + idle + "/" + limit + " rpm";
                Check(n == loops.Length, "car sounds: " + where + ": every loop placed (" + n + ")");
                bool ratios = true;
                for (int i = 1; i < n; i++)
                {
                    ratios &= at[i] > at[i - 1] && Math.Abs(at[i] / at[0] - loops[i].Rpm / loops[0].Rpm) < 1e-3f;
                }
                Check(ratios, "car sounds: " + where + ": the loops keep their recordings' ratios, so two crossing over sound at one pitch");
                float placedIdle = idle > 100f ? idle : 800f;
                float placedLimit = limit > placedIdle * 1.5f ? limit : placedIdle * 6f;
                float idlePitch = CarSoundRules.PitchOf(placedIdle, at[0]);
                float topPitch = CarSoundRules.PitchOf(placedLimit, at[n - 1]);
                Check(idlePitch >= CarSoundRules.IdlePitch - 1e-4f && idlePitch <= 1.0001f, "car sounds: " + where + ": the idle loop plays at " + idlePitch.ToString("F2") + " at the car's idle");
                Check(topPitch <= CarSoundRules.TopPitch + 1e-3f || idlePitch <= CarSoundRules.IdlePitch + 1e-3f,
                    "car sounds: " + where + ": the top loop at the rev limit is pitched " + topPitch.ToString("F2") + ", within " + CarSoundRules.TopPitch + " unless the idle is as deep as it goes");

                // Every rev from a standstill to half again the limit, one rpm at a time.
                float worstPower = 0f, worstStep = 0f;
                int most = 0;
                bool first = true;
                for (float rpm = 0f; rpm <= placedLimit * 1.5f; rpm += 1f)
                {
                    CarSoundRules.Mix(rpm, at, n, gains);
                    float power = 0f;
                    int sounding = 0;
                    for (int i = 0; i < n; i++)
                    {
                        power += gains[i] * gains[i];
                        sounding += gains[i] > 0f ? 1 : 0;
                        if (!first)
                        {
                            worstStep = Math.Max(worstStep, Math.Abs(gains[i] - last[i]));
                        }
                        last[i] = gains[i];
                    }
                    first = false;
                    worstPower = Math.Max(worstPower, Math.Abs(power - 1f));
                    most = Math.Max(most, sounding);
                }
                Check(worstPower < 1e-4f, "car sounds: " + where + ": the power of the mix is one at every rev, no gap (most off " + worstPower.ToString("E1") + ")");
                Check(worstStep < 0.01f, "car sounds: " + where + ": no loop's level steps by a hundredth from one rpm to the next (most " + worstStep.ToString("F4") + ")");
                Check(most <= 2, "car sounds: " + where + ": at most two loops sound at once: " + most);

                // Determinism: the same revs, the same mix.
                CarSoundRules.Mix(placedIdle * 2.3f, at, n, gains);
                float a = gains[1];
                CarSoundRules.Mix(placedIdle * 2.3f, at, n, gains);
                Check(a == gains[1], "car sounds: " + where + ": the same revs give the same mix");
            }
        }
        // The edges: no loops, one loop, revs below the first and above the last.
        CarSoundRules.Mix(1000f, at, 0, gains);
        Check(true, "car sounds: an engine with no loops mixes nothing without failing");
        Check(CarSoundRules.Place(EngineVoice.None, 900f, 4700f, at) == 0, "car sounds: no recorded engine places no loops");
        CarSoundRules.Place(EngineVoice.Sport, 900f, 4700f, at);
        CarSoundRules.Mix(-50f, at, 4, gains);
        Check(gains[0] == 1f && gains[1] == 0f, "car sounds: revs below idle play the idle loop alone");
        CarSoundRules.Mix(1e6f, at, 4, gains);
        Check(gains[3] == 1f && gains[2] == 0f, "car sounds: revs past the top loop play the top loop alone");
        Check(CarSoundRules.PitchOf(1e6f, 1000f) == CarSoundRules.MaxPitch && CarSoundRules.PitchOf(0f, 1000f) == CarSoundRules.MinPitch,
            "car sounds: a loop's pitch stays within " + CarSoundRules.MinPitch + " and " + CarSoundRules.MaxPitch);
    }

    // ---- load, the exhausts' and turbos' sounds, pops, boost, the shop's revs, the squeal.
    private static void SoundLoadAndParts()
    {
        float worst = 0f, prev = CarSoundRules.OnShare(0f);
        bool rising = true;
        for (float t = 0f; t <= 1f; t += 0.001f)
        {
            float on = CarSoundRules.OnShare(t);
            float sum = (float)(Math.Sqrt(on) * Math.Sqrt(on) + Math.Sqrt(1f - on) * Math.Sqrt(1f - on));
            worst = Math.Max(worst, Math.Abs(sum - 1f));
            rising &= on >= prev - 1e-6f;
            prev = on;
        }
        Check(worst < 1e-4f && rising, "car sounds: on and off the throttle cross equal-power and the on layer only grows with the throttle");
        Check(CarSoundRules.OnShare(0f) == 0f && CarSoundRules.OnShare(1f) == 1f, "car sounds: shut is all off the throttle, floored is all on it");

        ExhaustSound stock = CarSoundRules.Exhaust(0), sport = CarSoundRules.Exhaust(1), straight = CarSoundRules.Exhaust(2);
        Check(stock.Volume == 1f && stock.PopsPerSecond == 0f, "car sounds: the stock exhaust is the engine as recorded and never pops");
        Check(sport.Volume > stock.Volume && straight.Volume > sport.Volume, "car sounds: louder exhausts are louder: " + stock.Volume + ", " + sport.Volume + ", " + straight.Volume);
        Check(sport.PopsPerSecond > 0f && straight.PopsPerSecond > sport.PopsPerSecond && straight.PopVolume > sport.PopVolume,
            "car sounds: the sports exhaust pops now and then, the straight-through more and louder");
        Check(sport.OffLevel > stock.OffLevel && straight.OffLevel > sport.OffLevel, "car sounds: a louder exhaust is rawer off the throttle");
        Check(CarSoundRules.Exhaust(9).Volume == 1f && CarSoundRules.Exhaust(-1).Volume == 1f, "car sounds: an exhaust the rules do not know is stock");
        Check(CarSoundRules.Volume(1f, 1f, 1f, straight) <= 1f && CarSoundRules.Volume(1f, 1f, 1f, straight) > 0.5f,
            "car sounds: the loudest loop of the loudest exhaust stays within a source's volume: " + CarSoundRules.Volume(1f, 1f, 1f, straight).ToString("F2"));
        Check(CarSoundRules.Level(0f, 1f, stock) < CarSoundRules.Level(1f, 1f, stock) && CarSoundRules.Level(0.5f, 0f, stock) < CarSoundRules.Level(0.5f, 1f, stock),
            "car sounds: the engine is louder with revs and on the throttle");

        // Pops: only after a lift-off from high revs, within the window, fading, never from stock.
        Check(CarSoundRules.Lifted(0.05f, 0.2f) && !CarSoundRules.Lifted(0.05f, 0.5f) && !CarSoundRules.Lifted(0.3f, 0.1f), "car sounds: a lift-off is the throttle shut within a third of a second");
        Check(CarSoundRules.PopChance(stock, 0.9f, 0f, 0.1f, 1f, 0.016f) == 0f, "car sounds: a stock exhaust never pops");
        Check(CarSoundRules.PopChance(straight, 0.3f, 0f, 0.1f, 1f, 0.016f) == 0f, "car sounds: nothing pops below " + CarSoundRules.PopFromRevs + " of the rev limit");
        Check(CarSoundRules.PopChance(straight, 0.9f, 0.5f, 0.1f, 1f, 0.016f) == 0f, "car sounds: nothing pops back on the throttle");
        Check(CarSoundRules.PopChance(straight, 0.9f, 0f, CarSoundRules.PopWindow + 0.1f, 1f, 0.016f) == 0f, "car sounds: nothing pops after the window");
        Check(CarSoundRules.PopChance(straight, 0.9f, 0f, 0.1f, 0.01f, 0.016f) == 0f, "car sounds: two pops are at least " + CarSoundRules.PopGap + " s apart");
        float early = CarSoundRules.PopChance(straight, 0.9f, 0f, 0.05f, 1f, 0.016f), late = CarSoundRules.PopChance(straight, 0.9f, 0f, 1.0f, 1f, 0.016f);
        Check(early > late && late > 0f, "car sounds: pops come most just after the lift and fade over the window");
        float expected = 0f;
        for (float s = 0f; s < CarSoundRules.PopWindow; s += 0.016f)
        {
            expected += CarSoundRules.PopChance(straight, 0.8f, 0f, s, 1f, 0.016f);
        }
        Check(expected > 1f && expected < 4f, "car sounds: a straight-through lift-off from high revs gives one to four pops: " + expected.ToString("F1"));

        // Boost: builds with throttle and revs, slower on a big turbo, dies when shut; none without a turbo.
        TurboSound none = CarSoundRules.Turbo(0), small = CarSoundRules.Turbo(1), big = CarSoundRules.Turbo(2);
        float b1 = 0f, b2 = 0f, b0 = 0f;
        for (int i = 0; i < 30; i++)
        {
            b0 = CarSoundRules.Boost(b0, 1f, 0.8f, 0.02f, none);
            b1 = CarSoundRules.Boost(b1, 1f, 0.8f, 0.02f, small);
            b2 = CarSoundRules.Boost(b2, 1f, 0.8f, 0.02f, big);
        }
        Check(b0 == 0f, "car sounds: no turbo, no boost");
        Check(b1 > b2 && b2 > 0f, "car sounds: a big turbo spools slower than a small one (" + b1.ToString("F2") + " against " + b2.ToString("F2") + " after 0.6 s)");
        Check(CarSoundRules.Boost(0f, 1f, 0.1f, 1f, small) == 0f, "car sounds: no boost below " + CarSoundRules.BoostFromRevs + " of the rev limit");
        float shut = 1f;
        for (int i = 0; i < 50; i++)
        {
            shut = CarSoundRules.Boost(shut, 0f, 0.8f, 0.02f, small);
        }
        Check(shut < 0.05f, "car sounds: boost dies within a second of shutting the throttle: " + shut.ToString("F3"));
        Check(CarSoundRules.Flutters(small, true, 0.6f) && !CarSoundRules.Flutters(small, true, 0.2f) && !CarSoundRules.Flutters(small, false, 0.9f) && !CarSoundRules.Flutters(none, true, 1f),
            "car sounds: a turbo flutters on a lift-off with boost, not without either, and never without a turbo");
        Check(CarSoundRules.WhistleVolume(1f, big) > CarSoundRules.WhistleVolume(1f, small) && CarSoundRules.WhistlePitch(1f, big) < CarSoundRules.WhistlePitch(1f, small)
              && CarSoundRules.WhistleVolume(0f, small) == 0f, "car sounds: a big turbo whistles louder and lower; no boost, no whistle");

        // The shop's revs: up towards most of the limit on the key, back to idle without it, never below idle.
        float rpm = 900f;
        for (int i = 0; i < 120; i++)
        {
            rpm = CarSoundRules.ShopRevs(rpm, true, 900f, 4700f, 1f / 60f);
        }
        Check(rpm > 0.8f * 4700f && rpm <= CarSoundRules.ShopRevTop * 4700f + 1f, "car sounds: two seconds on the throttle key take the turntable's revs to " + rpm.ToString("F0") + " rpm");
        float fallStep = 0f, before = rpm;
        for (int i = 0; i < 240; i++)
        {
            float next = CarSoundRules.ShopRevs(rpm, false, 900f, 4700f, 1f / 60f);
            fallStep = Math.Max(fallStep, rpm - next);
            rpm = next;
        }
        Check(Math.Abs(rpm - 900f) < 5f, "car sounds: letting go, the revs fall back to idle within four seconds: " + rpm.ToString("F0"));
        Check(fallStep <= CarSoundRules.ShopFall / 60f + 0.5f && before > rpm, "car sounds: the revs fall steadily, never faster than " + CarSoundRules.ShopFall + " rpm a second");
        Check(CarSoundRules.ShopRevs(0f, false, 900f, 4700f, 0.016f) >= 900f, "car sounds: the turntable's revs never sit below idle");

        // The squeal, as the game's skid sound reckons it.
        Check(CarSoundRules.Squeal(1f, 10f, 0f) == 1f && CarSoundRules.Squeal(1f, 0f, 0f) == 0f && CarSoundRules.Squeal(0f, 10f, 30f) == 0f,
            "car sounds: a squeal needs slip and movement");
        Check(Near(CarSoundRules.SquealEased(0f, 1f, 0.05f), 0.5f, 1e-4f) && CarSoundRules.SquealEased(0f, 1f, 1f) == 1f, "car sounds: the squeal eases at ten a second, as the game's");

        // Which engine each kind of car is given.
        Check(CarSoundRules.VoiceFor("ba:vehicletype_honzamimic", CarKind.Economy) == EngineVoice.Petrol, "car sounds: an economy car has the small petrol");
        Check(CarSoundRules.VoiceFor("ba:vehicletype_bima320", CarKind.Saloon) == EngineVoice.Petrol, "car sounds: a saloon has the small petrol");
        Check(CarSoundRules.VoiceFor("ba:vehicletype_mersaidis500", CarKind.Luxury) == EngineVoice.V8, "car sounds: a luxury car has the V8");
        Check(CarSoundRules.VoiceFor("ba:vehicletype_umcnunavut", CarKind.Utility) == EngineVoice.V8, "car sounds: an SUV or a pickup has the V8");
        Check(CarSoundRules.VoiceFor("ba:vehicletype_vordpony", CarKind.Sports) == EngineVoice.V8, "car sounds: the muscle coupe has the V8");
        Check(CarSoundRules.VoiceFor("empiremustanggt:vehicletype_mustanggt", CarKind.Sports) == EngineVoice.V8, "car sounds: the Mustang GT of another mod has the V8");
        Check(CarSoundRules.VoiceFor("ba:vehicletype_anselmoaf90", CarKind.Sports) == EngineVoice.Sport, "car sounds: a sports car has the sports engine");
        Check(CarSoundRules.VoiceFor("ba:vehicletype_umcdesert", CarKind.Van) == EngineVoice.Diesel && CarSoundRules.VoiceFor("ba:vehicletype_deliverytruck", CarKind.Truck) == EngineVoice.Diesel,
            "car sounds: vans and trucks have the diesel");
        Check(CarSoundRules.VoiceFor("ba:vehicletype_electricscooter", CarKind.Untouched) == EngineVoice.None && CarSoundRules.VoiceFor(null, CarKind.Untouched) == EngineVoice.None,
            "car sounds: a scooter (every two-wheeler) and a vehicle the mod does not tune keep the game's sound");
    }

    // ---- every clip the code names ships, every file shipped is named, every one has its licence line.
    private static void SoundFiles(string scripts)
    {
        string mod = Path.GetFullPath(Path.Combine(scripts, ".."));
        string sounds = Path.Combine(mod, CarSoundRules.Folder);
        string sources = Path.Combine(sounds, "SOURCES.md");
        string table = File.Exists(sources) ? File.ReadAllText(sources) : string.Empty;
        Check(table.Length > 0, "car sounds: Sounds/SOURCES.md is there");
        var named = new HashSet<string>(StringComparer.Ordinal);
        long bytes = 0;
        foreach (string clip in CarSoundRules.AllClips)
        {
            Check(named.Add(clip), "car sounds: the clip " + clip + " is named once");
            string file = Path.Combine(sounds, clip + CarSoundRules.Extension);
            bool there = File.Exists(file);
            Check(there, "car sounds: the clip " + clip + " the code names ships in Sounds");
            if (there)
            {
                byte[] head = new byte[4];
                using (FileStream f = File.OpenRead(file))
                {
                    f.Read(head, 0, 4);
                    bytes += f.Length;
                }
                Check(head[0] == 'O' && head[1] == 'g' && head[2] == 'g' && head[3] == 'S', "car sounds: " + clip + ".ogg is an Ogg file by its header");
            }
            Match row = Regex.Match(table, @"^\| `" + Regex.Escape(clip + CarSoundRules.Extension) + @"` \| \[[^\]]+\]\(https://freesound\.org/people/[^)]+\) \| [^|]+ \| \[(CC0 1\.0|CC BY 4\.0)\]", RegexOptions.Multiline);
            Check(row.Success, "car sounds: " + clip + ".ogg has its line in SOURCES.md: the recording on Freesound, its author and a licence that allows it (CC0 1.0 or CC BY 4.0)");
        }
        if (Directory.Exists(sounds))
        {
            foreach (string file in Directory.GetFiles(sounds))
            {
                string name = Path.GetFileName(file);
                if (name == "SOURCES.md" || name.EndsWith(".meta", StringComparison.Ordinal))
                {
                    continue;
                }
                Check(name.EndsWith(CarSoundRules.Extension, StringComparison.Ordinal) && named.Contains(Path.GetFileNameWithoutExtension(name)),
                    "car sounds: Sounds/" + name + " is a clip the code names (nothing ships unread)");
            }
        }
        Check(bytes > 100000 && bytes < 15L * 1024 * 1024, "car sounds: the shipped sounds are modest: " + (bytes / 1024) + " KB of at most 15 MB");
        bool allowed = true;
        foreach (Match licence in Regex.Matches(table, @"\[([^\]]+)\]\(https?://creativecommons\.org/[^)]*\)"))
        {
            allowed &= licence.Groups[1].Value == "CC0 1.0" || licence.Groups[1].Value == "CC BY 4.0";
        }
        Check(allowed && !Regex.IsMatch(table, @"pixabay|youtube|by-nc|by-nd|sampling\+", RegexOptions.IgnoreCase),
            "car sounds: every licence in SOURCES.md is CC0 1.0 or CC BY 4.0 (no NC, ND, Sampling+, Pixabay or YouTube)");

        // The credits: the Workshop's and the in-game Guide's name both CC BY authors; the build ships the folder.
        string build = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
        string credits = Path.Combine(build, "Workshop", "CREDITS.md");
        string creditsText = File.Exists(credits) ? File.ReadAllText(credits) : string.Empty;
        foreach (string author in new[] { "Jonas_Jocys", "lonemonk" })
        {
            Check(creditsText.Contains(author) && table.Contains(author), "car sounds: the CC BY 4.0 author " + author + " is credited in CREDITS.md and SOURCES.md");
            string en = Path.Combine(mod, "Locales", "en.json");
            Check(File.Exists(en) && File.ReadAllText(en).Contains(author), "car sounds: the in-game Guide's credits name " + author);
        }
        string csproj = Path.Combine(build, "RealLifeOverhaul.csproj");
        string project = File.Exists(csproj) ? File.ReadAllText(csproj) : string.Empty;
        Check(project.Contains(@"$(ModSource)\Sounds\*.ogg") && project.Contains(@"$(ModSource)\Sounds\SOURCES.md") && project.Contains(@"SourceFiles=""@(SoundFiles)"""),
            "car sounds: the build copies Sounds/*.ogg and SOURCES.md into the package, and counts them shipped");
    }

    // ---- the sound parts: price and trade-off as the catalogue's other parts have, and how each sounds in the three languages.
    private static void SoundPartsPrices(string scripts)
    {
        PartDef sport = GarageCatalogue.Part("exhaust_sport"), straight = GarageCatalogue.Part("exhaust_straight");
        PartDef small = GarageCatalogue.Part("turbo_1"), big = GarageCatalogue.Part("turbo_2");
        Check(sport != null && straight != null && small != null && big != null, "car sounds: the shop sells both exhausts and both turbos");
        if (sport == null || straight == null || small == null || big == null)
        {
            return;
        }
        Check(sport.Slot == PartSlot.Exhaust && straight.Slot == PartSlot.Exhaust && small.Slot == PartSlot.Turbo && big.Slot == PartSlot.Turbo, "car sounds: the sound parts sit in the exhaust and turbo slots");
        Check(sport.Loud == 1 && straight.Loud == 2, "car sounds: the sports exhaust is the louder sound (1), the straight-through the loudest (2)");
        Check(small.Boost == 1 && big.Boost == 2 && sport.Boost == 0, "car sounds: the turbocharger whistles small (1), the big turbo big (2), an exhaust not at all");
        Check(Near(sport.Price, 0.5f, 1e-4f) && Near(straight.Price, 0.9f, 1e-4f) && Near(small.Price, 2.5f, 1e-4f) && Near(big.Price, 4.5f, 1e-4f),
            "car sounds: the parts' prices: exhausts 0.5 and 0.9, turbos 2.5 and 4.5 of the part base");
        foreach (CarKind kind in new[] { CarKind.Economy, CarKind.Saloon, CarKind.Luxury, CarKind.Sports, CarKind.Utility, CarKind.Van, CarKind.Truck })
        {
            int a = GarageCatalogue.PartPrice(sport, kind), b = GarageCatalogue.PartPrice(straight, kind), c = GarageCatalogue.PartPrice(small, kind), d = GarageCatalogue.PartPrice(big, kind);
            Check(a > 0 && b > a && c > b && d > c, "car sounds: on a " + kind + " the louder or bigger part costs more: " + a + ", " + b + ", " + c + ", " + d);
        }
        Check(straight.Accel > sport.Accel && sport.Accel > 1f && straight.Top > sport.Top, "car sounds: the straight-through is quicker than the sports exhaust, both quicker than stock");
        Check(sport.Wear == 0f && straight.Wear == 0f, "car sounds: an exhaust does not wear the car faster (its cost is the noise: the straight-through is not street legal)");
        Check(small.Wear > 0f && big.Wear > small.Wear && big.Insurance > 0f && small.Insurance == 0f,
            "car sounds: a turbo wears the car faster, the big one more and dearer to insure");
        Check(big.Accel > small.Accel && small.Accel > 1f, "car sounds: the big turbo is quicker than the small one");

        string locales = Path.Combine(scripts, "..", "Locales");
        foreach (PartDef part in new[] { sport, straight, small, big })
        {
            Check(part.Sound != null && part.Sound.StartsWith("reallife_garage_sound_", StringComparison.Ordinal), "car sounds: " + part.Id + " says how it sounds");
            foreach (string language in new[] { "en", "pt", "zh-cn" })
            {
                string file = Path.Combine(locales, language + ".json");
                Check(File.Exists(file) && part.Sound != null && File.ReadAllText(file).Contains("\"" + part.Sound + "\":"), "car sounds: " + part.Sound + " is in " + language + ".json");
            }
        }
        foreach (string key in new[] { "reallife_garage_sounds", "reallife_opt_tip_garage_sounds", "reallife_garage_test_sounds", "reallife_garage_sounds_test_on", "reallife_garage_sounds_test_wait", "reallife_garage_sounds_rev" })
        {
            foreach (string language in new[] { "en", "pt", "zh-cn" })
            {
                string file = Path.Combine(locales, language + ".json");
                Check(File.Exists(file) && File.ReadAllText(file).Contains("\"" + key + "\":"), "car sounds: " + key + " is in " + language + ".json");
            }
        }
        foreach (PartDef other in GarageCatalogue.Parts)
        {
            Check(other.Sound == null || other.Slot == PartSlot.Exhaust || other.Slot == PartSlot.Turbo, "car sounds: only exhausts and turbos say how they sound: " + other.Id);
        }
    }

    // ---- over the sources: every way out gives the game's sound back; nothing reaches traffic, the bus or the tow truck.
    private static void SoundExits(string scripts)
    {
        string director = Source(scripts, @"Driving\Sound\CarSoundDirector.cs");
        string car = Source(scripts, @"Driving\Sound\CarSoundCar.cs");
        string game = Source(scripts, @"Driving\Sound\CarSoundGame.cs");
        string links = Source(scripts, @"Wiring\Wiring.CarSoundLinks.cs");
        Check(director.Length > 0 && car.Length > 0 && game.Length > 0 && links.Length > 0, "car sounds: the sources are there to read");

        string want = Body(director, "private static CarController Want(");
        Check(want.Contains("!Settings.Garage") && want.Contains("!Settings.GarageSounds") && want.Contains("LoadScene.isLoading") && want.Contains("return null"),
            "car sounds: no car is wanted with the Garage or the option off, or during a load");
        Check(want.Contains("GoingPublic.MineVehicle(driven)"), "car sounds: only the player's own car is wanted (Going Public: never another player's)");
        Check(want.Contains("gm.selectedVehicle") && Regex.Matches(director + links, @"selectedVehicle|Turntable\(").Count >= 3 && !Regex.IsMatch(director + car + game + links, @"AllPlayerVehicles|FindObjects|GetComponentsInChildren<CarController>"),
            "car sounds: the only cars are the one driven and the turntable's, never a search");

        string update = Body(director, "private void OnUpdate(");
        Check(Regex.IsMatch(update, @"_held\.Car != want\)\s*\{\s*GiveBack\("), "car sounds: leaving the car, another car, the option off or a load give the game's sound back the same frame");
        string frame = Body(director, "private void Update(");
        Check(Regex.IsMatch(frame, @"FrameGuard\.Off\)\s*\{\s*GiveBack\(") && Regex.IsMatch(frame, @"catch \(Exception e\)\s*\{\s*Fault\(e\);")
              && Regex.IsMatch(Body(director, "private void Fault("), @"_failed = true;\s*GiveBack\("),
            "car sounds: a fault, and the feature switched off after its faults, give the game's sound back");
        Check(Regex.IsMatch(Body(director, "private void OnDestroy("), @"GiveBack\("), "car sounds: the city going gives the game's sound back");
        Check(Body(director, "private void GiveBack(").Contains("held.Release("), "car sounds: giving back releases the car");

        string release = Body(car, "public int Release(");
        Check(release.Contains("CarSoundGame.Unmute(_game)"), "car sounds: releasing unmutes the game's own sources");
        foreach (string field in new[] { "_on[i]", "_off[i]", "_pop", "_whistle", "_flutter", "_squeal" })
        {
            Check(release.Contains("Drop(ref " + field + ")"), "car sounds: releasing takes off the mod's source " + field);
        }
        Check(Regex.Matches(car + game, @"AddComponent<AudioSource>").Count == 1 && game.Contains("AddComponent<AudioSource>"),
            "car sounds: the mod's sources are made in one place (CarSoundGame.Source) and nowhere else");
        string mute = Body(game, "public static void Mute(");
        string unmute = Body(game, "public static int Unmute(");
        Check(mute.Contains("into.Distortion = into.Note.maxDistortion") && unmute.Contains("from.Note.maxDistortion = from.Distortion"),
            "car sounds: the engine note's distortion is recorded before it is taken off and put back after");
        Check(Body(game, "private static void Take(").Contains("into.WasMuted[slot] = source != null && source.mute") && unmute.Contains("source.mute = from.WasMuted[i]"),
            "car sounds: each of the game's sources is muted after its own mute is recorded, and given that back");
        Check(Regex.Matches(game, @"\.mute = true").Count == 1, "car sounds: the game's sources are muted in one place only");
        Check(!Regex.IsMatch(mute, @"\.(baseVolume|pitchOffset|clips|clip|volumeRange)\s*="), "car sounds: nothing else of the game's sound components is changed (the Garage's engine note keeps its own)");

        string all = director + car + game + links + Source(scripts, @"Driving\Sound\CarSoundRules.cs") + Source(scripts, @"Driving\Sound\CarSoundBank.cs") + Source(scripts, @"Driving\Sound\CarSoundBridges.cs");
        foreach (string other in new[] { "Gley", "TrafficManager", "EngineSoundComponent", "TrafficCar", "BusVehicle", "BusDriver", "TowTruck", "TowService", "ScooterController", "BikeMount" })
        {
            Check(!all.Contains(other), "car sounds: nothing of the recorded sounds names " + other + " (traffic, the bus, the tow truck and the two-wheelers keep their own)");
        }
    }
}
