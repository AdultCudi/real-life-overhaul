using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;
using CompanyWorld = UiSamples.CompanyWorld;
using CPerson = UiSamples.CPerson;
using CPlace = UiSamples.CPlace;
using CSeat = UiSamples.CSeat;
using CPro = UiSamples.CPro;
using CCampaign = UiSamples.CCampaign;
using CReport = UiSamples.CReport;
using CLicence = UiSamples.CLicence;
using CClaim = UiSamples.CClaim;

// The Company samples are one world (UiSamples.Company.World.cs). Every state is built from it, and these checks read every state's pages against it: a person has one role, one place and one
// wage wherever they appear, a count equals the rows it counts, a badge equals the page it stands for, and every day a page says is on the world's one calendar and agrees with today.
internal static partial class Program
{
    /// <summary>One thing the pages hold: its text fields by name, and the lists of text beside them.</summary>
    private sealed class WorldRecord
    {
        public string Type;
        public bool Face;
        public Dictionary<string, string> Text = new Dictionary<string, string>();
        public Dictionary<string, string[]> Lists = new Dictionary<string, string[]>();
        public Dictionary<string, int> Numbers = new Dictionary<string, int>();
        public object Source;
    }

    // Where in a page the walk is: the heading of the section a list stands under, or the field it is held in. A row says what a number means by where it stands.
    private static string _worldContext = string.Empty;

