using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Tech;

// The job an acqui-hire opens (flag --exit-job; Scripts/Tech/Venture/ExitJobRules.cs, ExitJobStore.cs, TechDirector.ExitJobs.cs, Careers/CareersDirector.ExitLead.cs): one lead for each sale, saved in a
// line of its own, the buyer's own office or a real vacancy of the city's, nothing forced, the founder of a city startup taken on in the population. Game-free; the sources are
// read for what only the game can do.
internal static partial class Program
{
    private static void ExitJobChecks()
    {
        ExitJobRuleChecks();
        ExitJobSaveChecks();
        ExitJobSourceChecks();
        ExitJobPeopleChecks();
        ExitJobTextChecks();
    }

    private static void ExitJobRuleChecks()
    {
        Check(!ExitJobRules.AtOffice(0, true) && ExitJobRules.AtOffice(1, true) && !ExitJobRules.AtOffice(1, false),
            "exit job: a startup of the city with a floor may have the post at its own office; a listed company never, and a startup with no floor neither");
        Check(ExitJobRules.Life == ProRules.OpeningDaysMost, "exit job: the lead lives as long as any lead does (" + ExitJobRules.Life + " days)");
        var f = new ExitJobFile();
        JobLead a = ExitJobRules.Begin(f, 4, 100, "Northgate Systems");
        Check(a != null && f.Leads.Count == 1 && a.Kind == JobKind.None && a.Buyer == "Northgate Systems", "exit job: a sale is entered before a lead is asked for");
        Check(ExitJobRules.Begin(f, 4, 100, "Northgate Systems") == null && f.Leads.Count == 1, "exit job: asking again for the same sale gives nothing (one lead for one acqui-hire)");
        Check(ExitJobRules.Begin(f, 5, 100, "Pocketloop") != null && ExitJobRules.Begin(f, 4, 101, "Pocketloop") != null && ExitJobRules.Begin(f, 6, 100, "") == null && ExitJobRules.Begin(null, 6, 100, "x") == null,
            "exit job: another sale has its own entry; no buyer, no entry");
        var g = new ExitJobFile();
        JobLead none = ExitJobRules.Begin(g, 1, 10, "Northgate Systems");
        JobLead next = ExitJobRules.Begin(g, 2, 11, "Pocketloop");
        Check(none.Kind == JobKind.None && !ExitJobRules.StillOpen(g, next, 11), "exit job: a sale that found no vacancy leaves nothing open for the next one");
        none.Kind = JobKind.AtBuyer;
        Check(ExitJobRules.StillOpen(g, next, 11) && ExitJobRules.StillOpen(g, next, 10 + ExitJobRules.Life) && !ExitJobRules.StillOpen(g, next, 10 + ExitJobRules.Life + 1),
            "exit job: a second acqui-hire while the first lead is open gives none; once it has lapsed the next may give one");
        Check(!ExitJobRules.StillOpen(g, none, 11), "exit job: a lead does not block itself");
        var big = new ExitJobFile();
        for (int i = 1; i <= ExitJobFile.Most + 10; i++)
        {
            ExitJobRules.Begin(big, i, i, "B");
        }
        Check(big.Leads.Count == ExitJobFile.Most && big.Of(1, 1) == null && big.Of(ExitJobFile.Most + 10, ExitJobFile.Most + 10) != null, "exit job: the ledger keeps the latest " + ExitJobFile.Most + " sales");
    }

