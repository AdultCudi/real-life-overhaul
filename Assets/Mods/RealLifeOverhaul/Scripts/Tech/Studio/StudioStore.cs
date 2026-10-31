using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The game studio in the save: one line under <see cref="Key"/> in <see cref="TechWriter"/>'s format, a header (h), the
    /// rival studios' next days (n), the games (g), the drafts (d), the professionals' genre experience (x), the studios'
    /// engines (e), the rival games (r) and the studios whose hit paid no bonus (s), each record's fields in the order
    /// <see cref="Write"/> writes them. New fields go at the end of a record; a record with fewer reads the rest as they
    /// start. A save without the line loads, and the older save's games are converted on the first day (StudioRules.Convert).
    /// The rival part is also what the shared city carries in a Going Public session (<see cref="StudioShare"/>). Game-free.
    /// </summary>
    public static class StudioStore
    {
        public const string Key = "reallife:tech_studio";
        public const int Version = 1;

        public static StudioFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(StudioFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.FirstDay).Add(f.LastDay).Add(f.Converted).Add(f.NextRival).Add(f.TrendGenre).Add(f.TrendUntil).Add(f.AwardYear);
            WriteRivalDays(w, f);
            w.Part('g');
            foreach (GameDev g in f.Games)
            {
                w.Record().Add(g.Id).Place(g.Place).Add(g.Genre).Add(g.Setting).Add(g.Platforms).Add(g.Model).Add(g.Engine).Add(g.Scope).Add(g.Budget).Add(g.Price).Add(g.Drm)
                    .Add(g.Published).Add(g.Sequel).Add(g.Ambition)
                    .Add(g.Phase).Add(g.Work).Add(g.Plan).Add(g.Done);
                for (int p = 0; p < (int)Part.Count; p++)
                {
                    w.Add(g.PartDone[p]);
                }
                for (int p = 0; p < (int)Part.Count; p++)
                {
                    w.Add(g.PartCraft[p]);
                }
                w.Add(g.Hidden).Add(g.Found).Add(g.BugCarry).Add(g.Fix).Add(g.Started).Add(g.Milestone).Add(g.MilestoneDay).Add(g.CrunchUntil).Add(g.BoostUntil).Add(g.CertUntil)
                    .Add(g.Spent).Add(g.Pledged)
                    .Add(g.Hype).Add(g.LastNews).Add(g.Beats).Add(g.LastTrailer).Add(g.LastExpo).Add(g.Campaign).Add(g.CampaignSpent).Add(g.ReleaseDay)
                    .Add(g.Launched).Add(g.Score).Add(g.Quality).Add(g.Reach).Add(g.Early)
                    .Add(g.Sold).Add(g.Pirated).Add(g.Gross).Add(g.Net).Add(g.Subscribers).Add(g.Active).Add(g.Carry).Add(g.WindowStart).Add(g.WindowUnits).Add(g.SaleUntil)
                    .Add(g.Lift).Add(g.NetMonth).Add(g.Converted).Add(g.Left)
                    .Add(g.Reported).Add(g.Asks).Add(g.Project).Add(g.ProjectArg).Add(g.ProjectDone).Add(g.ProjectWork).Add(g.Updates).Add(g.AddOns).Add(g.AddOnSold)
                    .Add(g.AddOnWindow).Add(g.AddOnStart).Add(g.Localised).Add(g.DrmRemoved).Add(g.Advance).Add(g.Royalties).Add(g.Fans).Add(g.Burnt).Add(g.Award)
                    .Add(g.RoyaltyGross)
                    .Add(g.Bonus).Add(g.Leads).Add(g.BonusDone).Add(g.LeadsPaid).Add(g.LastLeadsPay);
            }
            w.Part('d');
            foreach (GameDraft d in f.Drafts)
            {
                w.Record().Place(d.Place).Add(d.Genre).Add(d.Setting).Add(d.Platform).Add(d.Budget).Add(d.Price).Add(d.Model).Add(d.Engine).Add(d.Sequel).Add(d.Name);
            }
            w.Part('x');
            foreach (GenreExperience x in f.People)
            {
                w.Record().Add(x.Id);
                for (int g = 0; g < x.Days.Length; g++)
                {
                    w.Add(x.Days[g]);
                }
            }
            w.Part('e');
            foreach (StudioEngine e in f.Engines)
            {
                w.Record().Place(e.Place).Add(e.Done).Add(e.Ready).Add(e.Building);
            }
            WriteRivalGames(w, f);
            w.Part('s');
            foreach (KeyValuePair<string, int> s in f.SnubUntil)
            {
                w.Record().Place(s.Key).Add(s.Value);
            }
            return w.ToString();
        }

        /// <summary>The rival studios' next release days, one record: the part the shared city carries with the games.</summary>
        internal static void WriteRivalDays(TechWriter w, StudioFile f)
        {
            w.Part('n').Record();
            for (int i = 0; i < f.RivalNext.Length; i++)
            {
                w.Add(f.RivalNext[i]);
            }
        }

        internal static void WriteRivalGames(TechWriter w, StudioFile f)
        {
            w.Part('r');
            foreach (RivalGame r in f.Rivals)
            {
                w.Record().Add(r.Id).Add(r.Studio).Add(r.Name).Add(r.Genre).Add(r.Platforms).Add(r.Scope).Add(r.Score).Add(r.Launched).Add(r.Sold).Add(r.Units).Add(r.Award);
            }
        }

        internal static void ReadRivals(TechReader r, StudioFile f)
        {
            Fields n = r.One('n');
            for (int i = 0; i < f.RivalNext.Length; i++)
            {
                f.RivalNext[i] = n.Int();
            }
            f.Rivals.Clear();
            foreach (Fields x in r.Records('r'))
            {
                var g = new RivalGame
                {
                    Id = x.Int(), Studio = StudioRules.Clamp(x.Int(), 0, StudioCatalogue.RivalNames.Length - 1), Name = x.Text(), Genre = StudioRules.Clamp(x.Int(), 0, (int)Genre.Count - 1),
                    Platforms = x.Int(1), Scope = StudioRules.Clamp(x.Int(), 0, 2), Score = StudioRules.Clamp(x.Int(), 0, 100), Launched = x.Int(), Sold = Math.Max(0L, x.Long()),
                    Units = Math.Max(0L, x.Long()), Award = x.Bool()
                };
                if (f.Rivals.Count < StudioCatalogue.RivalGamesKept)
                {
                    f.Rivals.Add(g);
                }
            }
        }

        public static StudioFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new StudioFile();
            Fields h = r.One('h');
            f.Seed = h.Int(1);
            f.FirstDay = h.Int();
            f.LastDay = h.Int(-1);
            f.Converted = h.Bool();
            f.NextRival = Math.Max(1, h.Int(1));
            f.TrendGenre = h.Int(-1);
            f.TrendUntil = h.Int();
            f.AwardYear = h.Int(-1);
            ReadRivals(r, f);
            foreach (Fields x in r.Records('g'))
            {
                var g = new GameDev
                {
                    Id = x.Int(), Place = x.Place(), Genre = StudioRules.Clamp(x.Int(), 0, (int)Genre.Count - 1), Setting = StudioRules.Clamp(x.Int(), 0, 3), Platforms = x.Int(1),
                    Model = StudioRules.Clamp(x.Int(), 0, (int)GameModel.Count - 1), Engine = StudioRules.Clamp(x.Int(), 0, (int)GameEngine.Count - 1), Scope = StudioRules.Clamp(x.Int(), 0, 2),
                    Budget = x.Long(), Price = x.Long(), Drm = x.Bool(), Published = x.Bool(), Sequel = x.Int(), Ambition = StudioRules.Clamp(x.Int(1), 0, 2),
                    Phase = StudioRules.Clamp(x.Int(), 0, (int)GamePhase.Ended), Work = x.Long(100), Plan = x.Long(100), Done = x.Long()
                };
                for (int p = 0; p < (int)Part.Count; p++)
                {
                    g.PartDone[p] = x.Long();
                }
                for (int p = 0; p < (int)Part.Count; p++)
                {
                    g.PartCraft[p] = x.Long();
                }
                g.Hidden = x.Int();
                g.Found = x.Int();
                g.BugCarry = x.Long();
                g.Fix = x.Int(StudioCatalogue.FixShareBuild);
                g.Started = x.Int();
                g.Milestone = x.Int(-1);
                g.MilestoneDay = x.Int();
                g.CrunchUntil = x.Int();
                g.BoostUntil = x.Int();
                g.CertUntil = x.Int();
                g.Spent = x.Long();
                g.Pledged = x.Long();
                g.Hype = x.Int();
                g.LastNews = x.Int();
                g.Beats = x.Int();
                g.LastTrailer = x.Int(-1000);
                g.LastExpo = x.Int(-1000);
                g.Campaign = StudioRules.Clamp(x.Int(), 0, StudioCatalogue.CampaignShare.Length - 1);
                g.CampaignSpent = x.Long();
                g.ReleaseDay = x.Int();
                g.Launched = x.Int(-1);
                g.Score = x.Int(-1);
                g.Quality = x.Int();
                g.Reach = x.Int();
                g.Early = x.Bool();
                g.Sold = x.Long();
                g.Pirated = x.Long();
                g.Gross = x.Long();
                g.Net = x.Long();
                g.Subscribers = x.Long();
                g.Active = x.Long();
                g.Carry = x.Long();
                g.WindowStart = x.Int(-1);
                g.WindowUnits = x.Long();
                g.SaleUntil = x.Int();
                g.Lift = Math.Max(1000, x.Int(1000));
                g.NetMonth = x.Long();
                g.Converted = x.Bool();
                g.Left = x.Long();
                g.Reported = x.Int();
                g.Asks = x.Int();
                g.Project = StudioRules.Clamp(x.Int(), StudioRules.ProjectNone, StudioRules.ProjectLocalise);
                g.ProjectArg = x.Int();
                g.ProjectDone = x.Long();
                g.ProjectWork = x.Long();
                g.Updates = x.Int();
                g.AddOns = x.Int();
                g.AddOnSold = x.Long();
                g.AddOnWindow = x.Long();
                g.AddOnStart = x.Int(-1);
                g.Localised = x.Bool();
                g.DrmRemoved = x.Bool();
                g.Advance = x.Long();
                g.Royalties = x.Long();
                g.Fans = x.Long();
                g.Burnt = x.Bool();
                g.Award = x.Bool();
                g.RoyaltyGross = x.Long();
                g.Bonus = StudioRules.Clamp(x.Int(), 0, StudioCatalogue.BonusPool.Length - 1);
                g.Leads = StudioRules.Clamp(x.Int(), 0, StudioCatalogue.LeadRoyalty.Length - 1);
                g.BonusDone = x.Bool();
                g.LeadsPaid = x.Long();
                g.LastLeadsPay = x.Int();
                if (f.Games.Count < StudioCatalogue.GamesStored)
                {
                    f.Games.Add(g);
                }
            }
            foreach (Fields x in r.Records('d'))
            {
                var d = new GameDraft
                {
                    Place = x.Place(), Genre = StudioRules.Clamp(x.Int(), 0, (int)Genre.Count - 1), Setting = StudioRules.Clamp(x.Int(), 0, 3),
                    Platform = StudioRules.Clamp(x.Int(), 0, StudioCatalogue.GamePlatforms - 1), Budget = StudioRules.Clamp(x.Int(2), 0, StudioCatalogue.Budgets.Length - 1),
                    Price = StudioRules.Clamp(x.Int(2), 0, StudioCatalogue.PriceSteps.Length - 1), Model = StudioRules.Clamp(x.Int(), 0, (int)GameModel.Count - 1),
                    Engine = StudioRules.Clamp(x.Int(), 0, (int)GameEngine.Count - 1), Sequel = x.Int(), Name = x.Text()
                };
                if (f.Drafts.Count < 32)
                {
                    f.Drafts.Add(d);
                }
            }
            foreach (Fields x in r.Records('x'))
            {
                var e = new GenreExperience { Id = x.Text() };
                for (int g = 0; g < e.Days.Length; g++)
                {
                    e.Days[g] = StudioRules.Clamp(x.Int(), 0, StudioCatalogue.ExperienceMost);
                }
                if (e.Id.Length > 0 && f.People.Count < StudioCatalogue.PeopleStored)
                {
                    f.People.Add(e);
                }
            }
            foreach (Fields x in r.Records('e'))
            {
                var e = new StudioEngine { Place = x.Place(), Done = Math.Max(0L, x.Long()), Ready = x.Bool(), Building = x.Bool() };
                if (f.Engines.Count < 32)
                {
                    f.Engines.Add(e);
                }
            }
            foreach (Fields x in r.Records('s'))
            {
                string place = x.Place();
                int until = x.Int();
                if (place.Length > 0 && !f.SnubUntil.ContainsKey(place) && f.SnubUntil.Count < 32)
                {
                    f.SnubUntil.Add(place, until);
                }
            }
            return f;
        }
    }
}
