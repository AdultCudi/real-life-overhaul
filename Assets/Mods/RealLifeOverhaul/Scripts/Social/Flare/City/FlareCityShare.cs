using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>A business's figures as a guest of a Going Public session hears them from the host: its page as the host's residents see it.</summary>
    public sealed class FlareCityFigures
    {
        public int Followers;
        public int Rating;           // tenths of a star, 0 none
        public int Reviews;
        public int Attention;        // thousandths
        public int Came;             // visits Flare's attention brought on the host's last day
        public long Spent;           // cents
        public long Heard, Sour;     // thousandths of a resident
        public int Reviewer;         // the newest review's resident, 0 none
        public bool ReviewerFemale;
        public int Stars;
    }

    /// <summary>What a guest follows of the host's residents on Flare: the voices behind the city's people and the guest's own businesses' figures.</summary>
    public sealed class FlareCityFollowed
    {
        public int Day = -1;
        public readonly CityVoice[] Voices = new CityVoice[FlareLiveRules.Citizens];
        public readonly string[] VoiceDistricts = new string[FlareLiveRules.Citizens];   // each voice's district by the game's own id
        public readonly Dictionary<string, FlareCityFigures> Businesses = new Dictionary<string, FlareCityFigures>(StringComparer.Ordinal);   // by Flare's place
        public readonly List<FlareTrendSeen> Trends = new List<FlareTrendSeen>(FlareCityCatalogue.TrendsMost);   // what is trending among the host's residents
    }

    /// <summary>
    /// Flare in the city in a Going Public session: a part of the shared city of its own (<see cref="Part"/>, versioned; an older RLO, or one without
    /// this part, never knows the letter and leaves it out, SharedCity.Apply). The host owns the residents and what they hear, as it owns the
    /// population; its guests follow its voices, so the city's people on the feed are the same residents on every machine, and hear their own
    /// businesses' figures (the host's pools give them their customers). Sent whole once a day after the host's residents' day, a few thousand
    /// characters. Every number is read within its bounds and a line this version does not understand is left out. Game-free.
    ///
    ///   "FR1|day"
    ///   "V|voice|person|female|age|district|persona"                      each voice with a resident (the district by the game's id)
    ///   "B|place|followers|rating|reviews|attention|came|spent|heard|sour|reviewer|female|stars"   each business of a session player's
    ///   "T|kind|what|district|day|moved"                                 each trend, newest first (the district by the game's id, empty for the whole city)
    /// </summary>
    public static class FlareCityShare
    {
        public const char Part = 'Q';   // R is the tech markets' (Tech/Sector/SectorStore.cs): EconomyCheck's SharedLetters fails on a letter taken twice
        public const string Version = "FR1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The host's part, whole: its voices and the session players' businesses (<paramref name="guestPlace"/> gives a guest's business's place, or null).</summary>
        public static string Write(FlareCityState c, PopulationState s, int day, int[] personSlot, Func<int, string> guestPlace)
        {
            var b = new StringBuilder(16384);
            b.Append(Version).Append('|').Append(day.ToString(Inv));
            for (int v = 0; v < c.Voices.Length; v++)
            {
                CityVoice cv = FlareCityRules.VoiceOf(c, s, v, day, personSlot);
                if (cv.Person == 0)
                {
                    continue;
                }
                string district = cv.District >= 0 && cv.District < s.Districts.Count ? s.Districts[cv.District].Id : string.Empty;
                b.Append('\n').Append("V|").Append(v.ToString(Inv)).Append('|').Append(cv.Person.ToString(Inv)).Append('|').Append(cv.Female ? '1' : '0').Append('|')
                    .Append(cv.Age.ToString(Inv)).Append('|').Append(Esc(district)).Append('|').Append(((int)cv.Persona).ToString(Inv));
            }
            var newest = new FlareCityReview[1];
            for (int i = 0; i < c.BusinessCount && i < s.Businesses.Count; i++)
            {
                string place = guestPlace != null ? guestPlace(i) : null;
                if (place == null)
                {
                    continue;
                }
                FlareCityRules.Talk(c, i, out long heard, out long sour);
                int n = FlareCityRules.NewestReviews(c, i, newest);
                int slot = n > 0 ? FlareCityRules.Slot(personSlot, s, newest[0].Person) : -1;
                b.Append('\n').Append("B|").Append(Esc(place)).Append('|').Append(c.Followers[i].ToString(Inv)).Append('|').Append(c.Rating(i).ToString(Inv)).Append('|')
                    .Append(c.ReviewCount[i].ToString(Inv)).Append('|').Append(c.Attention[i].ToString(Inv)).Append('|').Append(c.LiftVisits[i].ToString(Inv)).Append('|')
                    .Append(c.Lift[i].ToString(Inv)).Append('|').Append(heard.ToString(Inv)).Append('|').Append(sour.ToString(Inv)).Append('|')
                    .Append(n > 0 ? newest[0].Person.ToString(Inv) : "0").Append('|').Append(slot >= 0 && s.People.Female[slot] != 0 ? '1' : '0').Append('|')
                    .Append(n > 0 ? newest[0].Stars.ToString(Inv) : "0");
            }
            foreach (FlareTrendSeen t in FlareCityRules.Seen(c, s))
            {
                b.Append('\n').Append("T|").Append(((int)t.Kind).ToString(Inv)).Append('|').Append(t.What.ToString(Inv)).Append('|').Append(Esc(t.District)).Append('|')
                    .Append(t.Day.ToString(Inv)).Append('|').Append(t.Moved.ToString(Inv));
            }
            return b.ToString();
        }

        /// <summary>The host's part read into what a guest follows; false for a text that is not this version's whole part.</summary>
        public static bool Read(string text, FlareCityFollowed into)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string[] lines = text.Split('\n');
            string[] head = lines[0].TrimEnd('\r').Split('|');
            if (head.Length < 2 || head[0] != Version || !int.TryParse(head[1], NumberStyles.Integer, Inv, out int day))
            {
                return false;
            }
            Array.Clear(into.Voices, 0, into.Voices.Length);
            Array.Clear(into.VoiceDistricts, 0, into.VoiceDistricts.Length);
            into.Businesses.Clear();
            into.Trends.Clear();
            into.Day = day;
            for (int i = 1; i < lines.Length; i++)
            {
                string[] f = lines[i].TrimEnd('\r').Split('|');
                if (f.Length >= 7 && f[0] == "V" && Int(f[1], 0, FlareLiveRules.Citizens - 1, out int v) && Int(f[2], 1, int.MaxValue, out int person)
                    && Int(f[4], 0, 130, out int age) && Int(f[6], 0, (int)FlarePersona.NightOwl, out int persona))
                {
                    into.Voices[v] = new CityVoice { Person = person, Female = f[3] == "1", Age = age, District = -1, Persona = (FlarePersona)persona };
                    into.VoiceDistricts[v] = Unesc(f[5]);
                }
                else if (f.Length >= 13 && f[0] == "B")
                {
                    string place = Unesc(f[1]);
                    if (string.IsNullOrEmpty(place) || into.Businesses.Count >= 4096)
                    {
                        continue;
                    }
                    var fig = new FlareCityFigures();
                    bool ok = Int(f[2], 0, int.MaxValue, out fig.Followers) & Int(f[3], 0, 50, out fig.Rating) & Int(f[4], 0, int.MaxValue, out fig.Reviews)
                              & Int(f[5], FlareCityCatalogue.AttentionLeast, FlareCityCatalogue.AttentionMost, out fig.Attention) & Int(f[6], -10000000, 10000000, out fig.Came)
                              & Long(f[7], out fig.Spent) & Long(f[8], out fig.Heard) & Long(f[9], out fig.Sour) & Int(f[10], 0, int.MaxValue, out fig.Reviewer)
                              & Int(f[12], 0, 5, out fig.Stars);
                    fig.ReviewerFemale = f[11] == "1";
                    if (ok)
                    {
                        into.Businesses[place] = fig;
                    }
                }
                else if (f.Length >= 6 && f[0] == "T" && into.Trends.Count < FlareCityCatalogue.TrendsMost && Int(f[1], 0, (int)TrendKind.Genre, out int kind)
                         && Int(f[2], 0, FlareCityRules.Kinds - 1, out int what) && Int(f[4], int.MinValue, int.MaxValue, out int since) && Int(f[5], 0, int.MaxValue, out int moved))
                {
                    string district = Unesc(f[3]);
                    into.Trends.Add(new FlareTrendSeen { Kind = (TrendKind)kind, What = what, District = string.IsNullOrEmpty(district) ? null : district, Day = since, Moved = moved });
                }
            }
            return true;
        }

        private static bool Int(string s, int least, int most, out int value)
        {
            if (!int.TryParse(s, NumberStyles.Integer, Inv, out value) || value < least || value > most)
            {
                value = 0;
                return false;
            }
            return true;
        }

        private static bool Long(string s, out long value)
        {
            return long.TryParse(s, NumberStyles.Integer, Inv, out value) || (value = 0) != 0;
        }

        private static string Esc(string s) => s == null ? string.Empty : s.Replace("%", "%25").Replace("|", "%7C").Replace("\n", "%0A").Replace("\r", "%0D");

        private static string Unesc(string s) => s == null ? null : s.Replace("%0D", "\r").Replace("%0A", "\n").Replace("%7C", "|").Replace("%25", "%");
    }
}
