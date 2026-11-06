using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Dialogs;
using Entities;
using Localizor;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The pay machine at a car park, in the game's own dialogue panel as its special desks open it (the game paused while the player
    /// talks, as the builders' front desks): what the car park is doing now and what it has taken; for the player's own, how it is
    /// let (public pay parking, or leases to the businesses and rivals nearby), its price an hour inside the band, and the offers
    /// waiting, to accept or decline, with the leases it holds; for a car park of the city's, its price and the bays the player may
    /// lease there by the month. Pages are made when shown, never on a tick. The same actions are in build mode's panel.
    /// </summary>
    public static class CarParkDesk
    {
        private const string DialogName = "reallife_carparkdesk_calldialogtype";
        public const string HeaderKey = "reallife_park_machine";   // the machine's name, over the pages and the pointer

        private static readonly Action Closed = Guard.Way(BuilderDeskGame.Release);

        /// <summary>The car park whose machine is talking.</summary>
        internal static int Park;

        private static int _bays = 2;   // the bays a lease at a car park of the city's is asked for

        private static ConstructionDirector Director => ConstructionDirector.Instance != null && ConstructionDirector.Instance.Running ? ConstructionDirector.Instance : null;

        private static CarParkFile Lot => Director?.State?.CarPark(Park);

        /// <summary>True while the game's dialogue panel is open, this conversation's or another's.</summary>
        public static bool Talking => DialogController.current != null;

        /// <summary>The machine's dialogue for a car park. False, and nothing shown, when the game's panel is not there or one is open.</summary>
        public static bool Open(ConstructionDirector d, int id)
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.playerHUD == null || uis.playerHUD.dialogUI == null || DialogController.current != null || d == null || d.State == null || d.State.CarPark(id) == null)
            {
                return false;
            }
            Park = id;
            _bays = 2;
            var type = (CallDialogType)ModEnumHash.GetSafeHash(DialogName);
            CallDialogFactory.RegisterDialog(type, Guard.Maker(() => new CarParkDialog()));
            if (UI.Smartphone.FullMenu.IsOpen && uis.fullMenu != null)
            {
                uis.fullMenu.Toggle(false);
            }
            uis.playerHUD.dialogUI.ShowDialog(type, NavigationBlocker.SpecialEmployeeDialog, null, Closed, null);
            BuilderDeskGame.Hold();
            return true;
        }

        /// <summary>The first page.</summary>
        internal static DialogEntry First() => Status();

        // ------------------------------------------------------------------ the words

        /// <summary>A car park's page in words: the state of it now, for the dialogue and for the tests.</summary>
        internal static string StatusWords(ConstructionDirector d, CarParkFile c)
        {
            string name = d.ParkName(c);
            string cars = ConstructionText.N(c.Cars), bays = ConstructionText.N(c.Bays);
            string week = ConstructionText.Money(c.Net(7));
            if (c.Owner == CarParkOwner.Builder)
            {
                string yours = c.LeasedByPlayer > 0 ? ConstructionText.Fill("reallife_park_desk_yours", "bays", ConstructionText.N(c.LeasedByPlayer)) : string.Empty;
                return ConstructionText.Fill("reallife_park_desk_city", "name", name, "owner", ConstructionDirector.ParkOwnerName(c), "bays", bays, "cars", cars, "mode",
                    ConstructionDirector.ParkModeWords(c), "yours", yours);
            }
            if (c.Mode == CarParkMode.Public)
            {
                return ConstructionText.Fill("reallife_park_desk_public", "name", name, "bays", bays, "cars", cars, "price", ConstructionText.Cents(c.Price), "today",
                    ConstructionText.Money(c.Taken / 100L),
                    "week", week);
            }
            return ConstructionText.Fill("reallife_park_desk_lease", "name", name, "bays", bays, "cars", cars, "leased", ConstructionText.N(c.Leased), "income",
                ConstructionText.Money(c.LeaseIncome / 100L),
                "offers", ConstructionText.N(c.Offers.Count), "week", week);
        }

        // ------------------------------------------------------------------ pages

        private static DialogEntry Status()
        {
            ConstructionDirector d = Director;
            CarParkFile c = Lot;
            if (d == null || c == null || !c.Standing)
            {
                return Closing(ConstructionText.T("reallife_park_desk_closed"));
            }
            string text = StatusWords(d, c);
            if (c.Owner == CarParkOwner.Builder)
            {
                int room = d.ParkLeaseRoom(c);
                if (room > 0)
                {
                    return Page(text, "reallife_park_desk_lease_here", LeaseHere, c.LeasedByPlayer > 0 ? "reallife_park_desk_your_leases" : null,
                        c.LeasedByPlayer > 0 ? (Func<DialogEntry>)YourLeases : null, Leave,
                        "reallife_park_desk_bye");
                }
                return Page(text, c.LeasedByPlayer > 0 ? "reallife_park_desk_your_leases" : null, c.LeasedByPlayer > 0 ? (Func<DialogEntry>)YourLeases : null, null, null, Leave,
                    "reallife_park_desk_bye");
            }
            if (c.Mode == CarParkMode.Public)
            {
                return Page(text, "reallife_park_desk_how", How, "reallife_park_desk_price", Price, Leave, "reallife_park_desk_bye");
            }
            return Page(text, "reallife_park_desk_how", How, "reallife_park_desk_offers", Offers, Leave, "reallife_park_desk_bye");
        }

        // The two ways to let it, and what each is.
        private static DialogEntry How()
        {
            CarParkFile c = Lot;
            if (c == null)
            {
                return Closing(ConstructionText.T("reallife_park_desk_closed"));
            }
            string text = ConstructionText.Fill("reallife_park_desk_how_text", "mode", ConstructionText.T(c.Mode == CarParkMode.Public ? "reallife_park_how_public" : "reallife_park_how_lease"));
            return Page(text, "reallife_park_how_public", () => Mode(CarParkMode.Public), "reallife_park_how_lease", () => Mode(CarParkMode.Lease), Status, "reallife_park_desk_back");
        }

        private static DialogEntry Mode(CarParkMode mode)
        {
            ConstructionDirector d = Director;
            CarParkFile c = Lot;
            if (d == null || c == null)
            {
                return Closing(ConstructionText.T("reallife_park_desk_closed"));
            }
            if (mode == CarParkMode.Public && c.Mode == CarParkMode.Lease && c.Leases.Count > 0)
            {
                // Going public ends the leases and pays back their unused days: asked in the game's question box first.
                DialogController.current?.FinishDialog();
                var body = new Dictionary<string, string> { { "count", c.Leases.Count.ToString(CultureInfo.InvariantCulture) } };
                int id = Park;
                GameConfirm.Show("reallife_park_confirm_public".Localize(), "reallife_park_confirm_public_body".Localize(body), () =>
                {
                    ConstructionDirector now = Director;
                    now?.ParkSetMode(now.State?.CarPark(id), CarParkMode.Public);
                }, null, null, null, true, false);
                return null;
            }
            string answer = d.ParkSetMode(c, mode);
            return answer != null ? Page(ConstructionText.T(answer), null, null, null, null, Status, "reallife_park_desk_back") : Status();
        }

        // The price an hour: up or down a step inside the band.
        private static DialogEntry Price()
        {
            ConstructionDirector d = Director;
            CarParkFile c = Lot;
            if (d == null || c == null)
            {
                return Closing(ConstructionText.T("reallife_park_desk_closed"));
            }
            int fair = d.ParkFair(c.Lot);
            string text = ConstructionText.Fill("reallife_park_desk_price_text", "price", ConstructionText.Cents(c.Price), "fair", ConstructionText.Cents(fair), "least",
                ConstructionText.Cents(CarParkRules.PriceLeast(fair)),
                "most", ConstructionText.Cents(CarParkRules.PriceMost(fair)), "step", ConstructionText.Cents(CarParkRules.PriceStep));
            return Page(text, "reallife_park_desk_raise", () => Step(1), "reallife_park_desk_lower", () => Step(-1), Status, "reallife_park_desk_back");
        }

        private static DialogEntry Step(int direction)
        {
            Director?.ParkStepPrice(Lot, direction);
            return Price();
        }

        // The leases and the offers waiting: the first offer to accept or decline, else the leases and a way to end one.
        private static DialogEntry Offers()
        {
            ConstructionDirector d = Director;
            CarParkFile c = Lot;
            if (d == null || c == null)
            {
                return Closing(ConstructionText.T("reallife_park_desk_closed"));
            }
            var lines = new List<string>(6);
            for (int i = 0; i < c.Leases.Count; i++)
            {
                CarParkLease l = c.Leases[i];
                lines.Add(ConstructionText.Fill(l.Ending ? "reallife_park_desk_lease_line_ending" : "reallife_park_desk_lease_line", "who", l.Name, "bays", ConstructionText.N(l.Bays),
                    "amount", ConstructionText.Money(l.Monthly * l.Bays / 100L), "day", ConstructionText.Day(l.PaidTo)));
            }
            string leases = lines.Count > 0 ? string.Join("\n", lines.ToArray()) : ConstructionText.T("reallife_park_desk_no_leases");
            if (c.Offers.Count == 0)
            {
                CarParkLease ending = null;
                for (int i = 0; i < c.Leases.Count && ending == null; i++)
                {
                    ending = c.Leases[i].Ending ? null : c.Leases[i];
                }
                string text = leases + "\n" + ConstructionText.Fill("reallife_park_desk_no_offers", "metres", ConstructionText.N(CarParkRules.ReachMetres));
                if (ending == null)
                {
                    return Page(text, null, null, null, null, Status, "reallife_park_desk_back");
                }
                int leaseId = ending.Id;
                return Page(text, "reallife_park_desk_end_lease", () => EndLease(leaseId), null, null, Status, "reallife_park_desk_back");
            }
            CarParkOffer o = c.Offers[0];
            string offer = ConstructionText.Fill("reallife_park_desk_offer", "who", o.Name, "bays", ConstructionText.N(o.Bays), "each", ConstructionText.Money(o.Monthly / 100L),
                "total", ConstructionText.Money(o.Monthly * o.Bays / 100L), "day", ConstructionText.Day(o.Expires), "more", ConstructionText.N(c.Offers.Count - 1));
            int id = o.Id;
            return Page(leases + "\n" + offer, "reallife_park_desk_accept", () => Accept(id), "reallife_park_desk_decline", () => Decline(id), Status, "reallife_park_desk_back");
        }

        private static DialogEntry Accept(int offerId)
        {
            string answer = Director?.ParkAccept(Lot, offerId);
            return answer != null ? Page(ConstructionText.T(answer), null, null, null, null, Offers, "reallife_park_desk_back") : Offers();
        }

        private static DialogEntry Decline(int offerId)
        {
            Director?.ParkDecline(Lot, offerId);
            return Offers();
        }

        private static DialogEntry EndLease(int leaseId)
        {
            Director?.ParkEnd(Lot, leaseId);
            return Offers();
        }

        // A car park of the city's: bays leased by the month, the number changed with the second button.
        private static DialogEntry LeaseHere()
        {
            ConstructionDirector d = Director;
            CarParkFile c = Lot;
            if (d == null || c == null)
            {
                return Closing(ConstructionText.T("reallife_park_desk_closed"));
            }
            int room = d.ParkLeaseRoom(c);
            if (room <= 0)
            {
                return Page(ConstructionText.T("reallife_park_answer_no_room"), null, null, null, null, Status, "reallife_park_desk_back");
            }
            _bays = Math.Max(1, Math.Min(_bays, room));
            long each = d.ParkLeasePrice(c) / 100L;
            string text = ConstructionText.Fill("reallife_park_desk_lease_text", "each", ConstructionText.Money(each), "room", ConstructionText.N(room), "bays", ConstructionText.N(_bays),
                "total", ConstructionText.Money(each * _bays));
            int next = _bays >= room ? 1 : Math.Min(room, _bays + 2);
            return Page(ConstructionText.T("reallife_park_desk_lease_text_head") + "\n" + text, "reallife_park_desk_lease_n", SignLease, "reallife_park_desk_more", () =>
            {
                _bays = next;
                return LeaseHere();
            }, Status, "reallife_park_desk_back", ConstructionText.N(_bays));
        }

        private static DialogEntry SignLease()
        {
            string answer = Director?.ParkLeaseFrom(Lot, _bays);
            return Page(ConstructionText.T(answer ?? "reallife_park_answer_leased"), null, null, null, null, Status, "reallife_park_desk_back");
        }

        // The player's own leases at a car park of the city's, each to end at its term's end.
        private static DialogEntry YourLeases()
        {
            CarParkFile c = Lot;
            if (c == null)
            {
                return Closing(ConstructionText.T("reallife_park_desk_closed"));
            }
            var lines = new List<string>(4);
            CarParkLease first = null;
            for (int i = 0; i < c.Leases.Count; i++)
            {
                CarParkLease l = c.Leases[i];
                if (l.Who != CarParkLease.Player)
                {
                    continue;
                }
                first = first == null && !l.Ending ? l : first;
                lines.Add(ConstructionText.Fill(l.Ending ? "reallife_park_desk_yours_line_ending" : "reallife_park_desk_yours_line", "bays", ConstructionText.N(l.Bays),
                    "amount", ConstructionText.Money(l.Monthly * l.Bays / 100L), "day", ConstructionText.Day(l.PaidTo)));
            }
            string text = lines.Count > 0 ? string.Join("\n", lines.ToArray()) : ConstructionText.T("reallife_park_desk_no_leases");
            if (first == null)
            {
                return Page(text, null, null, null, null, Status, "reallife_park_desk_back");
            }
            int id = first.Id;
            return Page(text, "reallife_park_desk_end_lease", () => EndLease(id, true), null, null, Status, "reallife_park_desk_back");
        }

        private static DialogEntry EndLease(int leaseId, bool yours)
        {
            Director?.ParkEnd(Lot, leaseId);
            return YourLeases();
        }

        // ------------------------------------------------------------------ the page's make

        /// <summary>A page: words made here, up to two choices, and the third button (Back, or Goodbye) which does what it is given.</summary>
        private static DialogEntry Page(string text, string confirmKey, Func<DialogEntry> onConfirm, string secondKey, Func<DialogEntry> onSecond, Func<DialogEntry> third, string thirdKey,
            string count = null)
        {
            var entry = new DialogEntry
            {
                headerKey = HeaderKey,
                messageData = "reallife_bld_page".Localize(new { text }),
                CancelTextOverride = thirdKey,
                OnCancel = () =>
                {
                    DialogEntry next = third != null ? third() : null;
                    if (next != null && DialogController.current != null)
                    {
                        DialogController.current.ShowEntry(Guard.Entry(next));
                    }
                }
            };
            if (onConfirm != null)
            {
                entry.ConfirmTextOverride = count != null ? "reallife_park_desk_lease_n".Localize(new { count }) : confirmKey.Localize();
                entry.OnConfirm = onConfirm;
            }
            if (onSecond != null)
            {
                entry.SecondOptionTextOverride = secondKey;
                entry.OnSecondOption = onSecond;
            }
            return entry;
        }

        // Goodbye: the dialogue over.
        private static DialogEntry Leave()
        {
            DialogController.current?.CancelDialog();
            return null;
        }

        private static DialogEntry Closing(string text)
        {
            return new DialogEntry
            {
                headerKey = HeaderKey,
                messageData = "reallife_bld_page".Localize(new { text }),
                CancelTextOverride = "reallife_park_desk_bye",
                OnCancel = DialogController.current != null ? DialogController.current.FinishDialog : (Action)null
            };
        }

        // ------------------------------------------------------------------ for the tests

        /// <summary>Every page's words for a car park without showing one (the test run's check): the status, the mode, the price, the offers.</summary>
        internal static string Words(ConstructionDirector d, CarParkFile c)
        {
            int was = Park;
            Park = c.Id;
            try
            {
                return StatusWords(d, c);
            }
            finally
            {
                Park = was;
            }
        }
    }

    /// <summary>The pay machine's conversation in the game's dialogue panel, opened by CarParkDesk.Open.</summary>
    public sealed class CarParkDialog : Dialog
    {
        public CarParkDialog()
        {
            npcNameKey = CarParkDesk.HeaderKey;
            DialogController.current.ShowEntry(Guard.Entry(CarParkDesk.First()));
        }
    }
}
