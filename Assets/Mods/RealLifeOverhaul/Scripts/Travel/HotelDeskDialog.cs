using Dialogs;
using Entities;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Reception in the game's dialogue panel, when <see cref="HotelReception"/>'s list cannot be made.</summary>
    public sealed class HotelDeskDialog : Dialog
    {
        internal static HotelSite Site;
        internal static HotelStay Stay;
        internal static bool Booked;

        public HotelDeskDialog()
        {
            npcNameKey = Site != null ? Site.Slot.NameKey : "reallife_travel_desk_name";
            if (Site == null || Stay == null || !Settings.Hotels)
            {
                DialogController.current.ShowEntry(Guard.Entry(Say(TravelText.T("reallife_travel_desk_closed"))));
                return;
            }
            if (TravelState.HasRoom)
            {
                HotelSlot other = TravelCatalogue.Hotel(TravelState.HotelId);
                string hotel = TravelText.T(other.NameKey);
                DialogController.current.ShowEntry(Guard.Entry(Say(TravelText.T("reallife_travel_desk_has_room", "hotel", hotel))));
                return;
            }
            string[] greetings = Site.Slot.Tier == HotelTier.Budget ? TravelCatalogue.HostelGreetings : TravelCatalogue.DeskGreetings;
            string greeting = TravelText.T(TravelText.Pick(greetings), "hotel", Site.Name);
            DialogController.current.ShowEntry(Guard.Entry(Room(0, greeting + " ")));
        }

        private static DialogEntry Say(string text)
        {
            return PhoneContacts.Closing("reallife_travel_say", new { text });
        }

        private static DialogEntry Room(int index, string lead)
        {
            RoomType[] rooms = TravelCatalogue.Rooms(Site.Slot.Tier);
            RoomType room = rooms[index % rooms.Length];
            int next = (index + 1) % rooms.Length;
            int free = HotelStay.FreeTonight(Site.Slot, room);
            int tonight = HotelStay.Tonight(Site.Slot, room);
            int week = HotelStay.Quote(Site.Slot, room, 7, out _);
            int today = TravelEffects.StayDay();
            string line = free > 0
                ? TravelText.T("reallife_travel_desk_room", "room", TravelText.T(room.NameKey), "rate", TravelText.Money(tonight),
                    "week", TravelText.Money(week), "free", TravelText.Number(free))
                : TravelText.T("reallife_travel_desk_room_full", "room", TravelText.T(room.NameKey));
            if (free > 0 && TravelCatalogue.WeekendNight(TravelEffects.Weekday(today)))
            {
                line += " " + TravelText.T("reallife_travel_desk_weekend");
            }
            if (free > 0 && TravelState.FreeNights > 0)
            {
                line += " " + TravelText.T("reallife_travel_desk_voucher");
            }
            var entry = new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new { text = lead + line }),
                OnCancel = DialogController.current.CancelDialog
            };
            if (free > 0)
            {
                entry.ConfirmTextOverride = "reallife_travel_desk_take".Localize();
                entry.OnConfirm = () => Nights(room, 0);
                entry.SecondOptionTextOverride = "reallife_travel_desk_other";
                entry.OnSecondOption = () => Room(next, string.Empty);
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_travel_desk_other".Localize();
                entry.OnConfirm = () => Room(next, string.Empty);
            }
            return entry;
        }

        private static DialogEntry Nights(RoomType room, int choice)
        {
            int nights = TravelCatalogue.NightChoices[choice % TravelCatalogue.NightChoices.Length];
            int total = HotelStay.Quote(Site.Slot, room, nights, out int perNight);
            int voucher = TravelState.FreeNights > 0 ? Mathf.Min(perNight, TravelCatalogue.FreeNightCap) : 0;
            string line = TravelText.T(nights >= 7 ? "reallife_travel_desk_nights_long" : "reallife_travel_desk_nights",
                "nights", TravelText.Number(nights), "total", TravelText.Money(Mathf.Max(total - voucher, 0)),
                "day", TravelEffects.DayName(TravelEffects.StayDay() + nights), "night", TravelText.Money(perNight));
            return new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new { text = line }),
                ConfirmTextOverride = "reallife_travel_desk_book".Localize(new { nights = TravelText.Number(nights) }),
                OnConfirm = () => Pay(room, nights),
                SecondOptionTextOverride = "reallife_travel_desk_more_nights",
                OnSecondOption = () => Nights(room, choice + 1),
                OnCancel = DialogController.current.CancelDialog
            };
        }

        private static DialogEntry Pay(RoomType room, int nights)
        {
            int total = HotelStay.Quote(Site.Slot, room, nights, out int perNight);
            int voucher = TravelState.FreeNights > 0 ? Mathf.Min(perNight, TravelCatalogue.FreeNightCap) : 0;
            if (nights == 1)
            {
                return Finish(room, nights, false);
            }
            int later = Mathf.RoundToInt((total - perNight) * (1f + TravelCatalogue.DepositSurcharge));
            string line = TravelText.T("reallife_travel_desk_pay", "total", TravelText.Money(Mathf.Max(total - voucher, 0)),
                "deposit", TravelText.Money(Mathf.Max(perNight - voucher, 0)), "later", TravelText.Money(later));
            return new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new { text = line }),
                ConfirmTextOverride = "reallife_travel_desk_pay_now".Localize(),
                OnConfirm = () => Finish(room, nights, false),
                SecondOptionTextOverride = "reallife_travel_desk_pay_deposit",
                OnSecondOption = () => Finish(room, nights, true),
                OnCancel = DialogController.current.CancelDialog
            };
        }

        private static DialogEntry Finish(RoomType room, int nights, bool deposit)
        {
            if (!Stay.Book(Site, room, nights, deposit, out string failKey))
            {
                return Say(TravelText.T(failKey ?? "reallife_travel_desk_closed", "hotel", Site.Name));
            }
            Booked = true;
            return Say(TravelText.T("reallife_travel_desk_booked", "room", TravelText.T(room.NameKey),
                "day", TravelEffects.DayName(TravelState.CheckoutDay), "time", TravelText.Hour(TravelCatalogue.CheckoutHour)));
        }
    }
}
