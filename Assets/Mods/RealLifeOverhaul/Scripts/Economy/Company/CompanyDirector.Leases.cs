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

        /// <summary>The trend of renewal rises as it stands, per mille; 0 while the leases do not run.</summary>
        public int CityTrend => LeasesOn ? LeaseRules.TrendOf(_leases.Trend) : 0;

        /// <summary>
        /// A rival's lease renewed with this rise, per mille over its rent: kept with the month's, so that what the rivals' landlords ask is part of the trend the player's
        /// landlords ask by (the city's month, <see cref="LeaseRules.KeepTrend"/>).
        /// </summary>
        public void RivalRenewed(string district, int risePermille)
        {
            if (LeasesOn)
            {
                _leases.RivalRenewed(risePermille);
            }
        }

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
                        MakeOffer(l, shop, day);   // also for a lease that ended while days were skipped: renewed at the landlord's figure
                    }
                }
                _leases.Leases.Sort((a, b) => a.End.CompareTo(b.End));
            }
            catch (Exception e)
            {
                LeaseFault("the day", e);
            }
        }

        // The city's leases: once a month, the renewals its landlords made in the districts the player rents in and those the rivals' leases made, by the same rule, set
        // the trend (LeaseRules.CityMonth, which the run of the checks calls too).
        private void CityMonth(int day, int monthDays)
        {
            _leaseDistricts.Clear();
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].Rent > 0 && !string.IsNullOrEmpty(_shops[i].District) && !_leaseDistricts.Contains(_shops[i].District))
                {
                    _leaseDistricts.Add(_shops[i].District);
                }
            }
            int renewed = LeaseRules.CityMonth(_leases, _state.Seed, _leaseDistricts, day, monthDays, CityPressure);
            if (renewed >= 0 && Verbose)
            {
                Logger?.Info("Company check (leases, the city's month): " + renewed + " renewal(s) of the city's and the rivals' leases in " + _leaseDistricts.Count + " district(s), the trend now "
                             + LeaseRules.TrendOf(_leases.Trend) + " per mille: PASS");
            }
        }

        private static int CityPressure(string district, int day) => Pressure(district);

        // The landlord's offer, fourteen days before the lease ends: a manager with the Leases task answers it at their hour inside their limit; otherwise
        // the owner is asked, as an escalation where the hierarchy runs and as a notice always.
        private void MakeOffer(LeaseFile l, CompanyShop shop, int day)
        {
            ManagedPlace mp = ManagedAt(l.Place);
            bool manager = mp != null && mp.Own != null && ManagerRules.Runs(mp.Own.Tasks, mp.Kind, ManagerTask.Leases);
            int end = l.End;
            OfferWas was = CompanyDay.Offer(this, l.Place, l, manager, LeaseTrend, Pressure(l.District), MarketOf(l), MonthDays, day);
            int asked = was == OfferWas.Renewed ? l.Rise : l.Offer;
            Logger?.Info("Company: the landlord of " + shop.Name + " asks " + LeaseRules.RentAt(l.Rent, asked) + " a day (the game's rent " + l.Rent + ", " + asked
                + " per mille over it) when the lease ends in "
                         + (end - day) + " day(s), on day " + end + ": "
                         + (was == OfferWas.Renewed ? "the days were skipped past the end, so it is renewed at their figure" : was == OfferWas.Manager ? "the manager answers it"
                             : "the owner is asked") + ".");
            if (was == OfferWas.Renewed)
            {
                Settled(l, shop, day);
            }
        }

        // The market rent a day for a lease's premises, from the one function the property feature feeds (CompanyBridges.MarketRent); 0 where it knows none.
        private static int MarketOf(LeaseFile l)
        {
            try
            {
                return CompanyBridges.MarketRent != null ? Math.Max(0, CompanyBridges.MarketRent(l.District, l.Place, l.Rent)) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // The owner told of an offer (once for an offer: CompanyDay.Ask, which raises the escalation where the hierarchy runs): a notice that opens the Company app
        // on the lease question. The question is answered from the app whether the hierarchy runs or not.
        private void TellLease(LeaseFile l, CompanyShop shop, int day)
        {
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
            CompanyDay.LeaseClosed(this, l.Place, day);
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
            if (!CompanyDay.OwnerAnswers(this, place, l, accept, CompanyText.T("reallife_hier_you"), Save.Day))
            {
                return;
            }
            Logger?.Info("Company: the lease of " + ShopName(place) + ": the offer " + (accept ? "accepted" : "declined") + " by the owner, from day " + l.End + ".");
            Changed();
        }

        /// <summary>Asks the landlord to take less, once: they may halve their rise (LeaseRules.Haggle). False when the offer stands or there is no offer to ask about.</summary>
        public bool LeaseHaggle(string place)
        {
            LeaseFile l = LeaseAt(place);
            bool asked;
            bool less = CompanyDay.OwnerHaggles(this, place, l, Save.Day, out asked);
            if (!asked)
            {
                return false;
            }
            Notify(less ? NotificationType.Success : NotificationType.Warning, less ? "reallife_co_note_lease_less" : "reallife_co_note_lease_same",
                new Dictionary<string, string> { { "business", ShopName(place) }, { "rent", CompanyText.Money(Monthly(LeaseRules.RentAt(l.Rent, l.Offer))) } }, 8f);
            Changed();
            return less;
        }

        // A manager with the Leases task, at their hour (CompanyDay.Lease, the one entry the run of the checks calls too): an offer inside their rise limit is answered
        // by them; past it, the owner is asked, once. A mistake is a meaner answer: a fair offer declined, never one past the limit accepted.
        private void DecideLeases(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            LeaseFile l = LeasesOn ? _leases.Of(mp.Place.Place) : null;
            if (l == null || mp.Shop == null)
            {
                return;
            }
            LeaseHour was = CompanyDay.Lease(this, mp.Place.Place, mp.Own, l, skill, review, who, day, hour);
            if (was == LeaseHour.Accepted || was == LeaseHour.Declined)
            {
                Decision d = _mgr.Decisions[_mgr.Decisions.Count - 1];
                Logger?.Info("Company: " + who + " (manager, skill " + skill + ") " + (was == LeaseHour.Accepted ? "accepted" : "declined") + " the landlord's offer for " + mp.Place.Name + ": " + d.C
                             + " a day against " + d.B + ", " + d.D + " per mille more" + (d.Mistake ? ", a mistake" : string.Empty) + ".");
            }
            else if (was == LeaseHour.PassedUp)
            {
                Logger?.Info("Company: " + who + " (manager) passed the landlord's offer for " + mp.Place.Name + " up to the owner, " + LeaseRules.OfferOver(l)
                    + " per mille over the rent now, past their limit.");
            }
        }

        /// <summary>
        /// The question a landlord's offer asks the owner when it is theirs to answer (the offer made, not answered, and asked of them: <see cref="LeaseFile.Told"/>), as a
        /// question that stands in no state: the hierarchy off, or none raised. Null for a number that is no lease's, or for a lease that waits for nobody.
        /// </summary>
        internal Escalation LeaseQuestion(int number)
        {
            for (int i = 0; LeasesOn && i < _leases.Leases.Count; i++)
            {
                LeaseFile l = _leases.Leases[i];
                if (LeaseRules.QuestionId(l.Place) == number && l.Told != 0 && LeaseRules.Waiting(l, Today))
                {
                    return LeaseAsked(l);
                }
            }
            return null;
        }

        /// <summary>The leases waiting on the owner, each as the question it asks: those the hierarchy has no open question for (it has none while it is off).</summary>
        internal void LeaseQuestions(List<Escalation> into)
        {
            for (int i = 0; LeasesOn && i < _leases.Leases.Count; i++)
            {
                LeaseFile l = _leases.Leases[i];
                if (l.Told != 0 && LeaseRules.Waiting(l, Today) && (!HierarchyOn || _hier.OpenOf(EscalationKind.Lease, l.Place) == null))
                {
                    into.Add(LeaseAsked(l));
                }
            }
        }

        private Escalation LeaseAsked(LeaseFile l)
        {
            return new Escalation
            {
                Id = LeaseRules.QuestionId(l.Place), Kind = (int)EscalationKind.Lease, Place = l.Place, Level = EscalationRules.LevelOwner, Day = l.Told, Moved = l.Told,
                State = (int)EscalationState.Open, A = LeaseRules.RentAt(l.Rent, l.Running), B = LeaseRules.RentAt(l.Rent, l.Offer), C = l.End - Today, Detail = l.Name
            };
        }

        // The question a lease asks the owner, in words, for the escalation's page.
        internal LeaseFile LeaseOf(Escalation e)
        {
            return e != null && e.Kind == (int)EscalationKind.Lease ? LeaseAt(e.Place) : null;
        }
    }
}
