using System;
using System.Text;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The Construction app's later features in the save's line, after the parts of before. "^n:" the finish and the height variance of the projects that have one (a record each,
    /// "p" and the project's number) and the rival developers' money and variances (a record each, "r" and the builder's index); "^e:" the auction calendar's next day and
    /// the auctions, run or lately over; "^i:" the player's offers for city lots. Fields are appended at the end of a record; a line without a part has none of it (a project
    /// with no finish chosen reads as the standard finish, a developer with no money record has made nothing), a part of a newer version is read as far as this one knows it.
    /// </summary>
    public static partial class BuildStore
    {
        private static void WriteNew(StringBuilder t, BuildState s)
        {
            bool any = false;
            for (int i = 0; i < s.Projects.Count && !any; i++)
            {
                any = ProjectHasNew(s.Projects[i]);
            }
            for (int i = 0; i < s.Rivals.Length && !any; i++)
            {
                any = RivalHasNew(s.Rivals[i]);
            }
            if (any)
            {
                t.Append("^n:");
                bool first = true;
                for (int i = 0; i < s.Projects.Count && i < BuildCatalogue.ProjectsMost; i++)
                {
                    ProjectFile p = s.Projects[i];
                    if (!ProjectHasNew(p))
                    {
                        continue;
                    }
                    t.Append(first ? string.Empty : ";").Append("p|");
                    first = false;
                    Join(t, p.Id, p.Finish, p.FinishBy, p.FinishFixed ? 1 : 0, p.FinishRented ? 1 : 0, (int)p.Variance, p.VarianceDay, p.VarianceFloors, p.VarianceAyes, p.VarianceNoes);
                }
                for (int i = 0; i < s.Rivals.Length; i++)
                {
                    RivalFile r = s.Rivals[i];
                    if (!RivalHasNew(r))
                    {
                        continue;
                    }
                    t.Append(first ? string.Empty : ";").Append("r|");
                    first = false;
                    Join(t, i, r.VarianceAsked, r.VarianceGranted);
                    t.Append('|').Append(r.Gained.ToString(Inv));
                }
            }
            if (s.Auctions.Count > 0 || s.NextAuctionDay > 0)
            {
                t.Append("^e:").Append(s.NextAuctionDay.ToString(Inv));
                for (int i = Math.Max(0, s.Auctions.Count - AuctionRules.KeptMost); i < s.Auctions.Count; i++)
                {
                    AuctionFile a = s.Auctions[i];
                    t.Append(';');
                    Join(t, a.Id, (int)a.State, a.Leader, a.Bids, a.Opened, a.Ends, a.Area, a.Closed, a.Round, a.HallId, a.LogCount);
                    for (int k = 0; k < AuctionFile.LogSize; k++)
                    {
                        t.Append('|').Append(a.LogWho[k].ToString(Inv)).Append('|').Append(a.LogDay[k].ToString(Inv)).Append('|').Append(a.LogBid[k].ToString(Inv));
                    }
                    t.Append('|').Append(a.List.ToString(Inv)).Append('|').Append(a.Opening.ToString(Inv)).Append('|').Append(a.Reserve.ToString(Inv)).Append('|').Append(a.Bid.ToString(Inv))
                        .Append('|').Append(a.Mine.ToString(Inv)).Append('|').Append(Esc(a.Lot)).Append('|').Append(Esc(a.Hall));
                }
            }
            if (s.CityOffers.Count > 0)
            {
                t.Append("^i:");
                for (int i = Math.Max(0, s.CityOffers.Count - AuctionRules.OffersKept), n = 0; i < s.CityOffers.Count; i++, n++)
                {
                    CityOfferFile o = s.CityOffers[i];
                    t.Append(n > 0 ? ";" : string.Empty);
                    Join(t, o.Id, (int)o.State, o.Made, o.Due, o.Answered);
                    t.Append('|').Append(o.Price.ToString(Inv)).Append('|').Append(o.List.ToString(Inv)).Append('|').Append(Esc(o.Lot));
                }
            }
        }

        private static bool ProjectHasNew(ProjectFile p) => p.Finish > 0 || p.FinishBy > 0 || p.FinishFixed || p.Variance != VarianceState.None;

        private static bool RivalHasNew(RivalFile r) => r.Gained != 0L || r.VarianceAsked > 0 || r.VarianceGranted > 0;

        // The later features' parts; false for a part of another's.
        private static bool ReadNew(char part, string body, BuildState s)
        {
            switch (part)
            {
                case 'n':
                    foreach (string rec in Records(body))
                    {
                        var r = new Fields(rec);
                        string kind = r.S();
                        if (kind == "p")
                        {
                            ProjectFile p = s.Project(r.I(0));
                            int finish = r.I(0), by = r.I(0), fixedIt = r.I(0), rented = r.I(0), variance = r.I(0), day = r.I(0), floors = r.I(0), ayes = r.I(0), noes = r.I(0);
                            if (p != null)
                            {
                                p.Finish = BuildCatalogue.Clamp(finish, 0, FinishRules.Tiers);
                                p.FinishBy = Math.Max(0, by);
                                p.FinishFixed = fixedIt == 1;
                                p.FinishRented = rented == 1;
                                p.Variance = (VarianceState)BuildCatalogue.Clamp(variance, 0, (int)VarianceState.Refused);
                                p.VarianceDay = Math.Max(0, day);
                                p.VarianceFloors = BuildCatalogue.Clamp(floors, 0, 60);
                                p.VarianceAyes = Math.Max(-1, ayes);
                                p.VarianceNoes = Math.Max(-1, noes);
                            }
                        }
                        else if (kind == "r")
                        {
                            int at = r.I(-1), asked = r.I(0), granted = r.I(0);
                            long gained = r.L(0L);
                            if (at >= 0 && at < s.Rivals.Length)
                            {
                                s.Rivals[at].VarianceAsked = Math.Max(0, asked);
                                s.Rivals[at].VarianceGranted = Math.Max(0, granted);
                                s.Rivals[at].Gained = gained;
                            }
                        }
                    }
                    return true;
                case 'e':
                {
                    string[] records = body.Split(';');
                    s.NextAuctionDay = int.TryParse(records[0], System.Globalization.NumberStyles.Integer, Inv, out int next) ? Math.Max(0, next) : 0;
                    for (int i = 1; i < records.Length && s.Auctions.Count < AuctionRules.KeptMost; i++)
                    {
                        if (records[i].Length == 0)
                        {
                            continue;
                        }
                        var r = new Fields(records[i]);
                        var a = new AuctionFile
                        {
                            Id = r.I(0), State = (AuctionState)BuildCatalogue.Clamp(r.I(0), 0, (int)AuctionState.Lapsed), Leader = BuildCatalogue.Clamp(r.I(-2), -2, BuildCatalogue.RivalCount - 1),
                            Bids = Math.Max(0, r.I(0)), Opened = r.I(0), Ends = r.I(0), Area = Math.Max(0, r.I(0)), Closed = Math.Max(0, r.I(0)), Round = r.I(0), HallId = Math.Max(0, r.I(0))
                        };
                        a.LogCount = BuildCatalogue.Clamp(r.I(0), 0, AuctionFile.LogSize);
                        for (int k = 0; k < AuctionFile.LogSize; k++)
                        {
                            a.LogWho[k] = BuildCatalogue.Clamp(r.I(-2), -2, BuildCatalogue.RivalCount - 1);
                            a.LogDay[k] = r.I(0);
                            a.LogBid[k] = Math.Max(0L, r.L(0L));
                        }
                        a.List = Math.Max(0L, r.L(0L));
                        a.Opening = Math.Max(0L, r.L(0L));
                        a.Reserve = Math.Max(0L, r.L(0L));
                        a.Bid = Math.Max(0L, r.L(0L));
                        a.Mine = Math.Max(0L, r.L(0L));
                        a.Lot = r.S();
                        a.Hall = r.S();
                        if (a.Id > 0 && a.Lot.Length > 0 && a.List > 0L && s.Auction(a.Id) == null)
                        {
                            s.Auctions.Add(a);
                        }
                    }
                    return true;
                }
                case 'i':
                    foreach (string rec in Records(body))
                    {
                        if (s.CityOffers.Count >= AuctionRules.OffersKept)
                        {
                            break;
                        }
                        var r = new Fields(rec);
                        var o = new CityOfferFile
                        {
                            Id = r.I(0), State = (CityOfferState)BuildCatalogue.Clamp(r.I(0), 0, (int)CityOfferState.Lapsed), Made = r.I(0), Due = r.I(0), Answered = Math.Max(0, r.I(0))
                        };
                        o.Price = Math.Max(0L, r.L(0L));
                        o.List = Math.Max(0L, r.L(0L));
                        o.Lot = r.S();
                        if (o.Id > 0 && o.Lot.Length > 0 && o.Price > 0L && s.CityOffer(o.Id) == null)
                        {
                            s.CityOffers.Add(o);
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }
    }
}
