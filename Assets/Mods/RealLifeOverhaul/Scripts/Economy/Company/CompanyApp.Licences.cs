using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The licences half of the Tax and licences tab. The table: every licence each business needs, with the day it is
    /// good to. The box: the one picked, with its fee, its term, what trading without it costs, the switch that renews
    /// it by itself and the button that buys a term.
    /// </summary>
    public sealed partial class CompanyApp
    {
        private static readonly string[] LicenceWhy = { "reallife_co_lic_why_hygiene", "reallife_co_lic_why_alcohol", "reallife_co_lic_why_late", "reallife_co_lic_why_signage" };

        partial void WriteLicences(InfoBox box, CompanyState state, int day, ref string selected)
        {
            if (!_director.On(CompanyPart.Licences))
            {
                return;
            }
            IReadOnlyList<CompanyShop> shops = _director.Shops;
            for (int i = 0; i < shops.Count; i++)
            {
                for (int k = 0; k < CompanyCatalogue.LicenceKinds; k++)
                {
                    if (!LicenceRules.Needed(shops[i].Needs, k))
                    {
                        continue;
                    }
                    Licence l = shops[i].File.Permits[k];
                    bool valid = LicenceRules.Valid(l, day), soon = valid && l.Until - day < CompanyCatalogue.LicenceRemindDays;
                    Pick("lic:" + shops[i].File.Place + ":" + k, shops[i].Name, CompanyText.T("reallife_co_row_licence", "licence", CompanyText.Licence(k)), valid ? CompanyText.Day(l.Until) : string.Empty,
                        CompanyText.T(!valid ? "reallife_co_row_lic_missing" : l.Auto ? "reallife_co_row_lic_auto" : soon ? "reallife_co_row_lic_soon" : "reallife_co_row_lic_valid"), !valid ? 2 : soon && !l.Auto ? 1 : 0, ref selected);
                }
            }
            string place;
            int kind;
            CompanyShop shop = Picked("lic:", out place, out kind) && kind >= 0 && kind < CompanyCatalogue.LicenceKinds ? _director.ShopAt(place) : null;
            if (shop != null)
            {
                WriteLicence(box, shop, kind, day);
            }
            else if (!_director.On(CompanyPart.Tax))
            {
                box.Title(CompanyText.T("reallife_co_tab_tax"));
                box.Line(CompanyText.T("reallife_co_lic_intro"));
                box.Line(CompanyText.T("reallife_co_lic_pick"));
            }
        }

        private void WriteLicence(InfoBox box, CompanyShop shop, int kind, int day)
        {
            Licence l = shop.File.Permits[kind];
            string place = shop.File.Place;
            bool valid = LicenceRules.Valid(l, day);
            box.Title(CompanyText.T("reallife_co_lic_title", "licence", CompanyText.Capital(CompanyText.Licence(kind)), "business", shop.Name));
            box.Line(CompanyText.T(LicenceWhy[kind], "hour", CompanyCatalogue.LateHour.ToString(), "early", CompanyCatalogue.EarlyHour.ToString()));
            box.Line(valid ? CompanyText.T("reallife_co_lic_valid", "day", l.Until.ToString()) : CompanyText.T("reallife_co_lic_missing"));
            box.Line(FinanceText.Pair("reallife_co_lic_fee", CompanyText.Money(LicenceRules.Fee(kind, _director.Index))));
            box.Line(FinanceText.Pair("reallife_co_lic_term", CompanyText.T("reallife_co_lic_term_value", "days", CompanyCatalogue.LicenceTerm[kind].ToString())));
            box.Line(CompanyText.T(LicenceRules.Shuts(kind) ? "reallife_co_lic_fine_shut" : "reallife_co_lic_fine", "amount", CompanyText.Money(LicenceRules.Fine(kind, _director.Index)),
                "hours", CompanyCatalogue.LicenceClosedHours.ToString()));
            bool hasSwitch = box.HasSwitch && box.Switch("reallife_co_lic_auto", l.Auto, on => _director.SetAutoRenew(place, kind, on));
            var buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, valid ? "reallife_co_lic_renew" : "reallife_co_lic_buy", () => _director.BuyLicence(place, kind));
            if (!hasSwitch)
            {
                bool auto = l.Auto;
                InfoBox.Button(buttons, InfoBox.Negative, auto ? "reallife_co_lic_auto_off" : "reallife_co_lic_auto_on", () => _director.SetAutoRenew(place, kind, !auto));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
        }
    }
}
