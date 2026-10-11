using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Dialogs;
using Entities;
using Helpers;
using Localizor;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Travel on the phone: "Trips" (the page with the current booking, the room and the nights
    /// left, past trips and loyalty; also where a ticket is used and the nearest hotel found)
    /// and the travel agency itself, callable once an agency has been visited (or from the
    /// start in a city that has none). Registered the way the mod's other contacts are.
    /// </summary>
    public static class TravelPhone
    {
        public const string TripsId = "reallife-trips";
        public const string AgentId = "reallife-travel";

        private static readonly List<Sprite> Icons = new List<Sprite>(2);   // kept for the session: the game caches contact icons by sprite name

        public static void Setup()
        {
            if (!Settings.PhoneContacts || (!Settings.Hotels && !Settings.Holidays))
            {
                return;
            }
            TravelDirector director = TravelDirector.Instance;
            try
            {
                ContactPictures.Add(TripsId);
                Register(TripsId, "reallife_contact_trips_desc", "reallife_trips_calldialogtype", () => new TripsDialog(), "reallife_text_trips_welcome");
                bool noAgency = director != null && director.Holidays != null && director.Holidays.AgencyCount == 0;
                if (Settings.Holidays && (TravelState.AgencyVisited || noAgency))
                {
                    ContactPictures.Add(AgentId);
                    TravelAgentDialog.Host = director != null ? director.Holidays : null;
                    Register(AgentId, "reallife_contact_travel_desc", "reallife_travelagent_calldialogtype", () => new TravelAgentDialog(), "reallife_text_travel_welcome");
                }
            }
            catch (Exception e)
            {
                TravelDirector.Logger?.Error(e);
                TravelDirector.Logger?.Warn("Travel: the phone contacts could not be added; hotels and agencies still work at their doors.");
            }
        }

        private static void Register(string id, string descriptionKey, string dialogTypeName, Func<Dialog> dialog, string welcomeKey)
        {
            Contact contact = Contact.GetContact(id, ContactCategoryName.General, descriptionKey, null, false, true);
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(dialogTypeName);
            contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
            CallDialogFactory.RegisterDialog(dialogType, dialog);
            if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
            {
                contact.SendMessage(new TextMessage(welcomeKey), notify: false);
            }
        }

    }

    /// <summary>The Trips page: what is booked now; then past trips and loyalty; the nearest hotel; a ticket used.</summary>
    public class TripsDialog : Dialog
    {
        public TripsDialog()
        {
            npcNameKey = TravelPhone.TripsId;
            DialogController.current.ShowEntry(Now());
        }

        private static DialogEntry Say(string text)
        {
            return PhoneContacts.Closing("reallife_travel_say", new { text });
        }

        private static DialogEntry Now()
        {
            var text = new StringBuilder(400);
            if (TravelState.HasRoom)
            {
                HotelSlot hotel = TravelCatalogue.Hotel(TravelState.HotelId);
                RoomType room = TravelCatalogue.Room(TravelState.RoomId);
                text.Append(TravelText.T("reallife_travel_trips_room", "hotel", TravelText.T(hotel.NameKey), "room", TravelText.T(room.NameKey),
                    "nights", TravelText.Number(HotelStay.NightsLeft()), "day", TravelEffects.DayName(TravelState.CheckoutDay)).Replace("{time}", TravelText.Hour(HotelStay.CheckoutHour)));
                if (TravelState.Owed > 0)
                {
                    text.Append(' ').Append(TravelText.T("reallife_travel_trips_owed", "amount", TravelText.Money(TravelState.Owed)));
                }
            }
            else
            {
                text.Append(TravelText.T("reallife_travel_trips_no_room"));
            }
            text.Append(' ');
            Trip trip = TravelState.HasTicket ? TravelCatalogue.TripById(TravelState.TicketId) : null;
            bool usable = trip != null && !TravelState.Away && !TravelState.Package;
            if (trip != null)
            {
                int left = Mathf.Max(TravelCatalogue.TicketDays - (TimeHelper.CurrentDay - TravelState.TicketDay), 0);
                text.Append(TravelState.Package
                    ? TravelText.T("reallife_travel_trips_weekend")
                    : TravelText.T("reallife_travel_trips_ticket", "trip", TravelText.T(trip.NameKey), "days", TravelText.Number(left)));
                if (TravelState.CompanionName != null)
                {
                    text.Append(' ').Append(TravelText.T("reallife_travel_trip_side_with", "name", TravelState.CompanionName));
                }
            }
            else
            {
                text.Append(TravelText.T(TravelState.FairOpen ? "reallife_travel_trips_fair" : "reallife_travel_trips_no_ticket"));
            }
            if (TravelState.BurntOut)
            {
                text.Append(' ').Append(TravelText.T("reallife_travel_trips_burnout"));
            }
            var entry = new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new { text = text.ToString() }),
                OnCancel = PhoneContacts.HangUp
            };
            if (usable)
            {
                entry.ConfirmTextOverride = "reallife_travel_agent_go_now".Localize();
                entry.OnConfirm = () =>
                {
                    TravelDirector.Instance?.Holidays.DepartSoon();
                    return Say(TravelText.T("reallife_travel_agent_going"));
                };
                entry.SecondOptionTextOverride = "reallife_travel_trips_more";
                entry.OnSecondOption = Record;
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_travel_trips_more".Localize();
                entry.OnConfirm = Record;
                entry.SecondOptionTextOverride = "reallife_travel_trips_nearest";
                entry.OnSecondOption = Nearest;
            }
            return entry;
        }

        // Past trips, newest first, and the loyalty card.
        private static DialogEntry Record()
        {
            var text = new StringBuilder(500);
            if (TravelState.Past.Count == 0)
            {
                text.Append(TravelText.T("reallife_travel_trips_past_none"));
            }
            else
            {
                text.Append(TravelText.T("reallife_travel_trips_past", "count", TravelText.Number(TravelState.TripsTaken)));
                for (int i = TravelState.Past.Count - 1; i >= 0 && i >= TravelState.Past.Count - 6; i--)
                {
                    string[] parts = TravelState.Past[i].Split(':');
                    Trip trip = parts.Length == 3 ? TravelCatalogue.TripById(parts[0]) : null;
                    if (trip != null)
                    {
                        text.Append(' ').Append(TravelText.T("reallife_travel_trips_past_line", "trip", TravelText.T(trip.NameKey), "day", parts[1], "days", parts[2]));
                    }
                }
            }
            text.Append(' ').Append(TravelText.T("reallife_travel_trips_loyalty", "stamps", TravelText.Number(TravelState.Stamps),
                "need", TravelText.Number(TravelCatalogue.StampsForFreeNight), "free", TravelText.Number(TravelState.FreeNights), "nights", TravelText.Number(TravelState.NightsEver)));
            if (TravelState.Souvenirs > 0)
            {
                text.Append(' ').Append(TravelText.T("reallife_travel_trips_souvenirs", "count", TravelText.Number(TravelState.Souvenirs)));
            }
            text.Append(' ').Append(TravelText.T(TravelState.Suitcase ? "reallife_travel_trips_suitcase" : "reallife_travel_trips_no_suitcase"));
            return new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new { text = text.ToString() }),
                ConfirmTextOverride = "reallife_travel_trips_nearest".Localize(),
                OnConfirm = Nearest,
                SecondOptionTextOverride = "reallife_travel_trips_back",
                OnSecondOption = Now,
                OnCancel = PhoneContacts.HangUp
            };
        }

        // Points the way to a bed: the player's own hotel if they have a room, else the nearest.
        private static DialogEntry Nearest()
        {
            TravelDirector director = TravelDirector.Instance;
            return Say(director != null ? director.PointToHotel() : TravelText.T("reallife_travel_trips_no_hotel"));
        }
    }
}