    private static void ExitJobSaveChecks()
    {
        var f = new ExitJobFile();
        JobLead a = ExitJobRules.Begin(f, 4, 100, "Northgate Systems");
        a.Kind = JobKind.PutForward;
        a.Opening = 77;
        ExitJobRules.Begin(f, 5, 120, "Pocketloop; the ~ | one");
        string line = ExitJobStore.Write(f);
        Check(ExitJobStore.Key == "reallife:tech_exit_jobs" && line != null && line.StartsWith("v=1"), "exit job: the leads have a line of their own, reallife:tech_exit_jobs");
        var data = new Dictionary<string, string> { { ExitJobStore.Key, line } };
        ExitJobFile back = ExitJobStore.Load(data, out int version);
        Check(back != null && version == 1 && back.Leads.Count == 2 && back.Of(4, 100).Kind == JobKind.PutForward && back.Of(4, 100).Opening == 77 && back.Of(5, 120).Kind == JobKind.None,
            "exit job: a reload keeps each lead as it was");
        Check(ExitJobStore.Write(back) == line, "exit job: the line is the same after a round trip");
        Check(ExitJobRules.Begin(back, 4, 100, "Northgate Systems") == null && ExitJobRules.Begin(back, 5, 120, "Pocketloop") == null, "exit job: after a reload the same sales give no second lead, and none is lost");
        Check(ExitJobStore.Load(new Dictionary<string, string>(), out _) == null && ExitJobStore.Write(new ExitJobFile()) == null, "exit job: a save from before has no line, loads with nothing in it, and an empty ledger writes nothing");
        var junk = new Dictionary<string, string> { { ExitJobStore.Key, "garbage" } };
        Check(ExitJobStore.Load(junk, out _) == null, "exit job: a damaged line loads as nothing");
        var exits = new ExitFile { Seed = 5 };
        FirmExit e = exits.Exit("Main~1", true);
        e.SoldDay = 50;
        e.SoldFor = 1000000;
        e.SoldTo = "Northgate Systems";
        string old = ExitStore.Write(exits);
        var oldData = new Dictionary<string, string> { { ExitStore.Key, old } };
        ExitFile oldBack = ExitStore.Load(oldData, out _);
        Check(oldBack != null && oldBack.Exit("Main~1", false).SoldDay == 50 && ExitJobStore.Load(oldData, out _) == null,
            "exit job: a firm sold before this build is in the exits line and has no entry in the jobs line");
        string exitsSrc = ModSource(@"Tech\Venture\ExitStore.cs"), ventureSrc = ModSource(@"Tech\Venture\VentureStore.cs");
        Check(exitsSrc.Length > 0 && !exitsSrc.Contains("tech_exit_jobs") && !exitsSrc.Contains("JobLead") && !ventureSrc.Contains("tech_exit_jobs") && !ventureSrc.Contains("JobLead"),
            "exit job: reallife:tech_exits and reallife:tech_ventures are not touched");
        ExitBridges.Reset();
        Check(ExitBridges.Job == null && ExitBridges.FounderHired == null, "exit job: put back, no lead is asked for and no founder is moved");
    }

