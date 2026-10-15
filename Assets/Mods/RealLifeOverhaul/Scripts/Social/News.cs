using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BigAmbitions.DayNightCycle;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>What kind of thing happened. The paper words each differently.</summary>
    public enum NewsKind
    {
        Accident,        // amount: 0 minor, 1 moderate, 2 severe
        PedestrianHit,   // amount: 1 if they died
        HitAndRun,
        Crime,           // detail: the crime catalogue's id
        Arrest,          // detail: the crime catalogue's id
        BreakIn,         // detail: the business; amount: what it cost
        CarBreakIn,
        PlayerCrash,
        PlayerArrested,
        PlayerKnocked,
        Story            // detail: the locale key of a finished piece with no placeholders (a verdict, an election result, a fire)
    }

    /// <summary>One line of the paper: a locale key and what fills its placeholders.</summary>
    public struct NewsItem
    {
        public string Key;
        public Dictionary<string, string> Data;
    }

    /// <summary>
    /// The city's rolling log of things worth printing, and the desk that writes the morning
    /// paper from it. Any feature reports with one call, <see cref="Report"/>: it stores a few
    /// numbers in a slot of a fixed array (nothing is allocated, no text is built, nothing is
    /// looked up) and returns at once when the paper is switched off or no save is loaded.
    /// Streets are worked out when the paper is written, once a day. The City Ledger (Scripts/Ledger,
    /// through Wiring/LedgerLinks) prints what is written here as a newspaper, for a player who has a copy.
    /// </summary>
    public static class News
    {
        private const int Capacity = 48;
        private const string KeyLog = "reallife:social_news";
        private const string KeyPaper = "reallife:social_paper";
        private const string KeyFiller = "reallife:social_filler";
        private const int PaperHour = 6;

        private struct Entry
        {
            public NewsKind Kind;
            public int Day, Hour;
            public float X, Z;
            public string Detail;
            public int Amount;
            public bool Printed;
        }

        private static readonly Entry[] Log = new Entry[Capacity];
        private static int _count;
        private static int _next;
        private static bool _ready;
        private static bool _rained;
        private static readonly List<NewsItem> Paper = new List<NewsItem>(8);
        private static readonly HashSet<int> FillerSeen = new HashSet<int>();
        private static int _lastFiller = -1;

        /// <summary>The day today's paper was written for; 0 when there has been none.</summary>
        public static int PaperDay { get; private set; }

        /// <summary>
        /// Pieces another feature has for the day's paper (the city's economy: Wiring/CityLinks). Called once when the
        /// paper is written, with the day and the paper so far; it adds what it has, after the business piece. It must
        /// give the same pieces for the same day however often it is asked (the test button writes a day's paper again).
        /// Nothing while it is not set.
        /// </summary>
        public static Action<int, List<NewsItem>> Extra;

        /// <summary>Today's paper, masthead first. Empty until one has been written.</summary>
        public static IReadOnlyList<NewsItem> Today => Paper;

        /// <summary>
        /// Raised for every report while a save is loaded, whether or not the paper prints it (the stock market hears
        /// of a crime, a break-in or an accident the moment it is reported: Wiring/MarketLinks). With the kind, the
        /// report's detail and its amount. A handler must not throw; one that does is logged and the report goes on.
        /// </summary>
        public static event Action<NewsKind, string, int> Reported;

        /// <summary>
        /// Raised for every report while a save is loaded, whether or not the paper prints it, with the kind and where it
        /// happened in the street (something reported inside a building is at the building's door; a report with no place
        /// is at zero). The district view notes crimes by district from it (Wiring/DistrictLinks). A handler must not
        /// throw; one that does is logged and the report goes on.
        /// </summary>
        public static event Action<NewsKind, Vector3> ReportedAt;

        /// <summary>
        /// Every report, also handed on as it is made (a guest of a Going Public session that follows the host's paper sends it
        /// to the host's: Ledger/LedgerDirector.Share.cs, through Wiring/LedgerLinks). Nothing while it is not set.
        /// </summary>
        public static Action<NewsKind, Vector3, string, int> Forward;

        /// <summary>
        /// Something happened that a newspaper would print. Safe to call from anywhere at any
        /// time: it does nothing until a save is loaded, or while the paper is switched off.
        /// </summary>
        /// <param name="detail">An id or a name that already exists as a string (never one built for this call).</param>
        public static void Report(NewsKind kind, Vector3 where, string detail = null, int amount = 0)
        {
            Action<NewsKind, Vector3, string, int> forward = Forward;
            if (forward != null && SaveGameManager.Current != null)
            {
                try
                {
                    forward(kind, where, detail, amount);
                }
                catch (Exception e)
                {
                    SocialDirector.Logger?.Error(e);
                }
            }
            Action<NewsKind, string, int> heard = Reported;
            if (heard != null && SaveGameManager.Current != null)
            {
                try
                {
                    heard(kind, detail, amount);
                }
                catch (Exception e)
                {
                    SocialDirector.Logger?.Error(e);
                }
            }
            Action<NewsKind, Vector3> placed = ReportedAt;
            if (placed != null && SaveGameManager.Current != null)
            {
                try
                {
                    placed(kind, BuildingManager.IsInsideBuilding && where != Vector3.zero ? SocialPlaces.OnStreet(where) : where);
                }
                catch (Exception e)
                {
                    SocialDirector.Logger?.Error(e);
                }
            }
            if (!_ready || (!Settings.SocialNewspaper && !Settings.Social))
            {
                return;
            }
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (BuildingManager.IsInsideBuilding)
            {
                where = SocialPlaces.OnStreet(where);   // something in a shop is reported at the shop's door
            }
            ref Entry entry = ref Log[_next];
            entry.Kind = kind;
            entry.Day = save.Day;
            entry.Hour = save.Hour;
            entry.X = where.x;
            entry.Z = where.z;
            entry.Detail = detail;
            entry.Amount = amount;
            entry.Printed = false;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity)
            {
                _count++;
            }
        }

        /// <summary>The newest thing of a kind within so many days, for conversation. False if there is none.</summary>
        public static bool Recent(NewsKind kind, int days, out Vector3 where, out string detail)
        {
            where = default;
            detail = null;
            if (!_ready || SaveGameManager.Current == null)
            {
                return false;
            }
            int today = SaveGameManager.Current.Day;
            for (int back = 1; back <= _count; back++)
            {
                ref Entry entry = ref Log[(_next - back + Capacity * 2) % Capacity];
                if (entry.Kind == kind && today - entry.Day <= days)
                {
                    where = new Vector3(entry.X, 0f, entry.Z);
                    detail = entry.Detail;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Called once an hour: whether it rained is only known by having looked.</summary>
        public static void NoteWeather()
        {
            if (RainHelper.isRaining)
            {
                _rained = true;
            }
        }

        /// <summary>True when the morning's paper is due: after six, once a day.</summary>
        public static bool Due(int day, int hour)
        {
            return _ready && Settings.SocialNewspaper && hour >= PaperHour && PaperDay != day;
        }

        public static void Reset()
        {
            _ready = false;
            _count = 0;
            _next = 0;
            _rained = false;
            PaperDay = 0;
            Paper.Clear();
            FillerSeen.Clear();
            _lastFiller = -1;
        }

        // ------------------------------------------------------------------ the save

        public static void Load(Dictionary<string, string> data)
        {
            Reset();
            try
            {
                if (data != null && data.TryGetValue(KeyLog, out string log) && !string.IsNullOrEmpty(log))
                {
                    foreach (string record in log.Split('\n'))
                    {
                        string[] f = record.Split('|');
                        if (f.Length < 8 || _count >= Capacity)
                        {
                            continue;
                        }
                        Log[_count++] = new Entry
                        {
                            Kind = (NewsKind)Int(f[0]), Day = Int(f[1]), Hour = Int(f[2]), X = Float(f[3]), Z = Float(f[4]),
                            Detail = f[5].Length > 0 ? f[5] : null, Amount = Int(f[6]), Printed = f[7] == "1"
                        };
                    }
                    _next = _count % Capacity;
                }
                if (data != null && data.TryGetValue(KeyPaper, out string paper) && !string.IsNullOrEmpty(paper))
                {
                    string[] lines = paper.Split('\n');
                    PaperDay = Int(lines[0]);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string[] f = lines[i].Split('|');
                        if (f[0].Length == 0)
                        {
                            continue;
                        }
                        var item = new NewsItem { Key = f[0], Data = new Dictionary<string, string>(4) };
                        for (int k = 1; k + 1 < f.Length; k += 2)
                        {
                            item.Data[f[k]] = f[k + 1];
                        }
                        Paper.Add(item);
                    }
                }
                if (data != null && data.TryGetValue(KeyFiller, out string filler) && !string.IsNullOrEmpty(filler))
                {
                    foreach (string index in filler.Split(','))
                    {
                        int value = Int(index);
                        if (value >= 0 && value < SocialCatalogue.NewsFiller.Length)
                        {
                            FillerSeen.Add(value);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // A log this version cannot read is only yesterday's news: start an empty one.
                _count = 0;
                _next = 0;
                Paper.Clear();
                PaperDay = 0;
            }
            _ready = true;
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (!_ready)
            {
                return;
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            var text = new StringBuilder(1024);
            for (int i = 0; i < _count; i++)
            {
                // Oldest first, so that reading them back in order keeps the order.
                ref Entry e = ref Log[(_next - _count + i + Capacity * 2) % Capacity];
                if (text.Length > 0)
                {
                    text.Append('\n');
                }
                text.Append((int)e.Kind).Append('|').Append(e.Day).Append('|').Append(e.Hour).Append('|').Append(e.X.ToString("F1", inv)).Append('|')
                    .Append(e.Z.ToString("F1", inv)).Append('|').Append(Clean(e.Detail)).Append('|').Append(e.Amount).Append('|').Append(e.Printed ? '1' : '0');
            }
            data[KeyLog] = text.ToString();

            text.Length = 0;
            text.Append(PaperDay);
            for (int i = 0; i < Paper.Count; i++)
            {
                text.Append('\n').Append(Paper[i].Key);
                if (Paper[i].Data != null)
                {
                    foreach (KeyValuePair<string, string> pair in Paper[i].Data)
                    {
                        text.Append('|').Append(pair.Key).Append('|').Append(Clean(pair.Value));
                    }
                }
            }
            data[KeyPaper] = text.ToString();
            data[KeyFiller] = string.Join(",", FillerSeen);
        }

        // ------------------------------------------------------------------ writing the paper

        /// <summary>
        /// Writes the paper for a day from what the log holds and what the save says. Once a day
        /// (or on the test button): it scans the city's buildings for street names and builds a
        /// handful of strings.
        /// </summary>
        public static void Compose(int day, bool again)
        {
            if (!_ready)
            {
                return;
            }
            Paper.Clear();
            PaperDay = day;
            Paper.Add(Item("reallife_news_masthead", "day", day.ToString(), "weekday", TimeHelper.GetDayOfWeek(Mathf.Max(1, day)).GetLocalization()));

            // ---- what happened: the worst three, then a tally if there was more
            int accidents = 0, arrests = 0, crimes = 0, fresh = 0;
            for (int i = 0; i < _count; i++)
            {
                ref Entry e = ref Log[i];
                if (again && day - e.Day <= 1)
                {
                    e.Printed = false;   // the test button prints the day's news again
                }
                if (e.Printed || day - e.Day > 1)
                {
                    continue;
                }
                fresh++;
                if (e.Kind == NewsKind.Accident || e.Kind == NewsKind.PedestrianHit || e.Kind == NewsKind.HitAndRun || e.Kind == NewsKind.PlayerCrash) accidents++;
                else if (e.Kind == NewsKind.Arrest || e.Kind == NewsKind.PlayerArrested) arrests++;
                else if (e.Kind != NewsKind.Story) crimes++;
            }
            int printed = 0;
            NewsKind lastKind = (NewsKind)(-1);
            int sameKind = 0;
            // Four events at most for the city's pages: on a busy day the worst three and a count of the rest.
            int events = fresh > 4 ? 3 : 4;
            for (int pass = 0; pass < events; pass++)
            {
                int best = -1;
                int bestWeight = 0;
                for (int i = 0; i < _count; i++)
                {
                    ref Entry e = ref Log[i];
                    if (e.Printed || day - e.Day > 1 || (e.Kind == lastKind && sameKind >= 2))
                    {
                        continue;
                    }
                    int weight = Weight(e);
                    if (weight > bestWeight)
                    {
                        bestWeight = weight;
                        best = i;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                ref Entry pick = ref Log[best];
                pick.Printed = true;
                sameKind = pick.Kind == lastKind ? sameKind + 1 : 1;
                lastKind = pick.Kind;
                Paper.Add(Write(pick));
                printed++;
            }
            for (int i = 0; i < _count; i++)
            {
                if (day - Log[i].Day <= 1)
                {
                    Log[i].Printed = true;   // what did not make the page today is not tomorrow's news
                }
            }
            if (printed == 0)
            {
                Paper.Add(Item(SocialLines.Pick(SocialCatalogue.NewsQuiet)));
            }
            else if (fresh > 4)
            {
                Paper.Add(Item(SocialLines.Pick(SocialCatalogue.NewsTally), "accidents", accidents.ToString(), "arrests", arrests.ToString(), "crimes", crimes.ToString()));
            }

            // ---- the weather
            Paper.Add(Item(SocialLines.Pick(RainHelper.isRaining ? SocialCatalogue.NewsRainNow : _rained ? SocialCatalogue.NewsRained : SocialCatalogue.NewsDry)));
            _rained = false;

            // ---- business: the player's own shops, from the game's own books for yesterday
            Paper.Add(Business(day));
            if (Extra != null)
            {
                try
                {
                    Extra(day, Paper);
                }
                catch (Exception e)
                {
                    SocialDirector.Logger?.Error(e);
                }
            }

            // ---- somebody the player knows
            if (People(day, out NewsItem person))
            {
                Paper.Add(person);
            }

            // ---- colour: never the same piece twice until all have run
            Paper.Add(Filler());
            Paper.Add(Filler());
            if (Paper.Count < 9)
            {
                Paper.Add(Filler());
            }
        }

        private static int Weight(in Entry e)
        {
            switch (e.Kind)
            {
                case NewsKind.PedestrianHit: return e.Amount > 0 ? 100 : 65;
                case NewsKind.Accident: return e.Amount >= 2 ? 90 : e.Amount == 1 ? 60 : 30;
                case NewsKind.HitAndRun: return 80;
                case NewsKind.PlayerArrested: return 75;
                case NewsKind.Story: return 85;
                case NewsKind.BreakIn: return 70;
                case NewsKind.Arrest: return 55;
                case NewsKind.PlayerCrash: return 50;
                case NewsKind.PlayerKnocked: return 50;
                case NewsKind.Crime: return e.Detail == "mugging" || e.Detail == "assault" || e.Detail == "holdup" || e.Detail == "player_mugging" ? 52 : 40;
                case NewsKind.CarBreakIn: return 35;
                default: return 10;
            }
        }

        private static NewsItem Write(in Entry e)
        {
            if (e.Kind == NewsKind.Story)
            {
                return Item(e.Detail);
            }
            string street = SocialPlaces.StreetAt(new Vector3(e.X, 0f, e.Z));
            string time = e.Hour.GetFormattedTime(0f, true);
            switch (e.Kind)
            {
                case NewsKind.Accident:
                    return Item(SocialLines.Pick(e.Amount >= 2 ? SocialCatalogue.NewsAccidentSevere : e.Amount == 1 ? SocialCatalogue.NewsAccidentModerate
                        : SocialCatalogue.NewsAccidentMinor), "street", street, "time", time);
                case NewsKind.PedestrianHit:
                    return Item(SocialLines.Pick(e.Amount > 0 ? SocialCatalogue.NewsPedestrianKilled : SocialCatalogue.NewsPedestrianHurt), "street", street, "time", time);
                case NewsKind.HitAndRun:
                    return Item(SocialLines.Pick(SocialCatalogue.NewsHitAndRun), "street", street, "time", time);
                case NewsKind.Crime:
                    return Item(SocialLines.Pick(SocialCatalogue.NewsCrime), "street", street, "time", time, "what", SocialCatalogue.CrimeNoun(e.Detail).GetLocalization());
                case NewsKind.Arrest:
                    return Item(SocialLines.Pick(SocialCatalogue.NewsArrest), "street", street, "time", time, "what", SocialCatalogue.CrimeNoun(e.Detail).GetLocalization());
                case NewsKind.BreakIn:
                    return Item(SocialLines.Pick(SocialCatalogue.NewsBreakIn), "shop", e.Detail ?? street, "amount", ((float)e.Amount).ToCurrencyFormat());
                case NewsKind.CarBreakIn:
                    return Item(SocialLines.Pick(SocialCatalogue.NewsCarBreakIn), "street", street);
                case NewsKind.PlayerCrash:
                    return Item(SocialLines.Pick(SocialCatalogue.NewsPlayerCrash), "street", street, "time", time);
                case NewsKind.PlayerArrested:
                    return Item(SocialLines.Pick(SocialCatalogue.NewsPlayerArrest), "street", street, "time", time);
                default:
                    return Item(SocialLines.Pick(SocialCatalogue.NewsPlayerKnocked), "street", street, "time", time);
            }
        }

        private static NewsItem Business(int day)
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                FinancialSummary.BusinessIncomeStatement best = null;
                float worstProfit = 0f;
                FinancialSummary.BusinessIncomeStatement worst = null;
                for (int i = save.financialSummaries.Count - 1; i >= 0 && best == null && worst == null; i--)
                {
                    FinancialSummary summary = save.financialSummaries[i];
                    if (summary == null || summary.businessIncomeStatements == null || summary.dayNumber < day - 2)
                    {
                        continue;
                    }
                    foreach (FinancialSummary.BusinessIncomeStatement statement in summary.businessIncomeStatements)
                    {
                        if (statement == null || statement.Address == null)
                        {
                            continue;
                        }
                        if (statement.TotalSales > 0f && (best == null || statement.TotalSales > best.TotalSales))
                        {
                            best = statement;
                        }
                        if (statement.TotalProfit < worstProfit)
                        {
                            worstProfit = statement.TotalProfit;
                            worst = statement;
                        }
                    }
                }
                // A good day's takings make the page; a loss only when there were no takings to speak of.
                FinancialSummary.BusinessIncomeStatement about = best ?? worst;
                if (about != null)
                {
                    BuildingRegistration registration = BuildingHelper.GetBuildingRegistration(about.Address);
                    string shop = registration != null && !string.IsNullOrEmpty(registration.BusinessName) ? registration.BusinessName : about.Address.ToFormattedString();
                    string street = about.Address.GetStreetNameLocalized();
                    return best != null
                        ? Item(SocialLines.Pick(SocialCatalogue.NewsShopGood), "shop", shop, "street", street, "amount", best.TotalSales.ToCurrencyFormat())
                        : Item(SocialLines.Pick(SocialCatalogue.NewsShopLoss), "shop", shop, "street", street, "amount", (-worstProfit).ToCurrencyFormat());
                }
            }
            catch (Exception e)
            {
                SocialDirector.Logger?.Error(e);
            }
            return Item(SocialLines.Pick(SocialCatalogue.NewsShopNone));
        }

        private static bool People(int day, out NewsItem item)
        {
            item = default;
            int perYear = SocialPlaces.DaysPerYear;
            SocialPerson chosen = null;
            bool birthday = false;
            int from = Random.Range(0, SocialRules.CastSize);
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get((from + i) % SocialRules.CastSize);
                if (p == null || !p.Met || p.IsFamily)
                {
                    continue;
                }
                if (p.Birthday == (day - 1) % perYear + 1)
                {
                    chosen = p;
                    birthday = true;
                    break;
                }
                if (chosen == null)
                {
                    chosen = p;
                }
            }
            if (chosen == null)
            {
                return false;
            }
            string job = SocialCatalogue.Jobs[Mathf.Clamp(chosen.Job, 0, SocialCatalogue.Jobs.Length - 1)].GetLocalization();
            string district = SocialPlaces.District(chosen.District);
            string[] pool = birthday ? SocialCatalogue.NewsPersonBirthday
                : chosen.LastOutingDay >= day - 1 && chosen.LastOutingDay > 0 ? SocialCatalogue.NewsPersonSeen : SocialCatalogue.NewsPersonJob;
            item = Item(SocialLines.Pick(pool), "name", chosen.Name, "job", job, "district", district);
            return true;
        }

        private static NewsItem Filler()
        {
            string[] pool = SocialCatalogue.NewsFiller;
            if (FillerSeen.Count >= pool.Length)
            {
                FillerSeen.Clear();
            }
            int index = Random.Range(0, pool.Length);
            for (int tries = 0; tries < pool.Length && (FillerSeen.Contains(index) || index == _lastFiller); tries++)
            {
                index = (index + 1) % pool.Length;
            }
            FillerSeen.Add(index);
            _lastFiller = index;
            return Item(pool[index]);
        }

        private static NewsItem Item(string key, string a = null, string av = null, string b = null, string bv = null, string c = null, string cv = null)
        {
            var data = new Dictionary<string, string>(3);
            if (a != null) data[a] = av ?? string.Empty;
            if (b != null) data[b] = bv ?? string.Empty;
            if (c != null) data[c] = cv ?? string.Empty;
            return new NewsItem { Key = key, Data = data };
        }

        private static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ');
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        private static float Float(string text)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
        }
    }
}
