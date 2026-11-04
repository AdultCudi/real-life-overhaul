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

        private readonly RentSlot _slot = new RentSlot();
        private EstateGame.RentRead _rentRead;
        private Func<bool> _rentStep;

        /// <summary>The city's rents as last read (Day -1 before the first read): shared with build mode's Rents layer.</summary>
        public RentMapData Rents => _slot.Now;

        // The city goes or another save is loaded: the read in hand is dropped from the day-spread and the rents forgotten.
        private void ResetRents()
        {
            DaySpread.Shared.Drop(this);
            _rentRead = null;
            _slot.Reset();
        }

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
                            Data("building", h.Name, "fault", EstateText.FaultNoun(b.Kind), "amount", EstateText.Money(b.Fee)), TabWorks, "own:" + h.Place);
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
            bool booked = EstateDay.BookService(h, kind, day, _state.Seed, firms, _climate.Index, Duties);
            if (booked)
            {
                Changed();
            }
            return booked;
        }

        /// <summary>The booked service for a kind of fault is cancelled.</summary>
        public bool CancelService(string place, int kind)
        {
            EstateHolding h = Mine(place);
            if (h != null && EstateDay.CancelService(h, kind))
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
            EstateDay.AnswerResult result = EstateDay.Answer(h, u, accept, Duties,
                cost => Cost(cost, EstateGame.WorksKey, EstateText.UnitName(h, unit), h.Place, "an improvement a tenant asked for", false));
            if (result == EstateDay.AnswerResult.Short)
            {
                Tell(NotificationType.Warning, "reallife_estate_note_improve_broke", Data("building", EstateText.UnitName(h, unit), "amount", EstateText.Money(u.ImproveCost)),
                    TabMine, UnitPick(h, unit));
            }
            if (result != EstateDay.AnswerResult.Done)
            {
                return false;
            }
            Touched(h);
            return true;
        }

        // ------------------------------------------------------------------------------------------------- the city's rents

        // The day's read of the city's rents, queued for the day-spread so no frame carries more than a slice of it.
        private void QueueRents(int day)
        {
            if (_rentRead != null || !_slot.Due(day) || !_cityRead)
            {
                return;
            }
            _rentRead = EstateGame.BeginRentRead(_slot.Begin(day), _state.Seed, _climate, day);
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
                _slot.Finish(done);   // the read before it is the next one's room
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
