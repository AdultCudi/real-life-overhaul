using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What a photo of the city's bank shows (FlareCityPhotoRules). Saved as a number in the bank's file.</summary>
    public enum FlarePhotoKind
    {
        None = 0,
        Street = 1,     // a street of a district at eye height, a little of the sky in it
        Front = 2,      // the front of a business, from across the street (its place and kind kept)
        Park = 3,       // a park or a playing field (Madison Park, Sunset Park, the golf course, the tennis courts)
        Event = 4,      // the street event that is on (a market, a parade, a race, a film crew)
        Landmark = 5    // one of the city's sights (the big wheel, the bridge, Times Square, a harbour)
    }

    /// <summary>The light a photo was taken in, by the game's hour (FlareCityPhotoRules.LightAt).</summary>
    public enum FlarePhotoLight
    {
        Day = 0,        // 07:00 to 16:59
        Dusk = 1,       // 06:00 to 06:59 and 17:00 to 20:59: low sun, lamps coming on
        Night = 2       // 21:00 to 05:59
    }

    /// <summary>One photo of the bank: where and what it is, the light and the weather it was taken in, and how many posts carry it.</summary>
    public sealed class FlareBankPhoto
    {
        public int Id;                 // its number in this save's bank: its file is Bank_(id)
        public string District;        // the district it was taken in (the game's own id)
        public FlarePhotoKind Kind;
        public string Place;           // a front: the business ("street~number"); a park or a sight: its id; an event: its label; a street: null
        public int Niche = -1;         // a front: the business's kind (FlareNiche); -1 for anything else
        public FlarePhotoLight Light;
        public bool Wet;               // taken in the rain (wet streets, grey sky)
        public int Day;                // the game day it was taken
        public int Minute;             // ...and the minute of that day
        public int Uses;               // posts that carry it
    }

    /// <summary>A post's photo as it was picked: the bank's photo, or none (-1) when nothing fitted (then it never carries one).</summary>
    public struct FlareBankPick
    {
        public int Post;
        public int Photo;
    }

    /// <summary>The bank of one save's city photos and the posts' picks (kept beside the photos, never in the save).</summary>
    public sealed class FlareBank
    {
        public int NextId = 1;
        public readonly List<FlareBankPhoto> Photos = new List<FlareBankPhoto>(FlareCityPhotoRules.BankMost + 8);
        public readonly List<FlareBankPick> Picks = new List<FlareBankPick>(FlareCityPhotoRules.PicksMost + 8);

        public FlareBankPhoto Photo(int id)
        {
            for (int i = 0; i < Photos.Count; i++)
            {
                if (Photos[i].Id == id)
                {
                    return Photos[i];
                }
            }
            return null;
        }

        /// <summary>The photo picked for a post: its number, -1 when none fitted, -2 when it has not been picked yet.</summary>
        public int PickOf(int post)
        {
            for (int i = Picks.Count - 1; i >= 0; i--)
            {
                if (Picks[i].Post == post)
                {
                    return Picks[i].Photo;
                }
            }
            return -2;
        }
    }

    /// <summary>What a post's photo must be: up to three ways in order of preference, in its district, light and weather.</summary>
    public struct FlarePhotoWish
    {
        public FlarePhotoKind Kind0, Kind1, Kind2;     // the kinds tried in order (None: no more)
        public string Place0, Place1, Place2;          // ...a business or a sight it must be, or null for any
        public int Niche0, Niche1, Niche2;             // ...a business's kind it must be, or -1 for any
        public string District;                        // where (null: a post that names none; an event is the city's own, wherever it was)
        public FlarePhotoLight Light;
        public bool Wet;
        public int Day;                                // the post's day: an event's photo is of that day, any other at most FreshDays apart

        public int Ways => Kind0 == FlarePhotoKind.None ? 0 : Kind1 == FlarePhotoKind.None ? 1 : Kind2 == FlarePhotoKind.None ? 2 : 3;

        public void Way(int k, out FlarePhotoKind kind, out string place, out int niche)
        {
            kind = k == 0 ? Kind0 : k == 1 ? Kind1 : Kind2;
            place = k == 0 ? Place0 : k == 1 ? Place1 : Place2;
            niche = k == 0 ? Niche0 : k == 1 ? Niche1 : Niche2;
        }

        public void Add(FlarePhotoKind kind, string place = null, int niche = -1)
        {
            if (Kind0 == FlarePhotoKind.None)
            {
                Kind0 = kind;
                Place0 = place;
                Niche0 = niche;
            }
            else if (Kind1 == FlarePhotoKind.None)
            {
                Kind1 = kind;
                Place1 = place;
                Niche1 = niche;
            }
            else if (Kind2 == FlarePhotoKind.None)
            {
                Kind2 = kind;
                Place2 = place;
                Niche2 = niche;
            }
        }
    }

    /// <summary>A business near where the player is, as the bank's pass sees it (its front may be photographed).</summary>
    public struct FlareFrontNear
    {
        public string Place;
        public string District;
        public int Niche;
        public float Metres;
        public bool Posts;    // a business of the city's posts today (the day's sample) or the player's own: its own front is the most wanted
    }

    /// <summary>What can be photographed where the player is now (FlareDirector.Photos.cs reads it from the game).</summary>
    public sealed class FlarePhotoScene
    {
        public string District;
        public FlarePhotoLight Light;
        public bool Wet;
        public int Day, Minute;
        public string Event;            // the street event's label when the player is near it, else null
        public string Sight;            // the park's or sight's id when the player stands at one, else null
        public readonly List<FlareFrontNear> Fronts = new List<FlareFrontNear>(16);
    }

    /// <summary>The photo the bank's pass takes now: its kind and what it is of.</summary>
    public struct FlarePhotoShot
    {
        public FlarePhotoKind Kind;
        public string Place;
        public int Niche;
    }

    /// <summary>
    /// The city's photos, as a phone's photos of the city: a bank of them taken quietly while the player is out and about,
    /// and the city's posts carrying one that fits, so that the city's people post photos as real people do.
    ///
    /// THE BANK. While the player is outdoors (on foot or driving), the director's second looks, after the pace, at what can
    /// be seen here (<see cref="Plan"/>): the street event if one is on near them, the park or sight they stand at, the front
    /// of a business within <see cref="FrontRange"/> metres (the city's posting businesses and the player's own first, then
    /// cafes, restaurants, night spots and shops), else the street. It takes one only where the bank lacks it: at most
    /// <see cref="StreetMost"/> streets of a district in each light and weather, one front of a business and one of a sight
    /// in each, <see cref="DistrictFrontsMost"/> fronts of a district, <see cref="EventMost"/> of a day's event. Each photo
    /// records its district, kind, place, light, weather and time. At most <see cref="BankMost"/> are kept (the oldest go
    /// first), none older than <see cref="FreshDays"/> game days, and an event's only on its own day (<see cref="Stale"/>).
    ///
    /// THE PICK. A city post carries a photo when its kind has something to see (<see cref="Wish"/>) and a roll of its seed
    /// falls under its kind's share (<see cref="Share"/>: food, night life, weather and events more often than a plain
    /// thought; the option scales it). From <see cref="LeadMinutes"/> before it goes up to <see cref="WindowMinutes"/> after,
    /// the bank is asked once for one that fits (<see cref="Pick"/>): its district, its kind (a food post a cafe's or a
    /// restaurant's front, a weather post a street in the same weather, an event post the day's event, a business's post its
    /// own front, else a street of its district), the light of its hour and the weather (a weather post's own: never a sunny
    /// photo for a rainy post, never a night photo for a morning one). Nothing fits: no photo, ever (never a wrong or broken
    /// one). A photo carries at most <see cref="UsesMost"/> posts, and is never one of the last <see cref="RecentMost"/>
    /// picks. The pick is kept (the bank's file), so a post keeps its photo after a load.
    ///
    /// GOING PUBLIC. Which posts carry one is the post's seed (the city's seed and its session number for a shared post),
    /// and among the photos that fit, the choice is that seed's too: every machine picks the same way from its own bank, and
    /// a machine whose bank has none that fits shows none. Nothing is sent.
    ///
    /// Game-free (EconomyCheck, FlareBankChecks.cs). COST: a pass over the bank (at most 144 photos) when a post is picked or
    /// a photo planned, on the director's second after the pace; nothing per frame, nothing allocated in a pick or a plan.
    /// </summary>
    public static class FlareCityPhotoRules
    {
        public const int Width = 320;                 // pixels of a city photo: a card shows it at 840 of the canvas's 2160 lines (420 pixels at 1080p)
        public const int Height = 180;
        public const int BankMost = 144;              // photos kept for a save at most (each small copy 169 KB: 24 MB)
        public const int PicksMost = 400;             // posts' picks kept at most (the feed keeps 220 posts)
        public const int StreetMost = 2;              // streets of a district in one light and weather
        public const int DistrictFrontsMost = 10;     // business fronts of a district in all
        public const int EventMost = 3;               // photos of one day's street event
        public const int FreshDays = 28;              // game days a photo is used for: the city's look keeps that long (seasons, decorations)
        public const int UsesMost = 3;                // posts one photo may carry
        public const int RecentMost = 4;              // the last picks a photo may not be picked again after (never twice in a row in the feed)
        public const int LeadMinutes = 30;            // game minutes before a post goes up its photo is picked (in the weather then)
        public const int WindowMinutes = 180;         // ...and after: a post first seen later (a load) carries none
        public const int DayMost = 24;                // bank photos taken in a game day at most, whatever the option
        public const int FrontRange = 250;            // metres from the player a business's front may be: the city's detail is there to be seen
        public const float PaceLeast = 20f;           // real seconds between two photos at least
        public const float PaceMost = 160f;           // ...and at most, after slow ones
        public const double SlowMs = 20.0;            // milliseconds of the main thread a photo's renders may take before the next waits longer
        public const string Folder = "Flare";         // in the save's photos' folder
        public const string Prefix = "Bank_";
        public const string LegacyPrefix = "City_";   // the photos of 0.29 and before (one per post): deleted when Flare starts
        public const string Extension = ".png";       // the name a photo would have; only its small copy is ever written (PhotoThumbs.FileOf)
        public const string IndexFile = "bank.txt";   // the bank's records and the posts' picks, beside the photos
        public const int OptionDefault = 2;           // "some"
        public const int Version = 1;
        private const int SaltPhoto = 860, SaltPick = 861;

        private static readonly int[] Scale = { 0, 500, 1000, 1600 };   // thousandths of each kind's share, by the option: none, a few, some, many

        /// <summary>The option's values.</summary>
        public static int Options => Scale.Length;

        // ---------------------------------------------------------------- what a post wants

        /// <summary>The light of an hour of the game's day.</summary>
        public static FlarePhotoLight LightAt(int hour)
        {
            hour = ((hour % 24) + 24) % 24;
            return hour >= 21 || hour < 6 ? FlarePhotoLight.Night : hour == 6 || hour >= 17 ? FlarePhotoLight.Dusk : FlarePhotoLight.Day;
        }

        /// <summary>
        /// The share of a post's kind that carries a photo at the "some" option, thousandths: an event, food, a night out and
        /// the weather the most, a business's and an influencer's posts often, a plain thought or a commute now and then.
        /// None for a kind with nothing to see, a reply, the player's own, a guest's post on its way.
        /// </summary>
        public static int KindShare(FlarePost p, int niche)
        {
            if (p == null || p.ReplyTo >= 0 || p.Pending)
            {
                return 0;
            }
            switch (p.Author)
            {
                case FlareAuthor.Citizen:
                    if (p.Source != FlareSource.CityTalk)
                    {
                        return 0;   // (a report or a review: the city's case has its own words, and a complaint is no photo shoot)
                    }
                    switch ((FlareTalk)(p.Arg / 10))
                    {
                        case FlareTalk.Event: return 750;
                        case FlareTalk.Food:
                        case FlareTalk.Night: return 700;
                        case FlareTalk.Weather: return 650;
                        case FlareTalk.Sport: return 550;
                        case FlareTalk.Everyday:
                        case FlareTalk.Commute: return 350;
                        default: return 0;   // (prices, the market, a policy, an election, a crime, a crash, a fire, works: nothing a bank photo truly shows)
                    }
                case FlareAuthor.Business: return p.Source == FlareSource.BusinessPost ? 550 : 0;
                case FlareAuthor.Influencer:
                    if (p.Source != FlareSource.Niche && p.Source != FlareSource.Visit)
                    {
                        return 0;
                    }
                    return niche == (int)FlareNiche.Tech || niche == (int)FlareNiche.Business ? 0 : 650;
                case FlareAuthor.Tycoon: return !string.IsNullOrEmpty(p.Place) ? 450 : 0;
                default: return p.Source == FlareSource.Invite && !string.IsNullOrEmpty(p.Place) ? 500 : 0;
            }
        }

        /// <summary>The share of a post that carries a photo with this option, thousandths (its kind's, scaled; never all).</summary>
        public static int Share(FlarePost p, int niche, int option)
        {
            int scale = Scale[Math.Max(0, Math.Min(Scale.Length - 1, option))];
            return Math.Min(900, KindShare(p, niche) * scale / 1000);
        }

        /// <summary>Whether a post carries a photo with this option: a roll of its seed (the same on every machine) under its share.</summary>
        public static bool Wants(FlarePost p, int citySeed, int option, int niche)
        {
            int share = Share(p, niche, option);
            return share > 0 && FlareRules.Roll(FlareCommentRules.Seed(p, citySeed), 0, SaltPhoto) < share;
        }

        /// <summary>Whether a post's photo is to be picked now: from <see cref="LeadMinutes"/> before it goes up to <see cref="WindowMinutes"/> after.</summary>
        public static bool Due(FlarePost p, long now)
        {
            return p != null && now >= p.At - LeadMinutes && now <= p.At + WindowMinutes;
        }

        /// <summary>
        /// What a post's photo must show, in order of preference; false for a post with nothing to see. 'niche' and 'home' are
        /// an influencer's (FlareNiche, their district), -1 and null for anybody else; 'wetNow' whether it rains as the photo
        /// is picked (a weather post says its own weather: its tag "rain").
        /// </summary>
        public static bool Wish(FlarePost p, int niche, string home, bool wetNow, out FlarePhotoWish w)
        {
            w = new FlarePhotoWish { Niche0 = -1, Niche1 = -1, Niche2 = -1 };
            if (KindShare(p, niche) <= 0)
            {
                return false;
            }
            w.District = !string.IsNullOrEmpty(p.District) ? p.District : home;
            w.Day = p.Day;
            w.Light = LightAt(p.Hour);
            w.Wet = wetNow;
            bool place = !string.IsNullOrEmpty(p.Place);
            switch (p.Author)
            {
                case FlareAuthor.Citizen:
                    switch ((FlareTalk)(p.Arg / 10))
                    {
                        case FlareTalk.Weather:
                            w.Wet = p.Topic != null && p.Topic.IndexOf("rain", StringComparison.Ordinal) >= 0;   // (the post says what the weather is)
                            w.Add(FlarePhotoKind.Street);
                            break;
                        case FlareTalk.Food:
                            w.Add(FlarePhotoKind.Front, place ? p.Place : null, (int)FlareNiche.Food);
                            break;
                        case FlareTalk.Night:
                            w.Add(FlarePhotoKind.Front, place ? p.Place : null, (int)FlareNiche.Nightlife);
                            if (w.Light != FlarePhotoLight.Day)
                            {
                                w.Add(FlarePhotoKind.Street);   // (a night out: the street at dusk or night will do, never by day)
                            }
                            break;
                        case FlareTalk.Sport:
                            w.Add(FlarePhotoKind.Park);
                            break;
                        case FlareTalk.Event:
                            w.Add(FlarePhotoKind.Event);
                            break;
                        case FlareTalk.Commute:
                            w.Add(FlarePhotoKind.Street);
                            break;
                        default:   // (everyday)
                            w.Add(FlarePhotoKind.Street);
                            w.Add(FlarePhotoKind.Park);
                            w.Add(FlarePhotoKind.Landmark);
                            break;
                    }
                    break;
                case FlareAuthor.Business:
                    if (place)
                    {
                        w.Add(FlarePhotoKind.Front, p.Place);
                    }
                    w.Add(FlarePhotoKind.Street);
                    break;
                case FlareAuthor.Influencer:
                    if (place)
                    {
                        w.Add(FlarePhotoKind.Front, p.Place);
                    }
                    switch (niche)
                    {
                        case (int)FlareNiche.Food: w.Add(FlarePhotoKind.Front, null, (int)FlareNiche.Food); break;
                        case (int)FlareNiche.Nightlife: w.Add(FlarePhotoKind.Front, null, (int)FlareNiche.Nightlife); break;
                        case (int)FlareNiche.Fashion: w.Add(FlarePhotoKind.Front, null, (int)FlareNiche.Fashion); break;
                        case (int)FlareNiche.Fitness: w.Add(FlarePhotoKind.Park); break;
                        default: w.Add(FlarePhotoKind.Street); w.Add(FlarePhotoKind.Landmark); break;
                    }
                    break;
                default:   // (a rival's building, an invitation's place: its own front or nothing)
                    w.Add(FlarePhotoKind.Front, p.Place);
                    break;
            }
            return w.Ways > 0;
        }

        /// <summary>True if a photo of the bank fits one way of a wish: kind, place, business kind, district, light, weather, and fresh (an event's of the post's own day).</summary>
        public static bool Fits(FlareBankPhoto photo, FlarePhotoWish w, int way)
        {
            w.Way(way, out FlarePhotoKind kind, out string place, out int niche);
            if (photo == null || kind == FlarePhotoKind.None || photo.Kind != kind || photo.Light != w.Light || photo.Wet != w.Wet)
            {
                return false;
            }
            if (kind == FlarePhotoKind.Event)
            {
                return photo.Day == w.Day;   // (the day's event, wherever in the city it was)
            }
            if (Math.Abs(w.Day - photo.Day) > FreshDays)
            {
                return false;
            }
            if (place != null && !string.Equals(photo.Place, place, StringComparison.Ordinal))
            {
                return false;
            }
            if (niche >= 0 && photo.Niche != niche)
            {
                return false;
            }
            return string.IsNullOrEmpty(w.District) || string.Equals(photo.District, w.District, StringComparison.Ordinal);
        }

        /// <summary>
        /// The bank's photo for a post, or null when none fits: the first way of the wish with any fitting photo that carries
        /// fewer than <see cref="UsesMost"/> posts and is not one of the last <see cref="RecentMost"/> picks; among those, the
        /// one the post's seed rolls (in the bank's order, its photos' numbers: the same bank gives the same pick).
        /// </summary>
        public static FlareBankPhoto Pick(FlareBank bank, FlarePhotoWish w, int seed)
        {
            if (bank == null)
            {
                return null;
            }
            for (int way = 0; way < w.Ways; way++)
            {
                int count = 0;
                for (int i = 0; i < bank.Photos.Count; i++)
                {
                    count += Free(bank, bank.Photos[i], w, way) ? 1 : 0;
                }
                if (count == 0)
                {
                    continue;
                }
                int pick = FlareRules.Roll(seed, way, SaltPick) % count;
                for (int i = 0; i < bank.Photos.Count; i++)
                {
                    if (Free(bank, bank.Photos[i], w, way) && pick-- == 0)
                    {
                        return bank.Photos[i];
                    }
                }
            }
            return null;
        }

        private static bool Free(FlareBank bank, FlareBankPhoto photo, FlarePhotoWish w, int way)
        {
            if (photo.Uses >= UsesMost || !Fits(photo, w, way))
            {
                return false;
            }
            for (int i = bank.Picks.Count - 1, seen = 0; i >= 0 && seen < RecentMost; i--)
            {
                if (bank.Picks[i].Photo < 0)
                {
                    continue;
                }
                seen++;
                if (bank.Picks[i].Photo == photo.Id)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A post's pick kept: the photo (its uses counted) or none (null). At <see cref="PicksMost"/> the oldest pick goes.</summary>
        public static void Picked(FlareBank bank, int post, FlareBankPhoto photo)
        {
            if (photo != null)
            {
                photo.Uses++;
            }
            if (bank.Picks.Count >= PicksMost)
            {
                bank.Picks.RemoveAt(0);
            }
            bank.Picks.Add(new FlareBankPick { Post = post, Photo = photo != null ? photo.Id : -1 });
        }

        /// <summary>The next post whose photo is to be picked now: due, wanted, not picked yet, not in 'skip' (looked at already in this look). A look along the posts, newest first; null for none.</summary>
        public static FlarePost NextPick(FlareFile f, FlareBank bank, int citySeed, int option, long now, Func<FlarePost, int> nicheOf, HashSet<int> skip)
        {
            if (f == null || bank == null || option <= 0)
            {
                return null;
            }
            for (int i = f.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = f.Posts[i];
                if (Due(p, now) && (skip == null || !skip.Contains(p.Id)) && bank.PickOf(p.Id) == -2 && Wants(p, citySeed, option, nicheOf != null ? nicheOf(p) : -1))
                {
                    return p;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- what the bank takes

        /// <summary>The kind of photo a park or sight of the city's (Leisure's landmarks) is; None for one that is no sight (a district's middle, the hospital, the pool indoors).</summary>
        public static FlarePhotoKind SightKind(string id)
        {
            switch (id)
            {
                case "madison_park":
                case "sunset_park":
                case "golf_course":
                case "tennis_courts":
                    return FlarePhotoKind.Park;
                case "ferris_wheel":
                case "bridge":
                case "casino_pier":
                case "times_square":
                case "st_patrick":
                case "garment_harbor":
                case "industry_harbor":
                case "hamptons":
                    return FlarePhotoKind.Landmark;
                default:
                    return FlarePhotoKind.None;
            }
        }

        /// <summary>How many photos of the bank are of this kind, place, district, light and weather (a null place or district: any).</summary>
        public static int Count(FlareBank bank, FlarePhotoKind kind, string place, string district, FlarePhotoLight light, bool wet)
        {
            int n = 0;
            for (int i = 0; i < bank.Photos.Count; i++)
            {
                FlareBankPhoto p = bank.Photos[i];
                if (p.Kind == kind && p.Light == light && p.Wet == wet && (place == null || string.Equals(p.Place, place, StringComparison.Ordinal))
                    && (district == null || string.Equals(p.District, district, StringComparison.Ordinal)))
                {
                    n++;
                }
            }
            return n;
        }

        private static int Fronts(FlareBank bank, string district)
        {
            int n = 0;
            for (int i = 0; i < bank.Photos.Count; i++)
            {
                n += bank.Photos[i].Kind == FlarePhotoKind.Front && string.Equals(bank.Photos[i].District, district, StringComparison.Ordinal) ? 1 : 0;
            }
            return n;
        }

        private static int EventsOf(FlareBank bank, int day, string label)
        {
            int n = 0;
            for (int i = 0; i < bank.Photos.Count; i++)
            {
                FlareBankPhoto p = bank.Photos[i];
                n += p.Kind == FlarePhotoKind.Event && p.Day == day && string.Equals(p.Place, label, StringComparison.Ordinal) ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// What the bank's pass photographs where the player is now, or false for nothing the bank lacks here: the street event
        /// near them (up to <see cref="EventMost"/> of a day's), the park or sight they stand at (one in each light and weather),
        /// the front of the best business near them the bank lacks (the city's posting businesses and the player's first, then
        /// the kinds posts ask for most: food and night life, then fashion; the nearer first; <see cref="DistrictFrontsMost"/> of
        /// a district), else the street (<see cref="StreetMost"/> in each light and weather). Nothing without a district.
        /// </summary>
        public static bool Plan(FlareBank bank, FlarePhotoScene here, out FlarePhotoShot shot)
        {
            shot = new FlarePhotoShot { Niche = -1 };
            if (bank == null || here == null || string.IsNullOrEmpty(here.District))
            {
                return false;
            }
            if (here.Event != null && EventsOf(bank, here.Day, here.Event) < EventMost)
            {
                shot.Kind = FlarePhotoKind.Event;
                shot.Place = here.Event;
                return true;
            }
            FlarePhotoKind sight = SightKind(here.Sight);
            if (sight != FlarePhotoKind.None && Count(bank, sight, here.Sight, null, here.Light, here.Wet) == 0)
            {
                shot.Kind = sight;
                shot.Place = here.Sight;
                return true;
            }
            if (Fronts(bank, here.District) < DistrictFrontsMost)
            {
                int best = -1, bestScore = -1;
                for (int i = 0; i < here.Fronts.Count; i++)
                {
                    FlareFrontNear f = here.Fronts[i];
                    if (f.Metres > FrontRange || string.IsNullOrEmpty(f.Place) || !string.Equals(f.District, here.District, StringComparison.Ordinal)
                        || Count(bank, FlarePhotoKind.Front, f.Place, null, here.Light, here.Wet) > 0)
                    {
                        continue;
                    }
                    int score = (f.Posts ? 4 : 0) + (f.Niche == (int)FlareNiche.Food || f.Niche == (int)FlareNiche.Nightlife ? 2 : f.Niche == (int)FlareNiche.Fashion ? 1 : 0);
                    if (score > bestScore || (score == bestScore && f.Metres < here.Fronts[best].Metres))
                    {
                        best = i;
                        bestScore = score;
                    }
                }
                if (best >= 0)
                {
                    shot.Kind = FlarePhotoKind.Front;
                    shot.Place = here.Fronts[best].Place;
                    shot.Niche = here.Fronts[best].Niche;
                    return true;
                }
            }
            if (Count(bank, FlarePhotoKind.Street, null, here.District, here.Light, here.Wet) < StreetMost)
            {
                shot.Kind = FlarePhotoKind.Street;
                return true;
            }
            return false;
        }

        /// <summary>A photo taken: its record in the bank under the number it was taken as, with the district, light, weather and time it was taken in.</summary>
        public static FlareBankPhoto Taken(FlareBank bank, int id, FlarePhotoShot shot, string district, FlarePhotoLight light, bool wet, int day, int minute)
        {
            var photo = new FlareBankPhoto
            {
                Id = id, District = district, Kind = shot.Kind, Place = shot.Place, Niche = shot.Kind == FlarePhotoKind.Front ? shot.Niche : -1,
                Light = light, Wet = wet, Day = day, Minute = minute
            };
            bank.NextId = Math.Max(bank.NextId, id + 1);
            bank.Photos.Add(photo);
            return photo;
        }

        /// <summary>
        /// The photos that go, into a list (emptied first): older than <see cref="FreshDays"/> game days, an event's past its
        /// day, and beyond <see cref="BankMost"/> the oldest (the lowest numbers) of those left. Returns how many.
        /// </summary>
        public static int Stale(FlareBank bank, int day, List<int> into)
        {
            into.Clear();
            int kept = 0;
            for (int i = 0; i < bank.Photos.Count; i++)
            {
                FlareBankPhoto p = bank.Photos[i];
                if (day - p.Day > FreshDays || (p.Kind == FlarePhotoKind.Event && p.Day < day))
                {
                    into.Add(p.Id);
                }
                else
                {
                    kept++;
                }
            }
            for (int i = 0; i < bank.Photos.Count && kept > BankMost; i++)   // (the photos are in the order taken: the oldest first)
            {
                if (!into.Contains(bank.Photos[i].Id))
                {
                    into.Add(bank.Photos[i].Id);
                    kept--;
                }
            }
            return into.Count;
        }

        /// <summary>Photos let go: their records and the picks of posts that carried them. How many went.</summary>
        public static int Drop(FlareBank bank, List<int> ids)
        {
            int n = 0;
            for (int i = bank.Photos.Count - 1; i >= 0; i--)
            {
                if (ids.Contains(bank.Photos[i].Id))
                {
                    bank.Photos.RemoveAt(i);
                    n++;
                }
            }
            for (int i = bank.Picks.Count - 1; i >= 0; i--)
            {
                if (bank.Picks[i].Photo >= 0 && ids.Contains(bank.Picks[i].Photo))
                {
                    bank.Picks.RemoveAt(i);
                }
            }
            return n;
        }

        /// <summary>The picks of posts the file no longer has let go (their photos' uses given back). How many went.</summary>
        public static int PrunePicks(FlareBank bank, Func<int, bool> postKept)
        {
            int n = 0;
            for (int i = bank.Picks.Count - 1; i >= 0; i--)
            {
                if (!postKept(bank.Picks[i].Post))
                {
                    FlareBankPhoto photo = bank.Picks[i].Photo >= 0 ? bank.Photo(bank.Picks[i].Photo) : null;
                    if (photo != null && photo.Uses > 0)
                    {
                        photo.Uses--;
                    }
                    bank.Picks.RemoveAt(i);
                    n++;
                }
            }
            return n;
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

        /// <summary>Whether a place is near enough to be seen as it is: within the range of the player.</summary>
        public static bool InRange(float dx, float dz)
        {
            return dx * dx + dz * dz <= (float)FrontRange * FrontRange;
        }

        // ---------------------------------------------------------------- the files

        /// <summary>A bank photo's name (the small copy is this name's PhotoThumbs.FileOf, in the folder's "Thumbs").</summary>
        public static string FileName(int photoId)
        {
            return Prefix + photoId + Extension;
        }

        /// <summary>The bank's photo a small copy's file is ("Bank_12.png.thumb"); -1 for a file that is not one.</summary>
        public static int IdOf(string fileName)
        {
            string tail = Extension + ".thumb";
            if (fileName == null || !fileName.StartsWith(Prefix, StringComparison.Ordinal) || !fileName.EndsWith(tail, StringComparison.Ordinal))
            {
                return -1;
            }
            string digits = fileName.Substring(Prefix.Length, fileName.Length - Prefix.Length - tail.Length);
            return digits.Length > 0 && digits.Length <= 9 && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ? id : -1;
        }

        /// <summary>True for a photo's file of the old kind (one a post, 0.29 and before): deleted when Flare starts.</summary>
        public static bool Legacy(string fileName)
        {
            return fileName != null && fileName.StartsWith(LegacyPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// The bank as its file's text: "v=1", "n=(next number)", then a line a photo
        /// ("p|id|district|kind|place|niche|light|wet|day|minute|uses") and a line a pick ("u|post|photo"). A place or a
        /// district never holds a '|' (one is written as '/').
        /// </summary>
        public static string Write(FlareBank bank)
        {
            var b = new StringBuilder(64 + bank.Photos.Count * 64 + bank.Picks.Count * 16);
            b.Append("v=").Append(Version).Append('\n').Append("n=").Append(bank.NextId).Append('\n');
            for (int i = 0; i < bank.Photos.Count; i++)
            {
                FlareBankPhoto p = bank.Photos[i];
                b.Append("p|").Append(p.Id).Append('|').Append(Clean(p.District)).Append('|').Append((int)p.Kind).Append('|').Append(Clean(p.Place)).Append('|')
                    .Append(p.Niche).Append('|').Append((int)p.Light).Append('|').Append(p.Wet ? 1 : 0).Append('|').Append(p.Day).Append('|').Append(p.Minute).Append('|')
                    .Append(p.Uses).Append('\n');
            }
            for (int i = 0; i < bank.Picks.Count; i++)
            {
                b.Append("u|").Append(bank.Picks[i].Post).Append('|').Append(bank.Picks[i].Photo).Append('\n');
            }
            return b.ToString();
        }

        /// <summary>The bank from its file's text; an empty bank for none, a later version or text that cannot be read (its photos then go, as files without records).</summary>
        public static FlareBank Read(string text)
        {
            var bank = new FlareBank();
            if (string.IsNullOrEmpty(text))
            {
                return bank;
            }
            string[] lines = text.Split('\n');
            if (lines[0].Trim() != "v=" + Version)
            {
                return bank;
            }
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.StartsWith("n=", StringComparison.Ordinal))
                {
                    if (Int(line.Substring(2), out int next))
                    {
                        bank.NextId = Math.Max(bank.NextId, next);
                    }
                    continue;
                }
                string[] f = line.Split('|');
                if (f[0] == "p" && f.Length >= 11 && Int(f[1], out int id) && Int(f[3], out int kind) && Int(f[5], out int niche) && Int(f[6], out int light) && Int(f[7], out int wet)
                    && Int(f[8], out int day) && Int(f[9], out int minute) && Int(f[10], out int uses) && id > 0 && kind > 0 && kind <= (int)FlarePhotoKind.Landmark && light >= 0 && light <= 2)
                {
                    bank.Photos.Add(new FlareBankPhoto
                    {
                        Id = id, District = f[2].Length > 0 ? f[2] : null, Kind = (FlarePhotoKind)kind, Place = f[4].Length > 0 ? f[4] : null, Niche = niche,
                        Light = (FlarePhotoLight)light, Wet = wet == 1, Day = day, Minute = minute, Uses = Math.Max(0, uses)
                    });
                    bank.NextId = Math.Max(bank.NextId, id + 1);
                }
                else if (f[0] == "u" && f.Length >= 3 && Int(f[1], out int post) && Int(f[2], out int photo))
                {
                    bank.Picks.Add(new FlareBankPick { Post = post, Photo = photo });
                }
            }
            return bank;
        }

        private static bool Int(string s, out int v) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);

        private static string Clean(string s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace('|', '/').Replace('\n', ' ');
    }
}
