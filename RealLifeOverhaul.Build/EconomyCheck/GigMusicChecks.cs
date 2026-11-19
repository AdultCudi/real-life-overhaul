using System;
using System.Collections.Generic;
using System.IO;
using DiscPath = System.IO.Path;
using RealLifeOverhaul;

// A player asked for gigs to play their own music the way the game's custom radio does. Busking plays the player's files from
// RealLifeOverhaul/GigMusic/Busking (Scripts/Jobs/GigMusicRules.cs, GigMusic.cs, GigMusicGame.cs) when "Play your own music in
// gigs" is on: which files are taken, the limits, the order and the shuffle by the save's seed, the fallback, the fade at the set's
// end, the folder made with its note, and the option and texts. First the rules against a made-up disc, then the sources.
internal static partial class Program
{
    private sealed class FakeDisc : IGigMusicFiles
    {
        public readonly Dictionary<string, long> Files = new Dictionary<string, long>(StringComparer.Ordinal);
        public readonly HashSet<string> Folders = new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Text = new Dictionary<string, string>(StringComparer.Ordinal);
        public bool Locked;      // folders cannot be made
        public int Reads;        // lists of a folder asked for

        // One kind of slash, as a disc has its folders.
        private static string Plain(string path) => path.Replace('\\', '/');

        public bool FolderExists(string folder) => Folders.Contains(Plain(folder));

        public string[] FilesIn(string folder)
        {
            Reads++;
            var list = new List<string>();
            foreach (string path in Files.Keys)
            {
                if (string.Equals(Plain(DiscPath.GetDirectoryName(path)), Plain(folder), StringComparison.Ordinal))
                {
                    list.Add(path);
                }
            }
            return list.ToArray();
        }

        public long SizeOf(string path) => Files.TryGetValue(path, out long size) ? size : -1L;

        public bool FileExists(string path) => Files.ContainsKey(path);

        public bool CreateFolder(string folder)
        {
            if (Locked)
            {
                return false;
            }
            Folders.Add(Plain(folder));
            return true;
        }

        public bool WriteText(string path, string text)
        {
            Files[path] = text.Length;
            Text[path] = text;
            return true;
        }

        public void Add(string folder, string name, long size = 1000L)
        {
            Folders.Add(Plain(folder));
            Files[DiscPath.Combine(folder, name)] = size;
        }

        public bool Has(string folder) => Folders.Contains(Plain(folder));
    }

    private static void GigMusic()
    {
        GigMusicTaken();
        GigMusicOrder();
        GigMusicChoice();
        GigMusicFolder();
        GigMusicSources();
        GigMusicWords();
    }

