using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.ParkSpaces;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Parking spaces in the Property app in the game's frame: a row a car park on the Market tab, a row a space held on the Mine
    /// tab, and a car park's page that sells or lets a space and gives the player's spaces there their way out. Written only when a
    /// page is; every press goes to <see cref="SpaceDirector"/>'s own call, asked first where money moves.
    /// </summary>
    public sealed partial class EstateApp
    {
        private const string SpaceRow = "spc:";
        private const string SpaceGuideTitle = "reallife_meta_title_parking_spaces";

        private static SpaceDirector SpacesNow => SpaceDirector.Instance != null && SpaceDirector.Instance.Running ? SpaceDirector.Instance : null;

        private static void AskSpace(string titleKey, string body, Action yes, bool negative)
        {
            GameConfirm.Show(titleKey.GetLocalization(), body, () => UiClone.Safe(yes), null, null, null, false, negative);
        }

        // The Market tab's rows: a row a car park. Returns the one picked, or null.
        private SpacePark WriteSpaceRows(List<string> districts, ref string selected)
        {
            SpaceDirector d = SpacesNow;
            SpacePark picked = null;
            int day = SaveGameManager.Current.Day;
            for (int i = 0; d != null && i < d.Parks.Count; i++)
            {
                SpacePark p = d.Parks[i];
                SpaceRules.Counts(d.State, p, day, out int player, out int _, out int _, out int free);
                if (Pick(SpaceRow + p.Number.ToString(CultureInfo.InvariantCulture), ref selected, out TableRow row))
                {
                    picked = p;
                }
                row.Kind = KindOf(districts, p.District);
                row.Kind2 = EstateTables.IsSpace;
                row.Set(0, p.Name);
                row.Set(1, p.District);
                row.Set(2, SpaceText.T("reallife_space_kind"));
                row.Set(3, SpaceGame.Money(p.Price), p.Price);
                row.Set(4, p.Owner == SpaceOwner.Player ? SpaceText.T("reallife_space_row_yours") : player > 0 ? SpaceText.Fill("reallife_space_row_held", "count", player.ToString())
                    : free > 0 ? SpaceText.Fill("reallife_space_row_free", "count", free.ToString()) : SpaceText.T("reallife_space_row_full"));
            }
            return picked;
        }

        // The Mine tab's rows: a row a space the player holds, which opens its car park's page on the Market tab.
        private void WriteSpacesHeld(ref string selected)
        {
            SpaceDirector d = SpacesNow;
            for (int i = 0; d != null && i < d.State.Held.Count; i++)
            {
                SpaceHolding h = d.State.Held[i];
                SpacePark p = h.Players ? d.Park(h.Park) : null;
                if (p == null)
                {
                    continue;
                }
                Pick(SpaceRow + p.Number.ToString(CultureInfo.InvariantCulture), ref selected, out TableRow row);
                row.Kind = EstateTables.OwnSpace;
                row.Set(0, SpaceText.Fill("reallife_space_row_name", "space", SpaceText.Space(h.Slot), "name", p.Name));
                row.Set(1, SpaceText.T("reallife_space_kind"));
                row.Set(2, SpaceText.Tenure(h));
                if (!h.Rented)
                {
                    row.Set(3, SpaceGame.Money(d.BackFor(h)), d.BackFor(h));
                    row.Set(4, SpaceGame.Money(h.Paid), h.Paid);
                }
            }
        }

        // A car park's page: what a space costs, who holds the spaces, buying and renting one, and the player's spaces there.
        private void WriteSpace(InfoBox box, SpacePark p)
        {
            SpaceDirector d = SpacesNow;
            if (d == null)
            {
                return;
            }
            int park = p.Number, day = SaveGameManager.Current.Day;
            SpaceRules.Counts(d.State, p, day, out int player, out int rivals, out int city, out int free);
            box.Title(SpaceText.Fill("reallife_space_page_title", "name", p.Name));
            box.Subtitle(EstateText.Pair(SpaceText.T("reallife_space_kind"), p.District));
            box.Chip(free > 0 ? SpaceText.Fill("reallife_space_row_free", "count", free.ToString()) : SpaceText.T("reallife_space_row_full"), free > 0 ? CellTone.Green : CellTone.Yellow);
            box.Section("reallife_ibox_sec_money");
            box.Row("reallife_space_fig_price", SpaceGame.Money(p.Price));
            box.Line(SpaceText.Fill("reallife_space_rent_line", "amount", SpaceGame.Money(p.RentMonth), "days", SpaceCatalogue.RentDays.ToString()));
            box.Row("reallife_space_l_seller", SpaceText.Owner(p));
            box.Note(SpaceText.T(p.Owner == SpaceOwner.Player ? "reallife_space_what_yours" : "reallife_space_what"));
            box.Section("reallife_space_sec_holders");
            box.Line(SpaceText.Fill("reallife_space_holders_line", "you", player.ToString(), "rivals", rivals.ToString(), "city", city.ToString(), "free", free.ToString(),
                "spaces", p.Capacity.ToString()));
            SpaceRefusal buy = d.MayTake(park, false, out int _), rent = d.MayTake(park, true, out int slot);
            if (p.Owner != SpaceOwner.Player)
            {
                if (buy != SpaceRefusal.None)
                {
                    box.Line(SpaceText.T(SpaceDirector.Why(buy)), CellTone.Yellow);
                }
                Transform buttons = box.ButtonRow();
                if (buy == SpaceRefusal.None)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_space_btn_buy", () => AskSpace("reallife_space_q_buy_title",
                        SpaceText.Fill("reallife_space_q_buy", "space", SpaceText.Space(slot), "name", p.Name, "amount", SpaceGame.Money(p.Price), "owner", SpaceText.Owner(p)), () =>
                        {
                            d.Buy(park);
                            Refresh();
                        }, false));
                }
                if (rent == SpaceRefusal.None)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_space_btn_rent", () => AskSpace("reallife_space_q_rent_title",
                        SpaceText.Fill("reallife_space_q_rent", "space", SpaceText.Space(slot), "name", p.Name, "amount", SpaceGame.Money(p.RentMonth), "days", SpaceCatalogue.RentDays.ToString()),
                        () =>
                        {
                            d.Rent(park);
                            Refresh();
                        }, false));
                }
            }
            bool any = false;
            for (int i = 0; i < d.State.Held.Count; i++)
            {
                SpaceHolding h = d.State.Held[i];
                if (!h.Players || h.Park != park)
                {
                    continue;
                }
                if (!any)
                {
                    box.Section("reallife_space_sec_mine");
                    any = true;
                }
                int at = h.Slot;
                long back = d.BackFor(h);
                bool ending = h.Ending;
                box.Line(EstateText.Pair(SpaceText.Space(h.Slot), SpaceText.Tenure(h)));
                Transform own = box.ButtonRow();
                if (h.Rented)
                {
                    InfoBox.Button(own, InfoBox.Additional, ending ? "reallife_space_btn_keep" : "reallife_space_btn_end", () =>
                    {
                        d.SetEnding(park, at, !ending);
                        Refresh();
                    });
                }
                else if (p.Owner != SpaceOwner.Player)
                {
                    InfoBox.Button(own, InfoBox.Negative, "reallife_space_btn_sell", () => AskSpace("reallife_space_q_sell_title",
                        SpaceText.Fill("reallife_space_q_sell", "space", SpaceText.Space(at), "name", p.Name, "amount", SpaceGame.Money(back),
                            "fee", (SpaceCatalogue.BackFee / 10f).ToString("0.#", CultureInfo.InvariantCulture)), () =>
                        {
                            d.Sell(park, at);
                            Refresh();
                        }, true));
                }
            }
            BackButton(box.ButtonRow());
            Meta.Guide.Point(box, SpaceGuideTitle);
        }
    }
}
