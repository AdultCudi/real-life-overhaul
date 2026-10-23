using System;
using System.Text;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The city around the company in the save's line, after the parts of before: "^z:" the council's zoning of each class
    /// of lot, "^g:" the rings on tenders, "^b:" the bills owed to the company and by it, "^y:" the building of the year.
    /// A line without them reads as a company the city has never ruled on; an older reader passes them over.
    /// </summary>
    public static partial class BuildStore
    {
        private static void WriteCity(StringBuilder t, BuildState s)
        {
            t.Append("^z:");
            Join(t, s.ClassAdded);
            t.Append("^g:");
            for (int i = Math.Max(0, s.Rings.Count - BuildState.RingsKept), n = 0; i < s.Rings.Count; i++, n++)
            {
                RingFile g = s.Rings[i];
                t.Append(n > 0 ? ";" : string.Empty);
                Join(t, g.Tender, g.Leader, g.Members, g.Asked ? 1 : 0, g.Answer, g.Awarded, g.LeaderWon ? 1 : 0, g.Found, g.Case);
                t.Append('|').Append(g.Cut.ToString(Inv));
            }
            t.Append("^b:");
            Join(t, s.NextBill);
            for (int i = Math.Max(0, s.Bills.Count - BuildState.BillsKept * 2); i < s.Bills.Count; i++)
            {
                BillFile b = s.Bills[i];
                t.Append(';');
                Join(t, b.Id, (int)b.Kind, b.Project, b.Who, b.Crew, b.Raised, b.Due, b.PaysOn, b.Chased, b.ChasedDay, b.Case, b.Closed, b.Outcome, b.City ? 1 : 0);
                t.Append('|').Append(b.Amount.ToString(Inv));
            }
            AwardFile a = s.Award;
            t.Append("^y:");
            Join(t, a.Year, a.Voting ? 1 : 0, a.LastYear, a.LastVotes);
            WriteEntry(t, a.Last);
            for (int i = 0; i < a.Entries.Count && i < AwardRules.EntriesMost; i++)
            {
                t.Append(';');
                Join(t, 1);
                WriteEntry(t, a.Entries[i]);
            }
        }

        private static void WriteEntry(StringBuilder t, AwardEntry e)
        {
            t.Append('|');
            Join(t, e.Kind, e.Project, e.Number, (int)e.Use, e.Builder, e.Day, e.Score, e.Votes, e.Short ? 1 : 0);
            t.Append('|').Append(Esc(e.Lot)).Append('|').Append(Esc(e.Street));
        }

        private static void ReadCity(char part, string body, BuildState s)
        {
            switch (part)
            {
                case 'z':
                {
                    var r = new Fields(body);
                    for (int c = 0; c < s.ClassAdded.Length; c++)
                    {
                        s.ClassAdded[c] = r.I(0) & ((1 << (int)BuildUse.Count) - 1);
                    }
                    break;
                }
                case 'g':
                    foreach (string rec in Records(body))
                    {
                        if (s.Rings.Count >= BuildState.RingsKept)
                        {
                            break;
                        }
                        var r = new Fields(rec);
                        var g = new RingFile
                        {
                            Tender = r.I(0), Leader = BuildCatalogue.Clamp(r.I(0), 0, BuildCatalogue.RivalCount - 1), Members = r.I(0) & ((1 << BuildCatalogue.RivalCount) - 1),
                            Asked = r.I(0) == 1, Answer = BuildCatalogue.Clamp(r.I(0), RingFile.NoAnswer, RingFile.Reported), Awarded = Math.Max(0, r.I(0)), LeaderWon = r.I(0) == 1,
                            Found = Math.Max(0, r.I(0)), Case = Math.Max(0, r.I(0)), Cut = Math.Max(0L, r.L(0L))
                        };
                        if (g.Tender > 0)
                        {
                            s.Rings.Add(g);
                        }
                    }
                    break;
                case 'b':
                {
                    bool first = true;
                    foreach (string rec in Records(body))
                    {
                        var r = new Fields(rec);
                        if (first)
                        {
                            first = false;
                            s.NextBill = Math.Max(1, r.I(1));
                            continue;
                        }
                        if (s.Bills.Count >= BuildState.BillsKept * 2)
                        {
                            break;
                        }
                        var b = new BillFile
                        {
                            Id = r.I(0), Kind = (BillKind)BuildCatalogue.Clamp(r.I(0), 0, (int)BillKind.Merchant), Project = Math.Max(0, r.I(0)), Who = Math.Max(0, r.I(0)),
                            Crew = Math.Max(0, r.I(0)), Raised = r.I(0), Due = r.I(0), PaysOn = Math.Max(BillRules.Never, r.I(0)), Chased = BuildCatalogue.Clamp(r.I(0), 0, BillRules.ChaseMost),
                            ChasedDay = r.I(0), Case = Math.Max(0, r.I(0)), Closed = Math.Max(0, r.I(0)), Outcome = BuildCatalogue.Clamp(r.I(0), BillFile.Open, BillFile.Sued),
                            City = r.I(0) == 1, Amount = Math.Max(0L, r.L(0L))
                        };
                        if (b.Id > 0 && b.Amount > 0)
                        {
                            s.Bills.Add(b);
                            s.NextBill = Math.Max(s.NextBill, b.Id + 1);
                        }
                    }
                    break;
                }
                case 'y':
                {
                    AwardFile a = s.Award;
                    bool first = true;
                    foreach (string rec in Records(body))
                    {
                        var r = new Fields(rec);
                        if (first)
                        {
                            first = false;
                            a.Year = Math.Max(-1, r.I(-1));
                            a.Voting = r.I(0) == 1;
                            a.LastYear = Math.Max(-1, r.I(-1));
                            a.LastVotes = Math.Max(0, r.I(0));
                            ReadEntry(r, a.Last);
                            continue;
                        }
                        if (a.Entries.Count >= AwardRules.EntriesMost || r.I(0) != 1)
                        {
                            continue;
                        }
                        var e = new AwardEntry();
                        ReadEntry(r, e);
                        a.Entries.Add(e);
                    }
                    break;
                }
            }
        }

        private static void ReadEntry(Fields r, AwardEntry e)
        {
            e.Kind = BuildCatalogue.Clamp(r.I(0), AwardEntry.OfPlayer, AwardEntry.OfCity);
            e.Project = Math.Max(0, r.I(0));
            e.Number = r.I(0);
            e.Use = (BuildUse)BuildCatalogue.Clamp(r.I(0), 0, (int)BuildUse.Count - 1);
            e.Builder = BuildCatalogue.Clamp(r.I(-1), -1, BuildCatalogue.RivalCount - 1);
            e.Day = Math.Max(0, r.I(0));
            e.Score = BuildCatalogue.Clamp(r.I(0), 0, 100);
            e.Votes = Math.Max(0, r.I(0));
            e.Short = r.I(0) == 1;
            e.Lot = r.S();
            e.Street = r.S();
        }
    }
}
