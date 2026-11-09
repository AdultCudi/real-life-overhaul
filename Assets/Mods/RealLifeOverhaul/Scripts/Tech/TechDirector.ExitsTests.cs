using System;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The test tools of the options page for exits: each does one thing now, for real, to the first firm or startup it fits.</summary>
    public sealed partial class TechDirector
    {
        /// <summary>The first firm of the player's with takings makes the first listed company or startup of the city an offer today, at the price its books give.</summary>
        public void TestExitOffer()
        {
            if (!TestVentures() || _exits == null)
            {
                return;
            }
            GatherBuyers();
            for (int i = 0; i < _firms.Count; i++)
            {
                FirmPlace firm = _firms[i];
                string place = firm.File.Place;
                FirmExit e = _exits.Exit(place, true);
                FirmValue(firm, out long value, out int growthBp);
                if (firm.Reg == null || value <= 0 || _buyers.Count == 0 || e.Offers.Count >= ExitCatalogue.OffersMost)
                {
                    continue;
                }
                ExitBuyer b = _buyers[0];
                var o = new ExitOffer
                {
                    Id = _exits.Next(), Kind = ExitRules.KindBuy, Buyer = b.Buyer, BuyerId = b.Id, Name = b.Name, Made = Save.Day, Lapse = Save.Day + ExitCatalogue.OfferDays,
                    Price = ExitRules.OfferPrice(value, VentureRules.Mood(HiringGame.Cycle), b.Buyer, 0)
                };
                if (b.Buyer == 1)
                {
                    o.Price = Math.Min(o.Price, b.Cash);
                }
                e.Offers.Add(o);
                TellTech(NotificationType.Info, "reallife_exit_note_offer",
                    Data("firm", firm.File.Name, "buyer", o.Name, "amount", TechText.Cents(Level(o.Price)), "day", o.Lapse.ToString(CultureInfo.InvariantCulture)), TabFunding, ExitRow + place);
                ChangedTech();
                return;
            }
            Notifications.ShowError("reallife_exit_test_none");
        }

        /// <summary>The first firm whose book is open is priced today.</summary>
        public void TestExitPricing()
        {
            if (!TestVentures() || _exits == null)
            {
                return;
            }
            for (int i = 0; i < _exits.Firms.Count; i++)
            {
                if (_exits.Firms[i].Stage == ListStage.Roadshow)
                {
                    _exits.Firms[i].PriceDay = Save.Day;
                    _exits.LastDay = -1;
                    FirmExitsDay(Save.Day);
                    _exits.LastDay = Save.Day;
                    return;
                }
            }
            Notifications.ShowError("reallife_exit_test_no_book");
        }

        /// <summary>Every startup of the city short of people takes one from the market the player hires from, today.</summary>
        /// <summary>The first buyer an acqui-hire could have puts the player forward for a post now, as a sale would (no sale, nothing kept in the ledger).</summary>
        public void TestExitLead()
        {
            if (!TestVentures() || _exits == null)
            {
                return;
            }
            GatherBuyers();
            if (_buyers.Count == 0)
            {
                Notifications.ShowError("reallife_exit_test_none");
                return;
            }
            ExitBuyer b = _buyers[_buyers.Count - 1];
            if (!GiveLead(b.Name, ExitJobRules.AtOffice(b.Buyer, true), Save.Day, null))
            {
                ExitJobNone(b.Name);
            }
        }

        /// <summary>The founder of the first startup of the city that has one is taken on by a buyer in the population now.</summary>
        public void TestExitFounder()
        {
            if (!TestVentures() || _exits == null)
            {
                return;
            }
            for (int i = 0; i < _venture.Startups.Count; i++)
            {
                Startup s = _venture.Startups[i];
                if (s.Alive && _exits.FounderOf(s.Id) != null && FounderTaken(s, Save.Day))
                {
                    return;
                }
            }
            Notifications.ShowError("reallife_exit_test_none");
        }

        public void TestStartupHire()
        {
            if (!TestVentures() || _exits == null || !_hiringOn)
            {
                Notifications.ShowError("reallife_exit_test_none");
                return;
            }
            int hired = 0;
            for (int i = 0; i < _venture.Startups.Count; i++)
            {
                Startup s = _venture.Startups[i];
                FounderRec r = s.Alive ? _exits.FounderOf(s.Id) : null;
                if (r == null)
                {
                    continue;
                }
                for (int day = Save.Day; day < Save.Day + 40 && hired < 40; day++)
                {
                    if (ExitPlaces.Hire(_hiring, _exits, r, s, r.Hired, Level(s.Burn), day) != null)
                    {
                        hired++;
                        break;
                    }
                }
            }
            Logger?.Info("Tech: " + hired + " startup(s) of the city took a person from the labour market the player hires from.");
            ChangedTech();
        }
    }
}
