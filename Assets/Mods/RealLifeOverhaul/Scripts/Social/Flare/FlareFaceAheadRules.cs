using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>One face's file in the portrait cache, as the clean-up sees it (FlareFaceAheadRules.Evict).</summary>
    public struct FlareFaceFile
    {
        public string Name;          // its file name (FlareAvatarRules.FilePrefix ...)
        public long Touched;         // when it was last written or read, in ticks (its file's last write time: a read older than a day writes it again)
    }

    /// <summary>
    /// Which faces Flare draws ahead of need, and when, so that its accounts show faces rather than initials without a
    /// frame of play ever paying for one (FlareFacePainter draws them; FlareAvatarRules says which face each account has):
    ///
    ///   AT A LOAD     behind the game's loading screen (Core/LoadHold holds it for <see cref="LoadBudgetMs"/> at most): the
    ///                 faces likely to be seen first that are not on disk yet, at most <see cref="LoadMost"/>, most likely
    ///                 first (<see cref="Likely"/>), one after another with no rest; another starts only while the time left
    ///                 holds one more at the cost measured so far (<see cref="Another"/>). The rest wait for the black screens.
    ///   BEHIND A BLACK SCREEN  (a door, sleep, travel) the faces still waiting of the same list, made again from the live
    ///                 account when Flare starts and each game day, at most <see cref="AheadMost"/>, with the camera's rest
    ///                 of a quarter of a second between faces (Phone/Contacts/PortraitTurn).
    ///   WITH FLARE OPEN  a face a card in view asks for and nobody drew yet, two seconds apart; the initials meanwhile.
    ///
    /// LIKELY, in this order, each face once: the people the player knows (the partner first: Mom and Alex are among them),
    /// the active influencers, then the kept posts newest first (their author, the people with names who reacted, the
    /// comments written under them), the message threads newest first, and the city's people who write tomorrow's posts.
    /// Only faces count (a logo, an emblem or a selfie needs no camera).
    ///
    /// THE DISK. A face is drawn once per look and kept as its file (27 KB) for every save that has it. The folder holds at
    /// most <see cref="DiskMost"/> faces (about 55 MB: four cities' people, their influencers and friends); past that the
    /// faces used longest ago go, down to <see cref="DiskKeep"/> (<see cref="Evict"/>), never one of the faces this load wants.
    /// A face's file is written again when it is read more than a day after it was last (<see cref="Stale"/>), so a face
    /// in use is never the oldest. The cities of saves that are gone are what ages out first.
    ///
    /// Going Public: nothing is sent. Every machine draws its own faces from the seeds and looks it already shares.
    ///
    /// Game-free (EconomyCheck, FlareFaceLoadChecks.cs). COST: <see cref="Likely"/> walks the kept posts once (two hundred at
    /// most, a handful of rolls each for their comments) when a save loads, when Flare starts and once a game day; never per
    /// frame. <see cref="Evict"/> sorts the folder's list on a worker thread once a load.
    /// </summary>
    public static class FlareFaceAheadRules
    {
        public const int LoadMost = 100;              // faces asked for behind the loading screen at most: what a first look at Flare shows (friends, influencers, the newest posts and their comments)
        public const int AheadMost = 160;             // faces waiting to be drawn ahead at most: the load's and the next day's posters
        public const int PostsMost = 60;              // kept posts looked at for faces, newest first (a first look scrolls a few dozen)
        public const int DmsMost = 12;                // message threads looked at, newest first
        public const double LoadBudgetMs = 2000.0;    // milliseconds the loading screen is held for the faces at most (Core/LoadHold.MostSeconds)
        public const double FirstFaceMs = 80.0;       // a face's cost guessed before one is measured: the portrait render and a read-back of a frame or two
        public const int DiskMost = 2048;             // faces kept on disk at most (27 KB each: 55 MB)
        public const int DiskKeep = 1792;             // ...cleaned down to this many, so the clean-up is not made at every load
        public const long TouchTicks = 864000000000L; // a day in ticks: a face read later than this after its last write is written again (its file's time, its place in the clean-up)

        /// <summary>
        /// The faces likely to be seen soon, most likely first, each once (by its picture), at most 'most', into 'into'
        /// (emptied first). 'day' is the game's day (tomorrow's posters are of day + 1). Returns how many.
        /// </summary>
        public static int Likely(FlareAvatarFacts facts, int day, int most, List<FlareAvatarPlan> into)
        {
            into.Clear();
            if (facts == null || most <= 0)
            {
                return 0;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<FlarePerson> people = facts.People;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; people != null && i < people.Count; i++)
                {
                    if (people[i].Partner == (pass == 0))
                    {
                        Add(facts, (people[i].Partner ? "p" : "f") + people[i].Index, most, seen, into);
                    }
                }
            }
            FlareFile f = facts.File;
            if (f == null)
            {
                return into.Count;
            }
            for (int i = 0; i < f.Influencers.Count; i++)
            {
                if (f.Influencers[i].Active)
                {
                    Add(facts, "i:" + f.Influencers[i].Id, most, seen, into);
                }
            }
            var reactions = new List<FlareReaction>(16);
            var comments = new List<FlareComment>(FlareCommentRules.WrittenMost);
            for (int i = f.Posts.Count - 1, looked = 0; i >= 0 && looked < PostsMost && into.Count < most; i--, looked++)
            {
                FlarePost p = f.Posts[i];
                Add(facts, FlareLiveRules.KeyOf(p), most, seen, into);
                FlareLiveRules.ReadReactors(p.Reactors, reactions);
                for (int r = 0; r < reactions.Count; r++)
                {
                    Add(facts, reactions[r].Who, most, seen, into);
                }
                bool city = FlareCommentRules.City(p);
                FlareCommentRules.Plan(p, facts.CitySeed, FlareLiveRules.Citizens, city ? null : people, FlareTaste.None, city ? null : f.Influencers, comments);
                for (int c = 0; c < comments.Count; c++)
                {
                    FlareComment k = comments[c];
                    Add(facts, k.Kind == 'f' ? "f" + k.Who : k.Kind == 'i' ? "i:" + k.Who : "c" + k.Who, most, seen, into);
                }
            }
            List<FlareDm> dms = f.Net.Dms;
            for (int i = dms.Count - 1, looked = 0; i >= 0 && looked < DmsMost && into.Count < most; i--)
            {
                if (dms[i].Mine || dms[i].With == null)
                {
                    continue;
                }
                looked++;
                Add(facts, ThreadKey(dms[i].With), most, seen, into);
            }
            int talk = FlareLiveRules.CityTalkPerDay[FlareLiveRules.CityTalkPerDay.Length - 1];   // (as many as the busiest feed makes: the option is not the rules' to know)
            for (int k = 0; k < talk && into.Count < most; k++)
            {
                Add(facts, "c" + FlareLiveRules.TalkCitizen(facts.CitySeed, day + 1, k), most, seen, into);
            }
            return into.Count;
        }

        /// <summary>A face's file name in the portrait cache: by its look, or for one of the staff by the save and the name.</summary>
        public static string FileName(FlareAvatarPlan plan, string saveId)
        {
            return plan.Kind == FlareAvatarKind.Staff ? FlareAvatarRules.StaffName(saveId, plan.Name) : FlareAvatarRules.FaceName(plan.Female, plan.Age, plan.Seed);
        }

        /// <summary>
        /// The faces of 'likely' that are not on disk ('onDisk': the folder's file names), in their order, into 'draw' (emptied
        /// first): a face drawn once is never drawn again while its file is there. Returns how many were on disk.
        /// </summary>
        public static int Split(List<FlareAvatarPlan> likely, ICollection<string> onDisk, string saveId, List<FlareAvatarPlan> draw)
        {
            draw.Clear();
            int there = 0;
            for (int i = 0; likely != null && i < likely.Count; i++)
            {
                if (onDisk != null && onDisk.Contains(FileName(likely[i], saveId)))
                {
                    there++;
                }
                else
                {
                    draw.Add(likely[i]);
                }
            }
            return there;
        }

        /// <summary>A message thread's account as a card shows it: the city's people's threads are "c:12", their card "c12".</summary>
        public static string ThreadKey(string with)
        {
            if (with != null && with.Length > 2 && with[0] == 'c' && with[1] == ':' && with[2] >= '0' && with[2] <= '9')
            {
                return "c" + with.Substring(2);
            }
            return with;
        }

        // An account's face, if it has one the camera draws and it is not in the list yet.
        private static void Add(FlareAvatarFacts facts, string key, int most, HashSet<string> seen, List<FlareAvatarPlan> into)
        {
            if (key == null || into.Count >= most)
            {
                return;
            }
            FlareAvatarPlan plan = FlareAvatarRules.Plan(key, facts);
            if ((plan.Kind == FlareAvatarKind.Face || plan.Kind == FlareAvatarKind.Staff) && seen.Add(plan.Picture))
            {
                into.Add(plan);
            }
        }

        /// <summary>
        /// Whether another face may start behind the loading screen: 'spentMs' since the hold began, 'drawnMs' the time the
        /// 'drawn' faces finished so far took from their asking to their file. The time left must hold one more at the cost
        /// measured so far (before any is measured, <see cref="FirstFaceMs"/>).
        /// </summary>
        public static bool Another(double spentMs, double drawnMs, int drawn, double budgetMs)
        {
            double each = drawn > 0 ? Math.Max(0.0, drawnMs) / drawn : FirstFaceMs;
            return spentMs >= 0.0 && spentMs + each <= budgetMs;
        }

        /// <summary>A face's measured cost so far, milliseconds each (0 for none yet).</summary>
        public static double Each(double drawnMs, int drawn)
        {
            return drawn > 0 ? Math.Max(0.0, drawnMs) / drawn : 0.0;
        }

        /// <summary>
        /// The faces' files to delete to keep the folder within its bound: none while it holds <see cref="DiskMost"/> or fewer;
        /// past it, the ones used longest ago (by name for the same time) until <see cref="DiskKeep"/> are left, never one
        /// named in 'keep'. Into 'into' (emptied first); returns how many.
        /// </summary>
        public static int Evict(List<FlareFaceFile> files, ICollection<string> keep, List<string> into)
        {
            into.Clear();
            if (files == null || files.Count <= DiskMost)
            {
                return 0;
            }
            var order = new List<FlareFaceFile>(files);
            order.Sort((a, b) => a.Touched != b.Touched ? a.Touched.CompareTo(b.Touched) : string.CompareOrdinal(a.Name, b.Name));
            int left = order.Count;
            for (int i = 0; i < order.Count && left > DiskKeep; i++)
            {
                if (keep != null && keep.Contains(order[i].Name))
                {
                    continue;
                }
                into.Add(order[i].Name);
                left--;
            }
            return into.Count;
        }

        /// <summary>Whether a face's file read now is written again (its time brought forward), so the faces in use are never the oldest.</summary>
        public static bool Stale(long touched, long now)
        {
            return now - touched > TouchTicks;
        }
    }
}
