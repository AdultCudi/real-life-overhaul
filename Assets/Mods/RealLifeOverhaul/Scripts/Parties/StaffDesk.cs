using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul.Parties
{
    /// <summary>What is asked for when a company event is booked: the business, the kind, the day and hour, and whether its manager asks.</summary>
    public struct StaffAsk
    {
        public string Place;
        public StaffEventKind Kind;
        public int Day, Hour;
        public bool ByManager;
    }

    /// <summary>
    /// A company event from the booking to its end, a manager's schedule, and the rival tycoons' outlets holding theirs by
    /// the same rule. An event is paid at the business, in its own books; it gives each of the staff who came a share of
    /// a month's cap of the game's satisfaction and the company's standing, half to somebody underpaid, and the standing
    /// is taken back as the lift fades. It costs the working time it takes: the business is shut for the opening hours it
    /// covers, and outside them some of the staff have a reason not to come. Game-free, against <see cref="IPartyWorld"/>.
    /// Nothing a frame: a booking, the clock's look, the day's round.
    /// </summary>
    public static class StaffDesk
    {
        public const string StaffKey = "reallife_transaction_parties_staff";   // a company event, booked at the business as a running cost; its refund is the same key
        private const string RivalPrefix = "r/";

        /// <summary>What an event costs: a head for every one of the staff, at a price level.</summary>
        public static int Cost(StaffEventKind kind, int level, int heads)
        {
            return Math.Max(1, PriceRules.At(PartyCatalogue.EventHeadBase(kind), level)) * Math.Max(0, heads);
        }

        private static int Index(StaffEventKind kind) => PartyCatalogue.Clamp((int)kind, 0, PartyCatalogue.EventMinutes.Length - 1);

        /// <summary>Why an event cannot be booked, the first reason that holds; None when it can. <paramref name="heads"/> is the staff now, -1 when the business is not the player's.</summary>
        public static PartyRefusal Refuse(PartyFile f, IPartyWorld w, StaffAsk ask, int heads, int cost)
        {
            PartyRefusal stand = PartyDesk.Stand(w);
            if (stand != PartyRefusal.None)
            {
                return stand;
            }
            if (heads < 0 || string.IsNullOrEmpty(ask.Place))
            {
                return PartyRefusal.NoBusiness;
            }
            if (heads == 0)
            {
                return PartyRefusal.NoStaff;
            }
            int k = Index(ask.Kind);
            if (ask.Hour < PartyCatalogue.EventHourFrom[k] || ask.Hour > PartyCatalogue.EventHourTo[k])
            {
                return PartyRefusal.BadHour;
            }
            int ahead = PartyRules.Stamp(ask.Day, ask.Hour) - w.Now;
            if (ahead < PartyCatalogue.LeadHours * 60)
            {
                return PartyRefusal.TooSoon;
            }
            if (ahead > PartyCatalogue.AheadDays * 1440)
            {
                return PartyRefusal.TooFar;
            }
            for (int i = 0; i < f.Events.Count; i++)
            {
                StaffEvent e = f.Events[i];
                if (e.Place == ask.Place && e.State != StaffEventState.Cancelled && Math.Abs(e.Day - ask.Day) < PartyCatalogue.EventGapDays)
                {
                    return PartyRefusal.TooOften;
                }
            }
            if (w.Money < cost)
            {
                return PartyRefusal.NoMoney;
            }
            if (w.Floor > 0 && w.Money - cost < w.Floor)
            {
                return PartyRefusal.Reserve;   // never out of what the wallet keeps for a firm's investors
            }
            return PartyRefusal.None;
        }

        /// <summary>Books a company event: refused before any money for every reason there is, paid at the business, then recorded.</summary>
        public static PartyRefusal Book(PartyFile f, IPartyWorld w, StaffAsk ask, out StaffEvent booked)
        {
            booked = null;
            PartyRefusal stand = PartyDesk.Stand(w);
            if (stand != PartyRefusal.None)
            {
                return stand;
            }
            var hands = new List<StaffHand>(16);
            int heads = w.Staff(ask.Place, hands);
            int cost = Cost(ask.Kind, w.Level, heads);
            PartyRefusal why = Refuse(f, w, ask, heads, cost);
            if (why != PartyRefusal.None)
            {
                return why;
            }
            if (!w.Pay(cost, StaffKey, ask.Place))
            {
                return PartyRefusal.NoMoney;
            }
            booked = new StaffEvent
            {
                Id = f.NextId++, Place = ask.Place, Name = w.Business(ask.Place) ?? string.Empty, Kind = (StaffEventKind)Index(ask.Kind), State = StaffEventState.Planned,
                Day = ask.Day, Hour = ask.Hour, Paid = cost, Heads = heads, ByManager = ask.ByManager
            };
            for (int i = 0; i < hands.Count; i++)
            {
                booked.Crew.Add(hands[i].Id);
            }
            f.Events.Add(booked);
            w.Tell(PartyTone.Good, ask.ByManager ? "reallife_party_note_event_manager" : "reallife_party_note_event_booked", "business", booked.Name,
                "when", w.When(ask.Day, ask.Hour), "count", heads.ToString(), "cost", w.Cash(cost));
            return PartyRefusal.None;
        }

        /// <summary>A planned event called off: part of the money back by the notice given. An event never held gives nothing.</summary>
        public static PartyRefusal Cancel(PartyFile f, IPartyWorld w, int id)
        {
            PartyRefusal stand = PartyDesk.Stand(w);
            if (stand != PartyRefusal.None)
            {
                return stand;
            }
            StaffEvent e = f.Event(id);
            if (e == null || e.State != StaffEventState.Planned)
            {
                return e != null && e.State == StaffEventState.Running ? PartyRefusal.Begun : PartyRefusal.NotPlanned;
            }
            Withdraw(f, w, e, w.Now, "reallife_party_note_event_cancelled");
            Trim(f);
            return PartyRefusal.None;
        }

        /// <summary>
        /// A planned event ended by the player's own doing, measured at the moment it was done (<paramref name="at"/>): calling it off, the option switched off, the business
        /// sold. Part of the money back by the notice that moment gave. The one place a planned event is given up by the player, so no way of ending one is better than
        /// calling it off at that hour.
        /// </summary>
        private static void Withdraw(PartyFile f, IPartyWorld w, StaffEvent e, int at, string key)
        {
            int back = PartyRules.Refund(e.Paid, e.Start, at);
            if (back > 0)
            {
                w.Refund(back, StaffKey, e.Place);
            }
            e.Paid -= back;
            e.State = StaffEventState.Cancelled;
            w.Tell(PartyTone.Info, key, "business", e.Name, "back", w.Cash(back));
            Rest(f, e);
        }

        // Called off by what the player did not do, or held for nobody: everything back.
        private static void CallOff(PartyFile f, IPartyWorld w, StaffEvent e, string key, StaffEventState state, int staff = 0)
        {
            int back = e.Paid;
            if (back > 0)
            {
                w.Refund(back, StaffKey, e.Place);
            }
            e.Paid = 0;
            e.State = state;
            w.Tell(PartyTone.Warn, key, "business", e.Name, "back", w.Cash(back), "staff", staff.ToString());
            Rest(f, e);
        }

        // An event of a manager's schedule that did not stand is not tried again the next day into the same cause: the schedule counts from its day.
        private static void Rest(PartyFile f, StaffEvent e)
        {
            StaffPlan plan = e.ByManager ? f.Plan(e.Place, false) : null;
            if (plan != null)
            {
                plan.Since = Math.Max(plan.Since, e.Day);
            }
        }

        /// <summary>
        /// The day's look at what the player's own doing may have ended since the booking: a planned event at a business that is no longer theirs is called off at the moment
        /// it was found, by the notice that gives, so selling a business is never a way to a full refund. A pass over the planned events, once a day.
        /// </summary>
        public static void Watch(PartyFile f, IPartyWorld w)
        {
            List<StaffHand> hands = null;
            int now = w.Now;
            for (int i = 0; i < f.Events.Count; i++)
            {
                StaffEvent e = f.Events[i];
                if (e.State != StaffEventState.Planned)
                {
                    continue;
                }
                hands = hands ?? new List<StaffHand>(16);
                if (w.Staff(e.Place, hands) < 0)
                {
                    Withdraw(f, w, e, now, "reallife_party_note_event_off_gone");
                }
            }
        }

        /// <summary>A business's manager is told to hold an event every so many days (0: no more). No money moves here.</summary>
        public static PartyRefusal Schedule(PartyFile f, IPartyWorld w, string place, int every, StaffEventKind kind)
        {
            PartyRefusal stand = PartyDesk.Stand(w);
            if (stand != PartyRefusal.None)
            {
                return stand;
            }
            if (string.IsNullOrEmpty(place) || w.Staff(place, new List<StaffHand>(8)) < 0)
            {
                return PartyRefusal.NoBusiness;
            }
            StaffPlan plan = f.Plan(place, true);
            plan.Every = Array.IndexOf(PartyCatalogue.ScheduleDays, every) >= 0 ? every : 0;
            plan.Kind = (StaffEventKind)Index(kind);
            plan.Since = w.Day;
            plan.Tried = -1000;
            if (plan.Every == 0)
            {
                f.Plans.Remove(plan);
            }
            return PartyRefusal.None;
        }

        /// <summary>
        /// The managers' round, once a day: a business whose event falls due tomorrow (so many days after its last one, or
        /// after the schedule was set) has its manager book it for tomorrow by the very rules the player books by. With no
        /// manager it waits; a refusal is told once a week.
        /// </summary>
        public static void ManagerDay(PartyFile f, IPartyWorld w)
        {
            if (PartyDesk.Stand(w) != PartyRefusal.None)
            {
                return;
            }
            int day = w.Day;
            for (int i = 0; i < f.Plans.Count; i++)
            {
                StaffPlan plan = f.Plans[i];
                if (plan.Every <= 0 || day + 1 < LastAt(f, plan.Place, plan.Since) + plan.Every || !w.Manager(plan.Place))
                {
                    continue;
                }
                var ask = new StaffAsk { Place = plan.Place, Kind = plan.Kind, Day = day + 1, Hour = PartyCatalogue.EventHourFrom[Index(plan.Kind)], ByManager = true };
                PartyRefusal why = Book(f, w, ask, out StaffEvent _);
                if (why != PartyRefusal.None && day - plan.Tried >= PartyCatalogue.ManagerRetryDays)
                {
                    plan.Tried = day;
                    w.Tell(PartyTone.Warn, "reallife_party_note_manager_refused", "business", w.Business(plan.Place) ?? string.Empty, "why", w.Why(why));
                }
            }
        }

        // The day of the newest event at a business that stands (planned, under way, held or held for nobody), or the day given with none.
        private static int LastAt(PartyFile f, string place, int none)
        {
            int last = none;
            for (int i = 0; i < f.Events.Count; i++)
            {
                StaffEvent e = f.Events[i];
                if (e.Place == place && e.State != StaffEventState.Cancelled && e.Day > last)
                {
                    last = e.Day;
                }
            }
            return last;
        }

        // ------------------------------------------------------------------------------------------------- the lift

        /// <summary>The points of standing a share of the cap is worth.</summary>
        public static int Standing(int used) => PartyCatalogue.CapStanding * PartyCatalogue.Clamp(used, 0, 1000) / 1000;

        // Brings the standing an employee holds from events to what is still felt of their lift today: up within the file's room, down as it fades.
        private static void Level(Lift lift, StaffFile file, int day)
        {
            int target = Standing(PartyRules.Faded(lift, day, PartyCatalogue.StaffWindowDays)), delta = target - lift.Stand;
            if (delta == 0)
            {
                return;
            }
            if (file == null)
            {
                lift.Stand = Math.Min(lift.Stand, target);   // nobody keeps their standing: nothing to add, and what was added went with the file
                return;
            }
            if (delta > 0)
            {
                int add = Math.Min(delta, CompanyCatalogue.StandingMost - file.Standing);
                if (add > 0)
                {
                    StaffRules.MoveStanding(file, add);
                    lift.Stand += add;
                }
                return;
            }
            StaffRules.MoveStanding(file, delta);
            lift.Stand = target;
        }

        /// <summary>
        /// One of the staff who came gets their share: the event's worth, half if they are underpaid, as far as their cap has
        /// room. The standing follows the lift; the game's satisfaction is given once and the game's own hourly rule wears
        /// it down again. Returns what was given, per mille of the cap.
        /// </summary>
        public static int LiftOne(PartyFile f, IPartyWorld w, StaffHand hand, StaffEventKind kind, int day)
        {
            int worth = PartyCatalogue.EventWorth[Index(kind)];
            if (hand.Underpaid)
            {
                worth = worth * PartyCatalogue.UnderpaidShare / 100;
            }
            Lift lift = f.Of(hand.Id, true);
            if (lift == null)
            {
                return 0;
            }
            int gain = PartyRules.Give(lift, day, PartyCatalogue.StaffWindowDays, worth);
            Level(lift, hand.File, day);
            if (gain > 0 && w != null && hand.Mood >= 0)
            {
                w.Mood(hand.Id, PartyCatalogue.CapMood * gain / 1000f);
            }
            return gain;
        }

        /// <summary>
        /// The day's fade: every lift lets go of the gains its window has left behind, and an employee's standing gives back
        /// what has worn off. The rivals' crews fade in their own day (<see cref="RivalDay"/>). A pass over the lifts, once a day.
        /// </summary>
        public static void Fade(PartyFile f, IPartyWorld w)
        {
            int day = w.Day;
            for (int i = f.Lifts.Count - 1; i >= 0; i--)
            {
                Lift lift = f.Lifts[i];
                if (lift.Person >= 0 && lift.Id.Length == 0)
                {
                    PartyRules.Prune(lift, day, PartyCatalogue.WindowDays);
                }
                else if (!lift.Id.StartsWith(RivalPrefix, StringComparison.Ordinal))
                {
                    PartyRules.Prune(lift, day, PartyCatalogue.StaffWindowDays);
                    Level(lift, w.File(lift.Id), day);
                }
                else
                {
                    continue;
                }
                if (lift.Gains.Count == 0 && lift.Stand <= 0)
                {
                    f.Lifts.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- the clock

        /// <summary>Whether one of the staff comes: never off sick; always in their working hours; outside them not somebody at the end of their patience, nor one in five by a draw.</summary>
        public static bool Comes(int seed, int eventId, int shut, StaffHand hand)
        {
            if (hand.Sick)
            {
                return false;
            }
            if (shut > 0)
            {
                return true;
            }
            if (hand.Mood >= 0 && hand.Mood <= PartyCatalogue.LowMood)
            {
                return false;
            }
            return PartyRules.Roll(seed, PartyRules.PurposeStaff, eventId, PartyRules.Hash(hand.Id)) >= PartyCatalogue.AfterHoursNo;
        }

        /// <summary>
        /// The clock's look at the events: one whose hour has come begins (the business shut for the opening hours it
        /// covers), one at its end is settled. <paramref name="off"/> is the game minute the option was last found off, or -1:
        /// an event that came due meanwhile is called off with everything back. True if anything changed.
        /// </summary>
        public static bool Turn(PartyFile f, IPartyWorld w, int now, int off)
        {
            bool changed = false;
            for (int i = f.Events.Count - 1; i >= 0; i--)
            {
                StaffEvent e = f.Events[i];
                if (e.State == StaffEventState.Planned && now >= e.Start)
                {
                    if (off >= 0 && e.Start >= off)
                    {
                        Withdraw(f, w, e, off, "reallife_party_note_event_off_while");   // the player's own doing, at the hour it was done
                    }
                    else
                    {
                        Begin(f, w, e, now);
                    }
                    changed = true;
                }
                else if (e.State == StaffEventState.Running && now >= e.End)
                {
                    Settle(f, w, e);
                    changed = true;
                }
            }
            if (changed)
            {
                Trim(f);
            }
            return changed;
        }

        // Whether one of the staff on the business now is one the event was booked for (an event from an older save knows no list: then everybody is).
        private static bool Booked(StaffEvent e, StaffHand hand)
        {
            return e.Crew.Count == 0 || e.Crew.Contains(hand.Id);
        }

        /// <summary>
        /// The event's hour has come. It begins only within <see cref="PartyCatalogue.EventLateMinutes"/> of its start: the clock's look is once a second, so only a time skip
        /// (a party, a job, a night's sleep) or a load goes past that, and an event whose hours passed that way was never held, and no closure is made for it after the fact:
        /// it is called off with everything back. A business no longer the player's is their doing, called off by the notice at this moment. Of those the event was booked for,
        /// the ones who have left the staff are not fed: their heads come back in full; with none left it does nothing and says so.
        /// </summary>
        private static void Begin(PartyFile f, IPartyWorld w, StaffEvent e, int now)
        {
            var hands = new List<StaffHand>(16);
            int heads = w.Staff(e.Place, hands);
            if (heads < 0)
            {
                Withdraw(f, w, e, now, "reallife_party_note_event_off_gone");
                return;
            }
            if (now - e.Start > PartyCatalogue.EventLateMinutes)
            {
                CallOff(f, w, e, "reallife_party_note_event_off_passed", StaffEventState.Cancelled);
                return;
            }
            int staying = 0;
            for (int i = 0; i < hands.Count; i++)
            {
                staying += Booked(e, hands[i]) ? 1 : 0;
            }
            if (staying == 0)
            {
                CallOff(f, w, e, "reallife_party_note_event_empty", StaffEventState.Empty);
                return;
            }
            int shut = w.Shut(e.Place, e.Day, e.Hour, e.End - e.Start);
            if (shut < 0)
            {
                CallOff(f, w, e, "reallife_party_note_event_off_open", StaffEventState.Cancelled);
                return;
            }
            int gone = e.Crew.Count > 0 ? e.Crew.Count - staying : 0;
            if (gone > 0 && e.Heads > 0)
            {
                int back = Math.Min(e.Paid, e.Paid / e.Heads * gone);
                if (back > 0)
                {
                    w.Refund(back, StaffKey, e.Place);
                }
                e.Paid -= back;
                w.Tell(PartyTone.Info, "reallife_party_note_event_short", "business", e.Name, "count", gone.ToString(), "back", w.Cash(back));
            }
            e.Shut = shut;
            e.State = StaffEventState.Running;
        }

        // The event's end: each of those it was booked for, still on the staff and able to come, gets their share. A business sold while it ran leaves nobody to count (the
        // event was held and stays paid); an event nobody could come to gives the money back and says so.
        private static void Settle(PartyFile f, IPartyWorld w, StaffEvent e)
        {
            var hands = new List<StaffHand>(16);
            int heads = w.Staff(e.Place, hands), day = w.Day, came = 0, counted = 0;
            for (int i = 0; i < hands.Count && heads > 0; i++)
            {
                if (!Booked(e, hands[i]))
                {
                    continue;
                }
                counted++;
                if (Comes(f.Seed, e.Id, e.Shut, hands[i]))
                {
                    came++;
                    LiftOne(f, w, hands[i], e.Kind, day);
                }
            }
            e.Came = came;
            if (heads < 0)
            {
                e.State = StaffEventState.Held;
                w.Tell(PartyTone.Warn, "reallife_party_note_event_sold", "business", e.Name);
                return;
            }
            if (came > 0)
            {
                e.State = StaffEventState.Held;
                w.Tell(PartyTone.Good, "reallife_party_note_event_held", "business", e.Name, "count", came.ToString(), "staff", counted.ToString(), "hours", e.Shut.ToString());
                return;
            }
            CallOff(f, w, e, "reallife_party_note_event_nobody", StaffEventState.Empty, counted);
        }

        /// <summary>Keeps the events bounded: every one still to come, and the newest that are over.</summary>
        public static void Trim(PartyFile f)
        {
            int over = 0;
            for (int i = f.Events.Count - 1; i >= 0; i--)
            {
                StaffEvent e = f.Events[i];
                if (e.State != StaffEventState.Planned && e.State != StaffEventState.Running && ++over > PartyCatalogue.EventsKept)
                {
                    f.Events.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- the rivals

        /// <summary>The id a rival's crew member's lift is kept under: never one of the player's own staff.</summary>
        public static string RivalId(string outlet, string id) => RivalPrefix + outlet + "/" + id;

        /// <summary>
        /// The rival tycoons' outlets' day (the NPC rule: what the player can do, rivals do too): each outlet's crew fades as
        /// the player's staff do, and an outlet whose time has come (every <see cref="PartyCatalogue.RivalEveryDays"/> days
        /// and a few more, by its own draw) holds a staff event of a drawn kind, paid from its tycoon's fortune, lifting its
        /// crew's standing by the very rule the player's is lifted by (<see cref="LiftOne"/>). An outlet whose tycoon cannot
        /// pay out of the worth above its floor (<c>RivalRules.At(StartWorth, level)</c>, the least the rivals' day keeps a fortune at) holds none. Returns what was
        /// paid. Read from the files only: the same day gives the same on every machine.
        /// </summary>
        public static long RivalDay(PartyFile f, RivalFile rivals, int day, int level)
        {
            long all = 0;
            for (int i = 0; rivals != null && i < rivals.Outlets.Count; i++)
            {
                RivalOutlet o = rivals.Outlets[i];
                Tycoon t = rivals.Tycoons[PartyCatalogue.Clamp(o.Tycoon, 0, rivals.Tycoons.Length - 1)];
                string key = RivalFirms.Key(o);
                for (int c = 0; c < o.Crew.Count; c++)
                {
                    Lift lift = f.Of(RivalId(key, o.Crew[c].Id), false);
                    if (lift != null)
                    {
                        PartyRules.Prune(lift, day, PartyCatalogue.StaffWindowDays);
                        Level(lift, o.Crew[c], day);
                    }
                }
                if (!t.Active || o.Crew.Count == 0)
                {
                    continue;
                }
                int mark = LastRival(f, key);
                if (mark < 0)
                {
                    RivalMove move = rivals.Move(o.Move);
                    mark = f.Mark(key, move != null ? move.Land : day);   // counted from its landing, or from the day it was first seen with no landing kept: saved, so it is a day to count from
                }
                int gap = PartyCatalogue.RivalEveryDays + PartyRules.Roll(f.Seed, PartyRules.PurposeRivalWhen, PartyRules.Hash(key), mark) * PartyCatalogue.RivalSpreadDays / 1000;
                if (day - mark < gap)
                {
                    continue;
                }
                int draw = PartyRules.Roll(f.Seed, PartyRules.PurposeRivalKind, PartyRules.Hash(key), day);
                StaffEventKind kind = draw < PartyCatalogue.RivalLunch ? StaffEventKind.Lunch
                    : draw < PartyCatalogue.RivalLunch + PartyCatalogue.RivalEvening ? StaffEventKind.EveningOut : StaffEventKind.TeamDay;
                int cost = Cost(kind, level, o.Crew.Count);
                if (t.Worth - RivalRules.At(t.Def.StartWorth, level) < cost)
                {
                    continue;   // only from worth above the floor the rivals' own growth lifts a fortune back to every day: money under it would come straight back
                }
                t.Worth -= cost;
                all += cost;
                int came = 0;
                for (int c = 0; c < o.Crew.Count; c++)
                {
                    StaffFile crew = o.Crew[c];
                    var hand = new StaffHand
                    {
                        Id = RivalId(key, crew.Id), Name = crew.Name, Mood = -1, Underpaid = StaffRules.Underpaid(crew, PartyCatalogue.RivalLevel), File = crew
                    };
                    // A lunch and a team day are in the outlet's hours; an evening out is after them, and some have a reason, as the player's staff do.
                    if (Comes(f.Seed, day, kind == StaffEventKind.EveningOut ? 0 : 1, hand))
                    {
                        came++;
                        LiftOne(f, null, hand, kind, day);
                    }
                }
                f.Rivals.Add(new RivalEvent { Day = day, Tycoon = o.Tycoon, Outlet = key, District = o.District ?? string.Empty, Kind = kind, Heads = came, Cost = cost });
            }
            TrimRivals(f, rivals);
            return all;
        }

        // The day of an outlet's newest event; -1 with none.
        private static int LastRival(PartyFile f, string outlet)
        {
            int last = -1;
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                if (f.Rivals[i].Outlet == outlet && f.Rivals[i].Day > last)
                {
                    last = f.Rivals[i].Day;
                }
            }
            return last;
        }

        // Bounded: events of outlets that closed go, with their crews' lifts, then the oldest that is not its outlet's newest.
        private static void TrimRivals(PartyFile f, RivalFile rivals)
        {
            for (int i = f.Marks.Count - 1; i >= 0; i--)
            {
                if (!Stands(rivals, f.Marks[i].Outlet))
                {
                    f.Marks.RemoveAt(i);
                }
            }
            for (int i = f.Rivals.Count - 1; i >= 0; i--)
            {
                if (!Stands(rivals, f.Rivals[i].Outlet))
                {
                    f.Rivals.RemoveAt(i);
                }
            }
            for (int i = f.Lifts.Count - 1; i >= 0; i--)
            {
                Lift lift = f.Lifts[i];
                if (lift.Person < 0 && lift.Id.StartsWith(RivalPrefix, StringComparison.Ordinal) && ((lift.Gains.Count == 0 && lift.Stand <= 0) || !CrewStands(rivals, lift.Id)))
                {
                    f.Lifts.RemoveAt(i);
                }
            }
            for (int i = 0; i < f.Rivals.Count && f.Rivals.Count > PartyCatalogue.RivalLogMost; i++)
            {
                if (LastRival(f, f.Rivals[i].Outlet) > f.Rivals[i].Day)
                {
                    f.Rivals.RemoveAt(i--);
                }
            }
        }

        private static bool Stands(RivalFile rivals, string outlet)
        {
            for (int i = 0; rivals != null && i < rivals.Outlets.Count; i++)
            {
                if (RivalFirms.Key(rivals.Outlets[i]) == outlet)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool CrewStands(RivalFile rivals, string liftId)
        {
            for (int i = 0; rivals != null && i < rivals.Outlets.Count; i++)
            {
                RivalOutlet o = rivals.Outlets[i];
                string key = RivalFirms.Key(o);
                for (int c = 0; c < o.Crew.Count; c++)
                {
                    if (RivalId(key, o.Crew[c].Id) == liftId)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
