using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The licences part: terms bought, and fines and closures for trading without one.</summary>
    public sealed partial class CompanyDirector
    {
        partial void LicenceDay(int day)
        {
            for (int i = 0; i < _shops.Count; i++)
            {
                CompanyShop shop = _shops[i];
                ShopFile file = shop.File;
                for (int k = 0; k < file.Permits.Length; k++)
                {
                    Licence l = file.Permits[k];
                    LicenceNote note = LicenceRules.Day(l, day);
                    if (note == LicenceNote.None || !LicenceRules.Needed(shop.Needs, k))
                    {
                        continue;   // one the business no longer needs runs out without a word
                    }
                    string pick = "lic:" + file.Place + ":" + k;
                    if (note == LicenceNote.Renew)
                    {
                        int fee = LicenceRules.Fee(k, _index);
                        if (Cost(fee, CompanyGame.LicenceKey, shop, shop.Name, CostKind.Licences, null,
                                CompanyText.Licence(k) + " renewed by itself for " + CompanyCatalogue.LicenceTerm[k] + " days at today's prices (" + _index + ")"))
                        {
                            LicenceRules.Grant(l, k, day);
                            continue;
                        }
                        if (!LicenceRules.Unpaid(l, day))
                        {
                            continue;   // said once; tried again each day it is still renewing by itself
                        }
                        note = LicenceNote.Lapsed;   // it could not be paid for
                    }
                    Tell(note == LicenceNote.Remind ? NotificationType.Info : NotificationType.Warning,
                        note == LicenceNote.Remind ? "reallife_co_note_licence_remind" : "reallife_co_note_licence_lapsed",
                        new Dictionary<string, string> { { "licence", CompanyText.Licence(k) }, { "business", shop.Name }, { "day", l.Until.ToString() } }, TabTax, pick);
                }
                int missing = LicenceRules.Missing(file, shop.Needs, day);
                if (missing != 0 && LicenceRules.Checked(_state.Seed, _state.Age, i))
                {
                    bool started = CompanyBridges.Inspect != null && CompanyBridges.Inspect(shop.Reg);
                    if (Verbose)
                    {
                        Logger?.Info("Company check (licences, " + shop.Name + "): trading without " + CompanyText.Licences(missing)
                                     + "; an inspection was asked of business pressure today: "
                                     + (started ? "one has started there" : "its own rules said no (a rest, an open issue, or it is switched off)") + ": PASS");
                    }
                }
            }
        }

        /// <summary>An inspection ended: each missing licence is fined, and food or alcohol shuts too.</summary>
        public void Inspected(string place)
        {
            CompanyShop shop = On(CompanyPart.Licences) && Save != null ? ShopAt(place) : null;
            if (shop == null)
            {
                return;
            }
            try
            {
                int missing = LicenceRules.Missing(shop.File, shop.Needs, Save.Day), fines = 0;
                if (missing == 0)
                {
                    return;
                }
                bool shut = false;
                for (int k = 0; k < CompanyCatalogue.LicenceKinds; k++)
                {
                    if ((missing & (1 << k)) == 0)
                    {
                        continue;
                    }
                    int fine = LicenceRules.Fine(k, _index);
                    bool paid = Charge(fine, CompanyGame.LicenceFineKey, shop, shop.Name, null, false,
                        "trading without " + CompanyText.Licence(k) + ": " + CompanyCatalogue.LicenceFineMultiple + " times its fee");
                    fines += paid ? fine : 0;
                    shut |= LicenceRules.Shuts(k) || !paid;   // a fine that cannot be paid shuts the doors too
                }
                shut = shut && CompanyBridges.Close != null && CompanyBridges.Close(shop.Reg, CompanyCatalogue.LicenceClosedHours);
                Tell(NotificationType.Error, shut ? "reallife_co_note_licence_fined_shut" : "reallife_co_note_licence_fined", new Dictionary<string, string>
                {
                    { "business", shop.Name }, { "list", CompanyText.Licences(missing) }, { "amount", CompanyText.Money(fines) },
                    { "hours", CompanyCatalogue.LicenceClosedHours.ToString() }
                }, TabTax, null);
                Logger?.Info("Company: an inspection at " + shop.Name + " found it trading without " + CompanyText.Licences(missing) + ": fined " + fines
                             + (shut ? ", and shut for " + CompanyCatalogue.LicenceClosedHours + " hours by business pressure's own closure." : "."));
                Changed();
            }
            catch (System.Exception e)
            {
                PartFault(CompanyPart.Licences, "an inspection", e);
            }
        }

        public void BuyLicence(string place, int kind)
        {
            CompanyShop shop = On(CompanyPart.Licences) ? ShopAt(place) : null;
            if (shop == null || kind < 0 || kind >= CompanyCatalogue.LicenceKinds)
            {
                return;
            }
            int fee = LicenceRules.Fee(kind, _index);
            if (!Cost(fee, CompanyGame.LicenceKey, shop, shop.Name, CostKind.Licences, null,
                    CompanyText.Licence(kind) + " for " + CompanyCatalogue.LicenceTerm[kind] + " days at today's prices (" + _index + ")"))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            LicenceRules.Grant(shop.File.Permits[kind], kind, Save.Day);
            Logger?.Info("Company: " + CompanyText.Licence(kind) + " bought for " + shop.Name + " for " + fee + ": good to day " + shop.File.Permits[kind].Until + ".");
            Notify(NotificationType.Success, "reallife_co_note_licence_bought", new Dictionary<string, string>
            {
                { "licence", CompanyText.Licence(kind) }, { "business", shop.Name }, { "day", shop.File.Permits[kind].Until.ToString() }
            }, 8f);
            Changed();
        }

        public void SetAutoRenew(string place, int kind, bool on)
        {
            ShopFile file = On(CompanyPart.Licences) ? _state.Shop(place) : null;
            if (file != null && kind >= 0 && kind < CompanyCatalogue.LicenceKinds)
            {
                file.Permits[kind].Auto = on;
                Changed();
            }
        }

        partial void LicenceAlerts(int day)
        {
            for (int i = 0; i < _shops.Count; i++)
            {
                ShopFile file = _shops[i].File;
                int number = PlaceNumber(file.Place);
                int missing = LicenceRules.Missing(file, _shops[i].Needs, day);
                if (missing != 0)
                {
                    Alert(14, number, day, CompanyText.T("reallife_co_plan_missing_title", "business", _shops[i].Name),
                        CompanyText.T("reallife_co_plan_missing_what", "list", CompanyText.Licences(missing)), null, CompanyText.T("reallife_co_plan_missing_missed"));
                }
                for (int k = 0; k < file.Permits.Length; k++)
                {
                    Licence l = file.Permits[k];
                    if (LicenceRules.Needed(_shops[i].Needs, k) && !l.Auto && l.Until >= day && l.Until - day < CompanyCatalogue.LicenceRemindDays)
                    {
                        Alert(3, number % 100000000 * 4 + k, l.Until,
                            CompanyText.T("reallife_co_plan_licence_title", "licence", CompanyText.Capital(CompanyText.Licence(k)), "business", _shops[i].Name),
                            CompanyText.T("reallife_co_plan_licence_what"), CompanyText.T("reallife_planner_costs", "amount", CompanyText.Money(LicenceRules.Fee(k, _index))),
                            CompanyText.T("reallife_co_plan_missing_missed"));
                    }
                }
            }
        }
    }
}