    private static void ExitJobSourceChecks()
    {
        string tech = ModSource(@"Tech\TechDirector.ExitJobs.cs"), exits = ModSource(@"Tech\TechDirector.Exits.cs"), venture = ModSource(@"Tech\TechDirector.Venture.cs");
        string leads = ModSource(@"Careers\CareersDirector.Leads.cs"), lead = ModSource(@"Careers\CareersDirector.ExitLead.cs"), vac = ModSource(@"Careers\CareersDirector.Vacancies.cs");
        int accept = exits.IndexOf("internal bool ExitAccept", StringComparison.Ordinal), call = exits.IndexOf("ExitJobFor(o, day)", StringComparison.Ordinal);
        int hand = exits.IndexOf("ExitBridges.HandOver(place, o.Name)", StringComparison.Ordinal), end = exits.IndexOf("internal void OpenExits", StringComparison.Ordinal);
        Check(accept > 0 && hand > accept && call > hand && call < end && Regex.Matches(exits + venture + tech, @"ExitJobFor\(").Count == 2
              && Regex.IsMatch(exits, @"if \(o\.Hire\)\s*\{\s*ExitJobFor\(o, day\);"),
            "exit job: the lead is asked for once, from an accepted acqui-hire after the business is handed over, never from a refused sale, a day or a load");
        Check(!tech.Contains("GoingPublic") && !tech.Contains("Network") && !ModSource(@"Tech\Venture\ExitJobRules.cs").Contains("GoingPublic"),
            "exit job: nothing goes over the network; the lead is in this player's own Careers");
        Check(tech.Length > 0 && !Regex.IsMatch(tech, @"HandOver|VentureGame|\.Caps|_firms|Give\(|Pay\(") && !lead.Contains("Job =") && !lead.Contains("Retired") && !Regex.IsMatch(leads, @"w\.Job\s*=[^=]"),
            "exit job: the offer touches no job, firm or money of the player's; it only adds an opening");
        Check(leads.Contains("w.Job == null || w.Job.Post.Prof != p.Id || r > w.Job.Post.Rank"), "exit job: somebody who has a job is offered only a post better than theirs, in a profession they have not reached");
        Check(leads.Contains("w.Retired > 0 || w.Openings.Count >= WorkFile.OpeningsMost") && leads.Contains("o.To = day + ProRules.OpeningDaysMost") && leads.Contains("o.Via = (int)Via.Referral"),
            "exit job: a lead stands for the days any lead stands, as a referral, and none is made for the retired or past the most openings");
        Check(vac.Contains("CareersBridges.Headquarters(firm, out _, out _)") && lead.Contains("office ? Lead(buyer, ExitDraws + salt, buyer,") && lead.Contains("return Lead(buyer, ExitDraws + salt, null,"),
            "exit job: a post at the buyer only where it has an office on a floor of a tower, else a real vacancy of the city's, never an invented one");
        Check(lead.Contains("reallife_exit_job_note_at") && lead.Contains("reallife_exit_job_note_forward") && tech.Contains("reallife_exit_job_none"),
            "exit job: the words say whether the post is the buyer's own or a vacancy its people put the name forward for, and say plainly when there is none");
        Check(exits.Contains("_exitJobs = null"), "exit job: stopped with the venture part, the ledger is let go");
        Check(Regex.IsMatch(tech, @"if \(ExitBridges\.Job == null\)\s*\{\s*return false;") && Regex.IsMatch(tech, @"_exits == null \|\| ExitBridges\.FounderHired == null"),
            "exit job: with Startups off, or the careers off, nothing is asked and nothing changes");
        Check(Regex.IsMatch(venture, @"if \(n\.Kind == VentureRules\.NoteAcquihire\)\s*\{\s*FounderTaken\(s, day\);") && ModSource(@"Wiring\Wiring.ExitJobLinks.cs").Contains("PopulationLinks.Employ"),
            "exit job: a city startup bought for its team moves its founder through the population, on the day it ends and nowhere else");
    }

