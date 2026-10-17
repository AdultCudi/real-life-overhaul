using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The travel agent, by phone or at an agency's door: trips, ways to travel, then the ticket.</summary>
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
                DialogController.current.ShowEntry(Guard.Entry(Say(TravelText.T("reallife_gp_travel_agent"))));   // no trips in a Going Public session
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
                    text = TravelText.T("reallife_travel_agent_ticket", "trip", TravelText.T(trip.NameKey), "days", TravelText.Number(left),
                        "amount", TravelText.Money(refund))
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
                    messageData = "reallife_travel_say".Localize(new
                    {
                        text = TravelText.T("reallife_travel_agent_suitcase", "amount", TravelText.Money(TravelCatalogue.SuitcasePrice))
                    }),
                    ConfirmTextOverride = "reallife_travel_agent_buy".Localize(),
                    OnConfirm = () => Say(TravelText.T(Host.BuySuitcase() ? "reallife_travel_agent_suitcase_sold" : "reallife_travel_agent_no_money")),
                    SecondOptionTextOverride = "reallife_travel_agent_else",
                    OnSecondOption = () => Offer(0, string.Empty),
                    OnCancel = Leave
                };
            }
            Trip trip = Offers[index];
            TravelSeason season = TravelCatalogue.Season(TimeHelper.CurrentDay);
            string pitch = TravelText.T(trip.PitchKey, "days", TravelText.Number(trip.Days), "price", TravelText.Money(Host.Price(trip, false, false, false, true)),
                "first", TravelText.Money(Host.Price(trip, true, false, false, true)));
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
            int who = combination / perPerson;   // 0 alone, then each companion
            int how = combination % perPerson;
            bool first = extras ? how >= 2 : how == 1;
            bool insured = extras && how % 2 == 1;
            TravelCompanion? companion = who > 0 ? Companions[who - 1] : (TravelCompanion?)null;
            int price = Host.Price(trip, first, companion.HasValue, insured, true);
            string line = TravelText.T(first ? "reallife_travel_agent_option_first" : "reallife_travel_agent_option_economy",
                "trip", TravelText.T(trip.NameKey), "price", TravelText.Money(price));
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
                    text = TravelText.T(trip.Kind == TripKind.Real ? "reallife_travel_agent_booked_real" : "reallife_travel_agent_booked",
                        "trip", TravelText.T(trip.NameKey), "days", TravelText.Number(TravelCatalogue.TicketDays))
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
