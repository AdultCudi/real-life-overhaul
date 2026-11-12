using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The startups the player founded, in the save: one line under <see cref="FoundCatalogue.Key"/> in <see cref="TechWriter"/>'s format, a header (h), the firms' records (f),
    /// their co-founders (c) and the niches they opened (e), and the people of the population already taken as co-founders (p). A save without the line loads with nothing in it.
    /// </summary>
    public static class FoundedStore
    {
        public const string Key = FoundCatalogue.Key;
        public const int Version = 1;

        public static FoundedFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(FoundedFile file)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(file.NextNo);
            w.Part('f');
            foreach (FoundedFirm f in file.Items)
            {
                w.Record().Place(f.Place).Add(f.Day).Add(f.Placed).Add(f.Engaged).Add(f.Sector).Add(f.Niche).Add(f.Articles).Add(f.Source).Add(f.Raised).Add(f.Fee).Add(f.Stage).Add(f.Rounds)
                    .Add(f.Learned).Add(f.Fit).Add(f.PmfDay).Add(f.Channel).Add(f.Partner).Add(f.Price).Add(f.Wom).Add(f.WomDay).Add(f.Users).Add(f.WonMonth).Add(f.LostMonth)
                    .Add(f.MentorUntil).Add(f.DemoDay).Add(f.DemoHeld);
            }
            w.Part('c');
            foreach (FoundedFirm f in file.Items)
            {
                foreach (CoFounder c in f.Cofounders)
                {
                    w.Record().Place(f.Place).Add(c.No).Add(c.Name).Add(c.Female).Add(c.Age).Add(c.Origin).Add(c.Person).Add(c.Role).Add(c.Level).Add(c.Profile).Add(c.Day).Add(c.Left)
                        .Add(c.Shares).Add(c.Kept);
                }
            }
            w.Part('e');
            foreach (FoundedFirm f in file.Items)
            {
                foreach (Expansion e in f.Expanded)
                {
                    w.Record().Place(f.Place).Add(e.Niche).Add(e.Day);
                }
            }
            w.Part('p');
            foreach (int person in file.Taken)
            {
                w.Record().Add(person);
            }
            return w.ToString();
        }

        public static FoundedFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var file = new FoundedFile();
            file.NextNo = Math.Max(1, r.One('h').Int(1));
            foreach (Fields x in r.Records('f'))
            {
                var f = new FoundedFirm
                {
                    Place = x.Place(), Day = x.Int(), Placed = x.Bool(), Engaged = x.Bool(), Sector = TalentCatalogue.Clamp(x.Int(), 0, (int)Venture.Count - 1),
                    Niche = Math.Max(-1, Math.Min(NicheCatalogue.Count - 1, x.Int(-1))), Articles = TalentCatalogue.Clamp(x.Int(), 0, FoundCatalogue.Articles - 1),
                    Source = TalentCatalogue.Clamp(x.Int(), 0, FoundCatalogue.Sources - 1), Raised = x.Long(), Fee = x.Long(),
                    Stage = TalentCatalogue.Clamp(x.Int(), 0, FoundCatalogue.Stages - 1), Rounds = x.Int(), Learned = TalentCatalogue.Clamp(x.Int(), 0, 1000),
                    Fit = TalentCatalogue.Clamp(x.Int(), 0, 1000), PmfDay = x.Int(), Channel = TalentCatalogue.Clamp(x.Int(), 0, FoundCatalogue.Channels - 1), Partner = x.Int(-1),
                    Price = x.Int(-1), Wom = x.Int(), WomDay = x.Int(), Users = x.Int(), WonMonth = x.Int(), LostMonth = x.Int(), MentorUntil = x.Int(), DemoDay = x.Int(), DemoHeld = x.Bool()
                };
                if (file.Items.Count < 64 && file.Of(f.Place) == null)
                {
                    file.Items.Add(f);
                }
            }
            foreach (Fields x in r.Records('c'))
            {
                FoundedFirm f = file.Of(x.Place());
                var c = new CoFounder
                {
                    No = x.Int(), Name = x.Text(), Female = x.Bool(), Age = x.Int(), Origin = x.Int(), Person = x.Int(), Role = TalentCatalogue.Clamp(x.Int(), 0, TalentCatalogue.RoleCount - 1),
                    Level = TalentCatalogue.Clamp(x.Int(), 0, TalentCatalogue.Levels - 1), Profile = x.Text(), Day = x.Int(), Left = x.Int(), Shares = x.Long(), Kept = x.Long()
                };
                if (f != null && f.Cofounders.Count < 4)
                {
                    f.Cofounders.Add(c);
                    file.NextNo = Math.Max(file.NextNo, c.No + 1);
                }
            }
            foreach (Fields x in r.Records('e'))
            {
                FoundedFirm f = file.Of(x.Place());
                int niche = x.Int(-1), day = x.Int();
                if (f != null && niche >= 0 && niche < NicheCatalogue.Count && f.Expanded.Count < NicheCatalogue.Active)
                {
                    f.Expanded.Add(new Expansion { Niche = niche, Day = day });
                }
            }
            foreach (Fields x in r.Records('p'))
            {
                int person = x.Int();
                if (person != 0 && file.Taken.Count < 256)
                {
                    file.Taken.Add(person);
                }
            }
            return file;
        }
    }
}
