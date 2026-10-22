using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>
    /// Permadeath's part of one save, in its modData (a save made with it loads without the mod). Game-free.
    ///   reallife:permadeath_state    v1|on|dead|warned bits|flu began at|flu warnings shown|day switched on|
    ///                                minute the hunger bar reached its bottom|its warnings shown (a line without the
    ///                                last two reads as fed)
    ///   reallife:permadeath_record   the life as it ended (<see cref="LifeStory"/>), only once it has
    ///   reallife:permadeath_seen     the businesses this save has run, by address, for the obituary's count of those closed
    /// The switch is per save, not an option. Once dead a save stays dead: nothing here can be set back.
    /// </summary>
    public static class PermadeathState
    {
        public const string Key = "reallife:permadeath_state";
        public const string RecordKey = "reallife:permadeath_record";
        public const string SeenKey = "reallife:permadeath_seen";
        public const int SeenMost = 200;     // businesses remembered: far more than a life has
        private const int Version = 1;

        public static bool Loaded { get; private set; }

        /// <summary>True if this save had no Permadeath line when it was read.</summary>
        public static bool Fresh { get; private set; }

        public static bool Armed;            // on for this save
        public static bool Dead;             // the character has died: the save is read, never played
        public static int Warned;            // a bit per DeathCause whose near miss this save has had
        public static float IllSince = -1f;  // the game minute the flu the warnings belong to began; -1 none
        public static int IllTold;           // how many of its warnings have been shown, 0 to 3
        public static int ArmedDay;          // the day it was last switched on
        public static float EmptySince = -1f; // the game minute the hunger bar reached its bottom, while it stays there; -1 fed
        public static int StarveTold;        // how many of going without food's warnings have been shown, 0 to 3

        public static readonly LifeStory Record = new LifeStory();   // only while Dead
        public static readonly HashSet<string> Seen = new HashSet<string>();

        /// <summary>The switch moved on in the main menu, for the next game begun in this run; taken by the first save read. Not saved.</summary>
        public static bool ArmNext;

        public static void Reset()
        {
            Loaded = false;
            Fresh = false;
            Armed = false;
            Dead = false;
            Warned = 0;
            IllSince = -1f;
            IllTold = 0;
            ArmedDay = 0;
            EmptySince = -1f;
            StarveTold = 0;
            Record.Clear();
            Seen.Clear();
        }

        public static bool HasWarned(DeathCause cause) => (Warned & Bit(cause)) != 0;

        public static void Warn(DeathCause cause)
        {
            Warned |= Bit(cause);
        }

        private static int Bit(DeathCause cause) => 1 << (int)cause;

        public static void Read(Dictionary<string, string> data)
        {
            Reset();
            string line = null;
            Fresh = data == null || !data.TryGetValue(Key, out line) || string.IsNullOrEmpty(line);
            if (!Fresh)
            {
                string[] f = line.Split('|');
                if (f.Length >= 7 && f[0] == "v" + Version)
                {
                    Armed = f[1] == "1";
                    Dead = f[2] == "1";
                    Warned = Int(f[3]);
                    IllSince = float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float since) ? since : -1f;
                    IllTold = Clamp(Int(f[5]), 0, PermadeathRules.IllStages - 1);
                    ArmedDay = Int(f[6]);
                    if (f.Length >= 9)
                    {
                        EmptySince = float.TryParse(f[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float empty) ? empty : -1f;
                        StarveTold = Clamp(Int(f[8]), 0, PermadeathRules.IllStages - 1);
                        if (EmptySince < 0f)
                        {
                            StarveTold = 0;
                        }
                    }
                }
                else
                {
                    Fresh = true;   // a version this does not know: taken as never seen (off), never as dead
                }
            }
            if (Dead && (data == null || !data.TryGetValue(RecordKey, out string record) || !Record.Read(record)))
            {
                Record.Clear();   // dead without a readable record: the obituary is written from what the save still says
            }
            if (data != null && data.TryGetValue(SeenKey, out string seen) && !string.IsNullOrEmpty(seen))
            {
                foreach (string place in seen.Split(';'))
                {
                    if (place.Length > 0 && Seen.Count < SeenMost)
                    {
                        Seen.Add(place);
                    }
                }
            }
            Loaded = true;
        }

        public static void Write(Dictionary<string, string> data)
        {
            if (!Loaded || data == null)
            {
                return;
            }
            var t = new StringBuilder(64);
            t.Append('v').Append(Version).Append('|').Append(Armed ? '1' : '0').Append('|').Append(Dead ? '1' : '0').Append('|')
             .Append(Warned.ToString(CultureInfo.InvariantCulture)).Append('|').Append(IllSince.ToString("R", CultureInfo.InvariantCulture)).Append('|')
             .Append(IllTold.ToString(CultureInfo.InvariantCulture)).Append('|').Append(ArmedDay.ToString(CultureInfo.InvariantCulture)).Append('|')
             .Append(EmptySince.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(StarveTold.ToString(CultureInfo.InvariantCulture));
            data[Key] = t.ToString();
            if (Dead)
            {
                data[RecordKey] = Record.Write();
            }
            t.Length = 0;
            foreach (string place in Seen)
            {
                if (t.Length > 0)
                {
                    t.Append(';');
                }
                t.Append(place.Replace(';', ' '));
            }
            data[SeenKey] = t.ToString();
        }

        /// <summary>A business this save runs, by its address: remembered once. True if it was new.</summary>
        public static bool See(string place)
        {
            return !string.IsNullOrEmpty(place) && Seen.Count < SeenMost && Seen.Add(place);
        }

        /// <summary>The flu the warnings belong to: a flu that began at another minute starts the count again.</summary>
        public static void Flu(float began)
        {
            if (IllSince < 0f || System.Math.Abs(IllSince - began) > 0.5f)
            {
                IllSince = began;
                IllTold = 0;
            }
        }

        /// <summary>No flu, or one treated: its warnings are over.</summary>
        public static void NoFlu()
        {
            IllSince = -1f;
            IllTold = 0;
        }

        /// <summary>The hunger bar at its bottom: the count starts at <paramref name="now"/> unless it is already running.</summary>
        public static void Empty(float now)
        {
            if (EmptySince < 0f)
            {
                EmptySince = now;
                StarveTold = 0;
            }
        }

        /// <summary>Something eaten, or the bar above its bottom: the danger and its warnings are over.</summary>
        public static void Fed()
        {
            EmptySince = -1f;
            StarveTold = 0;
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