    private static void ExitJobPeopleChecks()
    {
        PopulationState city = ExitCity();
        string key = ExitBridges.KeyOf(7);
        var asks = new List<LinkAsk> { new LinkAsk { Kind = LinkKind.Founder, Key = key, Age = 35, Female = false, Prefer = 5 } };
        PopulationLinks.People(city, asks, 2);
        int p = PopulationLinks.Linked(city, LinkKind.Founder, key);
        Check(p >= 0, "exit job: the founder is a resident of the city");
        int before = city.People.Job[p];
        Check(!PopulationLinks.Employ(city, LinkKind.Founder, ExitBridges.KeyOf(99), null, 5), "exit job: nobody linked, nobody moved");
        int tenant = -1;
        for (int e = 0; e < city.Employers.Count && tenant < 0; e++)
        {
            if (city.Employers.Kind[e] == (byte)RealLifeOverhaul.Population.EmployerKind.Tenants && city.Employers.Vacancies(e) > 0 && !city.Employers.Players[e] && e != before)
            {
                tenant = e;
            }
        }
        if (tenant >= 0)
        {
            string tk = city.Employers.Key[tenant];
            string office = tk.Substring(0, tk.Length - 2);
            int filled = city.Employers.Filled[tenant];
            bool moved = PopulationLinks.Employ(city, LinkKind.Founder, key, office, 5);
            Check(moved && city.People.Job[p] == tenant && city.Employers.Filled[tenant] == filled + 1 && city.People.Status[p] == (byte)Status.Worker,
                "exit job: the founder is taken on at the buyer's office building when it has a vacancy (" + tk + ")");
            Check(PopulationLinks.Employ(city, LinkKind.Founder, key, office, 6) && city.Employers.Filled[tenant] == filled + 1, "exit job: asked twice, they are not counted twice");
        }
        else
        {
            Check(false, "exit job: the check city has an office building with a vacancy");
        }
        PopulationState a = ExitCity(), b = ExitCity();
        PopulationLinks.People(a, asks, 2);
        PopulationLinks.People(b, asks, 2);
        bool ma = PopulationLinks.Employ(a, LinkKind.Founder, key, null, 5), mb = PopulationLinks.Employ(b, LinkKind.Founder, key, null, 5);
        int pa = PopulationLinks.Linked(a, LinkKind.Founder, key), pb = PopulationLinks.Linked(b, LinkKind.Founder, key);
        Check(ma && mb && a.People.Job[pa] == b.People.Job[pb] && a.People.Job[pa] >= 0 && !a.Employers.Players[a.People.Job[pa]], "exit job: with no office the founder goes to a vacancy of the city's, the same on every machine");
        PopulationState c = ExitCity();
        PopulationLinks.People(c, asks, 2);
        for (int e = 0; e < c.Employers.Count; e++)
        {
            c.Employers.Players[e] = true;
        }
        int pc = PopulationLinks.Linked(c, LinkKind.Founder, key), jobC = c.People.Job[pc];
        Check(!PopulationLinks.Employ(c, LinkKind.Founder, key, null, 5) && c.People.Job[pc] == jobC, "exit job: where only the player's businesses have a vacancy, the founder is left as they were");
        string pl = ModSource(@"Population\PopulationLinks.cs");
        int at = pl.IndexOf("public static bool Employ", StringComparison.Ordinal);
        string body = at > 0 ? pl.Substring(at, Math.Min(2200, pl.Length - at)) : string.Empty;
        Check(body.Contains("LabourRules.Hire") && !body.Contains("Model") && !body.Contains("Face") && !body.Contains("Hair") && !body.Contains("Body"),
            "exit job: only the population's record of the founder moves; no model, face, body or hair");
    }

    private static void ExitJobTextChecks()
    {
        string[] keys = { "reallife_exit_job_note_at", "reallife_exit_job_note_forward", "reallife_exit_job_none", "reallife_exit_test_lead", "reallife_exit_test_founder" };
        string[] langs = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };
        Dictionary<string, string> en = LocaleFile("en");
        foreach (string lang in langs)
        {
            Dictionary<string, string> d = LocaleFile(lang);
            bool all = true;
            foreach (string k in keys)
            {
                string t;
                all &= d.TryGetValue(k, out t) && !string.IsNullOrWhiteSpace(t);
                foreach (Match m in Regex.Matches(en[k], @"\{\w+\}"))
                {
                    all &= t != null && t.Contains(m.Value);
                }
            }
            Check(all, "exit job: " + lang + " has every key with its placeholders");
            string row = WhatsNewKey(en, "the buyer now puts your name forward for a real job");   // the row that holds it
            string next = WhatsNewText(d, row);
            Check(next.Length > 400 && (lang == "en" || next != WhatsNewText(en, row)), "exit job: " + lang + " has the What's new line");
        }
        foreach (string k in keys)
        {
            Check(en[k].Length <= 240 && !en[k].Contains("!") && !en[k].Contains("—") && !en[k].Contains("–") && !en[k].StartsWith("Test"), "exit job: " + k + " is a plain notice or label");
        }
        Check(WhatsNewHas(en, "the buyer now puts your name forward for a real job"), "exit job: the What's new line says it in en");
    }
}
