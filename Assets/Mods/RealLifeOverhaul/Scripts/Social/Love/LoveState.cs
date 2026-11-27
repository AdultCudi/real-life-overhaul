using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The love life of the save that is loaded: <see cref="LoveFile"/> and the line it came from. A save without the key is an empty
    /// file; a line this version cannot read is kept as it was read and written back, so a later version can (Core/SaveLines).
    /// Game-free (EconomyCheck, LoveChecks.cs).
    /// </summary>
    public static class LoveState
    {
        public static LoveFile File = new LoveFile();

        public static bool Loaded { get; private set; }

        private static string _unreadable;

        public static void Reset()
        {
            File = new LoveFile();
            Loaded = false;
            _unreadable = null;
        }

        /// <summary>Reads the save's line; a line no one here can read is kept, and the file starts empty.</summary>
        public static void Load(Dictionary<string, string> data)
        {
            Reset();
            string text = null;
            if (data != null)
            {
                data.TryGetValue(LoveStore.Key, out text);
            }
            LoveFile read = LoveStore.Read(text);
            if (read == null)
            {
                _unreadable = text;
                SaveLines.Unreadable(data, LoveStore.Key, "it could not be read");
            }
            else
            {
                File = read;
            }
            Loaded = true;
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (!Loaded || data == null)
            {
                return;
            }
            if (_unreadable != null)
            {
                data[LoveStore.Key] = _unreadable;   // as it was read
                return;
            }
            string text = LoveStore.Write(File);
            if (text.Length > 0)
            {
                data[LoveStore.Key] = text;
            }
            else
            {
                data.Remove(LoveStore.Key);
            }
        }

        /// <summary>This cast's seed for everything the love life rolls (<see cref="LoveRules.SeedOf"/>).</summary>
        public static int Seed => LoveRules.SeedOf(SocialState.CastSeed);

        /// <summary>Whether the person in this slot is the player's second, the one they are seeing besides their partner.</summary>
        public static bool IsSecond(int index)
        {
            return Loaded && index >= 0 && File.Second == index;
        }

        /// <summary>The second person while they are still together with the player; null for nobody.</summary>
        public static SocialPerson SecondPerson
        {
            get
            {
                if (!Loaded || File.Second < 0)
                {
                    return null;
                }
                SocialPerson p = SocialState.Get(File.Second);
                return p != null && p.Met && p.Together ? p : null;
            }
        }

        /// <summary>
        /// Puts the file right with the cast: a second who is no longer with the player, or a partner who is gone and left the second
        /// alone, is nobody's second any more. True if anything changed.
        /// </summary>
        public static bool Tidy()
        {
            if (!Loaded || File.Second < 0)
            {
                return false;
            }
            SocialPerson second = SecondPerson;
            SocialPerson first = FirstPerson;
            if (second == null || first == null)
            {
                int was = File.Second;
                File.EndSecond();
                File.ConfrontWho = File.ConfrontWho == was ? -1 : File.ConfrontWho;
                return true;
            }
            return false;
        }

        /// <summary>The player's first partner: the one who is together with them and not the second.</summary>
        public static SocialPerson FirstPerson
        {
            get
            {
                for (int i = 0; SocialState.Loaded && i < SocialState.Count; i++)
                {
                    SocialPerson p = SocialState.People[i];
                    if (p != null && p.Together && !IsSecond(p.Index))
                    {
                        return p;
                    }
                }
                return null;
            }
        }
    }

    /// <summary>
    /// What the love life asks of the other features and tells them, each null until its owner sets it (Wiring): how well known the
    /// player is, the dent in business standing and what Flare makes of a scandal, whether a person was tagged lately on Flare, and
    /// how Family takes a separation, a child and the talk at home. Without an owner the love life stands on its own.
    /// </summary>
    public static class LoveLinks
    {
        /// <summary>Standing: how known the player is, 0 to 100; null or -1 when Standing is off.</summary>
        public static Func<int> Recognition;

        /// <summary>Standing: business standing takes a small dent of so many points, which fades by itself.</summary>
        public static Action<int> Dent;

        /// <summary>Standing: integrity falls by so many points (a child left to its other parent).</summary>
        public static Action<int> Integrity;

        /// <summary>Flare: followers lost and gained, per mille of the followers; returns the numbers of people (lost, gained), null while Flare is off.</summary>
        public static Func<int, int, int[]> FlareGossip;

        /// <summary>Parties: a guest who came to a party the player was at, with the party's quality; set by the director.</summary>
        public static Action<SocialPerson, int> PartyGuest;

        /// <summary>Family: this person is the partner and lives with the player, is engaged to them or married to them.</summary>
        public static Func<SocialPerson, bool> Bound;

        /// <summary>Family's separation flow; true when it is their decision.</summary>
        public static Action<bool> Separate;

        /// <summary>Family: the partner at home waits to talk about it; speaking to them opens the talk.</summary>
        public static Action AskHome;

        /// <summary>Family: nobody waits at home to talk about it any more.</summary>
        public static Action AskDone;

        /// <summary>Family: the in-laws hear of it and say so (the text's key).</summary>
        public static Action<string> InLawsHear;

        /// <summary>
        /// Family: a child of a night joins the family's children (girl, the other parent's name and slot, lives with
        /// them, who carried); returns its name, null if Family cannot take it.
        /// </summary>
        public static Func<bool, string, int, bool, int, string> AddChild;

        /// <summary>Family: how many more children the family can take; 0 with Family off.</summary>
        public static Func<int> ChildRoom;

        /// <summary>Family: what comes next for this partner after living together, as a line for their page; null for nothing to say.</summary>
        public static Func<SocialPerson, string> NextStep;
    }
}
