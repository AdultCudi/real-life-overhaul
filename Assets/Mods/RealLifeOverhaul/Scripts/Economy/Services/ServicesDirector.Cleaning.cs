using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A visit of today's round as the director keeps it: by the cleaner's id and the client's place, so that it stays right when the round is planned again.</summary>
    public sealed class DayVisit
    {
        public string CleanerId, CleanerName, Place;
        public int Slot;                 // which of the client's fixed visits; -1: as needed
        public int Trip;                 // visits with the same number are one time away from base
        public int Leave, Start, End;    // minutes of today
        public int Rate;                 // hundredths an hour, as planned
        public int Metres;               // the way to it
        public int HomeMetres, HomeMinutes;   // the way from it back to the cleaner's base
        public bool Walked;              // the way to it has been counted
        public bool HomeWalked;          // ...and the way home from it
        public bool Skipped;             // the cleaner was not at work when its hour came
        public int Done;                 // minutes of it that have been cleaned (or passed over) so far
    }

    /// <summary>
    /// The cleaning service's part of the director: the round, what a visit does each hour, the day's bookkeeping,
    /// contracts, and what the page asks and changes.
    ///
    /// THE CREW are cleaners the player has hired through the game and assigned, in the game, to any building of
    /// theirs with cleaning shifts in its schedule. Those shifts are their working hours and the game pays them for
    /// those hours as it always does; the mod only marks them as crew. During a shift the round takes a cleaner out
    /// to the businesses on the list; whatever time is left they clean their own building, as the game has it.
    ///
    /// THE ROUND is planned again every game hour for the rest of the day (RoundRules: which visit can begin
    /// soonest), keeping every visit a cleaner has already set out for. So a floor that falls under its threshold,
    /// a shift the player changes and a cleaner who is off sick all show in the same hour.
    ///
    /// A VISIT'S CLEANING is done on the game's hour event for the minutes of that hour the cleaner is there, by the
    /// game's own sum, on the game's own tiles (ServicesGame.Clean); a client under contract has the same sum taken
    /// off the figure the mod keeps for its floor. For the minutes a cleaner is out, their own building does not get
    /// their work (ServicesGame.Hold).
    /// </summary>
    public sealed partial class ServicesDirector
    {
        private static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();

        private readonly List<CrewMember> _crew = new List<CrewMember>(ServicesCatalogue.CrewMost);
        private readonly List<CrewMember> _crewPool = new List<CrewMember>(ServicesCatalogue.CrewMost);
        private readonly List<RoundCleaner> _shifts = new List<RoundCleaner>(ServicesCatalogue.CrewMost);
        private readonly List<int> _shiftTrip = new List<int>(ServicesCatalogue.CrewMost);
        private readonly List<RoundJob> _jobs = new List<RoundJob>(32);
        private readonly List<RoundVisit> _planned = new List<RoundVisit>(32);
        private readonly List<DayVisit> _visits = new List<DayVisit>(32);
        private readonly List<DayVisit> _visitPool = new List<DayVisit>(32);
        private readonly List<ContractCandidate> _candidates = new List<ContractCandidate>(64);
        private readonly TravelCache _cache = new TravelCache();
        private readonly Dictionary<string, int> _awayCounted = new Dictionary<string, int>(ServicesCatalogue.CrewMost);   // minutes away from base today that each cleaner's own building has been kept from being cleaned for

        private int _trip;
        private bool _resuming;                    // a look for the visiting cleaner is waiting for the end of this frame
        private BuildingRegistration _insideReg;   // the building the player was last seen in, and its place: worked out once per building
        private string _insidePlace;

        /// <summary>The crew as they were last read.</summary>
        public IReadOnlyList<CrewMember> Crew => _crew;

        /// <summary>Today's visits: made, under way and planned.</summary>
        public IReadOnlyList<DayVisit> Visits => _visits;

        // ---------------------------------------------------------------- reading the game

        // The crew and every client as the game has them now. One look-up per cleaner, one walk over the tiles of each shop.
        private void ReadAll()
        {
            int dropped;
            ServicesGame.ReadCrew(_state, _crew, _crewPool, out dropped);
            for (int i = 0; i < _state.Clients.Count; i++)
            {
                ServicesGame.Read(_state.Clients[i]);
            }
            if (dropped > 0)
            {
                Logger?.Info("Cleaning service: " + dropped + " cleaner(s) taken off the crew: no longer employed, or no longer a cleaner.");
            }
        }

        private CrewMember Member(string id)
        {
            for (int i = 0; i < _crew.Count; i++)
            {
                if (string.Equals(_crew[i].Id, id, StringComparison.Ordinal))
                {
                    return _crew[i];
                }
            }
            return null;
        }

        private void CleaningStart(bool otherDay)
        {
            ReadAll();
            for (int i = 0; otherDay && i < _state.Clients.Count; i++)
            {
                // The save was last written on another day: today's figures begin now, and nothing is measured across the gap.
                CleaningClient c = _state.Clients[i];
                c.MinutesBefore = 0;
                c.MinutesToday = c.CleanedToday = 0;
                c.WorstStart = c.Worst;
                c.CleanBefore = c.Now;
            }
            if (otherDay)
            {
                _state.VanMetres = 0;
            }
            Replan("the service started");
        }

        // The cleaning service stops (its option, a fault): no shift of the mod's stays in a schedule and no round is kept.
        private void CleaningStop()
        {
            CleaningPresence.Clear(true);
            RoundDrop();
        }

        private void RoundDrop()
        {
            for (int i = 0; i < _visits.Count; i++)
            {
                _visitPool.Add(_visits[i]);
            }
            _visits.Clear();
            _planned.Clear();
            _jobs.Clear();
            _shifts.Clear();
            _trip = 0;
            _awayCounted.Clear();
            _insideReg = null;
            _insidePlace = null;
        }

        // ---------------------------------------------------------------- the round

        /// <summary>
        /// Plans the rest of today's round from this minute. Every visit a cleaner has already set out for is kept;
        /// the rest are made again from what is due now. Says so with "Developer diagnostics" on when a reason is given.
        /// </summary>
        private void Replan(string why)
        {
            if (!CleaningOn || Save == null)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            int now = ServicesGame.Now, before = _visits.Count;
            for (int i = _visits.Count - 1; i >= 0; i--)
            {
                if (_visits[i].Leave > now)
                {
                    _visitPool.Add(_visits[i]);
                    _visits.RemoveAt(i);
                }
            }
            int kept = _visits.Count;
            List<CleaningClient> clients = _state.Clients;
            for (int i = 0; i < clients.Count; i++)
            {
                clients[i].DoneMask = 0;
                for (int k = 0; clients[i].LastDay == Save.Day && k < ServicesCatalogue.VisitsMost; k++)
                {
                    // A fixed visit whose hour had come when a cleaner was last in today has been made (this is what is known of the day after a load).
                    clients[i].DoneMask |= clients[i].LastMinute >= clients[i].Hours[k] * 60 ? 1 << k : 0;
                }
                clients[i].Pending = false;
                clients[i].NextStart = -1;
                clients[i].NextBy = null;
                clients[i].Unserved = 0;
            }
            for (int i = 0; i < _visits.Count; i++)
            {
                DayVisit v = _visits[i];
                CleaningClient c = _state.Client(v.Place);
                if (c != null)
                {
                    c.DoneMask |= v.Slot >= 0 ? 1 << v.Slot : 0;
                    c.Pending |= v.End > now;
                }
            }
            ServicesGame.Shifts(_crew, _shifts);
            _shiftTrip.Clear();
            for (int s = 0; s < _shifts.Count; s++)
            {
                // Somebody who is out when the round is planned again goes on from where they are.
                RoundCleaner shift = _shifts[s];
                DayVisit last = null;
                for (int i = 0; i < _visits.Count; i++)
                {
                    DayVisit v = _visits[i];
                    if (string.Equals(v.CleanerId, shift.Id, StringComparison.Ordinal) && v.Leave >= shift.From && v.Leave < shift.To && (last == null || v.End > last.End))
                    {
                        last = v;
                    }
                }
                CleaningClient at = last != null && last.End + last.HomeMinutes > now ? _state.Client(last.Place) : null;
                if (at != null)
                {
                    shift.Out = true;
                    shift.AtX = at.X;
                    shift.AtZ = at.Z;
                    shift.FreeFrom = Math.Max(last.End, now);
                    _shiftTrip.Add(last.Trip);
                }
                else
                {
                    _shiftTrip.Add(-1);
                }
            }
            if (_cache.Count > 4096)
            {
                _cache.Clear();   // (doors change only when a business does: this is a guard, not a rhythm)
            }
            RoundRules.Jobs(clients, now, _jobs);
            _planned.Clear();
            int taken = RoundRules.Plan(_shifts, _jobs, _cache, _state.Van, now, _planned);
            for (int i = 0; i < _planned.Count; i++)
            {
                RoundVisit p = _planned[i];
                RoundCleaner shift = _shifts[p.Cleaner];
                CleaningClient c = clients[p.Client];
                DayVisit v;
                if (_visitPool.Count > 0)
                {
                    v = _visitPool[_visitPool.Count - 1];
                    _visitPool.RemoveAt(_visitPool.Count - 1);
                }
                else
                {
                    v = new DayVisit();
                }
                if (_shiftTrip[p.Cleaner] < 0)
                {
                    _shiftTrip[p.Cleaner] = ++_trip;
                }
                v.CleanerId = shift.Id;
                v.CleanerName = shift.Name;
                v.Place = c.Place;
                v.Slot = p.Slot;
                v.Trip = _shiftTrip[p.Cleaner];
                v.Leave = p.Leave;
                v.Start = p.Start;
                v.End = p.End;
                v.Rate = p.Rate;
                v.Metres = p.Metres;
                v.HomeMetres = _cache.Metres(c.X, c.Z, shift.X, shift.Z);
                v.HomeMinutes = TravelRules.Minutes(v.HomeMetres, _state.Van);
                v.Walked = v.HomeWalked = v.Skipped = false;
                v.Done = 0;
                _visits.Add(v);
            }
            int left = 0;
            for (int j = 0; j < _jobs.Count; j++)
            {
                if (!_jobs[j].Taken)
                {
                    clients[_jobs[j].Client].Unserved = _jobs[j].Why;
                    left++;
                }
            }
            for (int i = 0; i < _visits.Count; i++)
            {
                DayVisit v = _visits[i];
                CleaningClient c = v.End > now ? _state.Client(v.Place) : null;
                if (c != null)
                {
                    c.Pending = true;
                    if (c.NextStart < 0 || v.Start < c.NextStart)
                    {
                        c.NextStart = v.Start;
                        c.NextEnd = v.End;
                        c.NextBy = v.CleanerName;
                    }
                }
            }
            if (Verbose && (why != null || _visits.Count != before))
            {
                Logger?.Info("Cleaning check (round, " + (why ?? "the hour") + "): from " + Clock(now) + ", " + _shifts.Count + " shift(s) of " + _crew.Count + " cleaner(s), " + _jobs.Count + " visit(s) due, "
                             + taken + " planned and " + left + " left over" + (left > 0 ? " (" + LeftOver() + ")" : string.Empty) + ", " + kept + " kept from before; " + (_state.Van ? "by van" : "on foot") + ", "
                             + _cache.Misses + " distance(s) worked out and " + _cache.Hits + " kept; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " ms: PASS");
            }
        }

        private string LeftOver()
        {
            int none = 0, far = 0, time = 0;
            for (int j = 0; j < _jobs.Count; j++)
            {
                if (!_jobs[j].Taken)
                {
                    none += _jobs[j].Why == RoundRules.NoCleaner ? 1 : 0;
                    far += _jobs[j].Why == RoundRules.TooFar ? 1 : 0;
                    time += _jobs[j].Why == RoundRules.NoTime ? 1 : 0;
                }
            }
            return none + " with nobody on shift, " + far + " too far, " + time + " with no time left";
        }

        private static string Clock(int minute)
        {
            return (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");
        }

        // The minutes of today, up to a minute, that a cleaner is away from base: for each trip, from setting out until back from its last visit.
        private int Away(string id, int upTo)
        {
            int away = 0;
            for (int i = 0; i < _visits.Count; i++)
            {
                DayVisit first = _visits[i];
                if (!string.Equals(first.CleanerId, id, StringComparison.Ordinal))
                {
                    continue;
                }
                int from = first.Leave, back = first.End + first.HomeMinutes, lastEnd = first.End;
                bool isFirst = true;
                for (int k = 0; k < _visits.Count && isFirst; k++)
                {
                    DayVisit other = _visits[k];
                    if (k == i || other.Trip != first.Trip)   // (a trip's number is one cleaner's)
                    {
                        continue;
                    }
                    isFirst = other.Leave > first.Leave || (other.Leave == first.Leave && k > i);
                    if (other.End >= lastEnd)
                    {
                        lastEnd = other.End;
                        back = other.End + other.HomeMinutes;
                    }
                }
                away += isFirst ? RoundRules.Overlap(from, back, 0, upTo) : 0;
            }
            return away;
        }

        // ---------------------------------------------------------------- the hour

        // The hour that is beginning: the round is brought up to date, and every visit is cleaned for its minutes up to
        // the end of this hour that have not been cleaned yet (as the game cleans an hour of a shift at the hour's
        // start). Usually those are this hour's minutes; a visit planned in the middle of the last hour, because the
        // player changed something, brings that hour's minutes with it. The same count is kept of the minutes each
        // cleaner is away from their own building.
        // Runs on the game's hour event (on the day event for the hour that opens a day), before the game's own hourly cleaning.
        private void CleaningHour(int hour, bool newDay)
        {
            CleaningPresence.Clear(false);   // (before the game's hourly cleaning, so that the shift shown to the player is never counted by it)
            if (!newDay)
            {
                ReadAll();
            }
            Replan(null);
            int day = Save.Day, hourStart = hour * 60, on = 0, minutes = 0, skipped = 0, held = 0, heldMinutes = 0;
            for (int i = 0; i < _visits.Count; i++)
            {
                DayVisit v = _visits[i];
                if (!v.Walked && v.Leave < hourStart + 60)
                {
                    v.Walked = true;
                    _state.VanMetres += _state.Van ? v.Metres : 0;
                }
                if (!v.HomeWalked && v.End + v.HomeMinutes <= hourStart + 60 && LastOfTrip(v))
                {
                    v.HomeWalked = true;
                    _state.VanMetres += _state.Van ? v.HomeMetres : 0;
                }
                int m = RoundRules.Due(v.Start, v.End, v.Done, hourStart);
                if (m <= 0)
                {
                    continue;
                }
                v.Done += m;
                CrewMember member = Member(v.CleanerId);
                CleaningClient c = _state.Client(v.Place);
                if (member == null || member.Employee == null || c == null || c.Gone)
                {
                    continue;
                }
                if (!member.Employee.IsWorking(true, WorkShiftType.Cleaning))
                {
                    v.Skipped = true;   // off sick, or their shift at base is no longer what it was when they were sent
                    skipped++;
                    continue;
                }
                int off;
                if (c.Outside)
                {
                    int was = c.Dirt;
                    ContractRules.Visit(c, CleaningRules.Rate(member.Skill, member.Mood, c.Force), m);
                    off = was - c.Dirt;
                }
                else
                {
                    off = ServicesGame.Clean(c, member.Employee, m);
                }
                ServicesGame.Read(c);
                c.MinutesToday += m;
                c.CleanedToday += off;
                c.LastDay = day;
                c.LastMinute = Math.Max(v.Start, hourStart);
                c.LastCleaner = ServicesStore.Clean(member.Name);
                on++;
                minutes += m;
            }
            for (int i = 0; i < _crew.Count; i++)
            {
                // What their own building has not yet been kept from being cleaned for, an hour of it at most now: the game takes an hour off it at a time.
                int total = Away(_crew[i].Id, hourStart + 60), counted;
                _awayCounted.TryGetValue(_crew[i].Id, out counted);
                int away = Math.Min(60, total - counted);
                if (away <= 0)
                {
                    continue;
                }
                if (ServicesGame.Hold(_crew[i], away) > 0)
                {
                    _awayCounted[_crew[i].Id] = counted + away;
                    held++;
                    heldMinutes += away;
                }
                else
                {
                    _awayCounted[_crew[i].Id] = total;   // the game is not cleaning their building with them this hour: there is nothing to keep back
                }
            }
            if (BuildingManager.IsInsideBuilding && !_resuming && !CleaningPresence.Broken && isActiveAndEnabled)
            {
                _resuming = true;
                StartCoroutine(RealLifeOverhaul.Guard.Routine(PresenceAfterTick()));   // (the class's own Guard is a method: Core/Guard by its full name)
            }
            if (Verbose && (on > 0 || skipped > 0 || held > 0))
            {
                Logger?.Info("Cleaning check (hour " + hour.ToString("00") + ":00): " + on + " visit(s) on, " + minutes + " minute(s) cleaned by the game's own sum" + (skipped > 0 ? ", " + skipped + " not made (the cleaner is not at work)" : string.Empty)
                             + "; " + held + " base(s) kept from being cleaned for " + heldMinutes + " minute(s) their cleaner is out" + (_state.Van ? "; " + _state.VanMetres + " m driven today" : string.Empty) + ": PASS");
            }
            if (on > 0 || skipped > 0)
            {
                Changed();
            }
        }

        private bool LastOfTrip(DayVisit v)
        {
            for (int i = 0; i < _visits.Count; i++)
            {
                DayVisit other = _visits[i];
                if (!ReferenceEquals(other, v) && other.Trip == v.Trip && other.End > v.End)
                {
                    return false;
                }
            }
            return true;
        }

        // ---------------------------------------------------------------- a visiting cleaner seen in the shop

        // At the end of the frame in which the hour turned: after the game's hourly cleaning, and before the game's
        // cleaning stations look for their cleaner (a frame later). A cleaner whose visit goes on is put back for the
        // new hour, so the station finds the same person and leaves them where they are.
        private IEnumerator PresenceAfterTick()
        {
            yield return EndOfFrame;
            _resuming = false;
            try
            {
                if (CleaningOn && Save != null)
                {
                    PresenceLook();
                }
            }
            catch (Exception e)
            {
                CleaningFault("showing a visiting cleaner", e);
            }
        }

        // Once a second: if the player stands in a shop where a visit is on now, the cleaner is shown there; else nobody is.
        private void PresenceLook()
        {
            if (CleaningPresence.Broken)
            {
                return;
            }
            if (!BuildingManager.IsInsideBuilding)
            {
                if (CleaningPresence.Showing || CleaningPresence.Leaving)
                {
                    CleaningPresence.Clear(false);   // the building has been left: its stations are gone, only the shift is taken out
                    CleaningPresence.Tidy();
                }
                _insideReg = null;
                return;
            }
            if (_visits.Count == 0 && !CleaningPresence.Showing && !CleaningPresence.Leaving)
            {
                return;
            }
            BuildingRegistration reg = InstanceBehavior<BuildingManager>.Instance.buildingRegistration;
            if (reg == null)
            {
                CleaningPresence.Clear(true);
                return;
            }
            if (!ReferenceEquals(reg, _insideReg))
            {
                _insideReg = reg;
                _insidePlace = reg.HasValidAddress ? ServicesGame.Place(reg) : null;
            }
            int now = ServicesGame.Now;
            DayVisit on = null;
            for (int i = 0; _insidePlace != null && i < _visits.Count; i++)
            {
                DayVisit v = _visits[i];
                if (v.Start <= now && now < v.End && !v.Skipped && string.Equals(v.Place, _insidePlace, StringComparison.Ordinal))
                {
                    on = v;
                    break;
                }
            }
            CrewMember member = on != null ? Member(on.CleanerId) : null;
            // (at their own base the game shows them by their own shift)
            if (member == null || member.Employee == null || ReferenceEquals(member.Base, reg) || !member.Employee.IsWorking(true, WorkShiftType.Cleaning))
            {
                CleaningPresence.Clear(true);
                return;
            }
            bool was = CleaningPresence.Showing;
            bool shown = CleaningPresence.Show(reg, member.Employee, Save.Hour);
            if (Verbose && shown && !was)
            {
                Logger?.Info("Cleaning check (a visiting cleaner): " + member.Name + " is visiting '" + ServicesGame.Name(reg) + "' until " + Clock(on.End) + " and the player is inside: a shift of this hour only is in the building's schedule on a free cleaning station, and the station has put them in the room: PASS");
            }
        }

        // ---------------------------------------------------------------- the day

        // The morning: yesterday's van, each business's daily dirt, contracts, a new offer. Then the new day's round (the caller does the hour).
        private void CleaningDay(int day, bool next)
        {
            int slot = ((day % 7) + 7) % 7, slotBefore = (((day - 1) % 7) + 7) % 7;
            int cost = _state.Van ? TravelRules.Cost(_state.VanMetres, ServicesGame.Index) : 0;
            if (cost > 0)
            {
                Charge(cost, ServicesGame.VanKey, ServicesText.Distance(_state.VanMetres), null, day - 1, true, true, _state.VanMetres + " m driven on day " + (day - 1) + " at " + ServicesCatalogue.VanCostPerKm + " a kilometre, price index " + ServicesGame.Index);
            }
            _state.VanCost[slotBefore] = cost;
            _state.VanMetres = 0;
            _state.FeeIncome[slot] = 0;
            RoundDrop();
            ReadAll();
            for (int i = _state.Clients.Count - 1; i >= 0; i--)
            {
                CleaningClient c = _state.Clients[i];
                if (c.Gone)
                {
                    _state.Clients.RemoveAt(i);
                    Logger?.Info("Cleaning service: '" + c.Name + "' is taken off the round: " + (c.Outside ? "the business has closed or changed hands, and its contract ends with it" : "it is no longer a business of the player's that the game cleans") + ".");
                    if (c.Outside)
                    {
                        Tell(NotificationType.Warning, "reallife_svc_note_contract_gone", TabContracts, null, "business", c.Name);
                    }
                    continue;
                }
                if (c.Outside)
                {
                    int before = c.Now;
                    bool under = ContractRules.Morning(c);
                    c.CleanBefore = before;
                    c.NeedDay = c.DirtDay;
                    int fee, penalty;
                    ContractWeek week = ContractRules.Week(c, day, out fee, out penalty);
                    if (week == ContractWeek.Paid)
                    {
                        Pay(fee, ServicesGame.FeeKey, c.Name, null, null, "the week's fee: the floor was under its standard of " + c.Standard + " on " + ServicesCatalogue.ContractDaysUnderMost + " morning(s) at most");
                        _state.FeeIncome[slot] += fee;
                        Tell(NotificationType.Success, "reallife_svc_note_fee", TabContracts, "con:" + c.Place, "business", c.Name, "amount", ServicesText.Money(fee));
                    }
                    else if (week == ContractWeek.Missed || week == ContractWeek.Lost)
                    {
                        Charge(penalty, ServicesGame.PenaltyKey, c.Name, null, null, false, true, "a week missed: no fee, and " + ServicesCatalogue.ContractPenaltyPct + "% of it as a penalty; " + c.Missed + " week(s) missed running");
                        _state.FeeIncome[slot] -= penalty;
                        if (week == ContractWeek.Lost)
                        {
                            _state.Clients.RemoveAt(i);
                            Tell(NotificationType.Error, "reallife_svc_note_contract_lost", TabContracts, null, "business", c.Name, "amount", ServicesText.Money(penalty));
                            continue;
                        }
                        Tell(NotificationType.Warning, "reallife_svc_note_week_missed", TabContracts, "con:" + c.Place, "business", c.Name, "amount", ServicesText.Money(penalty), "standard", c.Standard.ToString());
                    }
                    if (Verbose)
                    {
                        Logger?.Info("Cleaning check (contract, " + c.Name + ", day " + day + "): this morning's floor " + before + " against a standard of " + c.Standard + (under ? " (under)" : string.Empty) + ", " + c.DaysUnder
                                     + " morning(s) under this week, then " + c.DirtDay + " hundredths of new dirt (" + c.Dirt + " now); week " + week + ": PASS");
                    }
                    ServicesGame.Read(c);
                }
                else
                {
                    if (next)
                    {
                        c.NeedDay = CleaningRules.NeedDay(c.NeedDay, c.WorstStart, c.Worst, c.CleanedToday);
                    }
                    c.WorstStart = c.Worst;
                    c.CleanBefore = c.Clean;
                }
                c.MinutesBefore = c.MinutesToday;
                c.MinutesToday = 0;
                c.CleanedToday = 0;
            }
            int lapsed = ContractRules.Expire(_state, day);
            if (_state.Crew.Count > 0 && ContractRules.OfferDue(_state, day))
            {
                ServicesGame.Candidates(_candidates);
                CleaningOffer offer = ContractRules.Draw(_state, _candidates, day, ServicesGame.Index);
                if (Verbose)
                {
                    Logger?.Info("Cleaning check (contract offer, day " + day + "): " + _candidates.Count + " business(es) of others that the game would clean, " + lapsed + " offer(s) lapsed, "
                                 + (offer != null ? "'" + offer.Name + "' asks: " + offer.Fee + " a week for a floor kept at " + offer.Standard + ", open until day " + offer.Until : "nobody asks today") + ": PASS");
                }
                _candidates.Clear();
                if (offer != null)
                {
                    Tell(NotificationType.Info, "reallife_svc_note_offer", TabContracts, "offer:" + offer.Place, "business", offer.Name, "amount", ServicesText.Money(offer.Fee));
                }
            }
        }

        // ---------------------------------------------------------------- what the page asks

        /// <summary>The service's own cost a week: the crew's wages for the hours the game has them down for, and the van over the last seven days.</summary>
        public int ServiceWeek(out int wages, out int van)
        {
            long sum = 0;
            for (int i = 0; i < _crew.Count; i++)
            {
                sum += (long)_crew[i].Wage * _crew[i].WeekHours / 100;
            }
            wages = (int)Math.Min(int.MaxValue / 4, sum);
            van = ServicesState.Week(_state.VanCost);
            return wages + van;
        }

        /// <summary>What a middling member of the crew takes off a floor of this force in an hour; the contracts' reference cleaner while there is no crew.</summary>
        public int CrewRate(int force)
        {
            long sum = 0;
            for (int i = 0; i < _crew.Count; i++)
            {
                sum += CleaningRules.Rate(_crew[i].Skill, _crew[i].Mood, force);
            }
            return _crew.Count > 0 ? (int)(sum / _crew.Count) : ServicesCatalogue.ContractSkillRef / Math.Max(1, force);
        }

        /// <summary>The minutes of cleaning a day a client needs from such a cleaner; 0 until a day has been measured.</summary>
        public int NeedMinutes(CleaningClient c)
        {
            return CleaningRules.NeedMinutes(c.NeedDay, CrewRate(c.Force));
        }

        /// <summary>What one cleaner at each of the player's own shops on the round would cost a week, at the wage the game offers a cleaner as good as the crew.</summary>
        public int OwnWeek()
        {
            int skill = 0;
            for (int i = 0; i < _crew.Count; i++)
            {
                skill += _crew[i].Skill;
            }
            int wage = ServicesGame.GoingWage(_crew.Count > 0 ? skill / _crew.Count : 50);
            long sum = 0;
            for (int i = 0; i < _state.Clients.Count; i++)
            {
                sum += _state.Clients[i].Outside ? 0 : CleaningRules.OwnWeek(NeedMinutes(_state.Clients[i]), wage);
            }
            return (int)Math.Min(int.MaxValue / 4, sum);
        }

        /// <summary>The fees a week of the contracts that are running.</summary>
        public int ContractWeekFees()
        {
            int sum = 0;
            for (int i = 0; i < _state.Clients.Count; i++)
            {
                sum += _state.Clients[i].Outside ? _state.Clients[i].Fee : 0;
            }
            return sum;
        }

        // ---------------------------------------------------------------- what the page changes

        private bool Ready(bool cleaning)
        {
            return _running && !_broken && Save != null && (cleaning ? CleaningOn : SaleOn);
        }

        private void Guard(string what, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                CleaningFault(what, e);
            }
        }

        /// <summary>A shop of the player's goes on the round, served as needed until the player sets hours.</summary>
        public void AddClient(string place)
        {
            if (!Ready(true))
            {
                return;
            }
            Guard("adding a business", () =>
            {
                BuildingRegistration reg = ServicesGame.Find(place);
                if (!ServicesGame.Cleaned(reg) || _state.Client(place) != null)
                {
                    return;
                }
                if (_state.Clients.Count >= ServicesCatalogue.ClientsMost)
                {
                    Notifications.ShowError("reallife_svc_refuse_full");
                    return;
                }
                var c = new CleaningClient { Place = place };
                _state.Clients.Add(c);
                ServicesGame.Read(c);
                c.WorstStart = c.Worst;
                c.CleanBefore = c.Clean;
                Logger?.Info("Cleaning service: '" + c.Name + "' is on the round, served as needed under a cleanliness of " + c.Threshold + " (now " + c.Clean + "; its own schedule has " + ServicesGame.OwnCleaningHours(reg) + " cleaning hour(s) a week).");
                Replan("a business was added");
                Changed();
            });
        }

        /// <summary>A shop of the player's is taken off the round. A contract is ended with <see cref="EndContract"/>.</summary>
        public void RemoveClient(string place)
        {
            if (!Ready(true))
            {
                return;
            }
            Guard("removing a business", () =>
            {
                CleaningClient c = _state.Client(place);
                if (c == null || c.Outside)
                {
                    return;
                }
                _state.Clients.Remove(c);
                Logger?.Info("Cleaning service: '" + c.Name + "' is off the round.");
                Replan("a business was removed");
                Changed();
            });
        }

        /// <summary>How a client is served: 0 as needed, 1 to 3 that many visits a day.</summary>
        public void SetMode(string place, int mode)
        {
            Change(place, c => c.Mode = ServicesCatalogue.Clamp(mode, 0, ServicesCatalogue.VisitsMost), "a visiting pattern was changed");
        }

        public void SetHour(string place, int visit, int hour)
        {
            Change(place, c => c.Hours[ServicesCatalogue.Clamp(visit, 0, ServicesCatalogue.VisitsMost - 1)] = ServicesCatalogue.Clamp(hour, 0, 23), "a visit's hour was changed");
        }

        public void SetThreshold(string place, int threshold)
        {
            Change(place, c => c.Threshold = ServicesCatalogue.Clamp(threshold, 0, 100), "a threshold was changed");
        }

        private void Change(string place, Action<CleaningClient> change, string why)
        {
            if (!Ready(true))
            {
                return;
            }
            Guard("changing a visiting pattern", () =>
            {
                CleaningClient c = _state.Client(place);
                if (c == null)
                {
                    return;
                }
                change(c);
                Replan(why);
                Changed();
            });
        }

        public void SetVan(bool van)
        {
            if (!Ready(true) || _state.Van == van)
            {
                return;
            }
            Guard("the van", () =>
            {
                _state.Van = van;
                Logger?.Info("Cleaning service: the crew " + (van ? "drives a van from now on (" + ServicesCatalogue.VanCostPerKm + " a kilometre at a price index of 100)" : "walks from now on") + ".");
                Replan("the van was " + (van ? "taken" : "given up"));
                Changed();
            });
        }

        /// <summary>A cleaner joins the crew or leaves it. Nothing of theirs in the game is changed either way.</summary>
        public void SetCrew(string id, bool on)
        {
            if (!Ready(true) || string.IsNullOrEmpty(id) || _state.InCrew(id) == on)
            {
                return;
            }
            Guard("the crew", () =>
            {
                if (on && _state.Crew.Count >= ServicesCatalogue.CrewMost)
                {
                    Notifications.ShowError("reallife_svc_refuse_crew_full");
                    return;
                }
                if (on)
                {
                    _state.Crew.Add(id);
                }
                else
                {
                    _state.Crew.Remove(id);
                    for (int i = _visits.Count - 1; i >= 0; i--)
                    {
                        if (string.Equals(_visits[i].CleanerId, id, StringComparison.Ordinal) && _visits[i].End > ServicesGame.Now)
                        {
                            _visitPool.Add(_visits[i]);   // what they had not finished is planned again for the others
                            _visits.RemoveAt(i);
                        }
                    }
                }
                ReadAll();
                CrewMember m = Member(id);
                Logger?.Info("Cleaning service: " + (m != null ? m.Name + " joins the crew (cleaning " + m.Skill + ", satisfaction " + m.Mood + ", based at '" + m.BaseName + "', " + m.WeekHours + " hour(s) a week in the game's schedule)" : "a cleaner leaves the crew") + ".");
                Replan("the crew changed");
                Changed();
            });
        }

        /// <summary>A contract offer is taken up.</summary>
        public void AcceptOffer(string place)
        {
            if (!Ready(true))
            {
                return;
            }
            Guard("taking up a contract", () =>
            {
                CleaningOffer offer = _state.Offer(place);
                if (offer == null)
                {
                    return;
                }
                CleaningClient c = ContractRules.Accept(_state, offer, Save.Day);
                if (c == null)
                {
                    Notifications.ShowError("reallife_svc_refuse_contracts_full");
                    return;
                }
                ServicesGame.Read(c);
                Logger?.Info("Cleaning service: a contract with '" + c.Name + "': " + c.Fee + " a week for a floor kept at " + c.Standard + " (" + c.Sqm + " m2, " + c.DirtDay + " hundredths of dirt a day, floor now at " + c.Now + "); served as needed under " + c.Threshold + ".");
                Replan("a contract began");
                Changed();
            });
        }

        public void RefuseOffer(string place)
        {
            if (!Ready(true))
            {
                return;
            }
            Guard("refusing a contract", () =>
            {
                CleaningOffer offer = _state.Offer(place);
                if (offer != null)
                {
                    _state.Offers.Remove(offer);
                    Changed();
                }
            });
        }

        /// <summary>A contract is given up, after the game's own question. The running week is not paid.</summary>
        public void EndContract(string place)
        {
            CleaningClient c = Ready(true) ? _state.Client(place) : null;
            if (c == null || !c.Outside)
            {
                return;
            }
            GameConfirm.Show("reallife_svc_confirm_end_title".Localize(new { business = c.Name }), "reallife_svc_confirm_end_body".Localize(new { business = c.Name }), () => Guard("ending a contract", () =>
            {
                CleaningClient still = _state != null ? _state.Client(place) : null;
                if (still == null || !still.Outside)
                {
                    return;
                }
                _state.Clients.Remove(still);
                Logger?.Info("Cleaning service: the contract with '" + still.Name + "' is given up; its running week is not paid.");
                Replan("a contract ended");
                Changed();
            }), null, null, null, true, true);
        }
    }
}
