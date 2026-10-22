using System;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// The rules of talking to people in the street; the model only words the line the rules chose. Game-free.
    /// COST: arithmetic and a few short lists, on a press or a click.
    /// </summary>
    public static partial class StreetRules
    {
        public const float Reach = 3.2f;          // metres: talking distance in front of the player
        public const float LetGo = 4.0f;          // metres: the one offered keeps the offer until this far, so it does not flicker
        public const float Facing = 0.5f;         // the cosine of the widest angle off the player's facing for a new person (60 degrees)
        public const float KeepFacing = 0.17f;    // ...and for the one offered (about 80 degrees)
        public const float Dwell = 0.3f;          // real seconds somebody must be in front before Talk is offered
        public const float Grace = 1.5f;          // real seconds the offer stays on somebody who has just left the reach or the angle
        public const float GraceMetres = 4.2f;    // ...within this many metres (under WalkAway, so a press still holds them)
        public const float Tick = 0.2f;           // real seconds between two looks for somebody to talk to, on foot out of doors
        public const float Height = 1.5f;         // metres up or down somebody may stand from the player
        public const float WalkAway = 4.5f;       // metres: the player this far from them ends the conversation
        public const float PoliceMetres = 35f;    // metres: an officer this near hears a threat

        public const double Hold = 1.1;           // real seconds a line waits for the model's words at most, then the written line
        public const float Beat = 0.45f;          // real seconds of turning to the player before the first line, when nothing is waited for
        public const float TurnSeconds = 0.6f;    // seconds they take to turn to the player (the game's own turn)
        public const double GuessSeconds = 12.0;  // real seconds the line guessed while the player looks at someone is kept for the press

        public const int MemoryMost = 48;         // people remembered today at most (the oldest go first)
        public const int PatienceLeast = 4;       // turns anybody who stops gives the player
        public const int PatienceMost = 10;       // ...at most, earned turns included
        public const int EarnedMost = 3;          // turns a conversation earns back at most, one for each good exchange
        public const int CoolestStranger = -1;    // warmth: how cool a stranger is at worst without a reason (Warmth)
        public const int InfamousBelow = 40;      // public standing: below this, somebody who knows the player's face has heard bad things
        public const int RapportMost = 10;
        public const int NumberAt = 3;            // rapport (today's and this talk's, with their warmth) at which a number is given
        public const int NumberLiked = 1;         // ...enough when the city thinks well of the player
        public const int LikedFrom = 70;          // public standing: the law's "liked" (Reputation.LikedFrom)
        public const int DislikedBelow = 30;      // ...below this the city thinks badly of the player
        public const int RecogniseMost = 90;      // in hundredths: at full recognition nine in ten know the player's face
        public const int CompatiblePercent = 45;  // of adults 21 to 60, a romance would be possible
        public const int TakenPercent = 30;       // with somebody else
        public const int CommuterPercent = 45;    // on their way to work at the rush hours
        public const int HumourPercent = 60;
        public const int ChattyPercent = 30;
        public const int RetiredFrom = 66;        // years

        // Brush-offs, in thousandths before the option's factor.
        public const int HurryOdds = 110;         // anybody, any time: somewhere to be
        public const int WorkOdds = 600;          // a commuter at the rush hours
        public const int NightOdds = 450;         // a stranger in the small hours
        public const int RainOdds = 280;          // in the rain
        public const int MoodOdds = 450;          // on a rough day
        public const int UnwashedOdds = 350;      // the player has not washed
        public const int RecordOdds = 500;        // they know the player's face and their record
        public const int CallOdds = 400;          // on a call
        public const int TextingOdds = 150;       // looking at their phone
        public const int OddsMost = 950;
        public static readonly float[] BrushFactor = { 0.5f, 1f, 1.7f };   // the option: rarely, sometimes, often

        public const int MouthPercent = 1;        // per cent more customers for each person who says they will go
        public const int MouthMost = 3;           // per cent at most for one business, however many are told
        public const int MouthDays = 2;           // game days it lasts from the day it was told
        public const int MouthPerDay = 3;         // people told a day that count, for all the player's businesses

        public const float WalkMetresPerMinute = 75f;   // a walk, to say how far
        public const float FarMetres = 900f;            // metres: further than this is "a fair way"
        public const float FindMetres = 1600f;          // metres a place is looked for at most

        /// <summary>How people talk (the card's style line): spoken ways, never written ones such as lowercase or emoji.</summary>
        public static readonly string[] Styles = { "warm", "dry", "blunt", "shy", "cheeky", "brief", "chatty", "formal", "playful", "gruff" };

        /// <summary>Parts of the game's business type names that make a place of each kind.</summary>
        public static readonly string[][] PlaceTypes =
        {
            new[] { "coffeeshop" },
            new[] { "restaurant" },
            new[] { "nightclub", "bar" },
            new[] { "supermarket", "fruitandvegetable", "grocery" },
            new[] { "clothingstore", "shoestore" },
            new[] { "gym" },
            new[] { "cinema", "theater" },
            new[] { "_bank" }
        };

        /// <summary>A number from a seed and a slot, 0 to n-1: the same every time for the same person and purpose.</summary>
        public static int Roll(int seed, int slot, int n)
        {
            if (n <= 1)
            {
                return 0;
            }
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)(slot + 0x5bd1) * 2246822519u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h % (uint)n);
            }
        }

        /// <summary>The seed of somebody, from the save's own number, the day and the game's figure.</summary>
        public static int Seed(int save, int day, int key)
        {
            unchecked
            {
                return (int)((uint)save * 73856093u ^ (uint)day * 19349663u ^ (uint)key * 83492791u) & 0x7fffffff;
            }
        }

        /// <summary>Who somebody is, from their seed and the sex and age the game shows; they live, more often than not, where met.</summary>
        public static StreetStranger Identity(int save, int day, int key, bool female, int age, int districts, int metIn)
        {
            int seed = Seed(save, day, key);
            int life = Seed(save, 0, key);   // what stays from day to day while the figure does: name, job, home
            var s = new StreetStranger { Key = key, Seed = seed, Female = female, Age = Math.Max(18, Math.Min(85, age)) };
            string[] firsts = female ? SocialCatalogue.Women : SocialCatalogue.Men;
            s.First = firsts[Roll(life, 1, firsts.Length)];
            s.Last = SocialCatalogue.Surnames[Roll(life, 2, SocialCatalogue.Surnames.Length)];
            s.Job = s.Age >= RetiredFrom ? SocialCatalogue.Jobs.Length - 1 : Roll(life, 3, SocialCatalogue.Jobs.Length - 1);
            int n = Math.Max(1, districts);
            s.District = metIn >= 0 && metIn < n && Roll(life, 4, 100) < 60 ? metIn : Roll(life, 5, n);
            SocialTrait[] pair = SocialCatalogue.TraitPairs[Roll(life, 6, SocialCatalogue.TraitPairs.Length)];
            s.Traits = pair[0] | pair[1];
            s.Mood = Roll(seed, 7, 5) - 2;
            s.Style = Roll(life, 8, Styles.Length);
            s.Compatible = s.Age >= 21 && s.Age <= 60 && Roll(life, 9, 100) < CompatiblePercent;
            s.Taken = Roll(life, 10, 100) < TakenPercent;
            s.Commuter = s.Job != SocialCatalogue.Jobs.Length - 1 && Roll(seed, 11, 100) < CommuterPercent;
            s.Humour = Roll(life, 12, 100) < HumourPercent;
            s.Chatty = Roll(life, 13, 100) < ChattyPercent;
            s.Handle = "@" + s.First.ToLowerInvariant() + (10 + Roll(life, 14, 90)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return s;
        }

        public static bool Night(int hour) => hour >= 23 || hour < 6;

        public static bool RushHour(int hour, bool weekend) => !weekend && ((hour >= 7 && hour < 10) || (hour >= 17 && hour < 19));

        /// <summary>Whether they stop and how they take the player, rolled so asking twice does not reroll; <paramref name="phone"/> 0, 1 texting, 2 a call.</summary>
        public static StreetApproach Approach(StreetStranger s, StreetMemo memo, StreetFacts f, int option, int phone)
        {
            var a = new StreetApproach();
            int talks = memo != null ? memo.Talks : 0;
            int roll = Seed(s.Seed, f.Hour, talks);
            a.Recognised = f.Recognition > 0 && Roll(roll, 20, 100) < Math.Min(RecogniseMost, f.Recognition);
            a.Famed = a.Recognised && (f.Standing >= Reputation55 || f.Standing < InfamousBelow);   // the famous and the infamous know the name too
            a.Warmth = Warmth(s, memo, f, a.Recognised);
            a.Brush = Brush(s, memo, f, a.Recognised, a.Warmth, roll, option, phone);
            int patience = PatienceLeast + Math.Max(0, a.Warmth) + (a.Warmth >= 2 ? 1 : 0) + (s.Chatty ? 2 : 0) - (s.Commuter && RushHour(f.Hour, f.Weekend) ? 1 : 0);
            a.Patience = Math.Max(PatienceLeast, Math.Min(PatienceMost, patience));
            a.Opening = memo != null && memo.Talks > 0 && memo.Rapport >= 0 ? StreetLine.OpenAgain
                : a.Recognised && f.Standing >= Reputation55 ? StreetLine.OpenFamous
                : a.Recognised && f.Standing < InfamousBelow ? StreetLine.OpenInfamous
                : a.Warmth >= 1 ? StreetLine.OpenWarm
                : a.Warmth <= -1 ? StreetLine.OpenCool
                : StreetLine.OpenFine;
            return a;
        }

        private const int Reputation55 = 55;   // the newcomer's public standing (Reputation.Start)

        /// <summary>-3 to 3: their mood with what they make of the player; at worst <see cref="CoolestStranger"/> without a reason.</summary>
        public static int Warmth(StreetStranger s, StreetMemo memo, StreetFacts f, bool recognised)
        {
            int w = s.Mood;
            w += f.Standing >= LikedFrom ? 1 : f.Standing < DislikedBelow ? -1 : 0;
            if (recognised)
            {
                w += f.Standing >= Reputation55 ? 1 : -1;
                w -= f.Record ? 1 : 0;
            }
            w -= f.Unwashed ? 1 : 0;
            w -= Night(f.Hour) ? 1 : 0;
            if (memo != null)
            {
                w += memo.Rapport >= 2 ? 1 : memo.Rapport <= -2 ? -1 : 0;
            }
            bool reason = Night(f.Hour) || (recognised && (f.Record || f.Standing < InfamousBelow)) || (memo != null && (memo.Rude || memo.Rapport <= -2));
            return Math.Max(reason ? -3 : CoolestStranger, Math.Min(3, w));
        }

        private static StreetBrush Brush(StreetStranger s, StreetMemo memo, StreetFacts f, bool recognised, int warmth, int roll, int option, int phone)
        {
            if (memo != null && memo.Rude)
            {
                return StreetBrush.Rude;   // they remember: not today
            }
            if (recognised && f.Wanted)
            {
                return StreetBrush.Wanted;
            }
            if (memo != null && memo.Talks > 0 && memo.Rapport >= 1)
            {
                return StreetBrush.None;   // they liked talking to the player earlier today
            }
            float k = BrushFactor[Math.Max(0, Math.Min(BrushFactor.Length - 1, option))];
            if (recognised && f.Record && Roll(roll, 21, 1000) < Odds(RecordOdds, k))
            {
                return StreetBrush.Record;
            }
            if (f.Unwashed && Roll(roll, 22, 1000) < Odds(UnwashedOdds, k))
            {
                return StreetBrush.Unwashed;
            }
            if (s.Commuter && RushHour(f.Hour, f.Weekend) && Roll(roll, 23, 1000) < Odds(WorkOdds, k))
            {
                return StreetBrush.Work;
            }
            if (Night(f.Hour) && warmth <= 0 && Roll(roll, 24, 1000) < Odds(NightOdds, k))
            {
                return StreetBrush.Night;
            }
            if (f.Raining && Roll(roll, 25, 1000) < Odds(RainOdds, k))
            {
                return StreetBrush.Rain;
            }
            if (s.Mood <= -2 && Roll(roll, 26, 1000) < Odds(MoodOdds, k))
            {
                return StreetBrush.Mood;
            }
            if (phone > 0 && Roll(roll, 27, 1000) < Odds(phone >= 2 ? CallOdds : TextingOdds, k))
            {
                return StreetBrush.Phone;
            }
            if (Roll(roll, 28, 1000) < Odds(HurryOdds, k))
            {
                return StreetBrush.Hurry;
            }
            return StreetBrush.None;
        }

        private static int Odds(int odds, float factor) => Math.Min(OddsMost, (int)(odds * factor));

        public static StreetLine BrushLine(StreetBrush b)
        {
            switch (b)
            {
                case StreetBrush.Work: return StreetLine.BrushWork;
                case StreetBrush.Night: return StreetLine.BrushNight;
                case StreetBrush.Rain: return StreetLine.BrushRain;
                case StreetBrush.Mood: return StreetLine.BrushMood;
                case StreetBrush.Rude: return StreetLine.BrushRude;
                case StreetBrush.Unwashed: return StreetLine.BrushUnwashed;
                case StreetBrush.Record: return StreetLine.BrushRecord;
                case StreetBrush.Wanted: return StreetLine.BrushWanted;
                case StreetBrush.Phone: return StreetLine.BrushPhone;
                default: return StreetLine.BrushHurry;
            }
        }
    }
}
