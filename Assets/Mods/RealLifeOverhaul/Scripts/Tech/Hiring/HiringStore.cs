using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The labour market and the player's hiring in the save: one line of the save's modData under <see cref="Key"/>,
    /// written by <see cref="TechWriter"/>. Game-free.
    ///
    ///     v=1~h:seed|first day|last day|next id~s:...~a:...~r:...~p:...~o:...~c:...
    ///
    ///   s  the seekers: id|role|level|main|A|B|years|school|certs|record|female|age|name|district|in work|employer|ask|wants|until|fit|claim|refs|notice|taken|passive
    ///   a  the player's ads and search briefs: id|place|role|level|pay least|pay most|skill asked|arrangement|from|to|boost|came|search|fee|fee paid|due
    ///   r  the rivals' ads: rival|family|level|until|pay over the going rate
    ///   p  the applicants: id|seeker|ad|day|stage|references|background|interviewed|interest|offer|offer day|counter|rival bid|rival|start|revealed
    ///   o  the recruitment office's campaigns seen: key|place|agency|skill|from|to|planned|delivered
    ///   c  files for the game's candidates: id|day|role|level|A|B|years|school|certs|record
    ///
    /// Every list is capped by the catalogue, so the line stays at a few tens of kilobytes at its fullest.
    /// </summary>
    public static class HiringStore
    {
        public const string Key = "reallife:tech_hiring";
        public const int Version = 1;

        public static HiringFile Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                return Read(text, out version);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string Write(HiringFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.FirstDay).Add(f.LastDay).Add(f.NextId);
            w.Part('s');
            foreach (Seeker s in f.Seekers)
            {
                Profile p = s.P;
                w.Record().Add(s.Id).Add(p.Role).Add(p.Level).Add(p.Main).Add(p.A).Add(p.B).Add(p.YearsDays).Add(p.School).Add(p.Certs).Add(p.Record).Add(p.Female).Add(p.Age)
                    .Add(p.Name).Add(s.District).Add(s.Employed).Add(s.Employer).Add(s.Ask).Add(s.Want).Add(s.Until).Add(s.Fit).Add(s.Claim).Add(s.Refs).Add(s.Notice).Add(s.Taken).Add(s.Passive);
            }
            w.Part('a');
            foreach (JobAd a in f.Ads)
            {
                w.Record().Add(a.Id).Place(a.Place).Add(a.Role).Add(a.Level).Add(a.PayLeast).Add(a.PayMost).Add(a.MinSkill).Add(a.Work).Add(a.From).Add(a.To).Add(a.Boost).Add(a.Came)
                    .Add(a.Search).Add(a.Fee).Add(a.FeePaid).Add(a.Due);
            }
            w.Part('r');
            foreach (RivalAd r in f.RivalAds)
            {
                w.Record().Add(r.Rival).Add(r.Family).Add(r.Level).Add(r.To).Add(r.PayBp);
            }
            w.Part('p');
            foreach (Applicant a in f.Applicants)
            {
                w.Record().Add(a.Id).Add(a.Seeker).Add(a.Ad).Add(a.Day).Add(a.Stage).Add(a.Refs).Add(a.Background).Add(a.Interviewed).Add(a.Interest).Add(a.Offer).Add(a.OfferDay)
                    .Add(a.Counter).Add(a.RivalBid).Add(a.RivalBy).Add(a.Start).Add(a.Revealed);
            }
            w.Part('o');
            foreach (OfficeCampaign c in f.Campaigns)
            {
                w.Record().Place(c.Key).Place(c.Place).Place(c.Agency).Add(c.Skill).Add(c.From).Add(c.To).Add(c.Planned).Add(c.Delivered);
            }
            w.Part('c');
            foreach (CandidateFile c in f.Candidates)
            {
                Profile p = c.P;
                w.Record().Add(c.Id).Add(c.Day).Add(p.Role).Add(p.Level).Add(p.A).Add(p.B).Add(p.YearsDays).Add(p.School).Add(p.Certs).Add(p.Record);
            }
            return w.ToString();
        }

        public static HiringFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new HiringFile();
            Fields h = r.One('h');
            f.Seed = h.Int(1);
            f.FirstDay = h.Int();
            f.LastDay = h.Int(-1);
            f.NextId = Math.Max(1, h.Int(1));
            foreach (Fields x in r.Records('s'))
            {
                var s = new Seeker { Id = x.Int() };
                Profile p = s.P;
                p.Role = TalentCatalogue.Clamp(x.Int(), 0, TalentCatalogue.RoleCount - 1);
                p.Level = TalentCatalogue.Clamp(x.Int(), 0, TalentCatalogue.Levels - 1);
                p.Main = x.Int();
                p.A = x.Int();
                p.B = x.Int();
                p.YearsDays = x.Int();
                p.School = x.Int();
                p.Certs = x.Int();
                p.Record = x.Bool();
                p.Female = x.Bool();
                p.Age = x.Int(30);
                p.Name = x.Text();
                p.Id = "s" + s.Id;
                s.District = x.Text();
                s.Employed = x.Bool();
                s.Employer = x.Text();
                s.Ask = x.Int(TalentCatalogue.RateLeast);
                s.Want = x.Int();
                s.Until = x.Int();
                s.Fit = x.Int(500);
                s.Claim = x.Int();
                s.Refs = x.Int(3);
                s.Notice = x.Int();
                s.Taken = x.Bool();
                s.Passive = x.Bool();
                if (f.Seekers.Count < HiringCatalogue.SeekersMost + HiringCatalogue.SearchCandidates * HiringCatalogue.SearchesMost)
                {
                    f.Seekers.Add(s);
                }
            }
            foreach (Fields x in r.Records('a'))
            {
                var a = new JobAd
                {
                    Id = x.Int(), Place = x.Place(), Role = TalentCatalogue.Clamp(x.Int(), 0, TalentCatalogue.RoleCount - 1), Level = TalentCatalogue.Clamp(x.Int(), 0, TalentCatalogue.Levels - 1),
                    PayLeast = x.Int(), PayMost = x.Int(), MinSkill = x.Int(), Work = x.Int(), From = x.Int(), To = x.Int(), Boost = x.Bool(), Came = x.Int(), Search = x.Bool(), Fee = x.Int(),
                    FeePaid = x.Int(), Due = x.Int()
                };
                if (f.Ads.Count < HiringCatalogue.AdsMost + HiringCatalogue.SearchesMost)
                {
                    f.Ads.Add(a);
                }
            }
            foreach (Fields x in r.Records('r'))
            {
                if (f.RivalAds.Count < HiringCatalogue.RivalAdsMost)
                {
                    f.RivalAds.Add(new RivalAd { Rival = x.Text(), Family = x.Int(), Level = x.Int(), To = x.Int(), PayBp = x.Int() });
                }
            }
            foreach (Fields x in r.Records('p'))
            {
                var a = new Applicant
                {
                    Id = x.Int(), Seeker = x.Int(), Ad = x.Int(), Day = x.Int(), Stage = x.Int(), Refs = x.Bool(), Background = x.Bool(), Interviewed = x.Bool(), Interest = x.Int(500),
                    Offer = x.Int(), OfferDay = x.Int(), Counter = x.Int(), RivalBid = x.Int(), RivalBy = x.Text(), Start = x.Int(), Revealed = x.Int()
                };
                if (f.Applicants.Count < HiringCatalogue.ApplicantsMost + HiringCatalogue.SearchCandidates * HiringCatalogue.SearchesMost)
                {
                    f.Applicants.Add(a);
                }
            }
            foreach (Fields x in r.Records('o'))
            {
                if (f.Campaigns.Count < 16)
                {
                    f.Campaigns.Add(new OfficeCampaign { Key = x.Place(), Place = x.Place(), Agency = x.Place(), Skill = x.Text(), From = x.Int(), To = x.Int(), Planned = x.Int(), Delivered = x.Int() });
                }
            }
            foreach (Fields x in r.Records('c'))
            {
                var c = new CandidateFile { Id = x.Text(), Day = x.Int() };
                c.P.Id = c.Id;
                c.P.Role = TalentCatalogue.Clamp(x.Int(), 0, TalentCatalogue.RoleCount - 1);
                c.P.Level = TalentCatalogue.Clamp(x.Int(), 0, TalentCatalogue.Levels - 1);
                c.P.A = x.Int();
                c.P.B = x.Int();
                c.P.YearsDays = x.Int();
                c.P.School = x.Int();
                c.P.Certs = x.Int();
                c.P.Record = x.Bool();
                if (f.Candidates.Count < 64)
                {
                    f.Candidates.Add(c);
                }
            }
            return f;
        }
    }
}
