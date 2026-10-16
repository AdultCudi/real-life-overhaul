using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What an account's picture is made from (FlareAvatarRules.Plan).</summary>
    public enum FlareAvatarKind
    {
        Initials = 0,   // the styled stand-in: the name's initials on the account's own colour (also while a picture is on its way)
        Face = 1,       // a face from the game's portrait camera, of a look the mod gives (Female, Age, Seed): the city's people, friends, the boss
        Staff = 2,      // a face from the game's portrait camera of one of the player's own staff as the game has them (Name)
        Player = 3,     // the player: their latest selfie on Flare (Name: its picture), else the game's own portrait of their character
        Selfie = 4,     // another player of a Going Public session: their latest selfie in the shared feed (Name: its picture)
        Logo = 5,       // a business or a brand: the game's own logo of it (Name; Mine for a business of the player's)
        Emblem = 6      // an account that is not a person (an official account, the paper, a listed company, a candidate's campaign, a rival tycoon): a drawn emblem (Emblem)
    }

    /// <summary>One account's picture, as the rules choose it: the same for the same account every time, on every machine.</summary>
    public struct FlareAvatarPlan
    {
        public FlareAvatarKind Kind;
        public bool Female;          // a face: the look's sex
        public int Age;              // ...years
        public int Seed;             // ...the look's seed (the game's appearance system), never 0
        public string Name;          // Staff: the employee's name; Logo: the business's or brand's; Player and Selfie: the picture's key, or null
        public int Emblem;           // Emblem: FlareAvatarRules.Emblem*
        public bool Mine;            // Logo: a business of the player's (the game keeps its logo in the save's folder)

        /// <summary>The picture's own key: one texture for every account that shows the same picture (a face by its look, a logo by its name).</summary>
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

    /// <summary>
    /// What an account's picture is made from, as data the app hands over each time it asks (never kept by the rules):
    /// the player's friends with their looks, the file (influencers, posts with selfies), the city's seed, and the names of
    /// the player's own businesses.
    /// </summary>
    public sealed class FlareAvatarFacts
    {
        public IReadOnlyList<FlarePerson> People;
        public FlareFile File;
        public int CitySeed;
        public List<string> MyBusinesses;     // names
    }

    /// <summary>
    /// Every account's picture on Flare: which kind each account gets and from what, the round picture made from rows of
    /// pixels, the few textures kept (the ones used last), and the files they are kept in.
    ///
    /// THE KINDS. A person gets a face drawn by the game's own portrait camera from a look, as the Contacts app's people are
    /// (Phone/Contacts/ContactPainter): one of the city's people from the city's seed (the same face on every machine of a
    /// Going Public session), a friend or the partner from the look the social life builds them in the street with (the
    /// person the player has met is the face they see), an influencer from their own seed (or the friend they are), the
    /// boss from the account's seed, one of the staff as the game has them. The player shows their latest selfie posted on
    /// Flare, else the game's own portrait of their character (the one at the top of the screen). Another player of a
    /// session shows their latest selfie in the shared feed: no picture is ever sent for it. A business or a brand shows the
    /// game's own logo of it. An account that is not a person (the City Office, the police, the transit authority, the
    /// utilities, the bank, the City Ledger, a listed company, a candidate's campaign, a rival tycoon) shows an emblem,
    /// never a face: the tycoons are parodies, and a face would be somebody's likeness. Until a picture is there, and for
    /// any account with none, the initials on the account's own colour.
    ///
    /// THE PICTURE. Square, <see cref="Size"/> pixels, round: the middle of the source cropped square, made smaller by
    /// averaging, and a clear corner outside a circle with a soft edge of one pixel, so the app needs no mask (a mask a
    /// row would cost the canvas a draw call each). RGBA, rows from the bottom up, as the engine keeps them.
    ///
    /// THE KEEPING. At most <see cref="Most"/> textures (36 KB each: 2.3 MB in all); the one used longest ago goes first
    /// (<see cref="FlareAvatarLru"/>). A face is kept on disk as its round picture's colour rows (Core/PhotoThumbs' format,
    /// 27 KB) under the game's data folder, by its look: drawn once per look, ever, and shared by every save that has it.
    ///
    /// Game-free (EconomyCheck, FlarePictureChecks.cs). COST: a plan is a few comparisons (a selfie's a look over the
    /// posts, two hundred at most), asked when a card is bound, never per frame; a round picture is some 40,000 sums, made
    /// on a worker thread.
    /// </summary>
    public static class FlareAvatarRules
    {
        public const int Size = 96;                  // pixels of a round picture: a card's avatar is 60 to 150 of the canvas's 2160 lines, 30 to 75 pixels at 1080p
        public const int Most = 64;                  // textures kept: a screen shows a dozen accounts, a scroll back finds them still there
        public const int AgeLeast = 19;              // years: the city's people and influencers
        public const int AgeSpan = 52;               // ...up to 70
        public const string FilePrefix = "flare96_"; // the round pictures' files among the portrait cache's (Phone/Contacts' faces are "v1_...jpg")
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
                    plan.Kind = FlareAvatarKind.Emblem;   // a rival tycoon: never a face (a parody: a face would be somebody's likeness)
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
                    return plan;   // (a player of the session with no selfie in the feed keeps the initials: no face that is not theirs)
                default:
                    return plan;
            }
        }

        /// <summary>One of the city's people: a face from the city's seed (their sex as their first name says).</summary>
        public static FlareAvatarPlan Citizen(int citySeed, int index)
        {
            return FacePlan(FlareLiveRules.CitizenFemale(citySeed, index), AgeLeast + FlareRules.Roll(citySeed, index, SaltAge) % AgeSpan, Seed(citySeed, index, SaltSeed));
        }

        // A friend, the partner or anybody of the social life: the look they are built in the street with; one of the
        // account's own when the social life has not told it.
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

        // A look's seed, never 0 (0 means "none" to the portrait cache's name and the contacts).
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
        /// The picture of the latest selfie posted: the player's own (authorId null; a selfie of photo mode or the Camera,
        /// posted with its picture) or another player's of the session (their shared post's picture, "n:"). Null for none.
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
            return FilePrefix + "v1_" + (female ? "f" : "m") + Math.Max(0, age) + "_" + Math.Max(0, seed) + FileExtension;
        }

        /// <summary>One of the staff's round picture's file, by the save and the name (a stable hash: the same file each session).</summary>
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

        // ---------------------------------------------------------------- the round picture

        /// <summary>
        /// The round picture of a source's rows: the middle of it cropped square (a portrait's own square whole; a wide selfie's
        /// middle), made <see cref="Size"/> pixels by averaging, as RGBA rows from the bottom up with a clear corner outside the
        /// circle. 'bytes' 3 (RGB) or 4 (RGBA; 'argb' for the engine's ARGB32 order). The rows keep the order they came in.
        /// False, with nothing written, for a source that is not whole. 'into' must be Size * Size * 4 bytes.
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
                    float edge = radius - (float)Math.Sqrt(dx * dx + dy * dy) + 0.5f;   // inside by this many pixels (a soft edge of one)
                    into[o + 3] = (byte)(edge >= 1f ? 255 : edge <= 0f ? 0 : (int)(edge * 255f));
                }
            }
            return true;
        }

        /// <summary>The colour rows of a round picture (for its file: Core/PhotoThumbs' format, RGB): 'into' Size * Size * 3 bytes.</summary>
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
    /// The textures kept, by their key, the one used last at the end: at most a set number; the one used longest ago goes
    /// first. Game-free. COST: a look along the list (64 at most) when a card is bound or a picture comes; nothing per frame.
    /// </summary>
    public sealed class FlareAvatarLru
    {
        private readonly List<string> _used;
        public readonly int Most;

        public FlareAvatarLru(int most)
        {
            Most = Math.Max(1, most);
            _used = new List<string>(Most + 1);
        }

        public int Count => _used.Count;

        public bool Contains(string key) => _used.Contains(key);

        /// <summary>A key used now: to the end. Returns the key that has to go to keep within the bound, or null.</summary>
        public string Use(string key)
        {
            if (key == null)
            {
                return null;
            }
            if (_used.Count == 0 || _used[_used.Count - 1] != key)
            {
                _used.Remove(key);
                _used.Add(key);
            }
            if (_used.Count <= Most)
            {
                return null;
            }
            string oldest = _used[0];
            _used.RemoveAt(0);
            return oldest;
        }

        public void Remove(string key) => _used.Remove(key);

        public void Clear() => _used.Clear();
    }
}
