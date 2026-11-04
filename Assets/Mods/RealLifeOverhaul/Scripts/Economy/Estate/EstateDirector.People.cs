using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The wave 4 side of the property business in the director: the named tenants, a unit's rent record, booked servicing, a tenant's
    /// requests for improvements, the city's rents read once a day (sliced) and the month's mean written to its ring. Runs in the day
    /// event and on the player's answers from the app; nothing a frame.
    /// </summary>
    public sealed partial class EstateDirector
    {
        private const int RentSlice = 240;   // buildings, registrations or listings looked at in one slice of the day's read of the city's rents

        private static readonly int HotRents = HotSpots.Slot("Estate: EstateDirector.RentStep");

        private RentMapData _rents = new RentMapData(), _rentsNext = new RentMapData();
        private EstateGame.RentRead _rentRead;
        private Func<bool> _rentStep;
        private int _rentDay = -1;

        /// <summary>The city's rents as last read (Day -1 before the first read): shared with build mode's Rents layer.</summary>
        public RentMapData Rents => _rents;

        // ------------------------------------------------------------------------------------------------- named tenants

        // Every let unit of a building the player lets himself has a person of the people pool: new leases and units from an older save are
        // named here, once, the same on every machine. Their strain on the rent (a lost job, a wage the rent is too much of) is read each morning.
        private void PeopleMorning(EstateHolding h, int day)
        {
            EstateDay.People(_state, h, day, EstateBridges.Pool != null ? EstateBridges.Pool() : null);
        }

        // A lease has just started: the tenant is named at once, so the page shows who it is.
        private void NameLease(EstateHolding h, int unit)
        {
            EstateDay.Lease(_state, h, unit, Save.Day, EstateBridges.Pool != null ? EstateBridges.Pool() : null);
        }

        // ------------------------------------------------------------------------------------------------- rent record and inspections

        // ------------------------------------------------------------------------------------------------- booked servicing

        // The visits booked for today are paid and done; a visit the money does not cover waits a day and the player is told.
        private void ServicesMorning(EstateHolding h, int day)
        {
            EstateDay.Services(h, day, _climate.DaysPerYear,
                b => Cost(b.Fee, EstateGame.RepairsKey, h.Name, h.Place, "a booked " + EstateText.Fault(b.Kind) + " service", false),
                (b, done) =>
                {
                    if (done)
                    {
                        Tell(NotificationType.Success, "reallife_estate_note_service_done",
                            Data("building", h.Name, "fault", EstateText.Fault(b.Kind), "amount", EstateText.Money(b.Fee)), TabWorks, "own:" + h.Place);
                    }
                    else
                    {
                        Tell(NotificationType.Warning, "reallife_estate_note_service_broke", Data("building", h.Name, "amount", EstateText.Money(b.Fee)), TabWorks, "own:" + h.Place);
                    }
                });
        }

        /// <summary>Books a service for a kind of fault at a building, the earliest day it can be done. False when it cannot be (booked already, the most booked, no building).</summary>
        public bool BookService(string place, int kind)
        {
            EstateHolding h = Mine(place);
            if (h == null || !Duties)
            {
                return false;
            }
            int day = Save.Day;
            string[] firms = EstateBridges.Contractors != null ? EstateBridges.Contractors() : null;
            string who = ServiceRules.Contractor(firms, _state.Seed, EstateRules.Hash(place), day + kind);
            bool booked = ServiceRules.Book(h, kind, day, ServiceRules.Earliest(day), who, ServiceRules.Fee(h, kind, _climate.Index));
            if (booked)
            {
                Changed();
            }
            return booked;
        }

        /// <summary>A booked service is cancelled.</summary>
        public bool CancelService(string place, int index)
        {
            EstateHolding h = Mine(place);
            if (h != null && ServiceRules.Cancel(h, index))
            {
                Changed();
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------- improvements

        /// <summary>A tenant's request for an improvement is met (paid for now) or refused.</summary>
        public bool AnswerImprovement(string place, int unit, bool accept)
        {
            EstateHolding h;
            EstateUnit u = UnitOf(place, unit, out h);
            if (u == null || !Duties || u.Improve <= 0)
            {
                return false;
            }
            if (accept)
            {
                int cost = u.ImproveCost;
                if (!Cost(cost, EstateGame.WorksKey, EstateText.UnitName(h, unit), h.Place, "an improvement a tenant asked for", false))
                {
                    return false;
                }
                h.CostYear += cost;
                h.CallsYear += cost;
                ImproveRules.Accept(h, u);
            }
            else
            {
                ImproveRules.Refuse(u);
            }
            Touched(h);
            return true;
        }

        // ------------------------------------------------------------------------------------------------- the city's rents

        // The day's read of the city's rents, queued for the day-spread so no frame carries more than a slice of it.
        private void QueueRents(int day)
        {
            if (_rentRead != null || _rentDay == day || !_cityRead)
            {
                return;
            }
            _rentDay = day;
            _rentRead = EstateGame.BeginRentRead(_rentsNext, _state.Seed, _climate, day);
            _rentStep = _rentStep ?? RentStep;
            DaySpread.Shared.Add(this, "Estate: the city's rents", _rentStep, Time.frameCount, HotRents);
        }

        private bool RentStep()
        {
            if (!_running || _broken || Save == null || _rentRead == null)
            {
                _rentRead = null;
                return true;
            }
            try
            {
                if (!EstateGame.StepRentRead(_rentRead, RentSlice))
                {
                    return false;
                }
                RentMapData done = _rentRead.Into;
                _rentRead = null;
                _rentsNext = _rents;   // the read before it is the next one's room
                _rents = done;
                EstateDay.Ring(_state, done);
                Changed();
                return true;
            }
            catch (Exception e)
            {
                _rentRead = null;
                Logger?.Error(e);
                Logger?.Warn("Estate: the city's rents could not be read today; they are read again tomorrow.");
                return true;
            }
        }
    }
}
