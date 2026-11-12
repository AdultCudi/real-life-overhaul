using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The save line of the acqui-hires' job leads: <c>reallife:tech_exit_jobs</c>, a header and the leads (l). Not read by an older RLO.</summary>
    public static class ExitJobStore
    {
        public const string Key = "reallife:tech_exit_jobs";
        public const int Version = 1;

        public static ExitJobFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        /// <summary>The line, or null while there is nothing to keep.</summary>
        public static string Write(ExitJobFile f)
        {
            if (f == null || f.Leads.Count == 0)
            {
                return null;
            }
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(0);
            w.Part('l');
            foreach (JobLead l in f.Leads)
            {
                w.Record().Add(l.Offer).Add(l.Day).Add(l.Kind).Add(l.Opening).Add(l.Buyer);
            }
            return w.ToString();
        }

        public static ExitJobFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new ExitJobFile();
            foreach (Fields x in r.Records('l'))
            {
                if (f.Leads.Count >= ExitJobFile.Most)
                {
                    break;
                }
                f.Leads.Add(new JobLead { Offer = x.Int(), Day = x.Int(), Kind = TalentCatalogue.Clamp(x.Int(), 0, JobKind.PutForward), Opening = Math.Max(0, x.Int()), Buyer = x.Text() });
            }
            return f;
        }
    }
}
