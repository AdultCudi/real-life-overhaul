using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The city's photos: a bank taken quietly while the player is outdoors (<see cref="Plan"/>), and the city's posts given
    /// a fitting photo (<see cref="Wish"/>, <see cref="Pick"/>) by their seed, so every machine of a session picks alike and
    /// nothing is sent. Game-free. Cost: a pass over the bank (144 photos at most) a pick or plan; no allocation.
    /// </summary>
    public static class FlareCityPhotoRules
    {
        public const int Width = 320;                 // pixels: a card shows it at 420 pixels high at 1080p
        public const int Height = 180;   // pixels
        public const int BankMost = 144;              // photos kept for a save at most: each small copy 169 KB, 24 MB in all
        public const int PicksMost = 400;             // posts' picks kept at most; the feed keeps 220 posts
        public const int StreetMost = 2;              // streets of a district in one light and weather
        public const int DistrictFrontsMost = 10;     // business fronts of a district in all
        public const int EventMost = 3;               // photos of one day's street event
        public const int FreshDays = 28;              // game days a photo is used for: the city's look keeps that long (seasons, decorations)
        public const int UsesMost = 3;                // posts one photo may carry at a full bank (UsesAllowed: more while the bank is small)
        public const int RecentMost = 4;              // the last picks a photo may not follow: never twice in a row in the feed
        public const int LeadMinutes = 30;            // game minutes before a post goes up its photo is picked, in the weather then
        public const int WindowMinutes = 180;         // game minutes the weather now is the post's weather: an older post (a load) is taken as dry
        public const int ReadyPhotos = 10;            // photos the bank holds before a post it has none for is settled as bare (until then it is tried again)
        public const int CatchUpPhotos = 30;          // below this the bank is filling: photos come a little faster
        public const int PenaltyDistrict = 1, PenaltyLight = 2, PenaltyWet = 2, PenaltyMost = 3;   // how far from a post's wish a photo may be, by what differs
        public const int DayMost = 24;                // bank photos taken in a game day at most, whatever the option
        public const int DayMostFilling = 36;         // ...and while the bank is filling (CatchUpPhotos)
        public const int FrontRange = 250;            // metres from the player a business's front may be: the city's detail is there to be seen
        public const float PaceLeast = 20f;           // real seconds between two photos at least
        public const float PaceFilling = 12f;         // ...while the bank is filling (CatchUpPhotos)
        public const float PaceMost = 160f;           // ...and at most, after slow ones
        public const double SlowMs = 20.0;            // milliseconds of the main thread a photo's renders may take before the next waits longer
        public const double FrameMs = 10.0;           // ...and one frame of them: the two renders go a frame each, half of SlowMs each
        public const string Folder = "Flare";         // in the save's photos' folder
        public const string Prefix = "Bank_";
        public const string LegacyPrefix = "City_";   // the old one-photo-a-post files, deleted when Flare starts
        public const string Extension = ".png";       // the name a photo would have; only its small copy is ever written (PhotoThumbs.FileOf)
        public const string IndexFile = "bank.txt";   // the bank's records and the posts' picks, beside the photos
        public const int OptionDefault = 2;           // "some"
        public const int Version = 1;   // of the bank's file
        private const int SaltPhoto = 860, SaltPick = 861;

        private static readonly int[] Scale = { 0, 500, 1000, 1600 };   // thousandths of each kind's share, by the option: none, a few, some, many

        public static int Options => Scale.Length;

        public static FlarePhotoLight LightAt(int hour)
        {
            hour = ((hour % 24) + 24) % 24;
            return hour >= 21 || hour < 6 ? FlarePhotoLight.Night : hour == 6 || hour >= 17 ? FlarePhotoLight.Dusk : FlarePhotoLight.Day;
        }

        /// <summary>The share of a post's kind that carries a photo at "some", thousandths; none for a kind with nothing to see.</summary>
        public static int KindShare(FlarePost p, int niche)
        {
            if (p == null || p.ReplyTo >= 0 || p.Pending || !string.IsNullOrEmpty(p.Pic) || !string.IsNullOrEmpty(p.NetPic))
            {
                return 0;   // a reply, and a post with its own picture (the player's photo, a session's)
            }
            bool place = !string.IsNullOrEmpty(p.Place);
            switch (p.Author)
            {
                case FlareAuthor.Citizen:
                    if (p.Source != FlareSource.CityTalk)
                    {
                        return 0;   // a report or a review: a complaint is no photo shoot
                    }
                    switch ((FlareTalk)(p.Arg / 10))
                    {
                        case FlareTalk.Event: return 900;
                        case FlareTalk.Food:
                        case FlareTalk.Night: return 850;
                        case FlareTalk.Visit: return place ? 800 : 0;   // where they were: that business's front
                        case FlareTalk.Weather: return 800;
                        case FlareTalk.Sport: return 800;
                        case FlareTalk.Everyday:
                        case FlareTalk.Commute: return 650;
                        case FlareTalk.Roadworks: return 450;
                        default: return 0;   // nothing a bank photo truly shows
                    }
                case FlareAuthor.Business: return p.Source == FlareSource.BusinessPost ? 850 : 0;
                case FlareAuthor.Influencer:
                    switch (p.Source)
                    {
                        case FlareSource.Niche:
                        case FlareSource.Visit:
                        case FlareSource.Hired:
                            return niche == (int)FlareNiche.Tech || niche == (int)FlareNiche.Business ? 500 : 850;
                        case FlareSource.Rival: return 650;
                        case FlareSource.CityNews: return 550;
                        default: return 0;   // an election or an apology
                    }
                case FlareAuthor.Tycoon: return place ? 750 : 0;   // a rival's building, by the rules of the player's own
                case FlareAuthor.Player:
                    switch (p.Source)   // the player's own business posts: its front, else the street
                    {
                        case FlareSource.Opened:
                        case FlareSource.RecordDay:
                        case FlareSource.Interior:
                        case FlareSource.Announce:
                        case FlareSource.Sponsored:
                            return place ? 850 : 0;
                        default: return 0;
                    }
                case FlareAuthor.Friend:
                    if (p.Source != FlareSource.Life)
                    {
                        return 0;
                    }
                    switch ((FlareRules.LifeTopic)(p.Arg / 10))
                    {
                        case FlareRules.LifeTopic.Sport:
                        case FlareRules.LifeTopic.Food: return 750;
                        case FlareRules.LifeTopic.Night:
                        case FlareRules.LifeTopic.Shops:
                        case FlareRules.LifeTopic.District: return 650;
                        case FlareRules.LifeTopic.Plain: return 500;
                        default: return 350;   // work
                    }
                default: return p.Source == FlareSource.Invite && place ? 700 : 0;
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

        /// <summary>Whether a post's photo is to be picked now: from <see cref="LeadMinutes"/> before it goes up, however long after (a post first seen at a load too).</summary>
        public static bool Due(FlarePost p, long now)
        {
            return p != null && now >= p.At - LeadMinutes;
        }

        /// <summary>How many posts one photo may carry: <see cref="UsesMost"/> at a full bank, more while the bank is small (every photo of a small bank is used more, never twice in a row).</summary>
        public static int UsesAllowed(int photos)
        {
            return photos >= 60 ? UsesMost : photos >= 30 ? UsesMost + 1 : photos >= 15 ? UsesMost + 3 : UsesMost + 5;
        }

        /// <summary>How many of the last picks a photo may not follow: <see cref="RecentMost"/> at a fair bank, fewer for a small one (never fewer than two).</summary>
        public static int RecentAllowed(int photos)
        {
            return photos >= 30 ? RecentMost : photos >= 12 ? RecentMost - 1 : RecentMost - 2;
        }

        /// <summary>What a post's photo must show, in order of preference; false for nothing to see.</summary>
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
                            w.Wet = p.Topic != null && p.Topic.IndexOf("rain", StringComparison.Ordinal) >= 0;   // the post says what the weather is
                            w.WetStrict = true;
                            w.Add(FlarePhotoKind.Street);
                            w.Add(FlarePhotoKind.Park);
                            w.Add(FlarePhotoKind.Landmark);
                            break;
                        case FlareTalk.Visit:
                            w.Add(FlarePhotoKind.Front, p.Place);
                            w.Add(FlarePhotoKind.Street);
                            break;
                        case FlareTalk.Food:
                            w.Add(FlarePhotoKind.Front, place ? p.Place : null, (int)FlareNiche.Food);
                            w.Add(FlarePhotoKind.Street);
                            break;
                        case FlareTalk.Night:
                            w.Add(FlarePhotoKind.Front, place ? p.Place : null, (int)FlareNiche.Nightlife);
                            if (w.Light != FlarePhotoLight.Day)
                            {
                                w.Add(FlarePhotoKind.Street);   // a night out: the street at dusk or night will do, never by day
                            }
                            break;
                        case FlareTalk.Sport:
                            w.Add(FlarePhotoKind.Park);
                            w.Add(FlarePhotoKind.Street);
                            break;
                        case FlareTalk.Event:
                            w.Add(FlarePhotoKind.Event);
                            w.Add(FlarePhotoKind.Street);
                            break;
                        case FlareTalk.Commute:
                        case FlareTalk.Roadworks:
                            w.Add(FlarePhotoKind.Street);
                            break;
                        default:   // everyday
                            w.Add(FlarePhotoKind.Street);
                            w.Add(FlarePhotoKind.Park);
                            w.Add(FlarePhotoKind.Landmark);
                            break;
                    }
                    break;
                case FlareAuthor.Business:
                case FlareAuthor.Player:
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
                        case (int)FlareNiche.Food: w.Add(FlarePhotoKind.Front, null, (int)FlareNiche.Food); w.Add(FlarePhotoKind.Street); break;
                        case (int)FlareNiche.Nightlife: w.Add(FlarePhotoKind.Front, null, (int)FlareNiche.Nightlife); w.Add(FlarePhotoKind.Street); break;
                        case (int)FlareNiche.Fashion: w.Add(FlarePhotoKind.Front, null, (int)FlareNiche.Fashion); w.Add(FlarePhotoKind.Street); break;
                        case (int)FlareNiche.Fitness: w.Add(FlarePhotoKind.Park); w.Add(FlarePhotoKind.Street); break;
                        default: w.Add(FlarePhotoKind.Street); w.Add(FlarePhotoKind.Landmark); break;
                    }
                    break;
                case FlareAuthor.Friend:
                    switch ((FlareRules.LifeTopic)(p.Arg / 10))
                    {
                        case FlareRules.LifeTopic.Sport: w.Add(FlarePhotoKind.Park); w.Add(FlarePhotoKind.Street); break;
                        case FlareRules.LifeTopic.Food: w.Add(FlarePhotoKind.Front, null, (int)FlareNiche.Food); w.Add(FlarePhotoKind.Street); break;
                        case FlareRules.LifeTopic.Night: w.Add(FlarePhotoKind.Front, null, (int)FlareNiche.Nightlife); w.Add(FlarePhotoKind.Street); break;
                        case FlareRules.LifeTopic.Shops: w.Add(FlarePhotoKind.Front, null, (int)FlareNiche.Fashion); w.Add(FlarePhotoKind.Street); break;
                        case FlareRules.LifeTopic.District: w.Add(FlarePhotoKind.Street); w.Add(FlarePhotoKind.Landmark); break;
                        default: w.Add(FlarePhotoKind.Street); w.Add(FlarePhotoKind.Park); break;
                    }
                    break;
                default:   // a rival's building, an invitation's place: its own front, else the street
                    if (place)
                    {
                        w.Add(FlarePhotoKind.Front, p.Place);
                    }
                    w.Add(FlarePhotoKind.Street);
                    break;
            }
            return w.Ways > 0;
        }

        /// <summary>True if a bank photo fits one way of a wish exactly: kind, place, business kind, district, light, weather and age.</summary>
        public static bool Fits(FlareBankPhoto photo, FlarePhotoWish w, int way)
        {
            return Penalty(photo, w, way) == 0;
        }

        /// <summary>
        /// How far a bank photo is from one way of a wish: 0 for an exact fit, up to <see cref="PenaltyMost"/> for the nearest fit (another district of the
        /// city, the dusk for the day or the night, the other weather unless the post speaks of it), -1 for no fit (the wrong kind, place, business kind or
        /// event day, too old, day for night, or too far off).
        /// </summary>
        public static int Penalty(FlareBankPhoto photo, in FlarePhotoWish w, int way)
        {
            w.Way(way, out FlarePhotoKind kind, out string place, out int niche);
            if (photo == null || kind == FlarePhotoKind.None || photo.Kind != kind)
            {
                return -1;
            }
            int penalty = 0;
            if (photo.Light != w.Light)
            {
                if (photo.Light != FlarePhotoLight.Dusk && w.Light != FlarePhotoLight.Dusk)
                {
                    return -1;   // never a day photo for a night post, nor a night one for the day
                }
                penalty += PenaltyLight;
            }
            if (photo.Wet != w.Wet)
            {
                if (w.WetStrict)
                {
                    return -1;   // a post about the rain, or about the sun
                }
                penalty += PenaltyWet;
            }
            if (kind == FlarePhotoKind.Event)
            {
                return photo.Day == w.Day && penalty <= PenaltyMost ? penalty : -1;   // the day's event, wherever in the city it was
            }
            if (Math.Abs(w.Day - photo.Day) > FreshDays || (place != null && !string.Equals(photo.Place, place, StringComparison.Ordinal)) || (niche >= 0 && photo.Niche != niche))
            {
                return -1;
            }
            if (!string.IsNullOrEmpty(w.District) && !string.Equals(photo.District, w.District, StringComparison.Ordinal))
            {
                penalty += PenaltyDistrict;
            }
            return penalty <= PenaltyMost ? penalty : -1;
        }

        /// <summary>The bank's photo for a post, or null: among the free photos the nearest to what the post shows, the one its seed rolls.</summary>
        public static FlareBankPhoto Pick(FlareBank bank, in FlarePhotoWish w, int seed)
        {
            if (bank == null || bank.Photos.Count == 0 || w.Ways == 0)
            {
                return null;
            }
            int uses = UsesAllowed(bank.Photos.Count);
            Recents recent = Recent(bank, RecentAllowed(bank.Photos.Count));
            int best = int.MaxValue, count = 0;
            for (int i = 0; i < bank.Photos.Count; i++)
            {
                int rank = Rank(bank.Photos[i], w, uses, recent);
                if (rank >= 0 && rank < best)
                {
                    best = rank;
                    count = 1;
                }
                else if (rank == best)
                {
                    count++;
                }
            }
            if (count == 0)
            {
                return null;
            }
            int pick = FlareRules.Roll(seed, best, SaltPick) % count;
            for (int i = 0; i < bank.Photos.Count; i++)
            {
                if (Rank(bank.Photos[i], w, uses, recent) == best && pick-- == 0)
                {
                    return bank.Photos[i];
                }
            }
            return null;
        }

        // How wanted a free photo is: its nearest way's penalty and two for each way before it; -1 for a worn out or recent photo, or none that fits.
        private static int Rank(FlareBankPhoto photo, in FlarePhotoWish w, int uses, in Recents recent)
        {
            if (photo.Uses >= uses || recent.Has(photo.Id))
            {
                return -1;
            }
            int rank = -1;
            for (int way = 0; way < w.Ways; way++)
            {
                int penalty = Penalty(photo, w, way);
                if (penalty >= 0 && (rank < 0 || penalty + 2 * way < rank))
                {
                    rank = penalty + 2 * way;
                }
            }
            return rank;
        }

        // The last photos picked, newest first, up to four: a photo may not follow them.
        private struct Recents
        {
            private int _a, _b, _c, _d, _n;

            public void Add(int id)
            {
                switch (_n++)
                {
                    case 0: _a = id; break;
                    case 1: _b = id; break;
                    case 2: _c = id; break;
                    default: _d = id; break;
                }
            }

            public readonly bool Has(int id)
            {
                return (_n > 0 && _a == id) || (_n > 1 && _b == id) || (_n > 2 && _c == id) || (_n > 3 && _d == id);
            }
        }

        private static Recents Recent(FlareBank bank, int most)
        {
            var r = new Recents();
            for (int i = bank.Picks.Count - 1, seen = 0; i >= 0 && seen < Math.Min(most, RecentMost); i--)
            {
                if (bank.Picks[i].Photo >= 0)
                {
                    r.Add(bank.Picks[i].Photo);
                    seen++;
                }
            }
            return r;
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

        /// <summary>
        /// Whether a post's pick is final: a photo is; none is once the post is up and the bank holds <see cref="ReadyPhotos"/> (a bank still filling leaves
        /// the post to be tried again as it grows, so the first posts of a save do not stay bare).
        /// </summary>
        public static bool Settled(FlareBank bank, FlarePost p, FlareBankPhoto photo, long now)
        {
            return photo != null || (now >= p.At && bank.Photos.Count >= ReadyPhotos);
        }

        /// <summary>
        /// The next post due a pick, wanted and not yet picked, newest first from <paramref name="cursor"/> (start at <see cref="int.MaxValue"/>; each call
        /// carries on below the last post it gave); null for none.
        /// </summary>
        public static FlarePost NextPick(FlareFile f, FlareBank bank, int citySeed, int option, long now, Func<FlarePost, int> nicheOf, ref int cursor)
        {
            if (f == null || bank == null || option <= 0 || bank.Photos.Count == 0)
            {
                return null;
            }
            for (int i = Math.Min(cursor, f.Posts.Count) - 1; i >= 0; i--)
            {
                FlarePost p = f.Posts[i];
                if (Due(p, now) && Wants(p, citySeed, option, nicheOf != null ? nicheOf(p) : -1) && bank.PickOf(p.Id) == -2)
                {
                    cursor = i;
                    return p;
                }
            }
            cursor = 0;
            return null;
        }

        /// <summary>The kind of photo a park or sight (Leisure's landmarks) is; None for one that is no sight.</summary>
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

        /// <summary>What the bank's pass photographs here now: the street event, a park or sight, the best front, else the street; false for nothing lacking.</summary>
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

        /// <summary>The photos that go, into <paramref name="into"/>: past <see cref="FreshDays"/>, an event's after its day, the oldest past <see cref="BankMost"/>.</summary>
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
            for (int i = 0; i < bank.Photos.Count && kept > BankMost; i++)   // the photos are in the order taken
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

        /// <summary>The photos a game day may take: <see cref="DayMost"/>, a few more while the bank is filling.</summary>
        public static int DayLimit(int photos)
        {
            return photos < CatchUpPhotos ? DayMostFilling : DayMost;
        }

        /// <summary>The real seconds to wait after a photo whose renders took this long: the least after a quick one (a little less while the bank fills), doubled after a slow one.</summary>
        public static float NextPace(float pace, double renderMs, int photos = int.MaxValue)
        {
            float least = photos < CatchUpPhotos ? PaceFilling : PaceLeast;
            if (renderMs <= SlowMs)
            {
                return least;
            }
            return Math.Min(PaceMost, Math.Max(least, pace) * 2f);
        }

        public static bool InRange(float dx, float dz)
        {
            return dx * dx + dz * dz <= (float)FrontRange * FrontRange;
        }

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

        /// <summary>True for a photo file of the old one-a-post kind: deleted when Flare starts.</summary>
        public static bool Legacy(string fileName)
        {
            return fileName != null && fileName.StartsWith(LegacyPrefix, StringComparison.Ordinal);
        }

        /// <summary>The bank as its file's text: "v=1", "n=" the next number, a "p|..." line a photo and a "u|post|photo" line a pick.</summary>
        public static string Write(FlareBank bank)
        {
            // A post that found no photo has no line: it is tried again at a load, when the bank has grown.
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
                if (bank.Picks[i].Photo >= 0)   // a post that found no photo is tried again at the next load: the bank has grown since
                {
                    b.Append("u|").Append(bank.Picks[i].Post).Append('|').Append(bank.Picks[i].Photo).Append('\n');
                }
            }
            return b.ToString();
        }

        /// <summary>The bank from its file's text; an empty bank for none, another version or text that cannot be read.</summary>
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
                else if (f[0] == "u" && f.Length >= 3 && Int(f[1], out int post) && Int(f[2], out int photo) && photo >= 0)   // an older file's "none" picks are let go: tried again
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