    private static void WorldWalk(object o, List<WorldRecord> into, int depth)
    {
        if (o == null || depth > 12)
        {
            return;
        }
        Type t = o.GetType();
        if (t.IsArray)
        {
            string heading = string.Empty;
            foreach (object element in (Array)o)
            {
                if (element is CompanyBlock block)
                {
                    heading = block.Kind == CompanyBlockKind.Section ? block.Text ?? string.Empty : heading;
                    string before = _worldContext;
                    _worldContext = heading;
                    WorldWalk(element, into, depth + 1);
                    _worldContext = before;
                }
                else if (element != null && !(element is string) && !element.GetType().IsPrimitive)
                {
                    WorldWalk(element, into, depth + 1);
                }
            }
            return;
        }
        if (t.IsPrimitive || t.IsEnum || o is string || t.Namespace != "RealLifeOverhaul.Ui")
        {
            return;
        }
        var rec = new WorldRecord { Type = t.Name, Source = o };
        rec.Text["#context"] = _worldContext;
        foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            object v = f.GetValue(o);
            if (v is string s)
            {
                rec.Text[f.Name] = s;
            }
            else if (v is string[] a)
            {
                rec.Lists[f.Name] = a;
            }
            else if (v is bool b && f.Name == "Face")
            {
                rec.Face = b;
            }
            else if (v is int n)
            {
                rec.Numbers[f.Name] = n;
            }
            else if (v != null && !(v is Delegate))
            {
                string before = _worldContext;
                _worldContext = f.Name == "Crew" ? "crew" : f.Name == "Blocks" || f.Name == "More" ? string.Empty : _worldContext;
                WorldWalk(v, into, depth + 1);
                _worldContext = before;
            }
        }
        into.Add(rec);
    }

    private static readonly string[] WorldNameFields = { "FaceName", "Title", "Name", "Asker", "Subject", "LeadAsker", "Manager" };

    // The rows a heading's list runs to: its Items block, and where a column break follows with a heading of no count of its own ("And these too"), the Items block of that as well.
    private static int WorldListed(CompanyBlock[] blocks, int items)
    {
        int n = blocks[items].Items.Length;
        bool broke = false, took = false;
        for (int j = items + 1; j < blocks.Length; j++)
        {
            broke |= blocks[j].Kind == CompanyBlockKind.Break;
            if (blocks[j].Kind == CompanyBlockKind.Break || blocks[j].Kind == CompanyBlockKind.Switch)
            {
                continue;
            }
            if (blocks[j].Kind == CompanyBlockKind.Section && string.IsNullOrEmpty(blocks[j].Value) && j + 1 < blocks.Length && blocks[j + 1].Kind == CompanyBlockKind.Switch)
            {
                j++;
                continue;
            }
            if (broke && !took && blocks[j].Kind == CompanyBlockKind.Section && string.IsNullOrEmpty(blocks[j].Value) && j + 1 < blocks.Length && blocks[j + 1].Kind == CompanyBlockKind.Items)
            {
                n += blocks[j + 1].Items.Length;
                took = true;
                j++;
                continue;
            }
            break;
        }
        return n;
    }

    private static int MoneyOf(string text)
    {
        var digits = new StringBuilder();
        foreach (char c in text ?? string.Empty)
        {
            if (char.IsDigit(c))
            {
                digits.Append(c);
            }
        }
        return digits.Length == 0 ? 0 : int.Parse(digits.ToString(), CultureInfo.InvariantCulture);
    }

    // The person a text names: the whole text is their name, or their name and then a comma ("Omar Haddad, Hudson Warehouse").
    private static CPerson WorldPersonIn(CompanyWorld w, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        foreach (CPerson p in w.People)
        {
            if (text == p.Name || text.StartsWith(p.Name + ", ", StringComparison.Ordinal))
            {
                return p;
            }
        }
        return null;
    }

    private static bool WorldKnownName(CompanyWorld w, string text)
    {
        if (WorldPersonIn(w, text) != null || text == w.Owner)
        {
            return true;
        }
        foreach (CSeat s in w.Seats)
        {
            if (s.Name != null && text.StartsWith(s.Name, StringComparison.Ordinal))
            {
                return true;
            }
        }
        foreach (CPro p in w.Pros)
        {
            if (text.StartsWith(p.Name, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static readonly Regex WorldDay = new Regex(@"(?<![A-Za-z])[Dd]ays? (\d+)(?! of )(?: to (?:day )?(\d+))?", RegexOptions.Compiled);
    private static readonly Regex WorldMoney = new Regex(@"\$(\d+)(?:\.(\d+))?", RegexOptions.Compiled);

    private static void CompanyWorldChecks()
    {
        UiWords en = UiWords("en");
        CompanyWords words = UiSamples.CompanyWordsOf(en);
        var bad = new StringBuilder();
        int states = 0, records = 0, people = 0, days = 0, counts = 0;
        void Fail(string state, string what)
        {
            if (bad.Length < 3000)
            {
                bad.Append("\n      ").Append(state).Append(": ").Append(what);
            }
        }
        // The world's own invariants first: the calendar and the arithmetic it is built from.
        foreach (string kind in new[] { "overview_big", "overview_one", "managers_flat", "managers_none" })
        {
            UiSamples.CompanyFactsOf(en, kind);
            CompanyWorld w = UiSamples.CompanyWorldNow;
            string why = WorldInvariants(w);
            if (why != null)
            {
                Fail(kind, "the world: " + why);
            }
        }
        foreach (string state in UiSamples.CompanyStates)
        {
            if (state == "company_off" || state.EndsWith("_off", StringComparison.Ordinal))
            {
                continue;
            }
            CompanyFacts facts = UiSamples.CompanyFactsOf(en, state);
            CompanyWorld w = UiSamples.CompanyWorldNow;
            var recs = new List<WorldRecord>();
            WorldWalk(facts, recs, 0);
            states++;
            records += recs.Count;
            string tab = state.Split('_')[0];

            // Every day a page says is a day of the world's calendar.
            var seen = new HashSet<string>();
            foreach (WorldRecord r in recs)
            {
                foreach (string text in r.Text.Values.Concat(r.Lists.Values.SelectMany(x => x)))
                {
                    foreach (Match m in WorldDay.Matches(text ?? string.Empty))
                    {
                        for (int g = 1; g <= 2; g++)
                        {
                            if (m.Groups[g].Success)
                            {
                                days++;
                                int day = int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
                                if (!w.Calendar.Contains(day) && seen.Add(m.Value))
                                {
                                    Fail(state, "\"" + text + "\" says day " + day + ", which is not a day of the world (today is " + w.Today + ")");
                                }
                            }
                        }
                    }
                }
            }

            // Every person of a page is somebody of the company, with the role, the place, the wage and the standing the company gives them.
            foreach (WorldRecord r in recs)
            {
                string name = null;
                foreach (string field in WorldNameFields)
                {
                    if (r.Text.TryGetValue(field, out string value) && !string.IsNullOrEmpty(value) && (WorldPersonIn(w, value) != null || (r.Face && field != "Manager")))
                    {
                        name = value;
                        break;
                    }
                }
                if (name == null)
                {
                    continue;
                }
                if (r.Face && !WorldKnownName(w, name))
                {
                    Fail(state, "\"" + name + "\" has a face but is nobody of the company");
                    continue;
                }
                CPerson p = WorldPersonIn(w, name);
                if (p == null)
                {
                    continue;
                }
                people++;
                var wages = new HashSet<int> { p.Paid, p.Going, p.Under, p.Asked, p.OfferWage, p.Game, p.Raise, p.City, p.Asked - p.Paid, (p.Asked - p.Paid) / 2, p.OfferWage - p.Paid };
                foreach (KeyValuePair<string, string> kv in r.Text)
                {
                    if (kv.Key == "Id" || kv.Key == "#context" || kv.Value == null || WorldNameFields.Contains(kv.Key))
                    {
                        continue;
                    }
                    foreach (string piece in kv.Value.Split(new[] { ", " }, StringSplitOptions.None))
                    {
                        string skill = w.People.Select(x => x.Skill).Distinct().FirstOrDefault(x => UiSamples.CompanySkillText(en, x) == piece.Trim());
                        if (skill != null && skill != p.Skill && (r.Type == "CompanyItem" || r.Type == "CompanyRow" || r.Type == "CompanyCard" || r.Type == "UiOrgNode" || r.Type == "CompanyQuestion"))
                        {
                            Fail(state, p.Name + " is " + p.Skill + ", but " + r.Type + "." + kv.Key + " says " + piece);
                        }
                        CPlace place = w.Places.FirstOrDefault(x => x.Name == piece.Trim());
                        if (place != null && place.Key != p.Place && kv.Key != "Right")
                        {
                            Fail(state, p.Name + " works at " + w.Pl(p.Place).Name + ", but " + r.Type + "." + kv.Key + " says " + piece);
                        }
                    }
                    if (kv.Key == "Figure" || kv.Key == "Note")
                    {
                        foreach (Match m in WorldMoney.Matches(kv.Value))
                        {
                            string cents = m.Groups[2].Success ? m.Groups[2].Value : string.Empty;
                            if (cents.Length != 2)
                            {
                                continue;   // an hourly wage is written with its cents; a sum of money (a budget, a day's takings) is not one
                            }
                            int hundredths = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 100 + int.Parse(cents, CultureInfo.InvariantCulture);
                            if (!wages.Contains(hundredths))
                            {
                                Fail(state, p.Name + " is paid " + CompanyWorld.Wage(p.Paid) + " (going " + CompanyWorld.Wage(p.Going) + "), but " + r.Type + "." + kv.Key + " says " + kv.Value);
                            }
                        }
                        Match skillNote = Regex.Match(kv.Value, @"^skill (\d+)$");
                        if (skillNote.Success && int.Parse(skillNote.Groups[1].Value, CultureInfo.InvariantCulture) != p.MSkill)
                        {
                            Fail(state, p.Name + " has manager skill " + p.MSkill + ", but " + r.Type + " says " + kv.Value);
                        }
                    }
                }
                // Their standing: a row that grades them and says the number says the person's own.
                if (r.Face && r.Numbers.TryGetValue("Grade", out int grade) && grade >= 0 && r.Text.TryGetValue("Figure", out string figure) && int.TryParse(figure, out int shown)
                    && !(r.Text.TryGetValue("Note", out string note) && note != null && note.StartsWith("skill", StringComparison.Ordinal)))
                {
                    bool placeRow = r.Text.TryGetValue("Note", out string n2) && n2 == words.T("reallife_co_crew_standing", "crew standing");
                    string context = r.Text["#context"] ?? string.Empty;
                    bool skillRow = context == "crew" || context.EndsWith("'s crew", StringComparison.Ordinal);
                    bool standingRow = context == words.T("reallife_co_strike_low_sec", "Who stands low") || context == words.T("reallife_co_could_manage", "Who could take a post");
                    int want = placeRow ? (p.Post == 2 ? w.StandingIn(w.Region) : w.CrewStanding(p.Place)) : skillRow ? p.MSkill : p.Standing;
                    if (shown != want || grade != want)
                    {
                        Fail(state, p.Name + (placeRow ? "'s crew stands at " : skillRow ? " has skill " : " stands at ") + want + ", but " + r.Type + " says " + shown + " (graded " + grade + ", under \"" + context + "\")");
                    }
                    if (!standingRow && !skillRow && !placeRow)
                    {
                        Fail(state, "a number under \"" + context + "\" is not known to be a standing or a skill");
                    }
                }
            }

            // Counts equal the rows they count.
            foreach (WorldRecord r in recs)
            {
                if (r.Type == "CompanyList" && r.Source is CompanyList list && list.Count != null && int.TryParse(list.Count, out int count))
                {
                    var real = list.Rows.Where(x => !x.Caption && x.Id != null).ToList();
                    var kinds = real.GroupBy(x => x.Id.Contains(":") ? x.Id.Substring(0, x.Id.IndexOf(':')) : x.Id).Select(g => g.Count()).ToList();
                    counts++;
                    if (!kinds.Contains(count) && count != real.Count)
                    {
                        Fail(state, "the list \"" + list.Title + "\" counts " + count + ", and holds " + string.Join(" and ", kinds) + " of its kinds (" + real.Count + " rows)");
                    }
                }
                if (r.Type == "CompanyCard" && r.Source is CompanyCard card)
                {
                    CompanyBlock[] b = card.Blocks;
                    for (int i = 0; b != null && i + 1 < b.Length; i++)
                    {
                        if (b[i].Kind == CompanyBlockKind.Section && !string.IsNullOrEmpty(b[i].Value) && int.TryParse(b[i].Value, out int n) && b[i + 1].Kind == CompanyBlockKind.Items && b[i + 1].Items != null)
                        {
                            counts++;
                            if (b[i + 1].Items.Length != n && WorldListed(b, i + 1) != n)
                            {
                                Fail(state, "the section \"" + b[i].Text + "\" counts " + n + " and lists " + b[i + 1].Items.Length);
                            }
                        }
                    }
                }
                if (r.Type == "CompanyProfile" && r.Source is CompanyProfile prof && prof.More != null)
                {
                    for (int i = 0; i + 1 < prof.More.Length; i++)
                    {
                        if (prof.More[i].Kind == CompanyBlockKind.Section && !string.IsNullOrEmpty(prof.More[i].Value) && int.TryParse(prof.More[i].Value, out int n) && prof.More[i + 1].Kind == CompanyBlockKind.Items)
                        {
                            counts++;
                            if (prof.More[i + 1].Items.Length != n && WorldListed(prof.More, i + 1) != n)
                            {
                                Fail(state, "the section \"" + prof.More[i].Text + "\" counts " + n + " and lists " + prof.More[i + 1].Items.Length);
                            }
                        }
                    }
                }
            }

            // The sidebar's counts are the world's, from the same function the live source uses; and each stands for what its page holds.
            string[] want2 = w.Counts();
            for (int t = 0; t < CompanyView.Tabs; t++)
            {
                string expect = want2[t];   // (the live sidebar never counts the rota, and nor does the function)
                if (facts.Counts[t] != expect)
                {
                    Fail(state, "the sidebar counts " + (facts.Counts[t] ?? "nothing") + " at tab " + t + ", the world has " + (expect ?? "nothing"));
                }
                counts++;
            }
            if (tab == "staff" && facts.Staff.List.Count != facts.Counts[CompanyView.Staff])
            {
                Fail(state, "the staff badge " + facts.Counts[CompanyView.Staff] + " is not the list's " + facts.Staff.List.Count);
            }
            if (tab == "marketing" && facts.Counts[CompanyView.Marketing] != (facts.Marketing.List.Rows.Count(x => x.Chip == words.T("reallife_ibox_chip_running", "Running")) is int run && run > 0 ? run.ToString() : null))
            {
                Fail(state, "the marketing badge " + facts.Counts[CompanyView.Marketing] + " is not the campaigns running on the page");
            }
            if (tab == "cover" && facts.Counts[CompanyView.Cover] != (facts.Cover.List.Rows.Count(x => x.Id != null && x.Id.StartsWith("claim:", StringComparison.Ordinal)) is int open && open > 0 ? open.ToString() : null))
            {
                Fail(state, "the cover badge " + facts.Counts[CompanyView.Cover] + " is not the claims to put in on the page");
            }
            if (tab == "managers" && facts.Managers.Tree.Length > 0)
            {
                int managers = facts.Managers.TreeIds.Count(x => x.StartsWith("mgr:", StringComparison.Ordinal) || x.StartsWith("reg:", StringComparison.Ordinal));
                if ((facts.Counts[CompanyView.Managers] ?? "0") != managers.ToString())
                {
                    Fail(state, "the managers badge " + facts.Counts[CompanyView.Managers] + " is not the managers of the tree, " + managers);
                }
                for (int i = 0; i < facts.Managers.TreeIds.Length; i++)
                {
                    string id = facts.Managers.TreeIds[i];
                    UiOrgNode node = facts.Managers.Tree[i];
                    if (id.StartsWith("mgr:", StringComparison.Ordinal) && node.Right != null)
                    {
                        string staff = w.StaffAt(id.Substring(4)).Count.ToString();
                        if (!node.Right.StartsWith(staff + " ", StringComparison.Ordinal))
                        {
                            Fail(state, node.Name + " has " + node.Right + " in the tree, and the place has " + staff + " staff");
                        }
                    }
                    if (id == "exe:1" && node.Right != null && !node.Right.StartsWith(w.Managers().Count + " ", StringComparison.Ordinal))
                    {
                        Fail(state, "the operating chief has " + node.Right + " in the tree, and the company has " + w.Managers().Count + " managers");
                    }
                    if (id == "own" && !node.Sub.Contains(" " + w.Places.Count + " "))
                    {
                        Fail(state, "the owner has \"" + node.Sub + "\" in the tree, and the company has " + w.Places.Count + " businesses");
                    }
                }
            }
            if (tab == "tax" || tab == "licence")
            {
                int missing = facts.Tax.Licences == null ? 0 : facts.Tax.Licences.Where(x => x.StateTone == UiRole.Danger).Select(x => x.Place).Distinct().Count();
                int due = facts.Tax.Tools != null && facts.Tax.Tools.Length > 0 && facts.Tax.Tools[0].ChipTone == UiRole.Warn ? 1 : 0;
                string expect = missing + due > 0 ? (missing + due).ToString() : null;
                if (facts.Counts[CompanyView.Tax] != expect)
                {
                    Fail(state, "the tax badge " + (facts.Counts[CompanyView.Tax] ?? "nothing") + " is not the businesses missing a permit plus the return due, " + (expect ?? "nothing"));
                }
            }
            if (tab == "overview" && facts.Overview.Businesses.Length > 0)
            {
                CompanyOverviewFacts o = facts.Overview;
                if (o.Line != null && !o.Line.Contains(" " + w.People.Count + " staff") || o.Line != null && !o.Line.Contains(" " + w.Managers().Count + " managers"))
                {
                    Fail(state, "the head says \"" + o.Line + "\", the company has " + w.People.Count + " staff and " + w.Managers().Count + " managers");
                }
                if (o.Waiting.Length > 0 && o.WaitingCount != o.Waiting.Length.ToString())
                {
                    Fail(state, "the overview counts " + o.WaitingCount + " waiting and lists " + o.Waiting.Length);
                }
                if (state != "overview_one" && o.Managers.Length != w.Managers().Count)
                {
                    Fail(state, "managers today lists " + o.Managers.Length + ", the company has " + w.Managers().Count);
                }
                if (o.TotalTakings != null)
                {
                    int takings = o.Businesses.Sum(x => MoneyOf(x.Takings)), costs = o.Businesses.Sum(x => MoneyOf(x.Costs));
                    if (MoneyOf(o.TotalTakings) != takings || MoneyOf(o.TotalCosts) != costs || MoneyOf(o.CostTotal) != costs || o.CostValues.Sum(x => MoneyOf(x)) != costs)
                    {
                        Fail(state, "the day by business adds up to " + takings + " and " + costs + ", the totals say " + o.TotalTakings + " and " + o.TotalCosts + " (costs " + o.CostTotal + ")");
                    }
                    if (!o.CostLabels[0].Contains(" " + w.People.Count + " "))
                    {
                        Fail(state, "the wages line says \"" + o.CostLabels[0] + "\" and the company has " + w.People.Count + " staff");
                    }
                }
            }
        }
        Check(bad.Length == 0 && states > 40, "company world: " + states + " states read against the one sample world (" + records + " records, " + people + " people, " + days + " days, " + counts + " counts): a person has one role, place, wage and standing, a count equals its rows, a badge its page, a day is on one calendar" + (bad.Length > 0 ? ", not so in:" + bad : string.Empty));
    }

    // The world on its own: its numbers agree with each other, and with the rules the game's mod uses for the same arithmetic.
    private static string WorldInvariants(CompanyWorld w)
    {
        var names = new HashSet<string>();
        foreach (CPerson p in w.People)
        {
            if (!names.Add(p.Name))
            {
                return p.Name + " is twice in the company";
            }
            if (p.Raise != Enumerable.Sum(p.RaiseSizes) && p.RaiseDays.Length > 0)
            {
                return p.Name + "'s raises add up to " + Enumerable.Sum(p.RaiseSizes) + ", their agreed raise is " + p.Raise;
            }
            if (p.RaiseDays.Any(d => d > w.Today))
            {
                return p.Name + " was given a raise on a day to come";
            }
            if (p.Asked > 0 && (p.AnswerBy <= w.Today || p.Asked < p.Paid))
            {
                return p.Name + " asks for a raise that is not one, or by a day gone";
            }
            if (p.Rival != null && (p.OfferBy <= w.Today || p.OfferWage <= p.Paid))
            {
                return p.Name + " has a rival's offer that is not more, or by a day gone";
            }
            if (p.Hired > w.Today || (p.Post != 0 && p.Since > w.Today))
            {
                return p.Name + " joined or took a post on a day to come";
            }
        }
        foreach (CPlace pl in w.Places)
        {
            if (pl.ManagerId != null && (w.P(pl.ManagerId).Place != pl.Key || w.P(pl.ManagerId).Post != 1))
            {
                return pl.Name + "'s manager does not work there as a manager";
            }
            if (pl.GapSince > w.Today)
            {
                return pl.Name + " fell short on a day to come";
            }
            if (pl.LeaseDay <= w.Today)
            {
                return pl.Name + "'s lease renews on a day gone";
            }
            if (pl.GapSince > 0 && w.StaffAt(pl.Key).Count >= pl.Keep)
            {
                return pl.Name + " is short of crew but has " + w.StaffAt(pl.Key).Count + " of the " + pl.Keep + " it keeps";
            }
        }
        foreach (CCampaign c in w.Campaigns)
        {
            CompanyDays.Campaign d = w.Days(c);
            if (c.From > w.Today || c.End <= w.Today || d.Done + d.Left != d.Length || d.Report != c.End)
            {
                return "the campaign at " + c.Place + " is not running today, or its days do not add up";
            }
        }
        foreach (CReport r in w.Reports)
        {
            if (r.From + r.Days - 1 >= w.Today)
            {
                return "the report of " + r.Kind + " at " + r.Place + " is for days that are not over";
            }
        }
        foreach (CClaim c in w.Claims)
        {
            if (c.Day > w.Today || c.By < c.Day || (c.Open && c.By < w.Today))
            {
                return "the " + c.Cover + " claim at " + c.Place + " has days that do not agree with today";
            }
        }
        foreach (CLicence l in w.Licences)
        {
            if (!l.Missing && l.Until < w.Today)
            {
                return "the " + l.Kind + " permit at " + l.Place + " ran out on a day gone";
            }
            if (l.State == "soon" && l.Until - w.Today >= CompanyCatalogue.LicenceRemindDays)
            {
                return "the " + l.Kind + " permit at " + l.Place + " is called soon but runs on";
            }
        }
        CompanyDays.Quarter q = w.Quarter;
        if (w.Today < w.Tax.From || w.Today > q.Last || q.Close != w.Tax.From + w.Tax.Days)
        {
            return "today is not in the quarter that is running";
        }
        if (w.Tax.ReturnWaiting && (w.Tax.ReturnDue != w.Tax.From + CompanyWorld.ReturnDays || w.Tax.ReturnQuarter != w.Tax.Number - 1))
        {
            return "the return that waits is not the closed quarter's, due " + CompanyWorld.ReturnDays + " days after it closed";
        }
        foreach (int[] f in w.Tax.Filed)
        {
            int due = w.Tax.From - w.Tax.Days * (w.Tax.Number - f[0]) + w.Tax.Days + CompanyWorld.ReturnDays;
            if ((f[1] > due) != (f[3] == 1) || f[1] > w.Today)
            {
                return "quarter " + f[0] + " was filed on day " + f[1] + ", due " + due + ", and is called " + (f[3] == 1 ? "late" : "on time");
            }
        }
        for (int k = 0; k < CompanyCatalogue.LicenceKinds; k++)
        {
            if (CompanyWorld.LicenceFee[k] != CompanyCatalogue.LicenceFee[k] || CompanyWorld.LicenceTerm[k] != CompanyCatalogue.LicenceTerm[k])
            {
                return "permit " + k + " costs or runs for other than the catalogue says";
            }
        }
        if (CompanyWorld.LicenceFineMultiple != CompanyCatalogue.LicenceFineMultiple)
        {
            return "the fine for trading without a permit is not the catalogue's";
        }
        foreach (int skill in new[] { 0, 52, 78, 100 })
        {
            if (CompanyWorld.Allowance(skill) != ManagerRules.Allowance(ManagerCatalogue.RolePlace, skill, 1000) || CompanyWorld.Allowance(skill, true) != ManagerRules.Allowance(ManagerCatalogue.RoleRegion, skill, 1000))
            {
                return "a manager of skill " + skill + " is allowed other than the rules give";
            }
        }
        // The arithmetic is the mod's own.
        if (CompanyWorld.ReturnDays != CompanyCatalogue.ReturnDays || CompanyWorld.QuarterDays != TaxRules.QuarterDays(30))
        {
            return "the return window or the quarter's length is not the catalogue's";
        }
        foreach (int takings in new[] { 0, 10000, 920700, 5000000 })
        {
            if (CompanyWorld.Penalty(takings) != TaxRules.Penalty(takings))
            {
                return "the penalty on " + takings + " is " + CompanyWorld.Penalty(takings) + ", the rules say " + TaxRules.Penalty(takings);
            }
            if (CompanyWorld.QuarterTax(takings, takings / 3, 20) != TaxRules.QuarterTax(takings, takings / 3, 20) || CompanyWorld.Taxable(takings, takings / 3) != TaxRules.Taxable(takings, takings / 3))
            {
                return "the tax on " + takings + " is not the rules'";
            }
        }
        var file = new TaxFile { YearTax = w.YearTax(), Reserve = w.Tax.Reserve };
        file.Costs[0] = w.QuarterCosts();
        TaxRules.QuarterFigures figures = TaxRules.Quarter(file, w.QuarterTakings(), w.Tax.Percent);
        if (figures.Still != w.Still() || figures.Tax != CompanyWorld.QuarterTax(w.QuarterTakings(), w.QuarterCosts(), w.Tax.Percent))
        {
            return "the quarter's figures are not TaxRules.Quarter's: still " + w.Still() + " against " + figures.Still;
        }
        // The sidebar's counts are the function the live source calls.
        string[] c1 = w.Counts(), c2 = CompanyDays.Counts(w.People.Count, w.Managers().Count, w.ShopsMissingLicence(), w.Tax.ReturnWaiting, w.OpenClaims(), w.Running());
        for (int i = 0; i < c1.Length; i++)
        {
            if (c1[i] != c2[i])
            {
                return "the sidebar's counts differ from CompanyDays.Counts";
            }
        }
        return null;
    }
}
