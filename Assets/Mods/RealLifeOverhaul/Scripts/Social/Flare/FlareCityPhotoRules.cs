using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What a photo of one of the city's posts shows (FlareCityPhotoRules.Subject).</summary>
    public enum FlarePhotoSubject
    {
        None = 0,       // no photo for a post of this kind (prices, the market, a policy: nothing to see)
        Street = 1,     // the street where it is said to happen, at eye height
        Sky = 2,        // the street and the sky over it (the weather)
        Front = 3       // the front of the business or the place the post names, from across the street
    }

    /// <summary>
    /// The photos the city posts with, as a phone's photo of what the post is about: a resident's weather post with the
    /// street and the sky, an event or an accident in the street, a night out, food or a match where it is, a business's
    /// shop front and its new stock, an influencer's niche, a rival tycoon's new building.
    ///
    /// WHICH POSTS. A post of the city's (one of its people, a business, an influencer, a rival tycoon) whose kind has
    /// something to see, and only a share of those, by a roll of the post's own seed: for a post shared in a Going Public
    /// session the city's seed and the post's session number (FlareCommentRules.Seed), so every machine agrees which posts
    /// carry a photo. The option says how large a share (<see cref="Shares"/>, a modest one by default).
    ///
    /// WHEN AND WHERE. A photo is taken at the time of the post, within <see cref="WindowMinutes"/> game minutes of it, of
    /// the place as it is then, and only where the place is there to be seen without loading anything: the front of a
    /// business within <see cref="FrontRange"/> metres of the player, a street or the sky in the district the player is in;
    /// never from indoors. Each machine takes its own (a guest of a session from where it stands, so two machines' photos of
    /// one post may differ a little): nothing is sent, so an older RLO or a machine without one is never sent anything new.
    /// A post whose photo could not be taken shows without one: never a broken picture.
    ///
    /// THE COST. A photo is two renders of the city at <see cref="Width"/> by <see cref="Height"/> (the game's lighting and
    /// exposure settle in the second), read back without waiting for the graphics card and kept as its small copy (Core/
    /// PhotoThumbs' format, 169 KB) by a worker thread. At most one in <see cref="PaceLeast"/> real seconds, longer after a
    /// slow one (<see cref="NextPace"/>: doubled after a render over <see cref="SlowMs"/> ms, up to <see cref="PaceMost"/>),
    /// and <see cref="DayMost"/> a game day.
    ///
    /// THE FILES. Beside the save's own photos (Photos/(the save)/Flare/Thumbs): a post's photo goes when the post goes
    /// (looked over when Flare starts and each game day: <see cref="Stale"/>), never more than <see cref="FilesMost"/>; a
    /// deleted save's go with its photos (Photos/PhotoStoreRules). Nothing is in the save: which post has one is worked out
    /// again from the post and the files.
    ///
    /// Game-free (EconomyCheck, FlarePictureChecks.cs). COST: a few comparisons a post when asked; nothing per frame.
    /// </summary>
    public static class FlareCityPhotoRules
    {
        public const int Width = 320;                 // pixels of a city photo: a card shows it at 840 of the canvas's 2160 lines (420 pixels at 1080p)
        public const int Height = 180;
        public const int WindowMinutes = 120;         // game minutes after a post its photo may still be taken (a weather post's sky is the sky of that hour)
        public const int DayMost = 12;                // photos taken in a game day at most, whatever the option
        public const int FrontRange = 250;            // metres from the player a business's front may be: the city's detail is there to be seen
        public const float PaceLeast = 20f;           // real seconds between two photos at least
        public const float PaceMost = 160f;           // ...and at most, after slow ones
        public const double SlowMs = 20.0;            // milliseconds of the main thread a photo's renders may take before the next waits longer
        public const int FilesMost = 120;             // photos kept for a save at most (20 MB): more than the city's posts of its kept days ever ask for
        public const string Folder = "Flare";         // in the save's photos' folder
        public const string Prefix = "City_";
        public const string Extension = ".png";       // the name a photo would have; only its small copy is ever written (PhotoThumbs.FileOf)
        public const int OptionDefault = 1;
        private const int SaltPhoto = 860;

        private static readonly int[] Shares = { 0, 150, 300, 500 };   // thousandths of the posts with something to see, by the option: none, a few, some, many

        /// <summary>The share of posts with something to see that carry a photo, in thousandths, for the option's value.</summary>
        public static int Share(int option)
        {
            return Shares[Math.Max(0, Math.Min(Shares.Length - 1, option))];
        }

        /// <summary>The option's values.</summary>
        public static int Options => Shares.Length;

        /// <summary>What a photo of the post would show; None for a post that has nothing to see or is not the city's.</summary>
        public static FlarePhotoSubject Subject(FlarePost p)
        {
            if (p == null || p.ReplyTo >= 0 || p.Pending)
            {
                return FlarePhotoSubject.None;
            }
            bool place = !string.IsNullOrEmpty(p.Place);
            switch (p.Author)
            {
                case FlareAuthor.Citizen:
                    if (p.Source != FlareSource.CityTalk)
                    {
                        return FlarePhotoSubject.None;   // (a report or a review: the city's case has its own words, and a complaint is no photo shoot)
                    }
                    switch ((FlareTalk)(p.Arg / 10))
                    {
                        case FlareTalk.Weather: return FlarePhotoSubject.Sky;
                        case FlareTalk.Food:
                        case FlareTalk.Night:
                        case FlareTalk.Sport: return place ? FlarePhotoSubject.Front : FlarePhotoSubject.Street;
                        case FlareTalk.Event:
                        case FlareTalk.Accident:
                        case FlareTalk.Fire:
                        case FlareTalk.Roadworks:
                        case FlareTalk.Commute:
                        case FlareTalk.Everyday: return FlarePhotoSubject.Street;
                        default: return FlarePhotoSubject.None;
                    }
                case FlareAuthor.Business:
                    return place ? FlarePhotoSubject.Front : FlarePhotoSubject.None;
                case FlareAuthor.Influencer:
                    return p.Source == FlareSource.Niche || p.Source == FlareSource.Visit ? (place ? FlarePhotoSubject.Front : FlarePhotoSubject.Street) : FlarePhotoSubject.None;
                case FlareAuthor.Tycoon:
                    return place ? FlarePhotoSubject.Front : FlarePhotoSubject.Street;
                default:
                    return p.Source == FlareSource.Invite && place ? FlarePhotoSubject.Front : FlarePhotoSubject.None;
            }
        }

        /// <summary>Whether a post carries a photo with this option: one whose kind has something to see, and a share of those by its seed (the same on every machine).</summary>
        public static bool Wants(FlarePost p, int citySeed, int option)
        {
            int share = Share(option);
            if (share <= 0 || Subject(p) == FlarePhotoSubject.None)
            {
                return false;
            }
            return FlareRules.Roll(FlareCommentRules.Seed(p, citySeed), 0, SaltPhoto) < share;
        }

        /// <summary>Whether a post's photo may be taken now: from its minute on, for the window after it.</summary>
        public static bool Due(FlarePost p, long now)
        {
            return p != null && now >= p.At && now - p.At <= WindowMinutes;
        }

        /// <summary>Whether a place is near enough to be seen as it is: a business's front within the range of the player.</summary>
        public static bool InRange(float dx, float dz)
        {
            return dx * dx + dz * dz <= (float)FrontRange * FrontRange;
        }

        /// <summary>Whether a street of the post's district can be taken from where the player is: the same district, or a post that names none.</summary>
        public static bool SameDistrict(FlarePost p, string playerDistrict)
        {
            return string.IsNullOrEmpty(p.District) || (playerDistrict != null && string.Equals(p.District, playerDistrict, StringComparison.Ordinal));
        }

        /// <summary>The real seconds to wait after a photo whose renders took this long: back to the least after a quick one, doubled after a slow one, never past the most.</summary>
        public static float NextPace(float pace, double renderMs)
        {
            if (renderMs <= SlowMs)
            {
                return PaceLeast;
            }
            return Math.Min(PaceMost, Math.Max(PaceLeast, pace) * 2f);
        }

        /// <summary>A post's photo's name (the small copy is this name's PhotoThumbs.FileOf, in the folder's "Thumbs").</summary>
        public static string FileName(int postId)
        {
            return Prefix + postId + Extension;
        }

        /// <summary>The post a small copy's file is the photo of ("City_12.png.thumb"); -1 for a file that is not one.</summary>
        public static int IdOf(string fileName)
        {
            string tail = Extension + ".thumb";
            if (fileName == null || !fileName.StartsWith(Prefix, StringComparison.Ordinal) || !fileName.EndsWith(tail, StringComparison.Ordinal))
            {
                return -1;
            }
            string digits = fileName.Substring(Prefix.Length, fileName.Length - Prefix.Length - tail.Length);
            int id;
            return digits.Length > 0 && digits.Length <= 9 && int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out id) ? id : -1;
        }

        /// <summary>
        /// The photos that go, into a list (emptied first): those of posts the file no longer has, and beyond
        /// <see cref="FilesMost"/> the oldest (the lowest post numbers) of those left. Returns how many.
        /// </summary>
        public static int Stale(List<int> files, FlareFile f, List<int> into)
        {
            into.Clear();
            int kept = 0;
            for (int i = 0; i < files.Count; i++)
            {
                if (f == null || f.Post(files[i]) == null)
                {
                    into.Add(files[i]);
                }
                else
                {
                    kept++;
                }
            }
            if (kept > FilesMost)
            {
                var left = new List<int>(kept);
                for (int i = 0; i < files.Count; i++)
                {
                    if (!into.Contains(files[i]))
                    {
                        left.Add(files[i]);
                    }
                }
                left.Sort();
                for (int i = 0; i < kept - FilesMost; i++)
                {
                    into.Add(left[i]);
                }
            }
            return into.Count;
        }

        /// <summary>The next post whose photo is to be taken now, among those not taken or tried yet; null for none. A look along the posts, newest first.</summary>
        public static FlarePost Next(FlareFile f, int citySeed, int option, long now, HashSet<int> done)
        {
            if (f == null || Share(option) <= 0)
            {
                return null;
            }
            for (int i = f.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = f.Posts[i];
                if (!done.Contains(p.Id) && Due(p, now) && Wants(p, citySeed, option))
                {
                    return p;
                }
            }
            return null;
        }
    }
}
