using System;
using System.Collections;
using BAModAPI;
using System.Collections.Generic;
using System.Text;
using BigAmbitions.DayNightCycle;
using Dialogs;
using Entities;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Holidays, bought from the game's own travel agencies (the office business type
    /// ba:businesstype_travelagency, which in the base game only ever serves simulated
    /// customers): at an agency's door, or by phone once one has been visited.
    ///
    /// The Hamptons weekend is real: the district is part of the game's world, so the package
    /// is a room at the cottages there and the player gets there themselves. Everywhere else
    /// has no place in the world, so those trips are played in the travel panel: a departure
    /// card, a card a day with a choice, and a return card with what it did. What is real about
    /// them is real in the game: the days pass through the game's own time skip, so the
    /// player's businesses run (or run down) without them, and the return card reads what they
    /// made from the game's own daily accounts.
    /// </summary>
    public sealed class Holidays
    {
        private const string AgencyType = "ba:businesstype_travelagency";
        private const string EmptyType = "ba:businesstype_empty";
        private const float DoorReach = 3.6f;

        // Card buttons.
        private const int SetOff = 101, Relax = 102, Explore = 103, Splurge = 104, NextDay = 105, Home = 106, Close = 107;

        private readonly TravelDirector _director;
        private bool _auto;
        private readonly List<Vector3> _agencyDoors = new List<Vector3>(8);
        private readonly List<BuildingRegistration> _agencies = new List<BuildingRegistration>(8);
        private int _agencyDay = -1;
        private bool _skipping;
        private bool _departPending;
        private string _cancelLine;     // a trip that fell through is being shown

        public Holidays(TravelDirector director)
        {
            _director = director;
        }

        public bool Busy => _skipping;

        /// <summary>The agency whose door the player stands at, or null.</summary>
        public BuildingRegistration AgencyInReach { get; private set; }

        public int AgencyCount => _agencies.Count;

        /// <summary>The door of the agency the player stands at (meaningful while <see cref="AgencyInReach"/> is not null).</summary>
        public Vector3 AgencyDoor { get; private set; }

        // ---------------------------------------------------------------- agencies

        /// <summary>Which buildings are travel agencies today. Looked up once a game day: businesses come and go.</summary>
        public void FindAgencies()
        {
            _agencyDay = TimeHelper.CurrentDay;
            _agencies.Clear();
            _agencyDoors.Clear();
            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            if (city == null || all == null)
            {
                return;
            }
            for (int i = 0; i < all.Count; i++)
            {
                BuildingRegistration registration = all[i];
                if (registration == null || registration.businessTypeName != AgencyType || !registration.HasValidAddress)
                {
                    continue;
                }
                CityBuildingController building = city.FindCityBuildingController(new Address(registration.StreetName, registration.StreetNumber));
                if (building == null || building.entranceDoors == null)
                {
                    continue;
                }
                for (int door = 0; door < building.entranceDoors.Length; door++)
                {
                    if (building.entranceDoors[door] != null && building.entranceDoors[door].doorTransform != null)
                    {
                        _agencies.Add(registration);
                        _agencyDoors.Add(building.entranceDoors[door].doorTransform.position);
                        break;
                    }
                }
            }
        }

        /// <summary>Once a second while the player is on foot out of doors.</summary>
        public void Tick(Vector3 player)
        {
            if (_agencyDay != TimeHelper.CurrentDay)
            {
                FindAgencies();
            }
            BuildingRegistration reach = null;
            Vector3 door = default(Vector3);
            for (int i = 0; i < _agencyDoors.Count; i++)
            {
                Vector3 offset = _agencyDoors[i] - player;
                if (Mathf.Abs(offset.y) < 2.5f && CitySpots.Flat(offset) < DoorReach * DoorReach)
                {
                    reach = _agencies[i];
                    door = _agencyDoors[i];
                }
            }
            AgencyInReach = reach;
            AgencyDoor = door;
        }

        public void Away()
        {
            AgencyInReach = null;
        }

        /// <summary>The agent, face to face at the agency's door.</summary>
        public void OpenAgent(BuildingRegistration agency)
        {
            if (agency != null && !BusinessHelper.IsBusinessOpen(agency))
            {
                TravelText.Toast(NotificationType.Info, TravelText.T("reallife_travel_agency_closed"), 6f);
                return;
            }
            if (!TravelState.AgencyVisited)
            {
                TravelState.AgencyVisited = true;
                TravelPhone.Setup();   // the agency is now a contact on the phone
            }
            TravelAgentDialog.Host = this;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash("reallife_travelagent_calldialogtype");
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new TravelAgentDialog()));
            InstanceBehavior<UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

        // ---------------------------------------------------------------- buying

        public void Companions(List<TravelCompanion> into)
        {
            into.Clear();
            try
            {
                TravelBridges.Social.Companions(into);
            }
            catch (Exception e)
            {
                _director.Fault(e);
                into.Clear();
            }
        }

        public int Price(Trip trip, bool first, bool companion, bool insured, bool coach)
        {
            int seat = 0;
            if (coach && trip.Kind == TripKind.Real)
            {
                TravelBridges.Transport.CoachToHamptons(out seat);
            }
            return TravelCatalogue.TripPrice(trip, TimeHelper.CurrentDay, first, companion, insured, seat);
        }

        /// <summary>
        /// What the player's businesses look like to leave: how many, what they made a day over
        /// the last week, their open problems, and the rent that carries on. From the game's own
        /// accounts; the agent says it instead of refusing the booking.
        /// </summary>
        public string AbsenceWarning(int days)
        {
            try
            {
                int count = 0, alerts = 0;
                float profit = 0f, rent = 0f;
                List<BuildingRegistration> all = SaveGameManager.Current.BuildingRegistrations;
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    BuildingRegistration registration = all[i];
                    if (registration == null || !GoingPublic.Mine(registration))
                    {
                        continue;
                    }
                    rent += registration.RentPerDay;
                    if (registration.businessTypeName == EmptyType || string.IsNullOrEmpty(registration.BusinessName))
                    {
                        continue;
                    }
                    count++;
                    profit += registration.GetAvgDailyIncome(7);
                    alerts += registration.Alerts;
                }
                if (count == 0)
                {
                    return rent > 0f
                        ? TravelText.T("reallife_travel_absence_rent", "rent", TravelText.Money(rent), "days", TravelText.Number(days))
                        : TravelText.T("reallife_travel_absence_none");
                }
                return TravelText.T(alerts > 0 || profit < 0f ? "reallife_travel_absence_trouble" : "reallife_travel_absence",
                    "count", TravelText.Number(count), "profit", TravelText.Money(profit), "alerts", TravelText.Number(alerts), "rent", TravelText.Money(rent))
                       .Replace("{days}", TravelText.Number(days));
            }
            catch (Exception e)
            {
                _director.Fault(e);
                return TravelText.T("reallife_travel_absence_none");
            }
        }

        /// <summary>Buys a ticket. False with the locale key of the reason if not.</summary>
        public bool Buy(Trip trip, bool first, TravelCompanion? companion, bool insured, out string failKey)
        {
            failKey = null;
            if (TravelState.HasTicket || TravelState.Away)
            {
                failKey = "reallife_travel_agent_has_ticket";
                return false;
            }
            if (!TravelBridges.Justice.MayLeaveTown(trip.Days, out string reason))
            {
                failKey = reason ?? "reallife_travel_agent_may_not_leave";
                return false;
            }
            bool coach = trip.Kind == TripKind.Real && TravelBridges.Transport.CoachToHamptons(out _);
            int price = Price(trip, first, companion.HasValue, insured, coach);
            if (!TravelEffects.Pay(price, "reallife_transaction_holiday"))
            {
                failKey = "reallife_travel_agent_no_money";
                return false;
            }
            TravelState.ClearTicket();
            TravelState.TicketId = trip.Id;
            TravelState.TicketFirst = first;
            TravelState.TicketInsured = insured;
            TravelState.TicketCoach = coach;
            TravelState.TicketPaid = price;
            TravelState.TicketDay = TimeHelper.CurrentDay;
            if (companion.HasValue)
            {
                TravelState.CompanionId = companion.Value.Id;
                TravelState.CompanionName = companion.Value.Name;
            }
            if (trip.Kind == TripKind.Business)
            {
                TravelState.FairOpen = false;
            }
            _director.Log("Travel: bought " + trip.Id + (first ? " first class" : " economy") + (insured ? ", insured" : "") + (companion.HasValue ? ", with a companion" : "") + " for " + price + ".");
            return true;
        }

        /// <summary>Hands a ticket back: all of it with insurance, half without. Returns the amount.</summary>
        public int CancelTicket(float uninsuredShare)
        {
            if (!TravelState.HasTicket || TravelState.Away)
            {
                return 0;
            }
            int refund = Mathf.RoundToInt(TravelState.TicketPaid * (TravelState.TicketInsured ? 1f : uninsuredShare));
            TravelEffects.Settle(refund, "reallife_transaction_holiday_refund");
            _director.Log("Travel: ticket for " + TravelState.TicketId + " handed back, " + refund + " refunded.");
            TravelState.ClearTicket();
            return refund;
        }

        public bool BuySuitcase()
        {
            if (TravelState.Suitcase || !TravelEffects.Pay(TravelCatalogue.SuitcasePrice, "reallife_transaction_luggage"))
            {
                return false;
            }
            TravelState.Suitcase = true;
            return true;
        }

        // ---------------------------------------------------------------- setting off

        /// <summary>The player said "now" in a conversation: the trip begins when that conversation has closed.</summary>
        public void DepartSoon()
        {
            _departPending = true;
        }

        /// <summary>Called a few times a second by the director while a departure is pending.</summary>
        public void TickPending()
        {
            if (!_departPending || DialogController.current != null || ThirdPersonCamera.IsUiOpen())
            {
                return;
            }
            _departPending = false;
            if (!Depart(out string why))
            {
                TravelText.Toast(NotificationType.Warning, why, 9f);
            }
        }

        public bool DepartPending => _departPending;

        /// <summary>Begins the trip the ticket is for. False with a sentence saying why not (the ticket is kept).</summary>
        public bool Depart(out string why, bool sure = false)
        {
            why = null;
            Trip trip = TravelState.HasTicket ? TravelCatalogue.TripById(TravelState.TicketId) : null;
            if (trip == null || TravelState.Away)
            {
                why = TravelText.T("reallife_travel_depart_no_ticket");
                return false;
            }
            if (GoingPublic.ClockShared)
            {
                why = TravelText.T("reallife_gp_travel_depart");   // days away cannot pass for one player of a Going Public session (Core/GoingPublic)
                return false;
            }
            HotelSite cottages = _director.Hotels != null ? _director.Hotels.ById(TravelCatalogue.HamptonsSlot) : null;
            if (trip.Kind == TripKind.Real && cottages != null)
            {
                return DepartForHamptons(trip, cottages, out why);
            }
            if (!_director.CanSeclude(out TestRefusal refusal))
            {
                why = TravelText.T(refusal == TestRefusal.Indoors ? "reallife_travel_depart_indoors" : "reallife_travel_depart_busy");
                return false;
            }
            // Now and then a trip falls through at the gate: what insurance is for.
            if (!sure && Random.value < TravelCatalogue.CancelChance)
            {
                string reason = TravelText.T(TravelText.Pick(TravelCatalogue.Cancellations));
                bool insured = TravelState.TicketInsured;
                int refund = CancelTicket(TravelCatalogue.UninsuredRefund);
                _cancelLine = reason + " " + TravelText.T(insured ? "reallife_travel_cancel_insured" : "reallife_travel_cancel_uninsured", "amount", TravelText.Money(refund));
                _director.Seclude(true);
                ShowCancel(trip);
                return true;
            }
            TravelState.Away = true;
            TravelState.AwayStep = 0;
            TravelState.AwayStartDay = TimeHelper.CurrentDay;
            TravelState.AwayMoneyAtStart = Mathf.RoundToInt(SaveGameManager.Current.Money);
            TravelState.AwaySpent = TravelState.AwayRelaxed = TravelState.AwaySouvenirs = 0;
            _director.Seclude(true);
            _director.Log("Travel: set off on " + trip.Id + " (" + trip.Days + " days).");
            ShowStep();
            return true;
        }

        // The real one: the room at the cottages is theirs from tonight, and they go there themselves.
        private bool DepartForHamptons(Trip trip, HotelSite cottages, out string why)
        {
            why = null;
            if (TravelState.HasRoom)
            {
                why = TravelText.T("reallife_travel_depart_has_room");
                return false;
            }
            RoomType room = TravelCatalogue.Rooms(HotelTier.Cottage)[TravelState.TicketFirst ? 1 : 0];
            _director.Stay.BookPackage(cottages, room, trip.Days);
            _director.Mark(cottages, 60f * 30f);
            bool rode = TravelState.TicketCoach && TravelBridges.Transport.RideCoachToHamptons();
            // The ticket becomes the stay: who came along and what was paid are kept until the weekend is over.
            TravelState.TicketDay = TimeHelper.CurrentDay;
            TravelText.Toast(NotificationType.Success, TravelText.T(rode ? "reallife_travel_hamptons_coach" : "reallife_travel_hamptons_go", "hotel", cottages.Name,
                "day", TravelEffects.DayName(TravelState.CheckoutDay)), 14f);
            _director.Log("Travel: the Hamptons weekend began; the cottages are booked for " + trip.Days + " night(s).");
            return true;
        }

        /// <summary>The cottage room was given up: with a night slept there it was a holiday.</summary>
        public void WeekendOver(int nightsSlept)
        {
            Trip trip = TravelCatalogue.TripById("hamptons");
            if (TravelState.TicketId != trip.Id)
            {
                return;
            }
            if (nightsSlept > 0)
            {
                int hours = 36 + 12 * nightsSlept + (TravelState.TicketFirst ? 12 : 0);
                TakeBreak(trip, nightsSlept, hours, false);
                TravelState.AddPast(trip.Id, TravelState.TicketDay, trip.Days);
                TravelText.Toast(NotificationType.Success, TravelText.T(trip.ReturnKey), 12f);
            }
            else
            {
                int back = TravelState.TicketInsured ? Mathf.RoundToInt(TravelState.TicketPaid * 0.5f) : 0;
                TravelEffects.Settle(back, "reallife_transaction_holiday_refund");
                TravelText.Toast(NotificationType.Warning, TravelText.T(back > 0 ? "reallife_travel_hamptons_missed_insured" : "reallife_travel_hamptons_missed", "amount", TravelText.Money(back)), 12f);
            }
            TravelState.ClearTicket();
        }

        // ---------------------------------------------------------------- the trip in cards

        /// <summary>A save made on a trip was loaded: the card it was at comes back.</summary>
        public void Resume()
        {
            if (!TravelState.Away)
            {
                return;
            }
            if (TravelCatalogue.TripById(TravelState.TicketId) == null || !_director.CanSeclude(out _))
            {
                Abort();
                return;
            }
            _director.Seclude(true);
            ShowStep();
        }

        /// <summary>The trip cannot go on (the watchdog said so): it ends here, the player in the street, nothing owed either way.</summary>
        public void Abort()
        {
            if (TravelState.Away)
            {
                _director.Log("Travel: the trip was cut short.");
                TravelState.AddPast(TravelState.TicketId, TravelState.AwayStartDay, Mathf.Max(TimeHelper.CurrentDay - TravelState.AwayStartDay, 0));
            }
            _skipping = false;
            _auto = false;
            _cancelLine = null;
            TravelState.ClearAway();
            TravelState.ClearTicket();
            HotelStay.EndSleepEffects();
        }

        /// <summary>The test button: wherever the trip is, the return card is next.</summary>
        public bool ReturnNow()
        {
            Trip trip = TravelState.Away ? TravelCatalogue.TripById(TravelState.TicketId) : null;
            if (trip == null || _skipping)
            {
                return false;
            }
            TravelState.AwayStep = 1 + 2 * trip.Days;
            ShowStep();
            return true;
        }

        public void Press(int id)
        {
            if (id == Close && _cancelLine != null)
            {
                _cancelLine = null;
                _director.Seclude(false);
                _director.Window.Close();
                return;
            }
            Trip trip = TravelState.Away ? TravelCatalogue.TripById(TravelState.TicketId) : null;
            if (trip == null || _skipping)
            {
                return;
            }
            int last = 1 + 2 * trip.Days;
            int step = TravelState.AwayStep;
            switch (id)
            {
                case SetOff:
                    if (step == 0)
                    {
                        TravelState.AwayStep = 1;
                        ShowStep();
                    }
                    break;
                case Relax:
                case Explore:
                case Splurge:
                    if (step >= 1 && step < last && step % 2 == 1)
                    {
                        Choose(trip, id == Relax ? TripChoice.Relax : id == Explore ? TripChoice.Explore : TripChoice.Splurge);
                    }
                    break;
                case NextDay:
                    if (step >= 2 && step < last && step % 2 == 0)
                    {
                        _director.Run(NextDayRoutine());
                    }
                    break;
                case Home:
                    if (step >= last)
                    {
                        ComeHome(trip);
                    }
                    break;
            }
        }

        private void Choose(Trip trip, TripChoice choice)
        {
            bool companion = TravelState.CompanionId != null;
            TravelEvent[] pool;
            switch (choice)
            {
                case TripChoice.Relax:
                    TravelEffects.Energy(15f);
                    TravelState.AwayRelaxed++;
                    pool = TravelCatalogue.RelaxEvents;
                    break;
                case TripChoice.Explore:
                    TravelEffects.Energy(-8f);
                    pool = TravelCatalogue.ExploreEvents;
                    break;
                default:
                    if (!TravelEffects.Pay(trip.Splurge, "reallife_transaction_holiday_spending"))
                    {
                        TravelText.Toast(NotificationType.Warning, TravelText.T("reallife_travel_trip_no_money"), 6f);
                        return;
                    }
                    TravelState.AwaySpent += trip.Splurge;
                    TravelEffects.Mood(1);
                    pool = TravelCatalogue.SplurgeEvents;
                    break;
            }
            TravelEvent happened = null;
            float roll = Random.value;
            if (companion && roll < 0.35f)
            {
                happened = TravelState.Deal(TravelCatalogue.CompanionEvents, true, () => Random.value);
            }
            else if (roll > 0.72f && trip.Events != null && trip.Events.Length > 0)
            {
                happened = OwnEvent(trip);
            }
            if (happened == null)
            {
                happened = TravelState.Deal(pool, companion, () => Random.value);
            }
            int before = Mathf.RoundToInt(SaveGameManager.Current.Money);
            TravelEffects.Apply(happened);
            if (happened != null)
            {
                int paid = before - Mathf.RoundToInt(SaveGameManager.Current.Money);
                if (paid > 0)
                {
                    TravelState.AwaySpent += paid;
                }
                if (happened.Souvenir)
                {
                    TravelState.AwaySouvenirs++;
                }
            }
            TravelState.AwayEvent = happened != null ? happened.Key : null;
            TravelState.AwayChoice = (int)choice;
            TravelState.AwayStep++;
            ShowStep();
        }

        // One of the destination's own events that this save has not had; null when all have been.
        private static TravelEvent OwnEvent(Trip trip)
        {
            int start = Random.Range(0, trip.Events.Length);
            for (int i = 0; i < trip.Events.Length; i++)
            {
                string key = trip.Events[(start + i) % trip.Events.Length];
                if (!TravelState.Seen.Contains(key))
                {
                    TravelState.Seen.Add(key);
                    TravelState.LastEvent = key;
                    return TravelCatalogue.PlaceEvent(key);
                }
            }
            return null;
        }

        // A day goes by at home: the game's own time skip, so shops trade, wages are paid, rent falls due.
        private IEnumerator NextDayRoutine()
        {
            _skipping = true;
            var goal = new Timestamp(TimeHelper.CurrentDay + 1, 9, 0f);
            EnergyHelper.RemoveEnergySpender("move");
            EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Bench);
            EnergyHelper.energyRegenMultiplier = EnergyHelper.DefaultEnergyRegenMultiplier * 0.5f;
            yield return _director.Skip(goal, "reallife_travel_tm_trip", true);
            HotelStay.EndSleepEffects();
            _skipping = false;
            if (!TravelState.Away)
            {
                yield break;   // cut short meanwhile
            }
            // Three meals a day are part of any holiday.
            GameInstance save = SaveGameManager.Current;
            if (save.Hunger < 70f)
            {
                TravelEffects.Feed(70f - save.Hunger);
            }
            TravelState.AwayStep++;
            ShowStep();
        }

        private const string ButtonDo = "reallife_travel_list_do";

        private Action<int> _onPick;
        private Action _onClosed;

        /// <summary>True while a cancelled trip's notice is on the list.</summary>
        public bool ShowingCancel => _cancelLine != null;

        /// <summary>True while a trip is played through with no list (the game's window could not be copied).</summary>
        public bool Auto => _auto;

        /// <summary>The cancelled trip's notice is dropped (the way out of everything has been taken).</summary>
        public void ClearCancel()
        {
            _cancelLine = null;
            _auto = false;
        }

        // The window for this trip's screens, made ready; null if the game's list cannot be copied.
        private TravelList Screen()
        {
            TravelList list = _director.Window.Take(this);
            if (list != null && _onPick == null)
            {
                _onPick = _director.Pressed;
                _onClosed = TripClosed;
            }
            return list;
        }

        // The list closed: for a cancelled trip's notice that ends it; a trip under way cannot be closed by the player (the list is locked).
        private void TripClosed()
        {
            if (Meta.Dev.Verbose)
            {
                _director.Log("Travel check (trip list): closed" + (TravelState.Away ? " while the trip is under way (the watchdog ends the trip if nothing reopens it)." : "."));
            }
            if (_cancelLine != null)
            {
                _cancelLine = null;
                _director.Seclude(false);
            }
        }

        // A trip that fell through at the gate: one note and one entry to close it.
        private void ShowCancel(Trip trip)
        {
            TravelList list = Screen();
            if (list == null)
            {
                TravelText.Toast(NotificationType.Warning, _cancelLine, 12f);
                _cancelLine = null;
                _director.Seclude(false);
                return;
            }
            list.Begin(TravelText.T(trip.NameKey), string.Empty);
            list.AddNote(TravelText.T("reallife_travel_cancel_title"), _cancelLine, list.HeightFor(_cancelLine.Length, false));
            list.Add(Close, TravelText.T("reallife_travel_btn_close"), null, ButtonDo, true);
            list.Open(_onPick, _onClosed);
            list.Locked = false;
            Said(trip, "cancelled at the gate", 1);
        }

        private void ShowStep()
        {
            Trip trip = TravelCatalogue.TripById(TravelState.TicketId);
            if (trip == null)
            {
                Abort();
                _director.Seclude(false);
                _director.Window.Close();
                return;
            }
            TravelList list = Screen();
            if (list == null)
            {
                Plain();
                return;
            }
            float began = Time.realtimeSinceStartup;
            bool opening = !list.IsOpen;
            int step = TravelState.AwayStep;
            int last = 1 + 2 * trip.Days;
            string with = TravelState.CompanionName;
            string side = TravelText.T(TravelState.TicketFirst ? "reallife_travel_trip_side_first" : "reallife_travel_trip_side_economy")
                          + (with != null ? "  " + TravelText.T("reallife_travel_trip_side_with", "name", with) : string.Empty)
                          + (TravelState.TicketInsured ? "  " + TravelText.T("reallife_travel_trip_side_insured") : string.Empty);
            list.Begin(TravelText.T(trip.NameKey), side);
            string heading;
            string body;
            int entries;
            if (step <= 0)
            {
                heading = TravelText.T("reallife_travel_trip_departure");
                body = TravelText.T(trip.DepartKey)
                       + " " + TravelText.T(TravelState.Suitcase ? "reallife_travel_trip_packed" : "reallife_travel_trip_unpacked")
                       + (with != null ? " " + TravelText.T("reallife_travel_trip_with", "name", with) : string.Empty);
                list.AddNote(heading, body, list.HeightFor(body.Length, false));
                list.Add(SetOff, TravelText.T("reallife_travel_btn_set_off"), TravelText.T("reallife_travel_list_set_off", "days", TravelText.Number(trip.Days)), ButtonDo, true);
                entries = 1;
            }
            else if (step >= last)
            {
                heading = TravelText.T("reallife_travel_trip_home");
                body = Summary(trip);
                list.AddNote(heading, body, list.HeightFor(body.Length, false));
                list.Add(Home, TravelText.T("reallife_travel_btn_home"), TravelText.T("reallife_travel_list_home"), ButtonDo, true);
                entries = 1;
            }
            else
            {
                int day = (step - 1) / 2;
                heading = TravelText.T("reallife_travel_trip_day", "day", TravelText.Number(day + 1), "days", TravelText.Number(trip.Days));
                if (step % 2 == 1)
                {
                    body = TravelText.T(trip.DayKeys[Mathf.Min(day, trip.DayKeys.Length - 1)]);
                    list.AddNote(heading, body, list.HeightFor(body.Length, false));
                    bool afford = SaveGameManager.Current.Money >= trip.Splurge;
                    list.Add(Relax, TravelText.T("reallife_travel_btn_relax"), TravelText.T("reallife_travel_list_relax"), ButtonDo, true);
                    list.Add(Explore, TravelText.T("reallife_travel_btn_explore"), TravelText.T("reallife_travel_list_explore"), ButtonDo, true);
                    list.Add(Splurge, TravelText.T("reallife_travel_btn_splurge", "amount", TravelText.Money(trip.Splurge)),
                        TravelText.T(afford ? "reallife_travel_list_splurge" : "reallife_travel_list_no_splurge"), ButtonDo, afford);
                    entries = 3;
                }
                else
                {
                    string choice = TravelText.T(TravelState.AwayChoice == (int)TripChoice.Relax ? "reallife_travel_trip_did_relax"
                        : TravelState.AwayChoice == (int)TripChoice.Explore ? "reallife_travel_trip_did_explore" : "reallife_travel_trip_did_splurge");
                    string what = TravelState.AwayEvent != null ? TravelText.T(TravelState.AwayEvent, "name", with ?? string.Empty) : string.Empty;
                    body = choice + " " + what;
                    list.AddNote(heading, body, list.HeightFor(body.Length, false));
                    bool final = day + 1 >= trip.Days;
                    list.Add(NextDay, TravelText.T(final ? "reallife_travel_btn_head_home" : "reallife_travel_btn_next_day"),
                        TravelText.T(final ? "reallife_travel_list_head_home" : "reallife_travel_list_next_day"), ButtonDo, true);
                    entries = 1;
                }
            }
            list.Open(_onPick, _onClosed);
            list.Locked = true;   // a trip is left by its own entries; the watchdog is what ends one that cannot go on
            if (Meta.Dev.Verbose)
            {
                _director.Log("Travel check (trip list): " + (opening ? "the game's list is open: " : "") + trip.Id + ", step " + step + " of " + last + ", " + entries + " choice(s), " + list.Made
                              + " entries made so far; " + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms: PASS.");
            }
        }

        private void Said(Trip trip, string what, int entries)
        {
            if (Meta.Dev.Verbose)
            {
                _director.Log("Travel check (trip list): " + trip.Id + ": " + what + ", " + entries + " choice(s): PASS.");
            }
        }

        // No window to show: the trip is played through with the easy choice each day, so that a ticket can never trap anyone.
        private void Plain()
        {
            if (_auto)
            {
                return;
            }
            _auto = true;
            _director.Log("Travel check (trip list): the game's list could not be made; the trip is played through taking it easy each day: FAIL.");
            _director.Run(AutoRoutine());
        }

        private IEnumerator AutoRoutine()
        {
            while (TravelState.Away && _auto)
            {
                Trip trip = TravelCatalogue.TripById(TravelState.TicketId);
                if (trip == null)
                {
                    break;
                }
                int last = 1 + 2 * trip.Days;
                int step = TravelState.AwayStep;
                if (step <= 0)
                {
                    TravelState.AwayStep = 1;
                }
                else if (step >= last)
                {
                    ComeHome(trip);
                    break;
                }
                else if (step % 2 == 1)
                {
                    Choose(trip, TripChoice.Relax);
                }
                else
                {
                    yield return NextDayRoutine();
                }
                yield return null;
            }
            if (_auto && TravelState.Away)
            {
                Abort();
                _director.Seclude(false);
            }
            _auto = false;
        }

        // What the days away came to, read from the game's own daily accounts.
        private string Summary(Trip trip)
        {
            var text = new StringBuilder(600);
            int today = TimeHelper.CurrentDay;
            int away = Mathf.Max(today - TravelState.AwayStartDay, 0);
            text.Append(TravelText.T(trip.ReturnKey)).Append(' ');
            text.Append(TravelText.T("reallife_travel_summary_days", "days", TravelText.Number(away),
                "spent", TravelText.Money(TravelState.TicketPaid + TravelState.AwaySpent)));
            try
            {
                float during = 0f, before = 0f;
                int daysDuring = 0, daysBefore = 0;
                bool any = false;
                List<FinancialSummary> summaries = SaveGameManager.Current.financialSummaries;
                for (int i = 0; summaries != null && i < summaries.Count; i++)
                {
                    FinancialSummary summary = summaries[i];
                    if (summary == null)
                    {
                        continue;
                    }
                    if (summary.dayNumber >= TravelState.AwayStartDay && summary.dayNumber < today)
                    {
                        during += summary.totalBusinessProfit;
                        daysDuring++;
                        any |= summary.businessIncomeStatements != null && summary.businessIncomeStatements.Count > 0;
                    }
                    else if (summary.dayNumber >= TravelState.AwayStartDay - 7 && summary.dayNumber < TravelState.AwayStartDay)
                    {
                        before += summary.totalBusinessProfit;
                        daysBefore++;
                    }
                }
                if (any && daysDuring > 0)
                {
                    text.Append(' ').Append(TravelText.T(daysBefore > 0 ? "reallife_travel_summary_business" : "reallife_travel_summary_business_new",
                        "total", TravelText.Money(during), "daily", TravelText.Money(during / daysDuring),
                        "before", TravelText.Money(daysBefore > 0 ? before / daysBefore : 0f)));
                }
                int money = Mathf.RoundToInt(SaveGameManager.Current.Money);
                text.Append(' ').Append(TravelText.T("reallife_travel_summary_balance", "amount", TravelText.Money(money - TravelState.AwayMoneyAtStart)));
            }
            catch (Exception e)
            {
                _director.Fault(e);
            }
            Missed(text, TravelBridges.Social.MissedWhileAway(TravelState.AwayStartDay, today));
            Missed(text, TravelBridges.Justice.MissedWhileAway(TravelState.AwayStartDay, today));
            if (TravelState.AwaySouvenirs > 0)
            {
                text.Append(' ').Append(TravelText.T("reallife_travel_summary_souvenirs", "count", TravelText.Number(TravelState.AwaySouvenirs)));
            }
            text.Append(' ').Append(TravelText.T(trip.JetLag ? (TravelState.TicketFirst ? "reallife_travel_summary_jetlag_first" : "reallife_travel_summary_jetlag")
                : trip.Kind == TripKind.Business ? "reallife_travel_summary_fair" : "reallife_travel_summary_rested"));
            return text.ToString();
        }

        private static void Missed(StringBuilder text, List<string> keys)
        {
            for (int i = 0; keys != null && i < keys.Count && i < 3; i++)
            {
                text.Append(' ').Append(TravelText.T(keys[i]));
            }
        }

        private void ComeHome(Trip trip)
        {
            int away = Mathf.Max(TimeHelper.CurrentDay - TravelState.AwayStartDay, 1);
            int hours = Mathf.Min(72 + 12 * TravelState.AwayRelaxed + (TravelState.TicketFirst ? 24 : 0), 144);
            if (trip.Kind == TripKind.Business)
            {
                ModState.AddModifier(TravelEffects.TradeFair);
                hours = 0;   // a trade fair is work
            }
            TakeBreak(trip, away, hours, trip.JetLag);
            TravelState.Souvenirs += TravelState.AwaySouvenirs + (TravelState.Suitcase && TravelState.AwaySouvenirs > 0 ? 1 : 0);   // a proper case brings one more thing home whole
            GiveSouvenirs();
            TravelState.AddPast(trip.Id, TravelState.AwayStartDay, away);
            _director.Log("Travel: back from " + trip.Id + " after " + away + " day(s); " + TravelState.AwaySpent + " spent there.");
            TravelState.ClearAway();
            TravelState.ClearTicket();
            _director.Seclude(false);
            _director.Window.Close();
            TravelText.Toast(NotificationType.Success, TravelText.T("reallife_travel_welcome_back"), 8f);
        }

        // What a holiday leaves behind: rested for days, the grind forgotten, perhaps jet lag, the companion closer.
        private static void TakeBreak(Trip trip, int days, int restedHours, bool jetLag)
        {
            if (restedHours > 0)
            {
                TravelBridges.Home.Holiday(days);
                ModState.AddModifier(TravelEffects.Rested, restedHours);
                TravelState.Streak = 0;
                TravelState.Warned = false;
                TravelState.BurntOut = false;
                ModState.RemoveModifier(TravelEffects.Burnout);
            }
            if (jetLag)
            {
                ModState.AddModifier(TravelEffects.JetLag, TravelState.TicketFirst ? 18 : 36);
                TravelEffects.Energy(TravelState.TicketFirst ? -8f : -18f);
            }
            else if (restedHours > 0)
            {
                TravelEffects.Energy(20f);
            }
            if (TravelState.CompanionId != null)
            {
                TravelBridges.Social.TripTogether(TravelState.CompanionId, days);
                ModState.AddModifier(TravelEffects.Together);
            }
        }

        /// <summary>
        /// Souvenirs go to friends: the social feature takes what it wants; without it the mod's
        /// own Friend and Mom contacts each thank the player for one.
        /// </summary>
        public static void GiveSouvenirs()
        {
            if (TravelState.Souvenirs <= 0)
            {
                return;
            }
            if (TravelBridges.HasSocial)
            {
                TravelState.Souvenirs = Mathf.Max(TravelState.Souvenirs - Mathf.Max(TravelBridges.Social.Souvenirs(TravelState.Souvenirs), 0), 0);
                return;
            }
            if (TravelState.Souvenirs > 0 && PhoneContacts.Friend != null)
            {
                TravelState.Souvenirs--;
                PhoneContacts.Friend.SendMessage(new TextMessage("reallife_travel_text_friend_souvenir", null, false, true), notify: true, sendNotificationInstantly: true);
                ModState.AddModifier(ModState.Social);
            }
            if (TravelState.Souvenirs > 0 && PhoneContacts.Mom != null)
            {
                TravelState.Souvenirs--;
                PhoneContacts.Mom.SendMessage(new TextMessage("reallife_travel_text_mom_souvenir", null, false, true), notify: true, sendNotificationInstantly: true);
                ModState.AddModifier(ModState.Family);
            }
        }

        // ---------------------------------------------------------------- the days at home

        /// <summary>Once per game day: the ticket's date, the trade fair's invitation, and how long it has been since a break.</summary>
        public void DayTick(int day)
        {
            if (TravelState.HasTicket && !TravelState.Away && !TravelState.Package && day - TravelState.TicketDay > TravelCatalogue.TicketDays)
            {
                Trip expired = TravelCatalogue.TripById(TravelState.TicketId);
                int refund = CancelTicket(TravelCatalogue.ExpiredRefund);
                TravelText.Toast(NotificationType.Warning, TravelText.T("reallife_travel_ticket_expired", "trip", TravelText.T(expired.NameKey), "amount", TravelText.Money(refund)), 12f);
            }
            if (TravelState.FairOpen && day - TravelState.FairDay > 5)
            {
                TravelState.FairOpen = false;
            }
            if (Settings.Holidays && Settings.TravelExtras && !TravelState.FairOpen && !TravelState.HasTicket && day - TravelState.FairDay >= TravelCatalogue.TradeFairEveryDays
                && day >= 10 && OwnsBusiness())
            {
                TravelState.FairOpen = true;
                TravelState.FairDay = day;
                TravelText.Toast(NotificationType.Info, TravelText.T("reallife_travel_fair_invite"), 12f);
            }
            if (TravelState.StreakDay != day)
            {
                TravelState.StreakDay = day;
                // A day counts towards the grind only for somebody who has work: a business to run, or a job taken this session.
                if (!TravelState.Away && (OwnsBusiness() || JobDirector.LastJobId != null))
                {
                    TravelState.Streak++;
                }
            }
            if (!Settings.Holidays)
            {
                return;
            }
            int level = TravelBridges.Home.HighStressDays;
            if (level < 0)
            {
                level = TravelState.Streak;
            }
            if (level >= TravelCatalogue.BurnoutWarnDays && !TravelState.Warned)
            {
                TravelState.Warned = true;
                string nudge = TravelText.Pick(TravelCatalogue.BurnoutNudges);
                if (PhoneContacts.Friend != null)
                {
                    PhoneContacts.Friend.SendMessage(new TextMessage(nudge, null, false, true), notify: true, sendNotificationInstantly: true);
                }
                else
                {
                    TravelText.Toast(NotificationType.Info, TravelText.T(nudge), 10f);
                }
            }
            if (level >= TravelCatalogue.BurnoutDays && !TravelState.BurntOut)
            {
                TravelState.BurntOut = true;
                TravelText.Toast(NotificationType.Warning, TravelText.T("reallife_travel_burnout_doctor"), 12f);
            }
            if (level < TravelCatalogue.BurnoutWarnDays && (TravelState.BurntOut || TravelState.Warned))
            {
                TravelState.BurntOut = false;
                TravelState.Warned = false;
                ModState.RemoveModifier(TravelEffects.Burnout);
            }
        }

        /// <summary>Once an hour: burnt out, work weighs a little more until a break is taken (a mild mood modifier, kept topped up).</summary>
        public void HourTick()
        {
            if (TravelState.BurntOut && Settings.Holidays && !TravelState.Away)
            {
                ModState.AddModifier(TravelEffects.Burnout, 6);
            }
        }

        public static bool OwnsBusiness()
        {
            List<BuildingRegistration> all = SaveGameManager.Current.BuildingRegistrations;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && GoingPublic.Mine(all[i]) && all[i].businessTypeName != EmptyType && !string.IsNullOrEmpty(all[i].BusinessName))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// The travel agent: the trips one at a time with today's prices, then how to travel
    /// (class, insurance, who comes along), then what leaving the businesses for that long
    /// looks like, then the ticket. On the phone or at an agency's door.
    /// </summary>
    public class TravelAgentDialog : Dialog
    {
        internal static Holidays Host;

        private static readonly List<Trip> Offers = new List<Trip>(8);
        private static readonly List<TravelCompanion> Companions = new List<TravelCompanion>(4);

        public TravelAgentDialog()
        {
            npcNameKey = TravelPhone.AgentId;
            if (Host == null || !Settings.Holidays || TravelState.Away)
            {
                DialogController.current.ShowEntry(Guard.Entry(Say(TravelText.T("reallife_travel_agent_off"))));
                return;
            }
            if (GoingPublic.ClockShared && !TravelState.HasTicket)
            {
                DialogController.current.ShowEntry(Guard.Entry(Say(TravelText.T("reallife_gp_travel_agent"))));   // no trips in a Going Public session (Core/GoingPublic)
                return;
            }
            if (TravelState.HasTicket)
            {
                DialogController.current.ShowEntry(Guard.Entry(Ticket()));
                return;
            }
            Offers.Clear();
            foreach (Trip trip in TravelCatalogue.Trips)
            {
                if (trip.Kind != TripKind.Business || (TravelState.FairOpen && Settings.TravelExtras))
                {
                    Offers.Add(trip);
                }
            }
            Host.Companions(Companions);
            DialogController.current.ShowEntry(Guard.Entry(Offer(0, TravelText.T(TravelText.Pick(TravelCatalogue.AgentGreetings)) + " ")));
        }

        private static DialogEntry Say(string text)
        {
            return PhoneContacts.Closing("reallife_travel_say", new { text });
        }

        private static Action Leave => DialogController.current.CancelDialog;

        // A ticket is held: set off, or hand it back.
        private static DialogEntry Ticket()
        {
            Trip trip = TravelCatalogue.TripById(TravelState.TicketId);
            if (TravelState.Package)
            {
                return Say(TravelText.T("reallife_travel_agent_weekend_on"));
            }
            int left = Mathf.Max(TravelCatalogue.TicketDays - (TimeHelper.CurrentDay - TravelState.TicketDay), 0);
            int refund = Mathf.RoundToInt(TravelState.TicketPaid * (TravelState.TicketInsured ? 1f : TravelCatalogue.ExpiredRefund));
            return new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new
                {
                    text = TravelText.T("reallife_travel_agent_ticket", "trip", TravelText.T(trip.NameKey), "days", TravelText.Number(left), "amount", TravelText.Money(refund))
                }),
                ConfirmTextOverride = "reallife_travel_agent_go_now".Localize(),
                OnConfirm = () =>
                {
                    Host.DepartSoon();
                    return Say(TravelText.T("reallife_travel_agent_going"));
                },
                SecondOptionTextOverride = "reallife_travel_agent_hand_back",
                OnSecondOption = () =>
                {
                    int back = Host.CancelTicket(TravelCatalogue.ExpiredRefund);
                    return Say(TravelText.T("reallife_travel_agent_handed_back", "amount", TravelText.Money(back)));
                },
                OnCancel = Leave
            };
        }

        private static DialogEntry Offer(int index, string lead)
        {
            // After the trips, the suitcase.
            if (index >= Offers.Count)
            {
                if (TravelState.Suitcase || !Settings.TravelExtras)
                {
                    return Offer(0, string.Empty);
                }
                return new DialogEntry
                {
                    messageData = "reallife_travel_say".Localize(new { text = TravelText.T("reallife_travel_agent_suitcase", "amount", TravelText.Money(TravelCatalogue.SuitcasePrice)) }),
                    ConfirmTextOverride = "reallife_travel_agent_buy".Localize(),
                    OnConfirm = () => Say(TravelText.T(Host.BuySuitcase() ? "reallife_travel_agent_suitcase_sold" : "reallife_travel_agent_no_money")),
                    SecondOptionTextOverride = "reallife_travel_agent_else",
                    OnSecondOption = () => Offer(0, string.Empty),
                    OnCancel = Leave
                };
            }
            Trip trip = Offers[index];
            TravelSeason season = TravelCatalogue.Season(TimeHelper.CurrentDay);
            string pitch = TravelText.T(trip.PitchKey, "days", TravelText.Number(trip.Days),
                "price", TravelText.Money(Host.Price(trip, false, false, false, true)), "first", TravelText.Money(Host.Price(trip, true, false, false, true)));
            if (season == trip.Peak)
            {
                pitch += " " + TravelText.T("reallife_travel_agent_peak");
            }
            else if (season == trip.Low)
            {
                pitch += " " + TravelText.T("reallife_travel_agent_low");
            }
            return new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new { text = lead + pitch }),
                ConfirmTextOverride = "reallife_travel_agent_interested".Localize(),
                OnConfirm = () => Options(trip, 0),
                SecondOptionTextOverride = "reallife_travel_agent_else",
                OnSecondOption = () => Offer(index + 1, string.Empty),
                OnCancel = Leave
            };
        }

        // The ways to travel, one after another: economy or first, insured or not, alone or with each person who would come.
        private static DialogEntry Options(Trip trip, int combination)
        {
            bool extras = Settings.TravelExtras;
            int people = extras ? Companions.Count : 0;
            int perPerson = extras ? 4 : 2;
            int count = perPerson * (1 + people);
            combination %= count;
            int who = combination / perPerson;            // 0 alone, then each companion
            int how = combination % perPerson;
            bool first = extras ? how >= 2 : how == 1;
            bool insured = extras && how % 2 == 1;
            TravelCompanion? companion = who > 0 ? Companions[who - 1] : (TravelCompanion?)null;
            int price = Host.Price(trip, first, companion.HasValue, insured, true);
            string line = TravelText.T(first ? "reallife_travel_agent_option_first" : "reallife_travel_agent_option_economy", "trip", TravelText.T(trip.NameKey), "price", TravelText.Money(price));
            if (companion.HasValue)
            {
                line += " " + TravelText.T("reallife_travel_agent_option_with", "name", companion.Value.Name);
            }
            if (extras)
            {
                line += " " + TravelText.T(insured ? "reallife_travel_agent_option_insured" : "reallife_travel_agent_option_uninsured");
            }
            line += " " + Host.AbsenceWarning(trip.Days);
            return new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new { text = line }),
                ConfirmTextOverride = "reallife_travel_agent_book".Localize(new { price = TravelText.Money(price) }),
                OnConfirm = () => Book(trip, first, companion, insured),
                SecondOptionTextOverride = "reallife_travel_agent_other_way",
                OnSecondOption = () => Options(trip, combination + 1),
                OnCancel = Leave
            };
        }

        private static DialogEntry Book(Trip trip, bool first, TravelCompanion? companion, bool insured)
        {
            if (!Host.Buy(trip, first, companion, insured, out string failKey))
            {
                return Say(TravelText.T(failKey ?? "reallife_travel_agent_off"));
            }
            return new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new
                {
                    text = TravelText.T(trip.Kind == TripKind.Real ? "reallife_travel_agent_booked_real" : "reallife_travel_agent_booked", "trip", TravelText.T(trip.NameKey),
                        "days", TravelText.Number(TravelCatalogue.TicketDays))
                }),
                ConfirmTextOverride = "reallife_travel_agent_go_now".Localize(),
                OnConfirm = () =>
                {
                    Host.DepartSoon();
                    return Say(TravelText.T("reallife_travel_agent_going"));
                },
                OnCancel = Leave
            };
        }
    }
}
