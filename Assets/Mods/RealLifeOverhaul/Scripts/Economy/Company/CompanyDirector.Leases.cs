using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Careers;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Leases on the player's rented premises (LeaseRules): a term and a renewal day over the game's rent, the landlord's offer 14 days before, an answer
    /// by the owner or by a manager who has the Leases task (inside a rise limit), and what the renewal adds to the rent as a daily business cost. The
    /// city's own leases renew by the same rule in the same districts, from a seed alone, and set the trend the landlords ask by. Nothing per frame: one
    /// pass over the rented premises a day, and one pass over a month's days of the city's leases once a month.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private static readonly int HotLeasesDay = HotSpots.Slot("Economy: Company day, leases");

        private LeaseState _leases;
        private bool _leasesUnread, _leasesBroken;
        private readonly List<string> _leaseDistricts = new List<string>(8);

        /// <summary>Whether leases run: the company runs, no fault has stopped them and the save's line (or a fresh one) is in hand.</summary>
        public bool LeasesOn => _running && !_broken && !_leasesBroken && _leases != null;

        internal LeaseState LeaseData => LeasesOn ? _leases : null;

        partial void LeaseLoad(GameInstance save)
        {
            string text = null;
            _leasesBroken = false;
            _leases = null;
            _leasesUnread = save != null && save.modData != null && save.modData.TryGetValue(LeaseStore.Key, out text) && !string.IsNullOrEmpty(text);
            if (_leasesUnread)
            {
                int version;
                _leases = LeaseStore.Read(text, out version);
                _leasesUnread = _leases == null;
                if (_leasesUnread)
                {
                    SaveLines.Unreadable(save.modData, LeaseStore.Key, "its version is not one this version of the mod reads");
                    Logger?.Warn("Company: the leases in this save are not of a version this one reads; none run this session, and the line is kept as it is.");
                    return;
                }
            }
            _leases = _leases ?? new LeaseState();
            if (Verbose)
            {
                Logger?.Info("Company check (leases): " + _leases.Leases.Count + " lease(s) read from the save (version " + LeaseStore.Version + "): PASS");
            }
        }

        partial void LeaseSave(GameInstance save)
        {
            if (_leasesUnread || _leases == null || save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            if (_leases.Leases.Count == 0 && _leases.Trend.Count == 0)
            {
                save.modData.Remove(LeaseStore.Key);
            }
            else
            {
                save.modData[LeaseStore.Key] = LeaseStore.Write(_leases);
            }
        }

        private void LeaseFault(string where, Exception e)
        {
            _leasesBroken = true;
            Logger?.Error(e);
            Logger?.Warn("Company: leases switched off for this session after an error in " + where + ". The game's rent is unchanged; what leases added stays in the save as it was.");
        }

        /// <summary>The lease of a rented place; null for premises the player owns, or with none yet.</summary>
        internal LeaseFile LeaseAt(string place)
        {
            return LeasesOn ? _leases.Of(place) : null;
        }

        /// <summary>The city's trend of renewal rises, per mille, and what a rival buying up a district adds there.</summary>
        internal int LeaseTrend => LeaseRules.TrendOf(_leases.Trend);

        internal static int Pressure(string district)
        {
            try
            {
                return CompanyBridges.Pressure != null && !string.IsNullOrEmpty(district) ? CompanyBridges.Pressure(district) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // The game's day, after the managers': leases for premises seen first, the rises paid, offers made, answers that have come to their end applied,
        // and the city's month.
        partial void LeaseDay(int day)
        {
            if (!LeasesOn)
            {
                return;
            }
            try
            {
                using var spot = HotSpots.Time(HotLeasesDay);
                int monthDays = MonthDays;
                CityMonth(day, monthDays);
                // A lease for each rented premises, none for what is no longer the player's or no longer rented.
                for (int i = _leases.Leases.Count - 1; i >= 0; i--)
                {
                    CompanyShop shop = ShopAt(_leases.Leases[i].Place);
                    if (shop == null || shop.Rent <= 0)
                    {
                        _leases.Leases.RemoveAt(i);
                    }
                }
                for (int i = 0; i < _shops.Count; i++)
                {
                    CompanyShop shop = _shops[i];
                    if (shop.Rent <= 0)
                    {
                        continue;
                    }
                    LeaseFile l = _leases.Of(shop.File.Place);
                    if (l == null)
                    {
                        if (_leases.Leases.Count >= LeaseStore.LeasesMost)
                        {
                            continue;
                        }
                        l = LeaseRules.Begin(_state.Seed, shop.File.Place, shop.Rent, day, monthDays);   // an old save's, or a new business's: starts today, a seeded term
                        _leases.Leases.Add(l);
                    }
                    l.Rent = shop.Rent;
                    l.Name = shop.Name;
                    l.District = shop.District;
                    int topUp = LeaseRules.TopUp(l);
                    if (topUp > 0)
                    {
                        Cost(topUp, CompanyGame.LeaseKey, shop, shop.Name, CostKind.Rent, day - 1,
                            "the rent's rise of " + CompanyText.Share(l.Running) + " over the game's rent of " + l.Rent + " a day, for day " + (day - 1), true);
                    }
                    if (LeaseRules.Settle(l, day, _state.Seed, monthDays))
                    {
                        Settled(l, shop, day);
                    }
                    else if (LeaseRules.OfferDue(l, day))
                    {
                        MakeOffer(l, shop, day);
                    }
                }
                _leases.Leases.Sort((a, b) => a.End.CompareTo(b.End));
            }
            catch (Exception e)
            {
                LeaseFault("the day", e);
            }
        }

        // The city's leases: once a month, the renewals its landlords made in the districts the player rents in, by the same rule, set the trend.
        private void CityMonth(int day, int monthDays)
        {
            if (_leases.TrendDay == 0)
            {
                _leases.TrendDay = day;   // the first month starts today: nothing to look back on
                return;
            }
            if (day - _leases.TrendDay < monthDays)
            {
                return;
            }
            _leaseDistricts.Clear();
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].Rent > 0 && !string.IsNullOrEmpty(_shops[i].District) && !_leaseDistricts.Contains(_shops[i].District))
                {
                    _leaseDistricts.Add(_shops[i].District);
                }
            }
            int sum = 0, renewed = 0, trend = LeaseRules.TrendOf(_leases.Trend);
            for (int d = _leases.TrendDay; d < _leases.TrendDay + monthDays; d++)
            {
                for (int k = 0; k < _leaseDistricts.Count; k++)
                {
                    int count;
                    sum += LeaseRules.CityDay(_state.Seed, _leaseDistricts[k], d, monthDays, trend, Pressure(_leaseDistricts[k]), out count);
                    renewed += count;
                }
            }
            if (_leaseDistricts.Count > 0)
            {
                LeaseRules.KeepTrend(_leases.Trend, sum, renewed);
            }
            _leases.TrendDay += monthDays;
            if (Verbose)
            {
                Logger?.Info("Company check (leases, the city's month): " + renewed + " renewal(s) of the city's leases in " + _leaseDistricts.Count + " district(s), the trend now "
                             + LeaseRules.TrendOf(_leases.Trend) + " per mille: PASS");
            }
        }

        // The landlord's offer, fourteen days before the lease ends: a manager with the Leases task answers it at their hour inside their limit; otherwise
        // the owner is asked, as an escalation where the hierarchy runs and as a notice always.
        private void MakeOffer(LeaseFile l, CompanyShop shop, int day)
        {
            l.Offer = LeaseRules.Offer(l, _state.Seed, LeaseTrend, Pressure(l.District));
            l.Asked = day;
            l.Answer = 0;
            int days = l.End - day;
            Logger?.Info("Company: the landlord of " + shop.Name + " asks " + LeaseRules.RentAt(l.Rent, l.Offer) + " a day (the game's rent " + l.Rent + ", " + l.Offer + " per mille over it, "
                         + LeaseRules.OfferOver(l) + " over what is paid now) when the lease ends in " + days + " day(s), on day " + l.End + ".");
            ManagedPlace mp = ManagedAt(l.Place);
            if (mp != null && mp.Own != null && ManagerRules.Runs(mp.Own.Tasks, mp.Kind, ManagerTask.Leases))
            {
                return;   // their hour is this morning's: inside their limit they answer it, past it they raise it to the owner
            }
            AskOwner(l, shop, mp, day);
        }

        // The owner asked: a notice that opens the Company app on the question, and an escalation where the hierarchy runs.
        private void AskOwner(LeaseFile l, CompanyShop shop, ManagedPlace mp, int day)
        {
            if (HierarchyOn && mp != null && _hier.OpenOf(EscalationKind.Lease, l.Place) == null)
            {
                Raise(EscalationKind.Lease, mp, LeaseRules.RentAt(l.Rent, l.Running), LeaseRules.RentAt(l.Rent, l.Offer), l.End - day, string.Empty, shop.Name, day);
            }
            Tell(NotificationType.Warning, "reallife_co_note_lease_offer", new Dictionary<string, string>
            {
                { "business", shop.Name }, { "days", (l.End - day).ToString(CultureInfo.InvariantCulture) }, { "rent", CompanyText.Money(Monthly(LeaseRules.RentAt(l.Rent, l.Offer))) },
                { "share", CompanyText.Share(LeaseRules.OfferOver(l)) }
            }, TabOverview, "ls:" + l.Place);
        }

        // A day's rent in a month's money, as the pages say rents.
        internal int Monthly(int perDay)
        {
            return (int)Math.Min(int.MaxValue / 4, (long)Math.Max(0, perDay) * MonthDays);
        }

        // The lease came to its end and its answer was applied (or the landlord renewed at their own figure): said once, and the question closed.
        private void Settled(LeaseFile l, CompanyShop shop, int day)
        {
            if (_hier != null)
            {
                Escalation e = _hier.OpenOf(EscalationKind.Lease, l.Place);
                if (e != null)
                {
                    Close(e, EscalationState.Lapsed, string.Empty, day);
                }
            }
            Logger?.Info("Company: the lease of " + shop.Name + " " + (l.Hold ? "was not renewed: month to month at the rent paid now and a premium of " + CompanyText.Share(LeaseRules.HoldPremium)
                                                                  : "was renewed for " + l.Term + " days to day " + l.End + ", the rent " + LeaseRules.RentAt(l.Rent, l.Rise) + " a day") + ".");
            Notify(NotificationType.Info, l.Hold ? "reallife_co_note_lease_hold" : "reallife_co_note_lease_renewed", new Dictionary<string, string>
            {
                { "business", shop.Name }, { "rent", CompanyText.Money(Monthly(LeaseRules.RentAt(l.Rent, l.Running))) }, { "end", l.End.ToString(CultureInfo.InvariantCulture) },
                { "share", CompanyText.Share(l.Last) }
            }, 9f);
            Changed();
        }

        /// <summary>
        /// The owner's answer to a landlord's offer: accept the rent for a new term, or decline and stay month to month. It takes effect on the day the
        /// lease ends; the rent runs as it does until then. Closes the question wherever it is asked.
        /// </summary>
        public void LeaseAnswer(string place, bool accept)
        {
            LeaseFile l = LeaseAt(place);
            if (l == null || !LeaseRules.Respond(l, Save.Day, accept))
            {
                return;
            }
            Escalation e = _hier != null ? _hier.OpenOf(EscalationKind.Lease, place) : null;
            if (e != null)
            {
                string who = CompanyText.T("reallife_hier_you");
                Close(e, EscalationState.Resolved, who, Save.Day);
                HierarchyEntry entry = _hier.Log(HierarchyLog.Settled, (int)Rung.Owner, who, place, Save.Day, Save.Hour);
                entry.A = e.Id;
                entry.B = e.Kind;
            }
            Logger?.Info("Company: the lease of " + ShopName(place) + ": the offer " + (accept ? "accepted" : "declined") + " by the owner, from day " + l.End + ".");
            Changed();
        }

        /// <summary>Asks the landlord to take less, once: they may halve their rise (LeaseRules.Haggle). False when the offer stands or there is no offer to ask about.</summary>
        public bool LeaseHaggle(string place)
        {
            LeaseFile l = LeaseAt(place);
            if (l == null || !LeaseRules.CanHaggle(l, Save.Day))
            {
                return false;
            }
            bool less = LeaseRules.Haggle(l, _state.Seed, Save.Day);
            Escalation e = _hier != null ? _hier.OpenOf(EscalationKind.Lease, place) : null;
            if (e != null)
            {
                e.B = LeaseRules.RentAt(l.Rent, l.Offer);
            }
            Notify(less ? NotificationType.Success : NotificationType.Warning, less ? "reallife_co_note_lease_less" : "reallife_co_note_lease_same",
                new Dictionary<string, string> { { "business", ShopName(place) }, { "rent", CompanyText.Money(Monthly(LeaseRules.RentAt(l.Rent, l.Offer))) } }, 8f);
            Changed();
            return less;
        }

        // A manager with the Leases task, at their hour: an offer inside their rise limit is answered by them; past it, the owner is asked. A mistake is a
        // meaner answer: a fair offer declined, never one past the limit accepted.
        private void DecideLeases(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            LeaseFile l = LeasesOn ? _leases.Of(mp.Place.Place) : null;
            if (l == null || !LeaseRules.Waiting(l, day) || mp.Shop == null)
            {
                return;
            }
            ManagerFile m = mp.Own;
            int limit = ManagerCatalogue.Step(ManagerCatalogue.LeaseRises, m.LeaseStep);
            bool fair = LeaseRules.Within(l, limit);
            Took(mp, who, day - l.Asked);
            if (!fair)
            {
                AskOwner(l, mp.Shop, mp, day);   // past the limit: it comes to the owner
                return;
            }
            int roll;
            bool slip = Slips(skill, review, day, mp.Place.Place, ManagerTask.Leases, string.Empty, out roll);
            bool accept = fair && !slip;
            LeaseRules.Respond(l, day, accept);
            Decision d = Record(mp, ManagerTask.Leases, string.Empty, who, day, hour, accept != fair);
            d.A = accept ? 1 : 0;
            d.B = LeaseRules.RentAt(l.Rent, l.Running);
            d.C = LeaseRules.RentAt(l.Rent, l.Offer);
            d.D = LeaseRules.OfferOver(l);
            d.E = l.End;
            Logger?.Info("Company: " + who + " (manager, skill " + skill + ") " + (accept ? "accepted" : "declined") + " the landlord's offer for " + mp.Place.Name + ": " + d.C
                         + " a day against " + d.B + ", " + d.D + " per mille more, inside their limit of " + limit + (accept != fair ? ", a mistake" : string.Empty) + ".");
        }

        // The question a lease asks the owner, in words, for the escalation's page.
        internal LeaseFile LeaseOf(Escalation e)
        {
            return e != null && e.Kind == (int)EscalationKind.Lease ? LeaseAt(e.Place) : null;
        }
    }
}
