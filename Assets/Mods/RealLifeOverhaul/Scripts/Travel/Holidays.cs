using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Holidays from the game's travel agencies: the Hamptons weekend in the world, other trips in cards.</summary>
    public sealed partial class Holidays
    {
        private const string AgencyType = "ba:businesstype_travelagency";
        private const string EmptyType = "ba:businesstype_empty";
        private const float DoorReach = 3.6f;   // metres

        private readonly TravelDirector _director;
        private bool _auto;
        private readonly List<Vector3> _agencyDoors = new List<Vector3>(8);
        private readonly List<BuildingRegistration> _agencies = new List<BuildingRegistration>(8);
        private int _agencyDay = -1;
        private bool _skipping;
        private bool _departPending;
        private string _cancelLine;     // the notice of a trip that fell through, while it is shown

        public Holidays(TravelDirector director)
        {
            _director = director;
        }

        public bool Busy => _skipping;

        public BuildingRegistration AgencyInReach { get; private set; }

        public int AgencyCount => _agencies.Count;

        public Vector3 AgencyDoor { get; private set; }

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
                if (Hotels.Door(building, out Vector3 door))
                {
                    _agencies.Add(registration);
                    _agencyDoors.Add(door);
                }
            }
        }

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
                TravelPhone.Setup();   // the agency is a phone contact from now on
            }
            TravelAgentDialog.Host = this;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash("reallife_travelagent_calldialogtype");
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new TravelAgentDialog()));
            InstanceBehavior<UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

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

        /// <summary>What leaving the player's businesses would mean, from the game's accounts: the agent warns, never refuses.</summary>
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
                    if (registration == null || !GoingPublic.Mine(registration) || !GameRegistrations.Found(registration))
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
                    alerts += GameRegistrations.Alerts(registration);
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
            _director.Log("Travel: bought " + trip.Id + (first ? " first class" : " economy") + (insured ? ", insured" : "")
                          + (companion.HasValue ? ", with a companion" : "") + " for " + price + ".");
            return true;
        }

        /// <summary>Hands a ticket back for all of it if insured, else the share given. Returns the amount.</summary>
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

        /// <summary>The player said "now" in a conversation: the trip begins when that conversation has closed.</summary>
        public void DepartSoon()
        {
            _departPending = true;
        }

        /// <summary>Four times a second: a departure asked for starts once no conversation or screen is open.</summary>
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
                why = TravelText.T("reallife_gp_travel_depart");   // days away cannot pass for one player of a Going Public session
                return false;
            }
            HotelSite cottages = _director.Hotels.ById(TravelCatalogue.HamptonsSlot);
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
                TakeBreak(nightsSlept, hours, false);
                TravelState.AddPast(trip.Id, TravelState.TicketDay, trip.Days);
                TravelText.Toast(NotificationType.Success, TravelText.T(trip.ReturnKey), 12f);
            }
            else
            {
                int back = TravelState.TicketInsured ? Mathf.RoundToInt(TravelState.TicketPaid * 0.5f) : 0;
                TravelEffects.Settle(back, "reallife_transaction_holiday_refund");
                TravelText.Toast(NotificationType.Warning,
                    TravelText.T(back > 0 ? "reallife_travel_hamptons_missed_insured" : "reallife_travel_hamptons_missed", "amount", TravelText.Money(back)), 12f);
            }
            TravelState.ClearTicket();
        }

        /// <summary>Once a game day: the ticket's date, the trade fair's invitation, and how long it has been since a break.</summary>
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
                // Only a day with work counts towards the grind: a business to run, or a job taken this session.
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

        /// <summary>Once an hour: burnt out, work weighs a little more until a break is taken (a mild mood, kept topped up).</summary>
        public void HourTick()
        {
            if (TravelState.BurntOut && Settings.Holidays && !TravelState.Away)
            {
                ModState.AddModifier(TravelEffects.Burnout, 6);
            }
        }

        private static bool OwnsBusiness()
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
}
