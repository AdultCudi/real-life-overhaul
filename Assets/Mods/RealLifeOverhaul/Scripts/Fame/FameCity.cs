using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Fame
{
    /// <summary>What the city asks of somebody successful. Saved as a number: new ones go at the end.</summary>
    public enum FameRequestKind
    {
        Sponsor = 0,         // a neighbourhood event, or a corner done up
        Charity = 1,
        Rescue = 2,          // a struggling business to be saved
        Complaint = 3,       // a public complaint to be answered
        Partnership = 4      // another owner's offer of a joint promotion
    }

    /// <summary>Where a request stands. Saved as a number.</summary>
    public enum FameRequestState
    {
        Open = 0,
        Accepted = 1,
        Refused = 2,
        Lapsed = 3,
        Withdrawn = 4        // a scandal: the other owner thinks better of it
    }

    /// <summary>Where a push on the council stands. Saved as a number.</summary>
    public enum FameLobbyState
    {
        Pending = 0,
        Won = 1,
        Lost = 2
    }

    /// <summary>A request from the city: what it is, until when, what it costs and where.</summary>
    public sealed class FameRequest
    {
        public int Id;
        public FameRequestKind Kind;
        public int Offered, Until;
        public int Amount;                   // dollars
        public string District;
        public string Place;                 // the player's business a partnership is about, or null
        public int Seed;                     // picks one of its two wordings
        public FameRequestState State;
    }

    /// <summary>A bribe the player paid: it may come out later.</summary>
    public sealed class FameBribe
    {
        public int Id;
        public int Day;
        public int Amount;
        public string Place;
        public int Surfaced = -1;            // the day it came out; -1 while it has not
        public int Case;                     // the court's case number; 0 for none
    }

    /// <summary>A push on the council for one of its levers (Civic's own numbers), decided a week on.</summary>
    public sealed class FameLobby
    {
        public int Id;
        public int Lever, Position;
        public int Day, DecideDay;
        public int Chance;                   // per cent
        public int Rival;                    // per cent the other side's backers took off it
        public FameLobbyState State;
    }

    /// <summary>One change the council could be pushed to make (Civic's own numbers), as the Standing page lists it.</summary>
    public struct FameLever
    {
        public int Lever, Position;
        public string Name, Effect;          // the council's own texts
    }

    /// <summary>More customers for a while: at one business (a partnership) or in a district (an event sponsored).</summary>
    public sealed class FameBoost
    {
        public string Place;                 // or null for the whole district
        public string District;
        public int Percent;
        public int Until;                    // the first day without it
    }

    /// <summary>
    /// The city's connections and power by rule: requests, evenings, lobbying the council, and bribes with the scandal
    /// they can become. Game-free.
    /// </summary>
    public static class FameCity
    {
        public const int RequestsMost = 8, BribesMost = 8, LobbiesMost = 4, BoostsMost = 8;

        public const int RequestFrom = 30;         // recognition, and wealth
        public const int RequestGapDays = 7;
        public const int RequestChance = 300;      // per mille a day once one may come
        public const int RequestDays = 5;          // to answer
        public static readonly int[] CostShare = { 100, 150, 400, 30, 50 };   // thousandths of a week's income, by kind
        public static readonly int[] CostLeast = { 500, 500, 2000, 100, 300 };        // dollars
        public static readonly int[] CostMost = { 20000, 30000, 80000, 3000, 8000 };  // dollars
        public const int SponsorBoost = 3, SponsorDays = 5, PartnerBoost = 4, PartnerDays = 7;   // per cent of custom, for days
        public const int BoostMost = 4;            // per cent from boosts and a scandal together, either way

        public const int InvestorsWealth = 45, InvestorsFrom = 35, ReceptionFrom = 45;
        public const int EventChance = 150;        // per mille a day
        public const int InvestorsHour = 19, ReceptionHour = 18;
        public const int CreditFromReception = 10; // influence for the next push

        public const int InfluenceFrom = 50;
        public const int LobbyRestDays = 28, LobbyDecideDays = 7, LobbyApplyDays = 2;
        public const int LobbyLeast = 10, LobbyMost = 70;    // per cent
        public const int RivalLeast = 10, RivalMost = 30;    // per cent the other side's backers take off
        public const int LobbyFeeShare = 50, LobbyFeeLeast = 500, LobbyFeeMost = 10000;   // thousandths of a week's income, dollars

        public const int BribeOffer = 350;         // per mille: an official asks, at an inspection or a licence
        public const int BribeIntegrity = -5;
        public const int SurfaceAfterDays = 10;
        public const int SurfaceBase = 6, SurfaceEach = 4, SurfaceMost = 40;   // per mille a day
        public const int ScandalDays = 14;
        public const int ScandalIntegrity = -8, ScandalCustomers = -4, ScandalWorkers = -2, ScandalPress = 20;   // points; the press in tenths
        public const int ScandalFootfall = -2;     // per cent
        public const int ScandalScrutiny = 500;    // thousandths more inspections and audits
        public const int ScandalEsteem = 3;

        public static bool InScandal(FameFile f, int day) => day < f.ScandalUntil;

        public static int Cost(FameRequestKind kind, long weekly)
        {
            int k = FameRules.Clamp((int)kind, 0, CostShare.Length - 1);
            long cost = Math.Max(0L, weekly) * CostShare[k] / 1000;
            cost = Math.Min(CostMost[k], Math.Max(CostLeast[k], cost));
            return (int)(cost / 50 * 50);
        }

        public static FameRequest Open(FameFile f)
        {
            for (int i = 0; i < f.Requests.Count; i++)
            {
                if (f.Requests[i].State == FameRequestState.Open)
                {
                    return f.Requests[i];
                }
            }
            return null;
        }

        public static FameRequest Request(FameFile f, int id)
        {
            for (int i = 0; i < f.Requests.Count; i++)
            {
                if (f.Requests[i].Id == id)
                {
                    return f.Requests[i];
                }
            }
            return null;
        }

        /// <summary>Whether the city asks something of the player today, from two rolls of 0 to 999. Made and listed if so.</summary>
        public static FameRequest RequestRoll(FameFile f, int day, int roll, int pick, string district, string place)
        {
            if (f.Recognition < RequestFrom || f.Wealth < RequestFrom || Open(f) != null || day - f.RequestDay < RequestGapDays || roll >= RequestChance)
            {
                return null;
            }
            var kinds = new List<FameRequestKind>(6) { FameRequestKind.Sponsor, FameRequestKind.Charity };
            if (f.Wealth >= 50)
            {
                kinds.Add(FameRequestKind.Rescue);
            }
            kinds.Add(FameRequestKind.Complaint);
            if (f.Rep[(int)FamePart.Customers] < FameRules.Start || f.Rep[(int)FamePart.Neighbourhood] < FameRules.Start)
            {
                kinds.Add(FameRequestKind.Complaint);   // twice as likely
            }
            if (place != null && !InScandal(f, day))
            {
                kinds.Add(FameRequestKind.Partnership);
            }
            return Offer(f, kinds[FameRules.Clamp(pick, 0, 999) * kinds.Count / 1000], day, pick, district, place);
        }

        public static FameRequest Offer(FameFile f, FameRequestKind kind, int day, int seed, string district, string place)
        {
            var r = new FameRequest
            {
                Id = f.NextId++, Kind = kind, Offered = day, Until = day + RequestDays, Amount = Cost(kind, f.Weekly), District = district,
                Place = kind == FameRequestKind.Partnership ? place : null, Seed = seed, State = FameRequestState.Open
            };
            FameRules.MakeRoom(f.Requests, RequestsMost, x => x.State != FameRequestState.Open);
            f.Requests.Add(r);
            f.RequestDay = day;
            return r;
        }

        /// <summary>A request answered, paid for already if accepted. Returns the piece the papers print of it, or none.</summary>
        public static FameStory Answer(FameFile f, FameRequest r, bool accept, int day)
        {
            if (r.State != FameRequestState.Open)
            {
                return FameStory.None;
            }
            r.State = accept ? FameRequestState.Accepted : FameRequestState.Refused;
            if (!accept)
            {
                if (r.Kind == FameRequestKind.Complaint)
                {
                    Ignored(f, r, day);
                }
                return FameStory.None;
            }
            switch (r.Kind)
            {
                case FameRequestKind.Sponsor:
                    FameRules.Move(f, FamePart.Neighbourhood, FameCause.Sponsor, 4, day, r.District);
                    FameRules.Move(f, FamePart.Customers, FameCause.Sponsor, 1, day);
                    FameRules.Publicity(f, 15, day, FameCause.Sponsor);
                    Boost(f, null, r.District, SponsorBoost, day + SponsorDays);
                    return (r.Seed & 1) == 0 ? FameStory.Sponsor : FameStory.Renovation;
                case FameRequestKind.Charity:
                    FameRules.Move(f, FamePart.Integrity, FameCause.Charity, 4, day);
                    FameRules.Publicity(f, 8, day, FameCause.Charity);
                    f.CharityDay = day;
                    return FameStory.Charity;
                case FameRequestKind.Rescue:
                    FameRules.Move(f, FamePart.Neighbourhood, FameCause.Rescue, 5, day, r.District);
                    FameRules.Move(f, FamePart.Integrity, FameCause.Rescue, 2, day);
                    FameRules.Publicity(f, 10, day, FameCause.Rescue);
                    return FameStory.Rescue;
                case FameRequestKind.Complaint:
                    FameRules.Move(f, FamePart.Customers, FameCause.Answered, 3, day);
                    FameRules.Move(f, FamePart.Integrity, FameCause.Answered, 1, day);
                    return FameStory.None;
                default:
                    Boost(f, r.Place, null, PartnerBoost, day + PartnerDays);
                    FameRules.Publicity(f, 5, day, FameCause.Partnership);
                    return FameStory.Partnership;
            }
        }

        private static void Ignored(FameFile f, FameRequest r, int day)
        {
            FameRules.Move(f, FamePart.Customers, FameCause.Ignored, -2, day);
            FameRules.Move(f, FamePart.Neighbourhood, FameCause.Ignored, -1, day, r.District);
        }

        /// <summary>The day's look at the requests: lapsed ones, partnerships a scandal calls off, boosts run out. How many requests changed.</summary>
        public static int RequestsDay(FameFile f, int day)
        {
            int changed = 0;
            for (int i = 0; i < f.Requests.Count; i++)
            {
                FameRequest r = f.Requests[i];
                if (r.State != FameRequestState.Open)
                {
                    continue;
                }
                if (r.Kind == FameRequestKind.Partnership && InScandal(f, day))
                {
                    r.State = FameRequestState.Withdrawn;
                    changed++;
                }
                else if (day > r.Until)
                {
                    r.State = FameRequestState.Lapsed;
                    if (r.Kind == FameRequestKind.Complaint)
                    {
                        Ignored(f, r, day);
                    }
                    changed++;
                }
            }
            for (int i = f.Boosts.Count - 1; i >= 0; i--)
            {
                if (day >= f.Boosts[i].Until)
                {
                    f.Boosts.RemoveAt(i);
                }
            }
            return changed;
        }

        private static void Boost(FameFile f, string place, string district, int percent, int until)
        {
            FameRules.MakeRoom(f.Boosts, BoostsMost, x => true);
            f.Boosts.Add(new FameBoost { Place = place, District = district, Percent = percent, Until = until });
        }

        /// <summary>The customers a business has from the boosts that run for it and from a scandal, per cent.</summary>
        public static int BoostAt(FameFile f, string place, string district, int day)
        {
            int percent = InScandal(f, day) ? ScandalFootfall : 0;
            for (int i = 0; i < f.Boosts.Count; i++)
            {
                FameBoost b = f.Boosts[i];
                if (day < b.Until && ((b.Place != null && b.Place == place) || (b.Place == null && b.District != null && b.District == district)))
                {
                    percent += b.Percent;
                }
            }
            return FameRules.Clamp(percent, -BoostMost, BoostMost);
        }

        /// <summary>Whether an invitation to an evening comes today, from rolls of 0 to 999. Made and listed if so.</summary>
        public static FameInvite EventRoll(FameFile f, int day, int roll, int pick)
        {
            if (FamePress.Open(f) != null || day - f.InviteDay < FamePress.InviteGapDays || roll >= EventChance)
            {
                return null;
            }
            bool investors = f.Wealth >= InvestorsWealth && f.Recognition >= InvestorsFrom;
            bool reception = f.Recognition >= ReceptionFrom;
            if (!investors && !reception)
            {
                return null;
            }
            bool first = investors && (!reception || pick < 500);
            return FamePress.Offer(f, first ? FameInviteKind.Investors : FameInviteKind.CityEvent, day, first ? InvestorsHour : ReceptionHour);
        }

        /// <summary>The player's weight with the council, 0 to 100.</summary>
        public static int Influence(FameFile f) => FameRules.Clamp((f.Recognition * 60 + f.Overall * 40) / 100 + f.LobbyCredit, 0, 100);

        public static FameLobby Pending(FameFile f)
        {
            for (int i = 0; i < f.Lobbies.Count; i++)
            {
                if (f.Lobbies[i].State == FameLobbyState.Pending)
                {
                    return f.Lobbies[i];
                }
            }
            return null;
        }

        public static bool MayLobby(FameFile f, int day) => Influence(f) >= InfluenceFrom && Pending(f) == null && day >= f.LobbyRest;

        public static int LobbyFee(long weekly)
        {
            long fee = Math.Min(LobbyFeeMost, Math.Max(LobbyFeeLeast, Math.Max(0L, weekly) * LobbyFeeShare / 1000));
            return (int)(fee / 50 * 50);
        }

        /// <summary>A push's chance before the other side's backers, per cent.</summary>
        public static int LobbyBase(FameFile f) => (Influence(f) - 40) * 12 / 10 + (f.Rep[(int)FamePart.Integrity] - FameRules.Start) / 5;

        public static FameLobby Lobby(FameFile f, int lever, int position, int day, int rivalRoll)
        {
            int rival = RivalLeast + FameRules.Clamp(rivalRoll, 0, 999) * (RivalMost - RivalLeast + 1) / 1000;
            var l = new FameLobby
            {
                Id = f.NextId++, Lever = lever, Position = Math.Sign(position), Day = day, DecideDay = day + LobbyDecideDays, Rival = rival,
                Chance = FameRules.Clamp(LobbyBase(f) - rival, LobbyLeast, LobbyMost), State = FameLobbyState.Pending
            };
            FameRules.MakeRoom(f.Lobbies, LobbiesMost, x => x.State != FameLobbyState.Pending);
            f.Lobbies.Add(l);
            f.LobbyRest = day + LobbyRestDays;
            f.LobbyCredit = 0;
            return l;
        }

        /// <summary>The council decides a push on its day, from a roll of 0 to 99. Nothing before its day.</summary>
        public static FameStory Decide(FameFile f, FameLobby l, int day, int roll)
        {
            if (l.State != FameLobbyState.Pending || day < l.DecideDay)
            {
                return FameStory.None;
            }
            bool won = roll < l.Chance;
            l.State = won ? FameLobbyState.Won : FameLobbyState.Lost;
            FameRules.Publicity(f, won ? 10 : 3, day, FameCause.Lobby);
            return won ? FameStory.LobbyWon : FameStory.LobbyLost;
        }

        /// <summary>True if an official asks for a bribe: the same answer for the same issue's seed.</summary>
        public static bool BribeAsked(int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                return (int)((h >> 8) % 1000) < BribeOffer;
            }
        }

        public static FameBribe Bribed(FameFile f, string place, int amount, int day)
        {
            FameRules.Move(f, FamePart.Integrity, FameCause.Bribe, BribeIntegrity, day);
            var b = new FameBribe { Id = f.NextId++, Day = day, Amount = amount, Place = place };
            FameRules.MakeRoom(f.Bribes, BribesMost, x => x.Surfaced >= 0);
            f.Bribes.Add(b);
            return b;
        }

        /// <summary>The chance, per mille, that a bribe comes out today.</summary>
        public static int SurfaceChance(FameFile f, FameBribe b, int day)
        {
            if (b.Surfaced >= 0 || day - b.Day < SurfaceAfterDays)
            {
                return 0;
            }
            int others = 0;
            for (int i = 0; i < f.Bribes.Count; i++)
            {
                others += f.Bribes[i] != b ? 1 : 0;
            }
            return Math.Min(SurfaceMost, SurfaceBase + SurfaceEach * others + f.Recognition / 20);
        }

        /// <summary>The day's look at the bribes, from a roll of 0 to 999: the one that came out and started a scandal, or null.</summary>
        public static FameBribe Surface(FameFile f, int day, int roll)
        {
            for (int i = 0; i < f.Bribes.Count; i++)
            {
                FameBribe b = f.Bribes[i];
                if (roll >= SurfaceChance(f, b, day))
                {
                    continue;
                }
                b.Surfaced = day;
                f.ScandalUntil = Math.Max(f.ScandalUntil, day + ScandalDays);
                FameRules.Move(f, FamePart.Integrity, FameCause.Scandal, ScandalIntegrity, day);
                FameRules.Move(f, FamePart.Customers, FameCause.Scandal, ScandalCustomers, day);
                FameRules.Move(f, FamePart.Workers, FameCause.Scandal, ScandalWorkers, day);
                FameRules.Publicity(f, ScandalPress, day, FameCause.Scandal);
                return b;
            }
            return null;
        }
    }
}
