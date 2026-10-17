using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A visit of today's round as the director keeps it: by cleaner id and client place, so it stays right when the round is planned again.</summary>
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
        public int Done;                 // minutes of it cleaned (or passed over) so far
    }

    /// <summary>
    /// The cleaning service's part: the round, each hour's visits, a visiting cleaner shown in a shop, and the morning. The
    /// crew work their own cleaning shifts at their own building, paid by the game as ever, and the round takes them out
    /// during those shifts; it is planned again every game hour, keeping each visit already set out for.
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
        private readonly Dictionary<string, int> _awayCounted = new Dictionary<string, int>(ServicesCatalogue.CrewMost);   // minutes away today already put back on each cleaner's base

        private int _trip;
        private bool _resuming;                    // a look for the visiting cleaner waits for the end of this frame
        private BuildingRegistration _insideReg;   // the building the player was last seen in, and its place, worked out once per building
        private string _insidePlace;

        /// <summary>Today's visits: made, under way and planned.</summary>
        public IReadOnlyList<DayVisit> Visits => _visits;

        // One look-up per cleaner, one walk over the tiles of each shop.
        private void ReadAll()
        {
            ServicesGame.ReadCrew(_state, _crew, _crewPool, out int dropped);
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
            if (otherDay)
            {
                // Today's figures begin now: nothing is measured across the gap since the save was written.
                for (int i = 0; i < _state.Clients.Count; i++)
                {
                    CleaningClient c = _state.Clients[i];
                    c.MinutesBefore = 0;
                    c.MinutesToday = c.CleanedToday = 0;
                    c.WorstStart = c.Worst;
                    c.CleanBefore = c.Now;
                }
                _state.VanMetres = 0;
            }
            Replan("the service started");
        }

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

        // Plans the rest of today's round from now, keeping every visit a cleaner has already set out for.
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
                    // A fixed visit whose hour had come when a cleaner was last in today was made (all that is known after a load).
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
                // Somebody who is out goes on from where they are.
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
                _cache.Clear();   // a bound, not a rhythm: doors change only when a business does
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
                Logger?.Info("Cleaning check (round, " + (why ?? "the hour") + "): from " + Clock(now) + ", " + _shifts.Count + " shift(s) of " + _crew.Count + " cleaner(s), " + _jobs.Count
                             + " visit(s) due, " + taken + " planned and " + left + " left over" + (left > 0 ? " (" + LeftOver() + ")" : string.Empty) + ", " + kept + " kept from before; "
                             + (_state.Van ? "by van" : "on foot") + ", " + _cache.Misses + " distance(s) worked out and " + _cache.Hits + " kept; "
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
                    if (k == i || other.Trip != first.Trip)   // a trip's number is one cleaner's
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

        // The hour beginning, before the game's hourly cleaning: every visit is cleaned for its minutes up to this hour's end
        // (as the game cleans an hour of a shift at its start), and each cleaner's time away is put back on their base.
        private void CleaningHour(int hour, bool newDay)
        {
            CleaningPresence.Clear(false);   // the game's hourly cleaning must never count the shift shown to the player
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
                // An hour at most at a time, as the game takes an hour of their work off their base at a time.
                int total = Away(_crew[i].Id, hourStart + 60);
                _awayCounted.TryGetValue(_crew[i].Id, out int counted);
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
                    _awayCounted[_crew[i].Id] = total;   // their base is not cleaned with them this hour: nothing to put back
                }
            }
            if (BuildingManager.IsInsideBuilding && !_resuming && !CleaningPresence.Broken && isActiveAndEnabled)
            {
                _resuming = true;
                StartCoroutine(Guard.Routine(PresenceAfterTick()));
            }
            if (Verbose && (on > 0 || skipped > 0 || held > 0))
            {
                Logger?.Info("Cleaning check (hour " + hour.ToString("00") + ":00): " + on + " visit(s) on, " + minutes + " minute(s) cleaned by the game's own sum"
                             + (skipped > 0 ? ", " + skipped + " not made (the cleaner is not at work)" : string.Empty) + "; " + held + " base(s) kept from being cleaned for "
                             + heldMinutes + " minute(s) their cleaner is out" + (_state.Van ? "; " + _state.VanMetres + " m driven today" : string.Empty) + ": PASS");
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

        // At the end of the frame the hour turned in: after the hourly cleaning, and a frame before the stations look for
        // their cleaner, so a visit that goes on keeps its person in the room.
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
            // At their own base the game shows them by their own shift.
            if (member == null || member.Employee == null || ReferenceEquals(member.Base, reg) || !member.Employee.IsWorking(true, WorkShiftType.Cleaning))
            {
                CleaningPresence.Clear(true);
                return;
            }
            bool was = CleaningPresence.Showing;
            bool shown = CleaningPresence.Show(reg, member.Employee, Save.Hour);
            if (Verbose && shown && !was)
            {
                Logger?.Info("Cleaning check (a visiting cleaner): " + member.Name + " is visiting '" + ServicesGame.Name(reg) + "' until " + Clock(on.End)
                             + " and the player is inside: a shift of this hour only is in the building's schedule on a free cleaning station, and the station has put them in the room: PASS");
            }
        }

        // The morning: yesterday's van, each business's daily dirt, contracts, a new offer; then the new day's round (the caller does the hour).
        private void CleaningDay(int day, bool next)
        {
            int slot = ((day % 7) + 7) % 7, slotBefore = (((day - 1) % 7) + 7) % 7;
            int cost = _state.Van ? TravelRules.Cost(_state.VanMetres, ServicesGame.Index) : 0;
            if (cost > 0)
            {
                Charge(cost, ServicesGame.VanKey, ServicesText.Distance(_state.VanMetres), null, day - 1, true, true,
                    _state.VanMetres + " m driven on day " + (day - 1) + " at " + ServicesCatalogue.VanCostPerKm + " a kilometre, price index " + ServicesGame.Index);
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
                    Logger?.Info("Cleaning service: '" + c.Name + "' is taken off the round: "
                                 + (c.Outside ? "the business has closed or changed hands, and its contract ends with it" : "it is no longer a business of the player's that the game cleans") + ".");
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
                    ContractWeek week = ContractRules.Week(c, day, out int fee, out int penalty);
                    if (week == ContractWeek.Paid)
                    {
                        Pay(fee, ServicesGame.FeeKey, c.Name, null, null,
                            "the week's fee: the floor was under its standard of " + c.Standard + " on " + ServicesCatalogue.ContractDaysUnderMost + " morning(s) at most");
                        _state.FeeIncome[slot] += fee;
                        Tell(NotificationType.Success, "reallife_svc_note_fee", TabContracts, "con:" + c.Place, "business", c.Name, "amount", ServicesText.Money(fee));
                    }
                    else if (week == ContractWeek.Missed || week == ContractWeek.Lost)
                    {
                        Charge(penalty, ServicesGame.PenaltyKey, c.Name, null, null, false, true,
                            "a week missed: no fee, and " + ServicesCatalogue.ContractPenaltyPct + "% of it as a penalty; " + c.Missed + " week(s) missed running");
                        _state.FeeIncome[slot] -= penalty;
                        if (week == ContractWeek.Lost)
                        {
                            _state.Clients.RemoveAt(i);
                            Tell(NotificationType.Error, "reallife_svc_note_contract_lost", TabContracts, null, "business", c.Name, "amount", ServicesText.Money(penalty));
                            continue;
                        }
                        Tell(NotificationType.Warning, "reallife_svc_note_week_missed", TabContracts, "con:" + c.Place, "business", c.Name, "amount", ServicesText.Money(penalty),
                            "standard", c.Standard.ToString());
                    }
                    if (Verbose)
                    {
                        Logger?.Info("Cleaning check (contract, " + c.Name + ", day " + day + "): this morning's floor " + before + " against a standard of " + c.Standard
                                     + (under ? " (under)" : string.Empty) + ", " + c.DaysUnder + " morning(s) under this week, then " + c.DirtDay + " hundredths of new dirt ("
                                     + c.Dirt + " now); week " + week + ": PASS");
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
                                 + (offer != null
                                     ? "'" + offer.Name + "' asks: " + offer.Fee + " a week for a floor kept at " + offer.Standard + ", open until day " + offer.Until
                                     : "nobody asks today")
                                 + ": PASS");
                }
                _candidates.Clear();
                if (offer != null)
                {
                    Tell(NotificationType.Info, "reallife_svc_note_offer", TabContracts, "offer:" + offer.Place, "business", offer.Name, "amount", ServicesText.Money(offer.Fee));
                }
            }
        }
    }
}
