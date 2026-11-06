using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Bake offs (Tech/Rivals): a big customer in a niche, one of the city's own businesses of the kind, asks the products that sell there to compete. The player takes part from the firm's
    /// page or through the desk session (Wiring/Wiring.BakeLinks); the city's startups and the listed companies take part by the same scoring; the best fitted product wins. A win is
    /// booked as the winning firm's takings at its own address, through the mod's booking path, so the firm account counts it like any sale. Nothing here takes money from a firm.
    /// </summary>
    public sealed partial class TechDirector
    {
        private const string BakeHead = "reallife", Bake = BakeHead + "_tech_desk_bake_";   // the keys' start, written so the source holds no key prefix as a literal

        // A business of the game's list that is a niche's kind of customer and not the player's own.
        private static bool IsCustomer(BuildingRegistration reg, NicheDef d)
        {
            return reg != null && !reg.RentedByPlayer && !string.IsNullOrEmpty(reg.businessTypeName) && Array.IndexOf(d.Types, reg.businessTypeName) >= 0 && !GameDlc.Gated(reg);
        }

        /// <summary>The business that holds a niche's bake off today: one of the city's own of the niche's kinds, picked by a seeded roll. Null where the city has none.</summary>
        private string BakeCustomer(int niche, int day)
        {
            NicheDef d = NicheCatalogue.Def(niche);
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            if (all == null || d.Types.Length == 0)
            {
                return null;
            }
            int count = 0;
            for (int i = 0; i < all.Count; i++)
            {
                count += IsCustomer(all[i], d) ? 1 : 0;
            }
            if (count == 0)
            {
                return null;
            }
            int pick = RivalryRules.PickCustomer(_world, niche, day, count);
            for (int i = 0; i < all.Count; i++)
            {
                if (IsCustomer(all[i], d) && pick-- == 0)
                {
                    BuildingRegistration reg = all[i];
                    return !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : reg.StreetName + " " + reg.StreetNumber.ToString(CultureInfo.InvariantCulture);
                }
            }
            return null;
        }

        // Customers hold bake offs where the player sells, now and then.
        private void OpenBakes(int day)
        {
            for (int n = 0; n < NicheCatalogue.Count; n++)
            {
                if (!RivalryRules.BakeWanted(_world, n, day))
                {
                    continue;
                }
                string customer = BakeCustomer(n, day);
                if (customer == null)
                {
                    continue;
                }
                BakeOff b = RivalryRules.Open(_world, n, customer, day);
                for (int i = 0; i < _world.You.Count; i++)
                {
                    RivalryYou y = _world.You[i];
                    if ((y.Served & (1 << n)) != 0)
                    {
                        TellTech(NotificationType.Info, "reallife_rival_note_bake", Data("customer", b.Customer, "niche", TechText.T(NicheCatalogue.NameKey(n)), "firm", y.Name,
                            "value", TechText.Cents(Level(b.Value)), "day", b.Closes.ToString(CultureInfo.InvariantCulture)), TabFunding, TechApp.CapRow + y.Place);
                    }
                }
            }
        }

        // A bake off that has closed: the winner's firm books the contract, and the player is told how it went.
        private void BakeDecided(BakeOff b, int day)
        {
            string niche = TechText.T(NicheCatalogue.NameKey(b.Niche));
            if (b.State == BakeState.Won)
            {
                FirmPlace firm = FirmOf(b.Place);
                if (firm != null)
                {
                    BakeGame.Takings(Level(b.Booked), firm.Reg, b.Customer, day);
                    Practised(TalentCatalogue.Selling);
                    TellTech(NotificationType.Success, "reallife_rival_note_bake_won", Data("firm", firm.File.Name, "customer", b.Customer, "amount", TechText.Cents(Level(b.Booked))),
                        TabFunding, TechApp.CapRow + b.Place);
                }
                Story("reallife_rival_news_bake_won", "name", b.Winner, "customer", b.Customer);
                return;
            }
            Story("reallife_rival_news_bake_lost", "name", string.IsNullOrEmpty(b.Winner) ? niche : b.Winner, "customer", b.Customer);
            for (int e = 0; e < b.Entries.Count; e++)
            {
                FirmPlace firm = FirmOf(b.Entries[e].Place);
                if (firm != null)
                {
                    TellTech(NotificationType.Warning, "reallife_rival_note_bake_lost", Data("firm", firm.File.Name, "customer", b.Customer, "winner", b.Winner), TabFunding,
                        TechApp.CapRow + b.Entries[e].Place);
                }
            }
        }

        // ---------------------------------------------------------------- taking part

        /// <summary>The open bake off a firm can enter and has not, or null.</summary>
        internal BakeOff BakeFor(string place)
        {
            EnsureWorld();
            for (int i = 0; _rival != null && _world != null && _ventureOn && i < _rival.Bakes.Count; i++)
            {
                if (RivalryRules.Entrant(_world, _rival.Bakes[i], place).HasValue)
                {
                    return _rival.Bakes[i];
                }
            }
            return null;
        }

        /// <summary>The bake offs of a firm's niches for its page: the open ones, and the last few that closed, newest first, into <paramref name="into"/> (cleared).</summary>
        internal int BakesOf(string place, List<BakeOff> into)
        {
            into.Clear();
            EnsureWorld();
            if (_rival == null || _world == null || !_ventureOn)
            {
                return 0;
            }
            int served = 0;
            for (int i = 0; i < _world.You.Count; i++)
            {
                served |= _world.You[i].Place == place ? _world.You[i].Served : 0;
            }
            for (int i = _rival.Bakes.Count - 1; i >= 0 && into.Count < 4; i--)
            {
                BakeOff b = _rival.Bakes[i];
                if ((served & (1 << b.Niche)) != 0 || b.EntryOf(place) != null)
                {
                    into.Add(b);
                }
            }
            return into.Count;
        }

        /// <summary>Why the bake off's desk session cannot be held at a firm now (a key), or null when it is open.</summary>
        internal string BakeBlocked(string place) => BakeFor(place) != null ? null : Bake + "why_none";

        /// <summary>The bake off entered at the desk: 0 a demo, 1 a sharper price, 2 the relationships. False, with the reason, when nothing could be done.</summary>
        internal bool BakeSession(string place, string name, int choice, out string key, out string[] pairs)
        {
            pairs = new[] { "firm", name ?? string.Empty };
            BakeOff b = BakeFor(place);
            FirmPlace firm = FirmOf(place);
            if (b == null || firm == null || !RivalryRules.Enter(_world, b, place, choice))
            {
                key = Bake + "why_none";
                return false;
            }
            Practised(TalentCatalogue.Selling);
            Practised(choice == 0 ? TalentCatalogue.ProductSense : choice == 1 ? TalentCatalogue.Analysis : TalentCatalogue.Relationships);
            key = Bake + "entered";
            pairs = new[] { "firm", firm.File.Name, "customer", b.Customer, "day", b.Closes.ToString(CultureInfo.InvariantCulture) };
            ChangedTech();
            return true;
        }

        /// <summary>A bake off entered from the firm's page: the same entry as at the desk, and its notice.</summary>
        internal bool EnterBake(string place, int tactic)
        {
            FirmPlace firm = FirmOf(place);
            bool done = BakeSession(place, firm != null ? firm.File.Name : string.Empty, tactic, out string key, out string[] pairs);
            Notifications.Show(done ? NotificationType.Info : NotificationType.Warning, key, Data(pairs), 8f, null, null, true, false);
            return done;
        }
    }
}
