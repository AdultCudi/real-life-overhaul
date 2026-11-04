using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>
    /// One business a rival tycoon runs in the city: the outlet an Outlet move opened. It has a lease on its premises and a crew on its staff, kept by the very rules
    /// the player's company keeps theirs by (<see cref="LeaseRules"/>, <see cref="StaffRules"/>), so that whatever the player can do with a lease or a crew a rival
    /// does too. Game-free. Saved with the rivals (<see cref="RivalStore"/>, lines "O").
    /// </summary>
    public sealed class RivalOutlet
    {
        public int Move;                                        // the Outlet move that opened it
        public int Tycoon;
        public string District = string.Empty, Type = string.Empty;
        public LeaseFile Lease;                                 // its premises: a term, the landlord's offer before it ends, the answer, the renewal
        public readonly List<StaffFile> Crew = new List<StaffFile>(RivalFirms.CrewMost);
        public string Seat = string.Empty;                      // the game's rival business it stands on (its address, RivalSeat.Key); empty: a seeded stand-in, the game had none there
        public readonly List<string> Out = new List<string>(RivalFirms.OutMost);   // the game's own people the firm let go (their ids): the crew is not read back for them
        public int NextHire = 1;                                // numbers the ids of its hires
        public int Hires, LetGo, Raises, Renewals, Holds;       // what its days have done
        public long Rent, Wages;                                // paid out so far, whole money
    }

    /// <summary>One person on a game rival business's crew, as the game holds them (BuildingRegistration.aiEmployees): the game's own id, name, main skill and wage for that skill.</summary>
    public sealed class RivalHand
    {
        public string Id = string.Empty, Name = string.Empty;
        public int Skill;                                       // the game's value of the main skill
        public int Hourly;                                      // hundredths an hour: the game's wage for that skill (EmployeeHelper.CalculateHourlyWageForSkill), before the city's prices
        public int WeekHours;                                   // hours a week the game says they expect to work
    }

    /// <summary>A game rival's business as the game holds it, for an outlet to stand on: its address, rent a day (whole money) and crew. Rent 0: the game has no figure.</summary>
    public sealed class RivalSeat
    {
        public string Key = string.Empty, Name = string.Empty;
        public int Rent;
        public readonly List<RivalHand> Hands = new List<RivalHand>(RivalFirms.CrewMost);
    }

    /// <summary>
    /// What the game holds of its rival businesses (RivalSeatsGame reads it; the checks hand in a fake). Game-free, so the firms' rules run on it. Both calls
    /// are for the rivals' day and never touch the game's people.
    /// </summary>
    public interface IRivalSeats
    {
        /// <summary>
        /// A rival's business of this kind in the district, not one in <paramref name="taken"/>: chosen from the candidates by <paramref name="pick"/> (0 to 999, seeded). Null with none.
        /// </summary>
        RivalSeat Find(string district, string type, IList<string> taken, int pick);

        /// <summary>The business at a key as it is now; null when it is gone (sold, closed, its rival out of the game).</summary>
        RivalSeat Read(string key);
    }

    /// <summary>What one outlet's day came to.</summary>
    public struct RivalFirmDay
    {
        public long Rent, Wages;                                // paid out today, whole money
        public int Hired, LetGo, Raised;
        public int Renewed;                                     // 1 on the day the lease renewed or was held over
        public int Rise;                                        // per mille the renewal added to the rent, where it renewed
    }

    /// <summary>
    /// The rival tycoons' businesses (docs: the NPC rule, whatever the player can do, rivals do too): an outlet opens with a lease and a crew hired through the staff
    /// rules; each day the crew's lateness is counted, somebody underpaid asks for a raise and the firm answers it as a manager does, somebody late three days running is
    /// let go and replaced, and the lease runs: the landlord's offer comes before its end, the firm answers inside its limit and holds over past it. What they pay (wages
    /// and rent) comes off the tycoon's worth, and the rises their landlords ask feed the trend the player's landlords ask by.
    ///
    /// What is the game's and what is the mod's. An outlet stands on a game rival's business of the same kind in the district where the game has one (<see cref="IRivalSeats"/>):
    /// its crew are the game's own people (id, name, skill, the game's wage for the skill and the hours it expects), read again every day, and its rent is the building's own
    /// RentPerDay. Where the game has none of the kind there, the outlet is a seeded stand-in (anonymous ids, the catalogue's pay and rent). On either, the mod's own layer is
    /// the lateness (a seeded roll: the game keeps no attendance for a rival's people), the raise a firm agrees, and the lease's term and renewals over the game's rent. Someone
    /// the firm lets go leaves its books only: the game's rival is never changed, and the crew read back skips that person.
    /// </summary>
    public static class RivalFirms
    {
        public const int CrewStart = 3;                         // people a new outlet hires, and up to two more by a seeded draw
        public const int CrewMost = 8;
        public const int OutMost = CrewMost * 4;                // people let go that an outlet remembers by id (those no longer on the game's crew are forgotten each day, so this is a bound, never a limit met)
        public const int OutletsMost = RivalCatalogue.OutletsMost;   // outlets open at once, for the moves and for the firms (one cap, in the catalogue): past it the oldest closes
        public const int HoursADay = 8;                         // a working day, paid
        public const int GoingBase = 1700;                      // hundredths an hour at the starting prices, for the middle of the pay
        public const int RentBase = 150;                        // a day at the starting prices
        public const int FirmBand = 3;                          // the step of ManagerCatalogue.WageBands the firm pays up to: the going rate
        public const int FirmLeaseStep = 3;                     // the step of ManagerCatalogue.LeaseRises a firm agrees on its own: a rise of up to 8 per cent
        public const int LateChance = 150;                      // per mille a worker is late on a working day
        public const int DriftDays = 60;                        // days the city's pay stays where it is before it moves again
        private const int PurposeCrew = 311, PurposeSkill = 312, PurposeLate = 313, PurposePay = 314, PurposeDrift = 315, PurposeSeat = 316;

        /// <summary>How far the city's pay has moved from the starting rate, per mille (-80 to +120): it moves every <see cref="DriftDays"/> days, by a seeded draw.</summary>
        public static int Drift(int seed, int day)
        {
            return ManagerRules.Roll(seed, day / DriftDays, "pay", PurposeDrift, string.Empty) * 200 / 1000 - 80;
        }

        /// <summary>The key of an outlet's premises and its people, as a place: one for each move.</summary>
        public static string Key(RivalOutlet o) => "rival" + o.Tycoon + "#" + o.Move;

        /// <summary>The outlet a move opened, or null.</summary>
        public static RivalOutlet OutletOf(RivalFile f, int move)
        {
            for (int i = 0; i < f.Outlets.Count; i++)
            {
                if (f.Outlets[i].Move == move)
                {
                    return f.Outlets[i];
                }
            }
            return null;
        }

        /// <summary>
        /// An outlet for a move that has come: a lease on premises at the district's rent (<see cref="LeaseRules.Begin"/>) and a crew of three to five hired
        /// (<see cref="Hire"/>).
        /// </summary>
        public static RivalOutlet Open(RivalMove m, int seed, int day, int monthDays, int index, IRivalSeats seats = null, IList<string> taken = null)
        {
            var o = new RivalOutlet { Move = m.Id, Tycoon = m.Tycoon, District = m.District ?? string.Empty, Type = m.Type ?? string.Empty };
            string key = Key(o);
            RivalSeat seat = seats != null && o.District.Length > 0 && o.Type.Length > 0
                ? seats.Find(o.District, o.Type, taken ?? new List<string>(), ManagerRules.Roll(seed, m.Id, key, PurposeSeat, string.Empty)) : null;
            o.Lease = LeaseRules.Begin(seed, key, seat != null && seat.Rent > 0 ? seat.Rent : CompanyCatalogue.AtPrices(RentBase, index), day, monthDays);
            o.Lease.District = o.District;
            o.Lease.Name = key;
            if (seat != null && seat.Key.Length > 0)
            {
                o.Seat = seat.Key;
                Mirror(o, seat, seed, day, index);
                return o;
            }
            int crew = CrewStart + ManagerRules.Roll(seed, m.Id, key, PurposeCrew, string.Empty) * 3 / 1000;
            for (int i = 0; i < crew; i++)
            {
                Hire(o, seed, day, index);
            }
            return o;
        }

        /// <summary>
        /// The outlet's crew and rent as the game's business holds them today: a person on the game's crew (not one the firm let go) who is not on the outlet's crew is hired
        /// (<see cref="StaffRules.Hire"/>, under the game's own id and name), one who is no longer on it has left (poached, or replaced by the game) and is dropped without
        /// counting as let go, and everyone's skill, wage and hours are the game's. The lease's rent is the building's own where the game gives one. Cost: one pass over at most
        /// eight hands, once a day.
        /// </summary>
        public static void Mirror(RivalOutlet o, RivalSeat seat, int seed, int day, int index)
        {
            if (seat.Rent > 0)
            {
                o.Lease.Rent = seat.Rent;
            }
            for (int i = o.Crew.Count - 1; i >= 0; i--)
            {
                if (HandOf(seat, o.Crew[i].Id) == null)
                {
                    StaffRules.LetGo(o.Crew[i]);   // (gone from the game's crew: the outlet's book closes theirs)
                    o.Crew.RemoveAt(i);
                }
            }
            for (int i = o.Out.Count - 1; i >= 0; i--)
            {
                if (HandOf(seat, o.Out[i]) == null)
                {
                    o.Out.RemoveAt(i);   // (somebody the game's crew no longer holds cannot come back: nothing to keep out)
                }
            }
            int drift = Drift(seed, day);
            for (int h = 0; h < seat.Hands.Count && h < CrewMost; h++)
            {
                RivalHand hand = seat.Hands[h];
                if (string.IsNullOrEmpty(hand.Id) || o.Out.Contains(hand.Id))
                {
                    continue;
                }
                StaffFile f = CrewOf(o, hand.Id);
                if (f == null)
                {
                    if (o.Crew.Count >= CrewMost)
                    {
                        continue;
                    }
                    f = StaffRules.Hire(hand.Id, 0);
                    f.Place = Key(o);
                    o.Crew.Add(f);
                    o.Hires++;
                }
                f.Name = hand.Name;
                f.Skill = hand.Skill;
                f.Base = hand.Hourly;
                f.Wage = PaidWage.Cents(f.Base, index, 0);
                f.DayHours = Math.Max(400, Math.Min(1000, hand.WeekHours * 100 / 5));
                f.Going = Going(o, f, index, drift);
            }
        }

        private static RivalHand HandOf(RivalSeat seat, string id)
        {
            for (int i = 0; i < seat.Hands.Count; i++)
            {
                if (string.Equals(seat.Hands[i].Id, id, StringComparison.Ordinal))
                {
                    return seat.Hands[i];
                }
            }
            return null;
        }

        private static StaffFile CrewOf(RivalOutlet o, string id)
        {
            for (int i = 0; i < o.Crew.Count; i++)
            {
                if (string.Equals(o.Crew[i].Id, id, StringComparison.Ordinal))
                {
                    return o.Crew[i];
                }
            }
            return null;
        }

        /// <summary>A hire for the outlet: a person at the going rate, a little under or over it by a seeded draw, with a record by <see cref="StaffRules.Hire"/>.</summary>
        public static StaffFile Hire(RivalOutlet o, int seed, int day, int index)
        {
            int n = o.NextHire++;
            string id = Key(o) + "p" + n;
            StaffFile f = StaffRules.Hire(id, 0);
            f.Place = Key(o);
            f.Name = id;
            f.Skill = 20 + ManagerRules.Roll(seed, n, id, PurposeSkill, string.Empty) * 60 / 1000;
            f.Going = Going(f, index, Drift(seed, day));
            f.Base = f.Wage = f.Going * (950 + ManagerRules.Roll(seed, n, id, PurposePay, string.Empty) * 100 / 1000) / 1000;
            f.DayHours = HoursADay * 100;
            o.Crew.Add(f);
            o.Hires++;
            return f;
        }

        /// <summary>
        /// What the city pays this person, hundredths an hour: for the game's own people their game wage at today's prices, otherwise the catalogue's rate for their skill; both
        /// moved by the city's pay.
        /// </summary>
        public static int Going(RivalOutlet o, StaffFile f, int index, int drift)
        {
            return o.Seat.Length > 0 && f.Base > 0 ? CompanyCatalogue.AtPrices(f.Base, index) * (1000 + drift) / 1000 : Going(f, index, drift);
        }

        /// <summary>What the city pays for a person's work, hundredths an hour: the starting rate at today's prices, more for the skilled, moved by the city's pay (<see cref="Drift"/>).</summary>
        public static int Going(StaffFile f, int index, int drift)
        {
            return CompanyCatalogue.AtPrices(GoingBase, index) * (900 + f.Skill * 3) / 1000 * (1000 + drift) / 1000;
        }

        /// <summary>
        /// One outlet's day: its crew's (lateness, a raise asked and answered, somebody let go and replaced) and its lease's (the rent paid, the landlord's offer before
        /// the end answered, the lease renewed or held over). <paramref name="trend"/> is what the city's landlords have been asking, per mille.
        /// </summary>
        public static RivalFirmDay Day(RivalOutlet o, int seed, int day, int monthDays, int trend, int index, IRivalSeats seats = null)
        {
            var r = new RivalFirmDay();
            int drift = Drift(seed, day);
            if (o.Seat.Length > 0 && seats != null)
            {
                RivalSeat seat = seats.Read(o.Seat);
                if (seat != null)
                {
                    Mirror(o, seat, seed, day, index);
                }
                else
                {
                    o.Seat = string.Empty;   // the game's business is gone: the outlet goes on as a seeded stand-in, with the crew and rent it has
                }
            }
            for (int i = o.Crew.Count - 1; i >= 0; i--)
            {
                StaffFile f = o.Crew[i];
                f.Going = Going(o, f, index, drift);
                int hours = Math.Max(1, f.DayHours / 100);
                bool late = ManagerRules.Roll(seed, day, f.Id, PurposeLate, string.Empty) < LateChance;
                StaffRules.LateDay(f, hours, late ? hours - CompanyCatalogue.LateShort : hours, false);
                r.Wages += StaffRules.Pay(f.Paid, hours * 100);
                StaffRules.Day(f, 1, day);
                if (f.AskDay > 0)
                {
                    // The firm answers as a manager does, inside the pay band: the going rate agreed, a refusal where the ask is past it.
                    RaiseAnswer answer = ManagerRules.RaiseAnswerFor(f.Paid, f.AskTo, ManagerRules.Ceiling(f.Going, FirmBand), false);
                    StaffRules.Answer(f, answer, day);
                    r.Raised += answer == RaiseAnswer.Agree ? 1 : 0;
                    o.Raises += answer == RaiseAnswer.Agree ? 1 : 0;
                }
                if (ManagerRules.LetGo(f.LateDays, true))
                {
                    StaffRules.LetGo(f);
                    o.Crew.RemoveAt(i);
                    if (o.Seat.Length > 0)
                    {
                        o.Out.Add(f.Id);   // (the game's person stays on the game's crew; the firm's book skips them)
                        while (o.Out.Count > OutMost)
                        {
                            o.Out.RemoveAt(0);
                        }
                    }
                    o.LetGo++;
                    r.LetGo++;
                }
            }
            while (o.Seat.Length == 0 && o.Crew.Count < CrewStart)
            {
                Hire(o, seed, day, index);   // the place is filled again
                r.Hired++;
            }
            LeaseFile l = o.Lease;
            r.Rent = LeaseRules.RentAt(l.Rent, l.Running);
            if (LeaseRules.Settle(l, day, seed, monthDays))
            {
                r.Renewed = 1;
                r.Rise = l.Hold ? 0 : l.Last;
                o.Renewals++;
                o.Holds += l.Hold ? 1 : 0;
            }
            else if (LeaseRules.OfferDue(l, day))
            {
                LeaseRules.Make(l, LeaseRules.Offer(l, seed, trend, 0, 0), day);
                LeaseRules.Respond(l, day, LeaseRules.Within(l, ManagerCatalogue.Step(ManagerCatalogue.LeaseRises, FirmLeaseStep)));   // inside the limit it renews; past it, held over
            }
            o.Rent += r.Rent;
            o.Wages += r.Wages;
            return r;
        }

        /// <summary>
        /// The outlets kept in step with the moves: one opens for each Outlet move that has come and has none; a tycoon past <see cref="OutletsMost"/> closes its oldest,
        /// and one out of play closes all. A closed outlet lets its crew go (<see cref="StaffRules.LetGo"/>). Returns how many opened.
        /// </summary>
        public static int Sync(RivalFile f, int seed, int day, int monthDays, int index, IRivalSeats seats = null)
        {
            int opened = 0;
            for (int i = 0; i < f.Moves.Count; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.Kind == RivalMoveKind.Outlet && (m.State == RivalMoveState.Running || m.State == RivalMoveState.Over) && f.Tycoons[Math.Max(0, Math.Min(f.Tycoons.Length - 1, m.Tycoon))].Active
                    && OutletOf(f, m.Id) == null && m.Land <= day && day - m.Land <= RivalCatalogue.MovesKept * 30)
                {
                    f.Outlets.Add(Open(m, seed, day, monthDays, index, seats, Taken(f)));
                    opened++;
                }
            }
            for (int i = f.Outlets.Count - 1; i >= 0; i--)
            {
                RivalOutlet o = f.Outlets[i];
                int same = 0;
                for (int k = i + 1; k < f.Outlets.Count; k++)
                {
                    same += f.Outlets[k].Tycoon == o.Tycoon ? 1 : 0;
                }
                if (!f.Tycoons[Math.Max(0, Math.Min(f.Tycoons.Length - 1, o.Tycoon))].Active || same >= OutletsMost)
                {
                    Close(o);
                    f.Outlets.RemoveAt(i);
                }
            }
            return opened;
        }

        // The game's businesses the outlets stand on already: no two outlets share one.
        private static List<string> Taken(RivalFile f)
        {
            var taken = new List<string>(f.Outlets.Count);
            for (int i = 0; i < f.Outlets.Count; i++)
            {
                if (f.Outlets[i].Seat.Length > 0)
                {
                    taken.Add(f.Outlets[i].Seat);
                }
            }
            return taken;
        }

        /// <summary>An outlet closes: everybody on its crew is let go.</summary>
        public static void Close(RivalOutlet o)
        {
            for (int i = 0; i < o.Crew.Count; i++)
            {
                StaffRules.LetGo(o.Crew[i]);
            }
            o.Crew.Clear();
        }

        /// <summary>
        /// The rivals' businesses' day, for every tycoon in play: each outlet's day, what it cost taken off the tycoon's worth (never under nothing), and each rise a
        /// renewal added told to <paramref name="renewed"/> (its district, per mille over the rent), which feeds the trend the player's landlords ask by. Returns the
        /// money paid out in all.
        /// </summary>
        public static long Day(RivalFile f, int seed, int day, int monthDays, int trend, int index, Action<string, int> renewed, IRivalSeats seats = null)
        {
            Sync(f, seed, day, monthDays, index, seats);
            long all = 0;
            for (int i = 0; i < f.Outlets.Count; i++)
            {
                RivalOutlet o = f.Outlets[i];
                RivalFirmDay d = Day(o, seed, day, monthDays, trend, index, seats);
                long cost = d.Rent + d.Wages;
                Tycoon t = f.Tycoons[Math.Max(0, Math.Min(f.Tycoons.Length - 1, o.Tycoon))];
                t.Worth = Math.Max(0L, t.Worth - cost);
                all += cost;
                if (d.Renewed > 0 && renewed != null)
                {
                    renewed(o.District, d.Rise);
                }
            }
            return all;
        }
    }
}