    private static string GigNames(GigMusicSet set)
    {
        var names = new string[set.Count];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = DiscPath.GetFileName(set.Paths[i]);
        }
        return string.Join(",", names);
    }

    private static void GigMusicTaken()
    {
        const string root = "data/GigMusic";
        string folder = GigMusicRules.FolderOf(root, GigSound.Busking);
        Check(GigMusicRules.Name(GigSound.Busking) == "Busking" && folder == DiscPath.Combine(root, "Busking"), "gig music: the busking folder is named for the gig, under the mod's GigMusic folder");

        foreach (string good in new[] { "a.wav", "a.ogg", "a.aiff", "a.aif", "a.flac", "a.mp3", "a.mp2", "LOUD.MP3", "x.y.Wav" })
        {
            Check(GigMusicRules.IsMusic(good), "gig music: " + good + " is a type the game's own radio reads");
        }
        foreach (string bad in new[] { "a.m4a", "a.aac", "a.wma", "a.opus", "a.mid", "notes.txt", "a", "a.mp3.zip", "" })
        {
            Check(!GigMusicRules.IsMusic(bad), "gig music: '" + bad + "' is not a type taken");
        }
        Check(!GigMusicRules.IsMusic(null), "gig music: no name is no music");

        var disc = new FakeDisc();
        disc.Add(folder, "b.mp3");
        disc.Add(folder, "A.wav");
        disc.Add(folder, "c.ogg");
        disc.Add(folder, "d.m4a");
        disc.Add(folder, "e.txt");
        disc.Add(folder, ".hidden.mp3");
        disc.Add(folder, "~lock.mp3");
        disc.Add(folder, "empty.mp3", 0L);
        disc.Add(folder, "huge.flac", GigMusicRules.MostBytes + 1);
        disc.Add(folder, "edge.wav", GigMusicRules.MostBytes);
        disc.Add(folder, GigMusicRules.ReadmeName);
        GigMusicSet set = GigMusicRules.Read(disc, folder, false, 1);
        Check(GigNames(set) == "A.wav,b.mp3,c.ogg,edge.wav", "gig music: music types are taken by name without regard to case, and nothing else (taken: " + GigNames(set) + ")");
        string left = string.Join("|", set.Left);
        Check(left.Contains("d.m4a") && left.Contains("empty.mp3") && left.Contains("huge.flac") && !left.Contains("edge.wav") && !left.Contains("hidden") && !left.Contains("README"),
            "gig music: a type the game does not read, an empty file and one over the size are named in the log; the file at the limit is taken (" + left + ")");
        Check(GigMusicRules.MostBytes == GigMusicRules.MostMegabytes * 1024L * 1024L && GigMusicRules.MostMegabytes == 40 && GigMusicRules.MostFiles == 24,
            "gig music: the limits are " + GigMusicRules.MostFiles + " files of " + GigMusicRules.MostMegabytes + " MB each");

        var many = new FakeDisc();
        for (int i = 0; i < 30; i++)
        {
            many.Add(folder, "song" + i.ToString("D2") + ".mp3");
        }
        GigMusicSet capped = GigMusicRules.Read(many, folder, false, 1);
        Check(capped.Count == GigMusicRules.MostFiles && DiscPath.GetFileName(capped.Paths[0]) == "song00.mp3" && DiscPath.GetFileName(capped.Paths[23]) == "song23.mp3" && capped.Left.Count == 6,
            "gig music: of thirty files the first " + GigMusicRules.MostFiles + " by name are taken and the other six are named as left out");
        Check(many.Reads == 1, "gig music: the folder is listed once for a read");

        var none = new FakeDisc();
        Check(GigMusicRules.Read(none, folder, true, 1).Count == 0 && GigMusicRules.Read(null, folder, true, 1).Count == 0, "gig music: a folder that is not there, or no disc, gives no files and no fault");
        none.Folders.Add(folder);
        Check(GigMusicRules.Read(none, folder, false, 1).Count == 0, "gig music: an empty folder gives no files");
        var onlyNote = new FakeDisc();
        onlyNote.Add(folder, GigMusicRules.ReadmeName);
        Check(GigMusicRules.Read(onlyNote, folder, false, 1).Count == 0, "gig music: the folder's own note is no music");
    }

    private static void GigMusicOrder()
    {
        const string root = "data/GigMusic";
        string folder = GigMusicRules.FolderOf(root, GigSound.Busking);
        var disc = new FakeDisc();
        for (int i = 0; i < 10; i++)
        {
            disc.Add(folder, "t" + i + ".mp3");
        }
        const string inOrder = "t0.mp3,t1.mp3,t2.mp3,t3.mp3,t4.mp3,t5.mp3,t6.mp3,t7.mp3,t8.mp3,t9.mp3";
        string plain = GigNames(GigMusicRules.Read(disc, folder, false, 5));
        Check(plain == inOrder, "gig music: in order plays by name, whatever the seed (" + plain + ")");
        Check(plain == GigNames(GigMusicRules.Read(disc, folder, false, 99999)), "gig music: the seed does not touch the order of an unshuffled read");

        int seed = GigMusicRules.SeedFor(123456, GigSound.Busking);
        string a = GigNames(GigMusicRules.Read(disc, folder, true, seed));
        string b = GigNames(GigMusicRules.Read(disc, folder, true, seed));
        Check(a == b, "gig music: a save's seed gives the same shuffle every time (" + a + ")");
        Check(seed == GigMusicRules.SeedFor(123456, GigSound.Busking) && seed >= 0, "gig music: the seed is a plain number of the save and the gig, the same every call");

        var seen = new HashSet<string>();
        bool permutation = true;
        for (int save = 1; save <= 200; save++)
        {
            string shuffled = GigNames(GigMusicRules.Read(disc, folder, true, GigMusicRules.SeedFor(save * 7919, GigSound.Busking)));
            seen.Add(shuffled);
            var parts = new List<string>(shuffled.Split(','));
            parts.Sort(StringComparer.Ordinal);
            permutation &= parts.Count == 10 && string.Join(",", parts) == inOrder;
        }
        Check(permutation, "gig music: a shuffle keeps every file once and adds none");
        Check(seen.Count > 150, "gig music: different saves hear different mixes (" + seen.Count + " different orders from 200 saves)");

        var one = new[] { "only.mp3" };
        GigMusicRules.Shuffle(one, 3);
        var zero = new string[0];
        GigMusicRules.Shuffle(zero, 3);
        var two = new[] { "x", "y" };
        GigMusicRules.Shuffle(two, 0);
        Check(one[0] == "only.mp3" && zero.Length == 0 && two.Length == 2, "gig music: a shuffle of one file, of none and with seed 0 is no fault");

        long total = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++)
        {
            total += GigMusicRules.Read(disc, folder, true, i).Count;
        }
        watch.Stop();
        Check(total == 20000 && watch.Elapsed.TotalMilliseconds < 400.0, "gig music: 2,000 reads of a folder of ten take " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms (under 400)");
    }

    private static void GigMusicChoice()
    {
        Check(!GigMusicRules.UsesOwn(false, 5) && !GigMusicRules.UsesOwn(true, 0) && GigMusicRules.UsesOwn(true, 1), "gig music: the player's files play only with the option on and a file taken; otherwise the game's own sound, as before");
        Check(GigMusicRules.SaysEmpty(true, 0, false) && !GigMusicRules.SaysEmpty(true, 0, true) && !GigMusicRules.SaysEmpty(false, 0, false) && !GigMusicRules.SaysEmpty(true, 2, false),
            "gig music: an option on with no files is said once; never with it off or with files");
        Check(GigMusicRules.Following(0, 3) == 1 && GigMusicRules.Following(2, 3) == 0 && GigMusicRules.Following(0, 1) == 0 && GigMusicRules.Following(0, 0) == -1,
            "gig music: the next file wraps round to the first, and with none there is none");
        Check(GigMusicRules.LoopsAlone(1) && !GigMusicRules.LoopsAlone(2) && !GigMusicRules.LoopsAlone(0), "gig music: a single file plays over again and with more the next one plays");
        Check(GigMusicRules.FadeGain(-1f) == 1f && GigMusicRules.FadeGain(0f) == 1f && GigMusicRules.FadeGain(GigMusicRules.FadeSeconds) == 0f && GigMusicRules.FadeGain(9f) == 0f,
            "gig music: the fade starts at full volume and is silent at its end, and stays so");
        bool falling = true;
        float last = 1f;
        for (int i = 1; i <= 8; i++)
        {
            float gain = GigMusicRules.FadeGain(i * GigMusicRules.FadeSeconds / 8f);
            falling &= gain <= last && gain >= 0f;
            last = gain;
        }
        Check(falling && GigMusicRules.FadeSeconds > 0.2f && GigMusicRules.FadeSeconds <= 1f, "gig music: the fade only falls and is short (" + GigMusicRules.FadeSeconds + " s)");
    }

    private static void GigMusicFolder()
    {
        const string root = "data/GigMusic";
        string note = DiscPath.Combine(root, GigMusicRules.ReadmeName);
        var disc = new FakeDisc();
        Check(GigMusicRules.Prepare(disc, root, "NOTE") && disc.Has(root) && disc.Has(DiscPath.Combine(root, "Busking")) && disc.Text[note] == "NOTE",
            "gig music: the button makes the root, a folder for each gig and the note");
        disc.Add(DiscPath.Combine(root, "Busking"), "mine.mp3");
        disc.Text[note] = "EDITED";
        int files = disc.Files.Count;
        Check(GigMusicRules.Prepare(disc, root, "NOTE") && disc.Files.Count == files && disc.Text[note] == "EDITED" && disc.Files.ContainsKey(DiscPath.Combine(root, "Busking", "mine.mp3")),
            "gig music: pressing it again changes nothing the player has put there, not even the note they edited");
        var locked = new FakeDisc { Locked = true };
        Check(!GigMusicRules.Prepare(locked, root, "NOTE") && locked.Files.Count == 0, "gig music: a disc that cannot be written to says so and writes nothing");
        foreach (GigSound kind in Enum.GetValues(typeof(GigSound)))
        {
            Check(disc.Has(GigMusicRules.FolderOf(root, kind)), "gig music: the " + kind + " gig has its folder");
        }
    }

    private static void GigMusicSources()
    {
        string rules = ModSource(@"Jobs\GigMusicRules.cs");
        string music = ModSource(@"Jobs\GigMusic.cs");
        string game = ModSource(@"Jobs\GigMusicGame.cs");
        string busker = ModSource(@"Jobs\Kinds\BuskerJob.cs");
        string playing = ModSource(@"Jobs\Kinds\BuskerJob.Playing.cs");
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string list = ModSource(@"Meta\FeatureList.cs");
        string captions = ModSource(@"Meta\OptionsLayout.Captions.cs");

        string open = MemberText(music, "public static GigMusic Open(GigSound kind, MonoBehaviour runner, IModLogger log)");
        int off = open.IndexOf("if (!Settings.GigMusic", StringComparison.Ordinal);
        Check(off >= 0 && off < open.IndexOf("GigMusicRules.Read(", StringComparison.Ordinal) && open.Contains("StreetGame.SaveSeed()") && open.Contains("Settings.GigMusicOrder == 1"),
            "gig music: with the option off nothing is read; the shuffle is by the save's seed");
        Check(Times(busker, "GigMusic.Open(") == 1 && MemberText(busker, "protected override bool OnBegin(float now)").Contains("GigMusic.Open(GigSound.Busking"),
            "gig music: the folder is read once, as a busking set starts");
        string stopping = MemberText(playing, "private void Music(bool on)");
        Check(stopping.Contains("_own.Stop(_music)") && stopping.Contains("_own.Play(_music, PlayGameClip)") && Times(playing, "_own.Play(") == 1,
            "gig music: the music starts and stops where the game's clip did, and the game's clip takes over when no file can be played");
        Check(Times(busker, "_own") == 1, "gig music: the set's songs, strums, length and pay do not read the player's file");
        Check(Times(playing, "GigMusic ") == 1, "gig music: the gig reaches the music only through its one player");
        Check(music.Contains("handler.streamAudio = true;") && music.Contains("UnityEngine.Object.Destroy(clip)") && music.Contains("request?.Dispose()"),
            "gig music: a file is played as a stream and the clip and its reader are let go when it ends or the set stops");
        Check(music.Contains("Told.Add(path)") && music.Contains("\"reallife_gig_music_failed\"") && music.Contains("\"file\", name") && music.Contains("otherwise?.Invoke()") && music.Contains("_gaveUp = true;"),
            "gig music: a file that fails is named once in a notice, skipped, and with none left the game's own sound plays");
        Check(music.Contains("ToldEmpty.Add(kind)") && music.Contains("\"reallife_gig_music_empty\""), "gig music: an option on with no files is said once");
        Check(music.Contains("case \".flac\": return (AudioType)7;") && music.Contains("case \".wav\": return AudioType.WAV;") && music.Contains("case \".ogg\": return AudioType.OGGVORBIS;")
              && music.Contains("case \".aif\": return AudioType.AIFF;") && music.Contains("default: return AudioType.MPEG;"), "gig music: each type is opened as the game's own radio opens it");
        Check(!music.Contains("void Update") && !music.Contains("LateUpdate") && !music.Contains("FindObject") && !music.Contains("Time.deltaTime") && !music.Contains("yield return null"),
            "gig music: nothing per frame and no search: a look every half second while a file plays, and eight steps of a fade");
        Check(!music.Contains("modData") && !music.Contains("SaveGameManager") && !game.Contains("modData") && !music.Replace("UnityEngine.Networking", "").Contains("Network") && !music.Contains("GoingPublic")
              && !game.Contains("UnityWebRequest.Post") && !music.Contains("UnityWebRequest.Post"),
            "gig music: nothing of the player's files is saved or sent, so Going Public players neither hear nor receive them");
        Check(!rules.Contains("UnityEngine") && !rules.Contains("Random"), "gig music: the rules have no game types and no random sequence of the game's");
        Check(game.Contains("Path.Combine(Application.persistentDataPath, \"RealLifeOverhaul\", GigMusicRules.Root)") && game.Contains("GigMusicRules.Prepare(Disc,") && game.Contains("explorer.exe"),
            "gig music: the button makes the folder in the game's data folder and opens it, as the bug report opens the explorer");
        Check(!Directory.Exists(DiscPath.GetFullPath(DiscPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\GigMusic"))), "gig music: no music of anyone's is bundled with the mod");

        Check(settings.Contains("GigMusicId = \"gig_music\"") && settings.Contains("public static bool GigMusic = false;") && settings.Contains("GigMusic = Bool(modId, GigMusicId, GigMusic);")
              && settings.Contains("GigMusicOrderId = \"gig_music_order\"") && settings.Contains("public static int GigMusicOrder = 0;") && settings.Contains("GigMusicOrder = Int(modId, GigMusicOrderId, GigMusicOrder);"),
            "gig music: both options are saved and off by default (in order)");
        Check(mod.Contains("AddToggle(Settings.GigMusicId, \"reallife_gig_music_enabled\", Settings.GigMusic,") && mod.Contains("AddDropdown(Settings.GigMusicOrderId, \"reallife_gig_music_order\",")
              && mod.Contains("AddButton(\"reallife_gig_music_folder\", GigMusicGame.OpenFolder)"), "gig music: the toggle, the order and the folder button are on the options page");
        Check(tree.Contains("\"gig_music\").Opt(\"gig_music_order\", \"gig_music\").Buttons(\"reallife_gig_music_folder\")") && list.Contains("Settings.GigMusicId, Settings.GigMusicOrderId)") && list.Contains(".Rows(\"reallife_gig_music_folder\")")
              && captions.Contains("{ \"reallife_gig_music_folder\", CaptionOpen }"), "gig music: they stand in the jobs section of the options, are owned by the jobs feature and the button says Open");
    }

    private static void GigMusicWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (langs.Length == 0 || langs[0] == null)
        {
            Check(false, "gig music: the English texts are read");
            return;
        }
        Dictionary<string, string> en = langs[0];
        string missing = string.Empty;
        foreach (string key in new[] { "reallife_gig_music_enabled", "reallife_opt_tip_gig_music", "reallife_gig_music_order", "reallife_gig_music_order_0", "reallife_gig_music_order_1", "reallife_opt_tip_gig_music_order",
                                       "reallife_gig_music_folder", "reallife_gig_music_empty", "reallife_gig_music_failed", "reallife_gig_music_no_folder", "reallife_gig_music_readme" })
        {
            if (!en.ContainsKey(key))
            {
                missing += " " + key;
            }
        }
        Check(missing.Length == 0, "gig music: every text is in English:" + missing);
        en.TryGetValue("reallife_gig_music_enabled", out string label);
        Check(label == "Play your own music in gigs", "gig music: the option is named as the player asked");
        bool short240 = true;
        foreach (string key in new[] { "reallife_gig_music_empty", "reallife_gig_music_failed", "reallife_gig_music_no_folder" })
        {
            short240 &= en.TryGetValue(key, out string text) && text.Length <= 240 && !text.Contains("!") && !text.Contains("—") && !text.Contains("–");
        }
        Check(short240, "gig music: each notice is at most 240 characters with no exclamation mark or dash");
        bool tips = true;
        foreach (string key in new[] { "reallife_opt_tip_gig_music", "reallife_opt_tip_gig_music_order" })
        {
            tips &= en.TryGetValue(key, out string tip) && tip.Length <= 190 && tip.Split(' ').Length <= 32;
        }
        Check(tips, "gig music: each option's tip is at most 32 words and 190 characters");
        Check(en.TryGetValue("reallife_gig_music_failed", out string failed) && failed.Contains("{file}") && failed.Contains("{folder}"), "gig music: the notice for a file that fails names the file");
        en.TryGetValue("reallife_gig_music_readme", out string readme);
        Check(readme != null && readme.Contains("{files}") && readme.Contains("{megabytes}") && readme.Contains("{types}") && readme.Contains("{folder}") && !readme.Contains("!"),
            "gig music: the note in the folder says what to put where, the limits and the types");
        Check(en.TryGetValue("reallife_guide_jobs", out string guide) && guide.Contains("# Your own music in gigs") && guide.Contains("<b>Play your own music in gigs</b>") && guide.Contains("{gig_music_files}")
              && guide.Contains("Going Public"), "gig music: the jobs' Guide page has the section");
        Check(WhatsNewHas(en, "play your own music files in your busking sets"), "gig music: What's new has the line in English");
    }
}
