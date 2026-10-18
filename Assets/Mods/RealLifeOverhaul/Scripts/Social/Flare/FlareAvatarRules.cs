using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What an account's picture is made from (FlareAvatarRules.Plan).</summary>
    public enum FlareAvatarKind
    {
        Initials = 0,   // the name's initials on the account's colour, also while a picture is on its way
        Face = 1,       // the game's portrait camera, of a look the mod gives (Female, Age, Seed)
        Staff = 2,      // the game's portrait camera, of one of the player's staff (Name)
        Player = 3,     // the player's latest selfie on Flare (Name), else the game's portrait of their character
        Selfie = 4,     // another player of a session: their latest selfie in the shared feed (Name)
        Logo = 5,       // a business or a brand: the game's logo of it (Name; Mine for the player's own)
        Emblem = 6      // an account that is not a person: a drawn emblem (Emblem)
    }

    /// <summary>One account's picture, as the rules choose it: the same for the same account every time, on every machine.</summary>
    public struct FlareAvatarPlan
    {
        public FlareAvatarKind Kind;
        public bool Female;          // a face: the look's sex
        public int Age;              // years
        public int Seed;             // the look's seed, never 0
        public string Name;          // Staff: the employee's name; Logo: the business's or brand's; Player and Selfie: the picture's key, or null
        public int Emblem;           // Emblem: FlareAvatarRules.Emblem*
        public bool Mine;            // Logo: a business of the player's, whose logo the game keeps in the save's folder

        /// <summary>The picture's key: one texture for every account that shows the same picture.</summary>
        public string Picture
        {
            get
            {
                switch (Kind)
                {
                    case FlareAvatarKind.Face: return "face:" + FlareAvatarRules.FaceName(Female, Age, Seed);
                    case FlareAvatarKind.Staff: return "staff:" + Name;
                    case FlareAvatarKind.Player: return "me:" + (Name ?? "portrait");
                    case FlareAvatarKind.Selfie: return "selfie:" + Name;
                    case FlareAvatarKind.Logo: return "logo:" + Name;
                    default: return null;
                }
            }
        }
    }

    /// <summary>What an account's picture is made from, handed over each time the app asks; never kept by the rules.</summary>
    public sealed class FlareAvatarFacts
    {
        public IReadOnlyList<FlarePerson> People;
        public FlareFile File;
        public int CitySeed;
        public List<string> MyBusinesses;     // names
    }

    /// <summary>
    /// Every account's picture on Flare: which kind each account gets and from what, the round picture made from rows of
    /// pixels, and the files they are kept in.
    ///
    /// A person gets a face from the game's own portrait camera, as the Contacts app's people do: a city person's from the
    /// city's seed (the same on every machine of a session), a friend's from the look the social life builds them with, an
    /// influencer's from their seed. The player shows their latest selfie, else the game's portrait of their character;
    /// another player their latest shared selfie, so no picture is ever sent for it. A business or a brand shows the game's
    /// logo. An account that is not a person shows an emblem, never a face: the tycoons are parodies, and a face would be
    /// somebody's likeness. Until a picture is there, the initials.
    ///
    /// The picture is <see cref="Size"/> pixels square, round with a soft edge, so the app needs no mask (a draw call a
    /// row). A face is kept on disk by its look under the game's data folder, shared by every save that has it.
    /// Game-free. Cost: a plan is a few comparisons when a card is bound; a round picture some 40,000 sums on a worker thread.
    /// </summary>
    public static class FlareAvatarRules
    {
        public const int Size = 96;                  // pixels: a card's avatar is 30 to 75 pixels at 1080p
        public const int Most = 512;                 // textures kept, 36 KB each: a city's 400 people, its influencers and the friends all fit
        public const int AgeLeast = 19;              // years: the influencers and a friend with no look told
        public const int AgeSpan = 52;               // ...up to 70
        public const string FilePrefix = "flare96_"; // the round pictures' files among the portrait cache's
        public const string FileExtension = ".thumb";

        public const int EmblemCity = 0, EmblemPolice = 1, EmblemTransit = 2, EmblemUtilities = 3, EmblemBank = 4, EmblemLedger = 5, EmblemCompany = 6,
            EmblemCandidate = 7, EmblemTycoon = 8, EmblemBusiness = 9;

        private const int SaltSex = 610, SaltAge = 611, SaltSeed = 612, SaltSeedLow = 613, SaltBoss = 620;

        /// <summary>The picture an account shows. Every key the app shows an account by is one of these kinds.</summary>
        public static FlareAvatarPlan Plan(string key, FlareAvatarFacts facts)
        {
            var plan = new FlareAvatarPlan { Kind = FlareAvatarKind.Initials };
            if (string.IsNullOrEmpty(key) || facts == null)
            {
                return plan;
            }
            int n;
            if (key == "me")
            {
                plan.Kind = FlareAvatarKind.Player;
                plan.Name = LatestSelfie(facts.File, null);
                return plan;
            }
            if (key == "b")
            {
                int seed = facts.File != null ? facts.File.Seed : 0;   // the boss: a face of the account's own, the same for the save
                return FacePlan(FlareRules.Roll(seed, 1, SaltBoss) % 2 == 0, AgeLeast + 22 + FlareRules.Roll(seed, 2, SaltBoss) % 25, Seed(seed, 3, SaltBoss));
            }
            if (key.Length > 1 && key[1] != ':')
            {
                switch (key[0])
                {
                    case 'c':
                        return int.TryParse(key.Substring(1), out n) && n >= 0 ? Citizen(facts.CitySeed, n) : plan;
                    case 'f':
                    case 'p':
                        return int.TryParse(key.Substring(1), out n) ? Person(facts, n) : plan;
                }
                return plan;
            }
            if (key.Length < 2)
            {
                return plan;
            }
            string rest = key.Length > 2 ? key.Substring(2) : string.Empty;
            switch (key[0])
            {
                case 'o':
                    plan.Kind = FlareAvatarKind.Emblem;
                    plan.Emblem = int.TryParse(rest, out n) ? Official(n) : EmblemCity;
                    return plan;
                case 'l':
                    plan.Kind = FlareAvatarKind.Emblem;
                    plan.Emblem = EmblemLedger;
                    return plan;
                case 'm':
                    plan.Kind = FlareAvatarKind.Emblem;
                    plan.Emblem = EmblemCompany;
                    return plan;
                case 'c':
                    plan.Kind = FlareAvatarKind.Emblem;   // a candidate's campaign account: its emblem, as an election poster has one
                    plan.Emblem = EmblemCandidate;
                    return plan;
                case 't':
                    plan.Kind = FlareAvatarKind.Emblem;   // a rival tycoon: never a face
                    plan.Emblem = EmblemTycoon;
                    return plan;
                case 'k':
                case 'b':
                    if (rest.Length == 0)
                    {
                        return plan;
                    }
                    plan.Kind = FlareAvatarKind.Logo;
                    plan.Name = rest;
                    plan.Mine = key[0] == 'k' && facts.MyBusinesses != null && facts.MyBusinesses.Contains(rest);
                    return plan;
                case 's':
                    if (rest.Length == 0)
                    {
                        return plan;
                    }
                    plan.Kind = FlareAvatarKind.Staff;
                    plan.Name = rest;
                    return plan;
                case 'i':
                    return int.TryParse(rest, out n) ? Influencer(facts, n) : plan;
                case 'p':
                    string selfie = LatestSelfie(facts.File, rest);
                    if (selfie != null)
                    {
                        plan.Kind = FlareAvatarKind.Selfie;
                        plan.Name = selfie;
                    }
                    return plan;   // a session's player with no selfie in the feed keeps the initials
                default:
                    return plan;
            }
        }

        /// <summary>One of the city's people: a face from the city's seed, of the person's own sex and age.</summary>
        public static FlareAvatarPlan Citizen(int citySeed, int index)
        {
            return FacePlan(FlareLiveRules.CitizenFemale(citySeed, index), FlareLiveRules.CitizenAge(citySeed, index), Seed(citySeed, index, SaltSeed));
        }

        // A friend, the partner or anybody of the social life: the look they are built in the street with, else one of the
        // account's own.
        private static FlareAvatarPlan Person(FlareAvatarFacts facts, int index)
        {
            IReadOnlyList<FlarePerson> people = facts.People;
            for (int i = 0; people != null && i < people.Count; i++)
            {
                if (people[i].Index == index && people[i].LookSeed != 0)
                {
                    return FacePlan(people[i].Female, people[i].Age, people[i].LookSeed);
                }
            }
            int seed = facts.File != null ? facts.File.Seed : 0;
            return FacePlan(FlareRules.Roll(seed, index, SaltSex) % 2 == 0, AgeLeast + FlareRules.Roll(seed, index, SaltAge) % AgeSpan, Seed(seed, index, SaltSeed));
        }

        // An influencer: the friend they are, else a face of their own seed.
        private static FlareAvatarPlan Influencer(FlareAvatarFacts facts, int id)
        {
            FlareInfluencer inf = facts.File != null ? facts.File.Influencer(id) : null;
            if (inf == null)
            {
                return new FlareAvatarPlan { Kind = FlareAvatarKind.Initials };
            }
            if (inf.Person >= 0)
            {
                IReadOnlyList<FlarePerson> people = facts.People;
                for (int i = 0; people != null && i < people.Count; i++)
                {
                    if (people[i].Index == inf.Person && people[i].LookSeed != 0)
                    {
                        return FacePlan(people[i].Female, people[i].Age, people[i].LookSeed);
                    }
                }
            }
            return FacePlan(FlareRules.Roll(inf.Seed, inf.Id, SaltSex) % 2 == 0, AgeLeast + FlareRules.Roll(inf.Seed, inf.Id, SaltAge) % 18, Seed(inf.Seed, inf.Id, SaltSeed));
        }

        private static FlareAvatarPlan FacePlan(bool female, int age, int seed)
        {
            return new FlareAvatarPlan { Kind = FlareAvatarKind.Face, Female = female, Age = Math.Max(18, Math.Min(85, age)), Seed = Math.Max(1, seed) };
        }

        // A look's seed, never 0: 0 means "none" to the portrait cache's name and the contacts.
        private static int Seed(int seed, int index, int salt)
        {
            return 1 + FlareRules.Roll(seed, index, salt) * 1000 + FlareRules.Roll(seed, index, salt + 1);
        }

        /// <summary>The emblem of an official account (FlareOfficial).</summary>
        public static int Official(int official)
        {
            switch ((FlareOfficial)official)
            {
                case FlareOfficial.Police: return EmblemPolice;
                case FlareOfficial.Transit: return EmblemTransit;
                case FlareOfficial.Utilities: return EmblemUtilities;
                case FlareOfficial.Bank: return EmblemBank;
                default: return EmblemCity;
            }
        }

        /// <summary>
        /// The picture of the latest selfie posted: the player's own (<paramref name="authorId"/> null) or another session
        /// player's shared one ("n:"). Null for none.
        /// </summary>
        public static string LatestSelfie(FlareFile f, string authorId)
        {
            if (f == null)
            {
                return null;
            }
            for (int i = f.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = f.Posts[i];
                if (p.Source != FlareSource.Selfie || p.ReplyTo >= 0)
                {
                    continue;
                }
                if (authorId == null && p.Author == FlareAuthor.Player)
                {
                    string own = !string.IsNullOrEmpty(p.Pic) ? p.Pic : !string.IsNullOrEmpty(p.Name) ? "f:" + p.Name : null;
                    if (own != null && own.StartsWith("f:", StringComparison.Ordinal))
                    {
                        return own;
                    }
                }
                else if (authorId != null && p.Author == FlareAuthor.Network && p.AuthorId == authorId && p.Pic != null && p.Pic.StartsWith("n:", StringComparison.Ordinal))
                {
                    return p.Pic;
                }
            }
            return null;
        }

        /// <summary>A face's file among the portrait cache's: its round picture, by its look.</summary>
        public static string FaceName(bool female, int age, int seed)
        {
            return FilePrefix + "v1_" + (female ? "f" : "m") + Math.Max(0, age) + "_" + Math.Max(0, seed) + PersonLooks.Mark(seed) + FileExtension;   // a kept look's apart: People/PersonLook
        }

        /// <summary>One of the staff's round picture's file, by the save and the name: a stable hash, the same file each session.</summary>
        public static string StaffName(string saveId, string name)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; saveId != null && i < saveId.Length; i++)
                {
                    hash = (hash ^ saveId[i]) * 16777619u;
                }
                hash = (hash ^ '|') * 16777619u;
                for (int i = 0; name != null && i < name.Length; i++)
                {
                    hash = (hash ^ name[i]) * 16777619u;
                }
                return FilePrefix + "staff_" + hash.ToString("x8") + FileExtension;
            }
        }

        /// <summary>
        /// The round picture of a source's rows: its middle cropped square, shrunk to <see cref="Size"/> by averaging, as RGBA
        /// rows with a clear corner outside the circle. <paramref name="bytes"/> 3 or 4 (<paramref name="argb"/> for ARGB32);
        /// <paramref name="into"/> is Size * Size * 4 bytes. False, writing nothing, for a source that is not whole.
        /// </summary>
        public static bool Disc(byte[] src, int w, int h, int bytes, bool argb, byte[] into)
        {
            if (src == null || into == null || w <= 0 || h <= 0 || (bytes != 3 && bytes != 4) || src.Length < (long)w * h * bytes || into.Length < Size * Size * 4)
            {
                return false;
            }
            int side = Math.Min(w, h), x0 = (w - side) / 2, y0 = (h - side) / 2;
            int r0 = argb ? 1 : 0, g0 = argb ? 2 : 1, b0 = argb ? 3 : 2;
            float centre = Size * 0.5f, radius = Size * 0.5f - 0.5f;
            for (int y = 0; y < Size; y++)
            {
                int ya = y0 + y * side / Size, yb = Math.Max(ya + 1, y0 + (y + 1) * side / Size);
                for (int x = 0; x < Size; x++)
                {
                    int xa = x0 + x * side / Size, xb = Math.Max(xa + 1, x0 + (x + 1) * side / Size);
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int sy = ya; sy < yb; sy++)
                    {
                        int row = sy * w;
                        for (int sx = xa; sx < xb; sx++)
                        {
                            int at = (row + sx) * bytes;
                            r += src[at + r0];
                            g += src[at + g0];
                            b += src[at + b0];
                            n++;
                        }
                    }
                    int o = (y * Size + x) * 4;
                    n = Math.Max(1, n);
                    into[o] = (byte)(r / n);
                    into[o + 1] = (byte)(g / n);
                    into[o + 2] = (byte)(b / n);
                    float dx = x + 0.5f - centre, dy = y + 0.5f - centre;
                    float edge = radius - (float)Math.Sqrt(dx * dx + dy * dy) + 0.5f;   // inside by this many pixels: a soft edge of one
                    into[o + 3] = (byte)(edge >= 1f ? 255 : edge <= 0f ? 0 : (int)(edge * 255f));
                }
            }
            return true;
        }

        /// <summary>The colour rows of a round picture, for its file (Core/PhotoThumbs' RGB): <paramref name="into"/> Size * Size * 3 bytes.</summary>
        public static void ColourOf(byte[] rgba, byte[] into)
        {
            for (int i = 0, o = 0; i < Size * Size * 4; i += 4, o += 3)
            {
                into[o] = rgba[i];
                into[o + 1] = rgba[i + 1];
                into[o + 2] = rgba[i + 2];
            }
        }

        /// <summary>A round picture again from its file's colour rows (the circle drawn again): false for rows that are not one.</summary>
        public static bool FromColour(byte[] rgb, int w, int h, byte[] into)
        {
            return w == Size && h == Size && Disc(rgb, w, h, 3, false, into);
        }
    }

    /// <summary>
    /// The textures kept by key, at most <see cref="Most"/>; the one used longest ago goes first. A linked list and a
    /// dictionary of its nodes, so a use is a look and a move, and nodes are reused. Game-free; nothing per frame.
    /// </summary>
    public sealed class FlareAvatarLru
    {
        private readonly LinkedList<string> _used = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> _at;
        private readonly Stack<LinkedListNode<string>> _spare = new Stack<LinkedListNode<string>>(4);
        public readonly int Most;

        public FlareAvatarLru(int most)
        {
            Most = Math.Max(1, most);
            _at = new Dictionary<string, LinkedListNode<string>>(Math.Min(Most, 1024) + 1, StringComparer.Ordinal);
        }

        public int Count => _used.Count;

        public bool Contains(string key) => key != null && _at.ContainsKey(key);

        /// <summary>A key used now: to the end. Returns the key that has to go to keep within the bound, or null.</summary>
        public string Use(string key)
        {
            if (key == null)
            {
                return null;
            }
            LinkedListNode<string> node;
            if (_at.TryGetValue(key, out node))
            {
                if (node != _used.Last)
                {
                    _used.Remove(node);
                    _used.AddLast(node);
                }
                return null;
            }
            node = _spare.Count > 0 ? _spare.Pop() : new LinkedListNode<string>(key);
            node.Value = key;
            _used.AddLast(node);
            _at[key] = node;
            if (_used.Count <= Most)
            {
                return null;
            }
            LinkedListNode<string> oldest = _used.First;
            string gone = oldest.Value;
            _used.RemoveFirst();
            _at.Remove(gone);
            _spare.Push(oldest);
            return gone;
        }

        public void Remove(string key)
        {
            LinkedListNode<string> node;
            if (key != null && _at.TryGetValue(key, out node))
            {
                _used.Remove(node);
                _at.Remove(key);
                _spare.Push(node);
            }
        }

        public void Clear()
        {
            while (_used.First != null)
            {
                LinkedListNode<string> node = _used.First;
                _used.RemoveFirst();
                _spare.Push(node);
            }
            _at.Clear();
        }
    }
}
