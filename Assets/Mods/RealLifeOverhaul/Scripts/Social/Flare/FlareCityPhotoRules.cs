using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The city's photos: a bank of them taken quietly while the player is out, and the city's posts carrying one that
    /// fits, so the city's people post photos as real people do. While the player is outdoors the bank's pass
    /// (<see cref="Plan"/>) takes what the bank lacks here. A city post whose kind has something to see (<see cref="Wish"/>)
    /// and whose seed rolls under its share is given the fitting photo (<see cref="Pick"/>), never a wrong one, kept after a
    /// load. Which posts carry one and which photo go by the post's seed, so every machine of a session picks alike and
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
        public const int UsesMost = 3;                // posts one photo may carry
        public const int RecentMost = 4;              // the last picks a photo may not follow: never twice in a row in the feed
        public const int LeadMinutes = 30;            // game minutes before a post goes up its photo is picked, in the weather then
        public const int WindowMinutes = 180;         // ...and after: a post first seen later (a load) carries none
        public const int DayMost = 24;                // bank photos taken in a game day at most, whatever the option
        public const int FrontRange = 250;            // metres from the player a business's front may be: the city's detail is there to be seen
        public const float PaceLeast = 20f;           // real seconds between two photos at least
        public const float PaceMost = 160f;           // ...and at most, after slow ones
        public const double SlowMs = 20.0;            // milliseconds of the main thread a photo's renders may take before the next waits longer
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
            if (p == null || p.ReplyTo >= 0 || p.Pending)
            {
                return 0;
            }
            switch (p.Author)
            {
                case FlareAuthor.Citizen:
                    if (p.Source != FlareSource.CityTalk)
                    {
                        return 0;   // a report or a review: a complaint is no photo shoot
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
                        default: return 0;   // nothing a bank photo truly shows
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
                            w.Add(FlarePhotoKind.Street);
                            break;
                        case FlareTalk.Food:
                            w.Add(FlarePhotoKind.Front, place ? p.Place : null, (int)FlareNiche.Food);
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
                            break;
                        case FlareTalk.Event:
                            w.Add(FlarePhotoKind.Event);
                            break;
                        case FlareTalk.Commute:
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
                default:   // a rival's building, an invitation's place: its own front or nothing
                    w.Add(FlarePhotoKind.Front, p.Place);
                    break;
            }
            return w.Ways > 0;
        }

        /// <summary>True if a bank photo fits one way of a wish: kind, place, business kind, district, light, weather and age.</summary>
        public static bool Fits(FlareBankPhoto photo, FlarePhotoWish w, int way)
        {
            w.Way(way, out FlarePhotoKind kind, out string place, out int niche);
            if (photo == null || kind == FlarePhotoKind.None || photo.Kind != kind || photo.Light != w.Light || photo.Wet != w.Wet)
            {
                return false;
            }
            if (kind == FlarePhotoKind.Event)
            {
                return photo.Day == w.Day;   // the day's event, wherever in the city it was
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

        /// <summary>The bank's photo for a post, or null: among the first way's free photos, the one its seed rolls.</summary>
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

        /// <summary>The next post due a pick, wanted and not yet picked or in <paramref name="skip"/>, newest first; null for none.</summary>
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

        /// <summary>The real seconds to wait after a photo whose renders took this long: the least after a quick one, doubled after a slow one.</summary>
        public static float NextPace(float pace, double renderMs)
        {
            if (renderMs <= SlowMs)
            {
                return PaceLeast;
            }
            return Math.Min(PaceMost, Math.Max(PaceLeast, pace) * 2f);
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
